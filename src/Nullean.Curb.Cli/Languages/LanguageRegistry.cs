using Nullean.Curb.Cli.Languages.CSharp;

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
	public static LanguageRegistry Default { get; } = new(CSharpLanguage.Instance);

	private readonly IInProcessLanguage[] _languages;

	// Flattened, so a lookup is one loop over every extension rather than a loop per language.
	private readonly string[] _extensions;
	private readonly int[] _owners;

	public LanguageRegistry(params IFormattingLanguage[] languages)
	{
		var inProcess = new IInProcessLanguage[languages.Length];
		var extensions = new List<string>();
		var owners = new List<int>();

		for (var i = 0; i < languages.Length; i++)
		{
			// Until FormattingRun can hand a batch to another tool, registering one would silently
			// drop its files, which is worse than not starting.
			inProcess[i] = languages[i] as IInProcessLanguage
				?? throw new NotSupportedException($"'{languages[i].Id}' formats through another tool, which this run cannot drive yet");

			foreach (var extension in languages[i].Extensions)
			{
				if (extensions.Contains(extension, StringComparer.OrdinalIgnoreCase))
					throw new ArgumentException($"'{extension}' is claimed by more than one language", nameof(languages));

				extensions.Add(extension);
				owners.Add(i);
			}
		}

		_languages = inProcess;
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
	public IInProcessLanguage this[int index] => _languages[index];

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
