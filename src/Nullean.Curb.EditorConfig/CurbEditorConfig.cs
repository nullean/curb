using System.IO.Abstractions;
using EditorConfig.Core;

namespace Nullean.Curb.EditorConfig;

/// <summary>
/// Resolves <c>.editorconfig</c> settings for source files.
/// </summary>
/// <remarks>
/// Wraps <see cref="EditorConfigParser"/> and holds it for the lifetime of a run so its
/// per-directory chain cache and compiled-glob cache stay warm. Formatting a repository then
/// resolves configuration once per directory rather than once per file.
/// </remarks>
public sealed class CurbEditorConfig
{
	private readonly IFileSystem _fileSystem;
	private readonly EditorConfigParser _parser;
	private readonly Dictionary<string, EditorConfigResolvedChain> _chains = new(StringComparer.Ordinal);

	// A custom factory rather than EditorConfigParser's default constructor, which routes every parse
	// through EditorConfig.Core's own EditorConfigFileCache — a cache that is *static*, process-wide,
	// and keyed on `{path}|{LastWriteTimeUtc.Ticks}|{Length}`. Two unrelated tests standing up a fresh
	// MockFileSystem at the same conventional path (`/repo/.editorconfig` and the like) with different,
	// same-length content can — and under parallel execution measurably do — collide on that key and
	// silently read back each other's parsed settings, since MockFileSystem's clock resolution is
	// coarser than the collision needs. `_files` is this parser instance's own replacement: scoped to
	// one CurbEditorConfig (one filesystem snapshot for the run), so it cannot leak between instances
	// the way the static cache does, while still avoiding a re-parse of the same ancestor
	// `.editorconfig` file for every sibling directory that shares it.
	private readonly Dictionary<string, EditorConfigFile> _files = new(StringComparer.Ordinal);

	/// <param name="fileSystem">
	/// Injected so tests can run against <c>MockFileSystem</c>. Every filesystem touch in Curb goes
	/// through this — there are no direct <c>System.IO.File</c> calls.
	/// </param>
	public CurbEditorConfig(IFileSystem fileSystem)
	{
		_fileSystem = fileSystem;
		_parser = new EditorConfigParser(ParseFile, fileSystem: fileSystem);
	}

	private EditorConfigFile ParseFile(string path)
	{
		if (!_files.TryGetValue(path, out var file))
		{
			file = EditorConfigFile.Parse(path, _fileSystem);
			_files[path] = file;
		}
		return file;
	}

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
