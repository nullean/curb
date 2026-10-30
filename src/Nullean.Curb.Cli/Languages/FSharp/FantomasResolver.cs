using System.IO.Abstractions;
using System.Text.Json;
using System.Xml;

namespace Nullean.Curb.Cli.Languages.FSharp;

/// <summary>How to start the Fantomas a manifest pins.</summary>
/// <param name="Manifest">The manifest it was resolved for.</param>
/// <param name="EntryPoint">The tool's <c>.dll</c>, run with <c>dotnet exec</c>; null to go through <c>dotnet tool run</c>.</param>
internal sealed record FantomasTool(FantomasManifest Manifest, string? EntryPoint)
{
	/// <summary>The command that starts it with <paramref name="arguments"/>.</summary>
	/// <param name="dotnet">The host to run it on.</param>
	/// <param name="arguments">What to pass Fantomas itself.</param>
	public ToolCommand Command(string dotnet, IEnumerable<string> arguments)
	{
		List<string> all;
		if (EntryPoint is null)
		{
			// The SDK finds the manifest from the working folder, which is why it is the manifest's own.
			all = ["tool", "run", Manifest.ToolId, "--"];
		}
		else
		{
			// `dotnet exec` rather than `dotnet tool run`: about 40 ms less per start, measured, and the
			// manifest was already read. `--roll-forward` is an exec option, so it goes before the path.
			all = ["exec"];
			if (Manifest.RollForward)
				all.AddRange(["--roll-forward", "Major"]);
			all.Add(EntryPoint);
		}

		all.AddRange(arguments);
		return new ToolCommand(dotnet, all, Manifest.Directory);
	}
}

/// <summary>Where the environment keeps the SDK's caches.</summary>
/// <param name="GetVariable">Reads an environment variable; null when unset.</param>
/// <param name="HomeDirectory">The user's profile folder.</param>
internal sealed record ToolEnvironment(Func<string, string?> GetVariable, string HomeDirectory)
{
	public static ToolEnvironment Current { get; } = new(
		Environment.GetEnvironmentVariable,
		Environment.GetFolderPath(Environment.SpecialFolder.UserProfile));

	/// <summary>The host to run tools on: the one MSBuild is running on when there is one, else <c>dotnet</c> from the path.</summary>
	public string Dotnet => GetVariable("DOTNET_HOST_PATH") is { Length: > 0 } host ? host : "dotnet";
}

/// <summary>Finds the files a restored local tool runs from, without asking the SDK.</summary>
/// <remarks>
/// <para>
/// Asking the SDK (<c>dotnet tool run</c>) costs a process start and a manifest walk of its own before
/// Fantomas even starts. Everything it would look at is on disk, so Curb reads it directly, in the same
/// order:
/// </para>
/// <list type="number">
/// <item>The SDK's resolver cache, <c>~/.dotnet/toolResolverCache/1/&lt;id&gt;</c>, which <c>dotnet tool
/// restore</c> writes: a JSON array of the versions it restored and the entry point of each.</item>
/// <item>The NuGet global packages folder, and the package's <c>DotnetToolSettings.xml</c>. A version 2
/// settings file names no entry point; it lists a package per runtime, and the <c>any</c> one holds the
/// framework-dependent build.</item>
/// <item><c>dotnet tool run</c>, when the package is there but its layout is one Curb does not know.</item>
/// </list>
/// <para>
/// When neither of the first two has the version at all, the tool is not restored, and that is known
/// without starting anything.
/// </para>
/// </remarks>
internal sealed class FantomasResolver(IFileSystem fileSystem, ToolEnvironment environment)
{
	/// <summary>What the resolver found: a tool, or the reason there is none.</summary>
	internal readonly record struct Resolution(FantomasTool? Tool, string? Problem);

	public Resolution Resolve(FantomasManifest manifest)
	{
		var fromCache = FromResolverCache(manifest);
		if (fromCache is not null)
			return new Resolution(new FantomasTool(manifest, fromCache), null);

		var packages = PackagesFolder();
		var packageFolder = fileSystem.Path.Combine(packages, manifest.ToolId.ToLowerInvariant(), manifest.Version.ToLowerInvariant());
		if (!fileSystem.Directory.Exists(packageFolder))
		{
			return new Resolution(null,
				$"{manifest.ToolId} {manifest.Version} is declared in {manifest.ManifestPath} but is not restored. Run `dotnet tool restore`.");
		}

		var entryPoint = FromPackage(packages, packageFolder, manifest.Version);
		return new Resolution(new FantomasTool(manifest, entryPoint), null);
	}

	/// <summary>The packages folder: <c>NUGET_PACKAGES</c> when set, else <c>~/.nuget/packages</c>.</summary>
	/// <remarks>A <c>globalPackagesFolder</c> in a <c>nuget.config</c> is not read; the resolver cache already covers it.</remarks>
	private string PackagesFolder() =>
		environment.GetVariable("NUGET_PACKAGES") is { Length: > 0 } packages
			? packages
			: fileSystem.Path.Combine(environment.HomeDirectory, ".nuget", "packages");

	private string? FromResolverCache(FantomasManifest manifest)
	{
		var home = environment.GetVariable("DOTNET_CLI_HOME") is { Length: > 0 } cliHome ? cliHome : environment.HomeDirectory;
		var path = fileSystem.Path.Combine(home, ".dotnet", "toolResolverCache", "1", manifest.ToolId.ToLowerInvariant());
		try
		{
			if (!fileSystem.File.Exists(path))
				return null;

			using var document = JsonDocument.Parse(fileSystem.File.ReadAllBytes(path));
			if (document.RootElement.ValueKind != JsonValueKind.Array)
				return null;

			foreach (var entry in document.RootElement.EnumerateArray())
			{
				if (entry.ValueKind != JsonValueKind.Object
					|| !entry.TryGetProperty("Version", out var version) || version.ValueKind != JsonValueKind.String
					|| !string.Equals(version.GetString(), manifest.Version, StringComparison.OrdinalIgnoreCase)
					|| !entry.TryGetProperty("PathToExecutable", out var executable) || executable.ValueKind != JsonValueKind.String)
				{
					continue;
				}

				// Only a framework-dependent entry point can be run with `dotnet exec`, and only one that is
				// still there: the cache outlives a cleared packages folder.
				if (executable.GetString() is { } dll
					&& dll.EndsWith(".dll", StringComparison.OrdinalIgnoreCase)
					&& fileSystem.File.Exists(dll))
				{
					return dll;
				}
			}
		}
		catch (Exception exception) when (exception is JsonException or IOException or UnauthorizedAccessException)
		{
			// Not Curb's file and not Curb's to repair; the packages folder is the next place to look.
		}

		return null;
	}

	/// <summary>The entry point a restored package declares, or null when its layout is one Curb does not read.</summary>
	private string? FromPackage(string packages, string packageFolder, string version)
	{
		var settings = FindSettings(packageFolder);
		if (settings is null)
			return null;

		var parsed = ReadSettings(settings);
		if (parsed.EntryPoint is { } entryPoint)
		{
			var dll = fileSystem.Path.Combine(fileSystem.Path.GetDirectoryName(settings)!, entryPoint);
			return parsed.Runner == "dotnet" && fileSystem.File.Exists(dll) ? dll : null;
		}

		// Version 2: the root package only points at a package per runtime. The `any` one is the
		// framework-dependent build `dotnet exec` can run; this machine's own may be native and is left to
		// `dotnet tool run`.
		foreach (var (runtime, id) in parsed.RuntimePackages)
		{
			if (runtime != "any")
				continue;

			var runtimeFolder = fileSystem.Path.Combine(packages, id.ToLowerInvariant(), version.ToLowerInvariant());
			if (!fileSystem.Directory.Exists(runtimeFolder) || FindSettings(runtimeFolder) is not { } inner)
				return null;

			var innerParsed = ReadSettings(inner);
			if (innerParsed.EntryPoint is null || innerParsed.Runner != "dotnet")
				return null;

			var dll = fileSystem.Path.Combine(fileSystem.Path.GetDirectoryName(inner)!, innerParsed.EntryPoint);
			return fileSystem.File.Exists(dll) ? dll : null;
		}

		return null;
	}

	/// <summary>
	/// The <c>DotnetToolSettings.xml</c> under <c>tools/&lt;framework&gt;/&lt;runtime&gt;/</c>, preferring the
	/// newest framework this machine's runtime can be asked to roll forward to.
	/// </summary>
	private string? FindSettings(string packageFolder)
	{
		var tools = fileSystem.Path.Combine(packageFolder, "tools");
		if (!fileSystem.Directory.Exists(tools))
			return null;

		string? best = null;
		var bestVersion = new Version(0, 0);
		foreach (var framework in fileSystem.Directory.EnumerateDirectories(tools))
		{
			foreach (var runtime in fileSystem.Directory.EnumerateDirectories(framework))
			{
				var settings = fileSystem.Path.Combine(runtime, "DotnetToolSettings.xml");
				if (!fileSystem.File.Exists(settings))
					continue;

				var name = fileSystem.Path.GetFileName(framework);
				var frameworkVersion = name.StartsWith("net", StringComparison.OrdinalIgnoreCase)
					&& Version.TryParse(name.AsSpan(3), out var parsed) ? parsed : new Version(0, 0);
				if (best is null || frameworkVersion > bestVersion)
				{
					best = settings;
					bestVersion = frameworkVersion;
				}
			}
		}

		return best;
	}

	private readonly record struct Settings(string? EntryPoint, string? Runner, List<(string Runtime, string Id)> RuntimePackages);

	private Settings ReadSettings(string path)
	{
		string? entryPoint = null;
		string? runner = null;
		var runtimePackages = new List<(string, string)>();
		try
		{
			using var stream = fileSystem.File.OpenRead(path);
			using var reader = XmlReader.Create(stream, new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null });
			while (reader.Read())
			{
				if (reader.NodeType != XmlNodeType.Element)
					continue;

				if (reader.LocalName == "Command" && entryPoint is null)
				{
					entryPoint = reader.GetAttribute("EntryPoint");
					runner = reader.GetAttribute("Runner");
				}
				else if (reader.LocalName == "RuntimeIdentifierPackage"
					&& reader.GetAttribute("RuntimeIdentifier") is { } runtime
					&& reader.GetAttribute("Id") is { } id)
				{
					runtimePackages.Add((runtime, id));
				}
			}
		}
		catch (Exception exception) when (exception is XmlException or IOException or UnauthorizedAccessException)
		{
			return new Settings(null, null, []);
		}

		return new Settings(entryPoint, runner, runtimePackages);
	}
}
