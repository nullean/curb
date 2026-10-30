using System.IO.Abstractions;
using EditorConfig.Core;

namespace Nullean.Curb.EditorConfig;

/// <summary>
/// Resolves <c>.editorconfig</c> settings for source files.
/// </summary>
/// <remarks>
/// Wraps <see cref="EditorConfigParser"/> and holds it for the lifetime of a run so its
/// per-directory chain cache, compiled-glob cache and file cache all stay warm. Formatting a
/// repository then resolves configuration once per directory rather than once per file.
/// </remarks>
/// <param name="fileSystem">
/// Injected so tests can run against <c>MockFileSystem</c>. Every filesystem touch in Curb goes
/// through this — there are no direct <c>System.IO.File</c> calls.
/// </param>
public sealed class CurbEditorConfig(IFileSystem fileSystem)
{
	// EditorConfigParser's file cache is private per instance by default as of editorconfig 0.18.0
	// (editorconfig/editorconfig-core-net#64, filed from this repo — nullean/curb#65). Before that,
	// the default constructor routed every parse through a static, process-wide cache keyed on
	// path+mtime+length with no regard for which IFileSystem produced it, so two MockFileSystem-backed
	// tests reusing the same conventional path could collide and one would silently read the other's
	// settings. Fixed upstream; no workaround needed here any more.
	private readonly EditorConfigParser _parser = new(fileSystem);
	private readonly Dictionary<string, EditorConfigResolvedChain> _chains = new(StringComparer.Ordinal);
	private readonly Dictionary<string, GlobMatcher> _matchers = new(StringComparer.Ordinal);

	/// <summary>The options the parser itself matches section globs with.</summary>
	private static readonly GlobMatcherOptions GlobOptions = new() { Dot = true, MatchBase = true, AllowWindowsPaths = true };

	/// <summary>Resolves the settings that apply to <paramref name="filePath"/>.</summary>
	public FileConfiguration For(string filePath) => _parser.Parse(filePath, ChainFor(Path.GetDirectoryName(filePath) ?? "."));

	/// <summary>
	/// The settings that apply to <paramref name="filePath"/> from explicit sections only: those whose
	/// glob names an extension or a file name, such as <c>[*.csproj]</c> or
	/// <c>[Directory.Packages.props]</c>, and not <c>[*]</c>, <c>[**]</c> or <c>[*.*]</c>.
	/// </summary>
	/// <remarks>
	/// XML reads the core keys from these alone. A repository's <c>[*]</c> section is written for its
	/// code; applying its four-space indent and its width to project files would rewrite every one of
	/// them, which nobody configured. The sections are the resolved chain's own, matched with the
	/// parser's glob options and applied in its order, so precedence is the parser's;
	/// <see cref="Matching"/> with every section reproduces <see cref="For"/>, and a test holds it to that.
	/// </remarks>
	public IReadOnlyDictionary<string, string> ExplicitFor(string filePath) => Matching(filePath, explicitOnly: true);

	/// <summary>The settings from the sections that match <paramref name="filePath"/>, in the chain's order.</summary>
	internal Dictionary<string, string> Matching(string filePath, bool explicitOnly)
	{
		var properties = new Dictionary<string, string>(StringComparer.Ordinal);
		var path = filePath.Replace('\\', '/');
		foreach (var section in ChainFor(Path.GetDirectoryName(filePath) ?? ".").Sections)
		{
			if (explicitOnly && !IsExplicit(section.Glob))
				continue;

			if (!_matchers.TryGetValue(section.Glob, out var matcher))
			{
				matcher = GlobMatcher.Create(section.Glob, GlobOptions);
				_matchers[section.Glob] = matcher;
			}

			if (!matcher.IsMatch(path))
				continue;

			foreach (var key in section.Keys)
				properties[key] = section[key];
		}

		return properties;
	}

	private static readonly System.Buffers.SearchValues<char> Wildcards = System.Buffers.SearchValues.Create("*?.{},[]!");

	/// <summary>True when a section's glob names an extension or a file name in its last path segment.</summary>
	public static bool IsExplicit(string glob)
	{
		var segment = glob.AsSpan(glob.LastIndexOf('/') + 1);
		return segment.IndexOfAnyExcept(Wildcards) >= 0;
	}

	private EditorConfigResolvedChain ChainFor(string directory)
	{
		if (!_chains.TryGetValue(directory, out var chain))
		{
			chain = _parser.GetResolvedChainForDirectory(directory);
			_chains[directory] = chain;
		}

		return chain;
	}
}
