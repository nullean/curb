namespace Nullean.Curb.Printing.CSharp;

/// <summary>
/// Turns a <c>file_header_template</c> value into the text <c>PrintFileHeader</c> writes and
/// <c>ContentVerifier</c> checks for.
/// </summary>
/// <remarks>
/// One place for the two things every reader of the template has to agree on: the <c>{fileName}</c>
/// substitution, and the literal <c>\n</c> escape an <c>.editorconfig</c> value uses in place of a
/// real line break. Kept out of the printer and the formatter both, since they need the identical
/// answer and previously computed it twice.
/// </remarks>
internal static class FileHeaderText
{
	/// <summary>The template's lines, in order, with <c>{fileName}</c> substituted.</summary>
	/// <param name="template">The <c>file_header_template</c> value, lines separated by a literal <c>\n</c>.</param>
	/// <param name="fileName">
	/// The file's name, or null when the caller has none — substituted as an empty string, matching
	/// Roslyn's own behaviour for a document with no path.
	/// </param>
	public static string[] Lines(string template, string? fileName) =>
		template.Replace("{fileName}", fileName ?? string.Empty, StringComparison.Ordinal).Split("\\n");

	/// <summary>
	/// The header as Curb writes it: each line as its own <c>//</c> comment, concatenated with no
	/// separator. The verifier ignores whitespace between characters, so the line breaks between
	/// comments do not need to be reproduced here.
	/// </summary>
	/// <param name="template">The <c>file_header_template</c> value, lines separated by a literal <c>\n</c>.</param>
	/// <param name="fileName">The file's name, or null when the caller has none.</param>
	public static string Rendered(string template, string? fileName) =>
		string.Concat(Lines(template, fileName).Select(line => line.Length == 0 ? "//" : "// " + line));
}
