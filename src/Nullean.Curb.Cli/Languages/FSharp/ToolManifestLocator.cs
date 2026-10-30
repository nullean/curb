using System.IO.Abstractions;
using System.Text.Json;

namespace Nullean.Curb.Cli.Languages.FSharp;

/// <summary>The Fantomas a local tool manifest pins.</summary>
/// <param name="ManifestPath">The manifest that declared it.</param>
/// <param name="ToolId">The package id as the manifest spells it: <c>fantomas</c>, or the legacy <c>fantomas-tool</c>.</param>
/// <param name="Version">The exact version the manifest pins.</param>
/// <param name="RollForward">The manifest's <c>rollForward</c>, which asks for <c>--roll-forward Major</c>.</param>
/// <param name="Problem">
/// Set when the manifest is one the SDK would refuse (not JSON, or Fantomas without a version): the files it
/// governs fail with this, rather than quietly going unformatted.
/// </param>
internal sealed record FantomasManifest(string ManifestPath, string ToolId, string Version, bool RollForward, string? Problem = null)
{
	/// <summary>The folder the manifest governs, which is where <c>dotnet tool run</c> has to start from.</summary>
	public string Directory
	{
		get
		{
			var directory = Path.GetDirectoryName(ManifestPath) ?? ".";
			return Path.GetFileName(directory) == ".config" ? Path.GetDirectoryName(directory) ?? directory : directory;
		}
	}
}

/// <summary>Finds the local tool manifest that declares Fantomas for a folder, the way the SDK does.</summary>
/// <remarks>
/// <para>
/// The SDK walks up from the working folder, reading <c>.config/dotnet-tools.json</c> and
/// <c>dotnet-tools.json</c> in each, and the nearest manifest that declares a tool wins. A manifest with
/// <c>"isRoot": true</c> ends the walk whether or not it declared the tool, so a repository can keep a
/// parent folder's tools out. Curb follows the same rules, because a Fantomas the SDK would not run for
/// a file is not one Curb may run for it either.
/// </para>
/// <para>
/// Only ever a declared tool. Curb never installs anything and never falls back to a global one: a
/// formatter nobody pinned formats differently on every machine, and a cache keyed on a version nobody
/// pinned would be a lie.
/// </para>
/// <para>
/// Answers are kept per folder, because the run binds options for every file and most share a folder.
/// Not thread-safe; it is used from the run's serial options pass.
/// </para>
/// </remarks>
internal sealed class ToolManifestLocator(IFileSystem fileSystem)
{
	/// <summary>The ids Fantomas has been published under, newest first.</summary>
	private static readonly string[] ToolIds = ["fantomas", "fantomas-tool"];

	private readonly Dictionary<string, FantomasManifest?> _byDirectory = new(StringComparer.Ordinal);

	/// <summary>The manifest that declares Fantomas for files in <paramref name="directory"/>, or null when none does.</summary>
	public FantomasManifest? Find(string directory)
	{
		if (_byDirectory.TryGetValue(directory, out var known))
			return known;

		// Every folder the walk passes through gets the same answer, so a sibling folder further down the
		// list stops at the first one it shares rather than re-reading the manifests above it.
		var visited = new List<string>();
		FantomasManifest? found = null;
		var current = directory;
		while (!string.IsNullOrEmpty(current))
		{
			if (_byDirectory.TryGetValue(current, out found))
				break;

			visited.Add(current);
			if (Probe(current, out found))
				break;

			current = fileSystem.Path.GetDirectoryName(current);
		}

		foreach (var folder in visited)
			_byDirectory[folder] = found;
		return found;
	}

	/// <summary>Reads the manifests in one folder. True when the walk ends here, with or without a find.</summary>
	private bool Probe(string directory, out FantomasManifest? found)
	{
		found = null;
		var isRoot = false;
		foreach (var candidate in (ReadOnlySpan<string>)[fileSystem.Path.Combine(directory, ".config", "dotnet-tools.json"), fileSystem.Path.Combine(directory, "dotnet-tools.json")])
		{
			if (!fileSystem.File.Exists(candidate))
				continue;

			var manifest = Read(candidate, out var root);
			if (manifest is not null)
			{
				found = manifest;
				return true;
			}

			isRoot |= root;
		}

		return isRoot;
	}

	private FantomasManifest? Read(string path, out bool isRoot)
	{
		isRoot = false;
		try
		{
			using var document = JsonDocument.Parse(fileSystem.File.ReadAllBytes(path), new JsonDocumentOptions
			{
				AllowTrailingCommas = true,
				CommentHandling = JsonCommentHandling.Skip,
			});

			var root = document.RootElement;
			if (root.ValueKind != JsonValueKind.Object)
				return null;

			isRoot = root.TryGetProperty("isRoot", out var isRootValue) && isRootValue.ValueKind == JsonValueKind.True;
			if (!root.TryGetProperty("tools", out var tools) || tools.ValueKind != JsonValueKind.Object)
				return null;

			foreach (var id in ToolIds)
			{
				foreach (var tool in tools.EnumerateObject())
				{
					if (!tool.Name.Equals(id, StringComparison.OrdinalIgnoreCase) || tool.Value.ValueKind != JsonValueKind.Object)
						continue;

					if (!tool.Value.TryGetProperty("version", out var version) || version.ValueKind != JsonValueKind.String
						|| version.GetString() is not { Length: > 0 } pinned)
					{
						return new FantomasManifest(path, id, "", false, $"{path} declares '{tool.Name}' without a version, so it cannot be run");
					}

					var rollForward = tool.Value.TryGetProperty("rollForward", out var roll) && roll.ValueKind == JsonValueKind.True;
					return new FantomasManifest(path, id, pinned, rollForward);
				}
			}

			return null;
		}
		catch (Exception exception) when (exception is JsonException or IOException or UnauthorizedAccessException)
		{
			// The SDK refuses a manifest it cannot read, and so does Curb, out loud. Skipping it would either
			// fall through to a parent's Fantomas or decline the files, and a check would pass having checked
			// nothing. So it is an answer: the files it governs fail with the reason.
			return new FantomasManifest(path, "fantomas", "", false, $"{path} could not be read as a tool manifest — {exception.Message}");
		}
	}
}
