using System.IO.Abstractions.TestingHelpers;
using Nullean.Curb.EditorConfig;
using Nullean.Curb.Options;

namespace Nullean.Curb.OptionDocs;

/// <summary>
/// Binds <c>.editorconfig</c> lines through the binder the CLI uses, so an example is formatted with
/// exactly the options <c>curb format</c> would resolve from the lines the page shows.
/// </summary>
internal static class EditorConfigText
{
	private static readonly string[] Header = ["root = true", "[*.cs]"];

	public static FormatOptions Bind(IReadOnlyList<string> lines, List<CurbDiagnostic> diagnostics)
	{
		var fileSystem = new MockFileSystem();
		var text = string.Join("\n", Header.Concat(lines));
		fileSystem.AddFile("/docs/.editorconfig", new MockFileData(text));
		fileSystem.AddFile("/docs/Example.cs", new MockFileData(string.Empty));
		var configuration = new CurbEditorConfig(fileSystem).For("/docs/Example.cs");
		return EditorConfigOptionsBinder.Bind(configuration, diagnostics);
	}
}
