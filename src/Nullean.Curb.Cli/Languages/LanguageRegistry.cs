using System.IO.Abstractions;
using Nullean.Curb.Cli.Languages.CSharp;
using Nullean.Curb.Cli.Languages.FSharp;
using Nullean.Curb.Cli.Languages.Xml;

namespace Nullean.Curb.Cli.Languages;

/// <summary>Which language owns a file, decided by its extension.</summary>
/// <remarks>
/// Asked once per file the walk finds, so the lookup allocates nothing: a linear scan of a handful of
/// extensions with <see cref="string.EndsWith(string, StringComparison)"/> beats hashing a substring
/// that would have to be cut out of the path first.
/// </remarks>
internal sealed class LanguageRegistry
{
	/// <summary>The languages <c>curb format</c> and <c>curb check</c> cover.</summary>
	/// <remarks>
	/// <para>
	/// XML is on by default for project, props, targets, solution and <c>nuget.config</c> files; a file
	/// whose configuration sets <c>curb_xml_format = false</c> is declined before it is read.
	/// </para>
	/// <para>
	/// F# is formatted only where a local tool manifest declares Fantomas, and declined, with one line
	/// saying why, everywhere else. A new registry per run, because F# keeps per-run state: the manifests
	/// it found, and the warnings it collected.
	/// </para>
	/// </remarks>
	/// <param name="fileSystem">What F# reads manifests and ignore files through.</param>
	/// <param name="launcher">Starts Fantomas; the real one when absent.</param>
	/// <param name="environment">Where the SDK keeps its caches; this process's when absent.</param>
	public static LanguageRegistry Default(IFileSystem fileSystem, IToolLauncher? launcher = null, ToolEnvironment? environment = null) =>
		new(CSharpLanguage.Instance, XmlLanguage.Instance, FSharp(fileSystem, launcher, environment));

	/// <summary>F# alone, for the MSBuild integration's F# target.</summary>
	public static LanguageRegistry FSharpOnly(IFileSystem fileSystem, IToolLauncher? launcher = null, ToolEnvironment? environment = null) =>
		new(FSharp(fileSystem, launcher, environment));

	private static FSharpLanguage FSharp(IFileSystem fileSystem, IToolLauncher? launcher, ToolEnvironment? environment) =>
		new(fileSystem, launcher ?? ProcessLauncher.Instance, environment ?? ToolEnvironment.Current);

	/// <summary>
	/// C# alone, for the MSBuild integration: a build formats the files it compiles, never the project
	/// files driving it.
	/// </summary>
	public static LanguageRegistry CSharpOnly { get; } = new(CSharpLanguage.Instance);

	private readonly IFormattingLanguage[] _languages;

	// Flattened, so a lookup is one loop over every extension rather than a loop per language.
	private readonly string[] _extensions;
	private readonly int[] _owners;

	public LanguageRegistry(params IFormattingLanguage[] languages)
	{
		var extensions = new List<string>();
		var owners = new List<int>();

		for (var i = 0; i < languages.Length; i++)
		{
			if (languages[i] is not (IInProcessLanguage or IExternalLanguage))
				throw new ArgumentException($"'{languages[i].Id}' neither formats in process nor through another tool", nameof(languages));

			foreach (var extension in languages[i].Extensions)
			{
				if (extensions.Contains(extension, StringComparer.OrdinalIgnoreCase))
					throw new ArgumentException($"'{extension}' is claimed by more than one language", nameof(languages));

				extensions.Add(extension);
				owners.Add(i);
			}
		}

		_languages = languages;
		_extensions = [.. extensions];
		_owners = [.. owners];

		// The walk still asks the file system for one extension when there is only one, as it always
		// has: a bare `*` would hand back a path string for every file in the tree, .git included,
		// only for the extension check to throw nearly all of them away.
		SearchPattern = _extensions.Length == 1 ? "*" + _extensions[0] : "*";
	}

	/// <summary>How many languages are registered; indexes run from zero to one below it.</summary>
	public int Count => _languages.Length;

	/// <summary>The language at <paramref name="index"/>, as returned by <see cref="IndexOf"/>.</summary>
	public IFormattingLanguage this[int index] => _languages[index];

	/// <summary>The pattern a directory walk passes to the file system. The extension check still has the final say.</summary>
	public string SearchPattern { get; }

	/// <summary>The index of the language that owns <paramref name="path"/>, or -1 when none does.</summary>
	public int IndexOf(string path)
	{
		for (var i = 0; i < _extensions.Length; i++)
		{
			if (path.EndsWith(_extensions[i], StringComparison.OrdinalIgnoreCase))
				return _owners[i];
		}

		return -1;
	}
}
