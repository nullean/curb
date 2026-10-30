using System.Globalization;
using System.IO.Abstractions;
using EditorConfig.Core;
using Nullean.Curb.EditorConfig;

namespace Nullean.Curb.Cli.Languages.FSharp;

/// <summary>F#, formatted by the Fantomas a repository pins in its local tool manifest.</summary>
/// <remarks>
/// <para>
/// Curb does not format F# itself. It finds the files, reads them, consults its cache, and writes what
/// comes back with its own byte-order mark; Fantomas decides the layout, from the same
/// <c>.editorconfig</c>. A repository opts in by declaring Fantomas in a tool manifest
/// (<c>dotnet tool install fantomas</c>); without one, its F# files are left alone and the run says so in
/// one line.
/// </para>
/// <para>
/// One instance per run: it keeps the manifest and ignore-file answers for the run's folders, and the
/// warnings to report at the end.
/// </para>
/// </remarks>
internal sealed class FSharpLanguage(IFileSystem fileSystem, IToolLauncher launcher, ToolEnvironment environment) : IExternalLanguage
{
	/// <summary>Said once per run, in place of a line per file, when F# files have no Fantomas to format them.</summary>
	internal const string NoManifest = "no Fantomas in a tool manifest (see https://curb.nullean.net/workflow/fsharp/)";

	private readonly ToolManifestLocator _manifests = new(fileSystem);
	private readonly Dictionary<string, string> _ignoreByDirectory = new(StringComparer.Ordinal);
	private readonly Dictionary<(FantomasManifest, string Extension, string Ignore), IReadOnlyDictionary<string, string>> _keys = [];
	private readonly HashSet<string> _warnings = new(StringComparer.Ordinal);
	private readonly List<string> _errors = [];

	public FSharpLanguage(IFileSystem fileSystem) : this(fileSystem, ProcessLauncher.Instance, ToolEnvironment.Current) { }

	public string Id => "fsharp";

	/// <summary>Implementation, signature and script files. Not <c>.ml</c>/<c>.mli</c>, which are as likely to be OCaml.</summary>
	public string[] Extensions { get; } = [".fs", ".fsi", ".fsx"];

	public LanguageOptions BindOptions(FileConfiguration configuration, CurbEditorConfig editorConfig)
	{
		var path = fileSystem.Path.GetFullPath(configuration.FileName);
		var directory = fileSystem.Path.GetDirectoryName(path) ?? path;

		// Only charset is read from this, for the byte-order mark; the layout itself is Fantomas's.
		var layout = LayoutOptionsBinder.Bind(configuration.Properties, null, new LayoutOptions());
		var excluded = configuration.Properties.TryGetValue("generated_code", out var generated)
			&& generated.Equals("true", StringComparison.OrdinalIgnoreCase);

		var manifest = _manifests.Find(directory);

		if (manifest is null)
			return new FSharpOptions(layout, excluded, null, null);

		var extension = fileSystem.Path.GetExtension(path).ToLowerInvariant();
		var ignore = IgnoreFingerprint(directory);
		if (!_keys.TryGetValue((manifest, extension, ignore), out var key))
		{
			// Folded into the cache key with the file's settings. A different Fantomas formats differently,
			// a signature file is parsed differently, and a file the ignore file covered is recorded as
			// left alone, which stops being true the moment the ignore file changes.
			key = new Dictionary<string, string>(StringComparer.Ordinal)
			{
				["curb_fsharp_formatter"] = manifest.ToolId,
				["curb_fsharp_formatter_version"] = manifest.Version,
				["curb_fsharp_extension"] = extension,
				["curb_fsharp_ignore"] = ignore,
			};
			_keys[(manifest, extension, ignore)] = key;
		}

		return new FSharpOptions(layout, excluded, manifest, key);
	}

	/// <summary>A file whose leading comments say it is generated, as a project's <c>AssemblyInfo.fs</c> often does.</summary>
	public bool SkipSource(string source) => HasGeneratedHeader(source);

	public async Task<FormatResult[]> FormatBatchAsync(IReadOnlyList<ExternalFile> files, bool produceText, CancellationToken cancellationToken)
	{
		var runner = new FantomasRunner(fileSystem, launcher, environment, Warn, Error);
		return await runner.RunAsync(files, produceText, cancellationToken).ConfigureAwait(false);
	}

	public IReadOnlyCollection<string> Warnings
	{
		get
		{
			lock (_warnings)
				return [.. _warnings.Order(StringComparer.Ordinal)];
		}
	}

	public IReadOnlyCollection<string> Errors
	{
		get
		{
			lock (_errors)
				return [.. _errors];
		}
	}

	private void Warn(string warning)
	{
		lock (_warnings)
			_warnings.Add(warning);
	}

	private void Error(string error)
	{
		lock (_errors)
			_errors.Add(error);
	}

	/// <summary>
	/// The nearest <c>.fantomasignore</c> at or above <paramref name="directory"/>, as its path and a hash of
	/// its content; empty when there is none. Fantomas reads only the nearest one, matching paths relative to
	/// its folder, so both halves decide what it ignores.
	/// </summary>
	private string IgnoreFingerprint(string directory)
	{
		if (_ignoreByDirectory.TryGetValue(directory, out var known))
			return known;

		var visited = new List<string>();
		var answer = "";
		for (var current = directory; !string.IsNullOrEmpty(current); current = fileSystem.Path.GetDirectoryName(current))
		{
			if (_ignoreByDirectory.TryGetValue(current, out var cached))
			{
				answer = cached;
				break;
			}

			visited.Add(current);
			var candidate = fileSystem.Path.Combine(current, ".fantomasignore");
			if (fileSystem.File.Exists(candidate))
			{
				try
				{
					answer = candidate + "\n" + Fingerprint.OfContent(fileSystem.File.ReadAllBytes(candidate)).ToString("x32", CultureInfo.InvariantCulture);
				}
				catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
				{
					// Unreadable: key it so it never matches a recorded entry, and let Fantomas decide.
					answer = candidate + "\nunreadable";
				}
				break;
			}
		}

		foreach (var folder in visited)
			_ignoreByDirectory[folder] = answer;
		return answer;
	}

	/// <summary>
	/// True when the comments the file opens with contain <c>&lt;auto-generated</c>. Only the leading
	/// comments, so a file that merely mentions the marker further down is still formatted.
	/// </summary>
	internal static bool HasGeneratedHeader(string source)
	{
		var position = 0;
		var inBlock = false;
		while (position < source.Length)
		{
			var lineEnd = source.IndexOf('\n', position);
			var line = source.AsSpan(position, (lineEnd < 0 ? source.Length : lineEnd) - position).Trim();
			position = lineEnd < 0 ? source.Length : lineEnd + 1;

			if (line.IsEmpty && !inBlock)
				continue;

			if (inBlock || line.StartsWith("(*") || line.StartsWith("//"))
			{
				if (line.Contains("<auto-generated", StringComparison.OrdinalIgnoreCase))
					return true;

				if (line.StartsWith("(*"))
					inBlock = true;
				if (inBlock && line.EndsWith("*)"))
					inBlock = false;
				continue;
			}

			return false;
		}

		return false;
	}
}

/// <summary>What one F# file was bound to: its byte-order mark, and the Fantomas that formats it.</summary>
internal sealed class FSharpOptions(LayoutOptions layout, bool excluded, FantomasManifest? manifest, IReadOnlyDictionary<string, string>? key) : LanguageOptions
{
	/// <summary>The manifest that declares Fantomas for the file; null when none does, and the file is declined.</summary>
	public FantomasManifest? Manifest { get; } = manifest;

	public override LayoutOptions Layout { get; } = layout;

	public override bool Excluded { get; } = excluded;

	public override bool Claimed => Manifest is not null;

	public override string? DeclinedReason => Manifest is null ? FSharpLanguage.NoManifest : null;

	public override IReadOnlyDictionary<string, string>? AdditionalProperties { get; } = key;

	public override bool ReportsCoverage => false;
}
