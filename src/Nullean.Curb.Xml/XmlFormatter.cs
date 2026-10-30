using Nullean.Curb.Documents;
using Nullean.Curb.Printing;

namespace Nullean.Curb.Xml;

/// <summary>
/// Formats XML: MSBuild project files, solution files, and any other XML a repository opts in.
/// </summary>
/// <remarks>
/// <para>
/// Lex, build the tree, print to a document, lay it out, then re-lex the output and verify it. Each
/// stage can refuse the file, and a refused file is reported and left alone: malformed XML is a
/// <see cref="FormatStatus.SyntaxError"/>, and output that differs in anything but layout is a
/// <see cref="FormatStatus.VerificationFailed"/>.
/// </para>
/// <para>
/// Like <c>CSharpFormatter</c>'s, one instance owns pooled buffers and is reused for every file
/// a thread formats. Not thread-safe. No IO.
/// </para>
/// </remarks>
public sealed class XmlFormatter : IDisposable
{
	private readonly XmlTokenBuffer _tokens = new();
	private readonly XmlTokenBuffer _outputTokens = new();
	private readonly XmlTree _tree = new();
	private readonly DocArena _arena = new();
	private readonly DocPrinter<XmlLayout> _printer = new();
	private readonly OutputBuffer _output = new();

	/// <param name="source">The decoded text, without its byte-order mark.</param>
	/// <param name="options">The file's options, its <see cref="XmlOptions.Profile"/> included.</param>
	/// <param name="produceText">False for <c>check</c>, which only needs to know whether anything would change.</param>
	public FormatResult Format(string source, in XmlOptions options, bool produceText = true)
	{
		var span = source.AsSpan();
		if (!XmlLexer.Lex(span, _tokens, out var error) || !_tree.Build(span, _tokens, options.Profile, out error))
		{
			var status = _tree.TooDeep ? FormatStatus.TooDeep : FormatStatus.SyntaxError;
			return new FormatResult(status, false, null, 0, error.Describe(span));
		}

		// A file whose bytes happen to be valid UTF-8 (ASCII, say) can still declare another encoding,
		// and writing it back as UTF-8 would make the declaration a lie the next edit acts on.
		var encoding = DeclaredEncoding(span, _tokens);
		if (!encoding.IsEmpty && !IsUtf8(encoding))
		{
			return new FormatResult(
				FormatStatus.SyntaxError, false, null, 0,
				$"declares encoding '{encoding}'; only UTF-8 XML is rewritten, so the file is left unchanged");
		}

		var layout = ResolveLayout(span, options);

		_arena.Reset(source.Length / 8);
		_output.Reset(source.Length + 64);
		XmlPrinter.Print(span, _tokens, _tree, _arena, options, layout);

#if DEBUG
		DocValidator.Validate<XmlLayout>(_arena, source.Length);
#endif

		_printer.Print(_arena, source.AsMemory(), layout, _output);
		var written = _output.Written;

		if (!XmlLexer.Lex(written, _outputTokens, out var outputError))
			return new FormatResult(FormatStatus.VerificationFailed, false, null, 1, "verification failed: the output does not lex " + outputError.Describe(written));

		if (!XmlVerifier.Verify(span, _tokens.Tokens, written, _outputTokens.Tokens, out var failure))
			return new FormatResult(FormatStatus.VerificationFailed, false, null, 1, failure);

		var changed = !written.SequenceEqual(span);
		return new FormatResult(FormatStatus.Formatted, changed, produceText ? _output.ToString() : null, 1, null);
	}

	/// <summary>
	/// The layout settings the printer actually uses: the profile's own, then whatever the file keeps
	/// because <c>.editorconfig</c> did not say.
	/// </summary>
	internal static LayoutOptions ResolveLayout(ReadOnlySpan<char> source, in XmlOptions options)
	{
		var layout = options.Layout;

		// Visual Studio writes and rewrites solution files with two spaces, so any other indent would be
		// undone the next time a project is added.
		if (options.Profile == XmlProfile.Slnx)
			layout = layout with { IndentSize = 2, TabWidth = 2, UseTabs = false };

		if (layout.EndOfLine == EndOfLine.Auto)
		{
			var lineBreak = source.IndexOfAny('\r', '\n');
			if (lineBreak >= 0)
			{
				layout = layout with
				{
					EndOfLine = source[lineBreak] == '\n' ? EndOfLine.Lf
						: lineBreak + 1 < source.Length && source[lineBreak + 1] == '\n' ? EndOfLine.CrLf
						: EndOfLine.Cr,
				};
			}
		}

		if (!options.FinalNewLineSet)
			layout = layout with { InsertFinalNewLine = source.Length > 0 && source[^1] is '\n' or '\r' };

		// Trailing whitespace on a layout line is the author's unless the XML's own configuration asks
		// for it to go: an unset key, or one inherited from [*], leaves it where it is.
		if (!options.TrimTrailingWhitespaceSet)
			layout = layout with { TrimTrailingWhitespace = false };

		return layout;
	}

	/// <summary>The value of the XML declaration's <c>encoding</c>, or null when there is none.</summary>
	internal static ReadOnlySpan<char> DeclaredEncoding(ReadOnlySpan<char> source, XmlTokenBuffer tokens)
	{
		if (tokens.Count == 0 || tokens[0].Kind != XmlTokenKind.XmlDeclaration)
			return default;

		var declaration = source.Slice(tokens[0].Start, tokens[0].Length);
		var at = declaration.IndexOf("encoding", StringComparison.Ordinal);
		if (at < 0)
			return default;

		var rest = declaration[(at + "encoding".Length)..].TrimStart(" \t\r\n");
		if (rest.IsEmpty || rest[0] != '=')
			return default;

		rest = rest[1..].TrimStart(" \t\r\n");
		if (rest.IsEmpty || rest[0] is not ('"' or '\''))
			return default;

		var close = rest[1..].IndexOf(rest[0]);
		return close < 0 ? default : rest.Slice(1, close);
	}

	private static bool IsUtf8(ReadOnlySpan<char> encoding) =>
		encoding.Equals("utf-8", StringComparison.OrdinalIgnoreCase) || encoding.Equals("utf8", StringComparison.OrdinalIgnoreCase);

	/// <summary>Renders the document for a file, for debugging why it laid out the way it did.</summary>
	public string DumpDocumentTree(string source, in XmlOptions options)
	{
		var span = source.AsSpan();
		if (!XmlLexer.Lex(span, _tokens, out var error) || !_tree.Build(span, _tokens, options.Profile, out error))
			return "# does not parse: " + error.Describe(span);

		_arena.Reset(source.Length / 8);
		XmlPrinter.Print(span, _tokens, _tree, _arena, options, ResolveLayout(span, options));
		return DocDumper.Dump<XmlLayout>(_arena, source);
	}

	/// <summary>Returns the pooled buffers this formatter holds.</summary>
	public void Dispose()
	{
		_output.Dispose();
		_tokens.Dispose();
		_outputTokens.Dispose();
		_tree.Dispose();
	}
}
