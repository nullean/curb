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

	/// <summary>Resolves the settings that apply to <paramref name="filePath"/>.</summary>
	public FileConfiguration For(string filePath)
	{
		var directory = Path.GetDirectoryName(filePath) ?? ".";
		if (!_chains.TryGetValue(directory, out var chain))
		{
			chain = _parser.GetResolvedChainForDirectory(directory);
			_chains[directory] = chain;
		}
		return _parser.Parse(filePath, chain);
	}
}
