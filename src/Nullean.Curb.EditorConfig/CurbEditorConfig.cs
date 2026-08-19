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
