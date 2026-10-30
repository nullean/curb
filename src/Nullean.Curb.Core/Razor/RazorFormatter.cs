using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace Nullean.Curb.Razor;

/// <summary>
/// Formats the C# inside the <c>@code</c> and <c>@functions</c> blocks of a <c>.razor</c> or
/// <c>.cshtml</c> file, and nothing else in it. Experimental.
/// </summary>
/// <remarks>
/// <para>
/// Each block <see cref="RazorBlockScanner"/> finds is wrapped as the members of a class, formatted by
/// <see cref="CSharpFormatter"/> with every check that runs on a <c>.cs</c> file, unwrapped, prefixed with
/// the <c>@code</c> line's own leading whitespace exactly as written (the indentation the formatter adds
/// after it follows <c>indent_style</c>), and spliced back over exactly the lines between the <c>{</c> line and the
/// <c>}</c> line. Markup is never handed to a printer, so it cannot change; the result is still re-scanned
/// and every byte outside the blocks compared with the source before anything is returned.
/// </para>
/// <para>
/// A block that cannot be formatted safely is refused on its own and left as written, and the rest of
/// the file is still formatted. Razor markup inside C# (<c>@&lt;p&gt;</c>, <c>&lt;text&gt;</c>,
/// <c>@:</c>, a <c>@</c> transition) does not parse as C#, so a block holding any is refused that way.
/// </para>
/// <para>
/// The block keeps the file's own line endings whatever <c>end_of_line</c> says: the markup around it
/// is never rewritten, and a file with two kinds of line break is worse than either.
/// </para>
/// <para>Not thread-safe: holds a <see cref="CSharpFormatter"/> and its pooled buffers.</para>
/// </remarks>
public sealed class RazorFormatter : IDisposable
{
	/// <summary>The <c>.editorconfig</c> key that turns this on.</summary>
	public const string EnableKey = "curb_razor_format_code_blocks";

	/// <summary>The name of the class a block is wrapped in. Never written anywhere.</summary>
	private const string WrapperName = "__CurbRazor__";

	private readonly CSharpFormatter _csharp = new();
	private readonly List<RazorBlock> _blocks = [];
	private readonly List<RazorBlock> _rescan = [];
	private readonly StringBuilder _wrapped = new();

	/// <summary>How many blocks this formatter re-parsed to verify.</summary>
	public int RoundTripsChecked => _csharp.RoundTripsChecked;

	/// <param name="source">The file's text, without its byte-order mark.</param>
	/// <param name="options">C# options, bound from the Razor file's own <c>.editorconfig</c> settings.</param>
	/// <param name="produceText">False for <c>check</c>.</param>
	/// <param name="verifyRoundTrip">Re-parse each block's output where the printer put a token boundary at risk.</param>
	/// <param name="component">
	/// True for a <c>.razor</c> component, false for a <c>.cshtml</c> view, where only <c>@functions</c> is a directive.
	/// </param>
	/// <returns>
	/// <see cref="FormatStatus.Formatted"/> unless verification failed, with
	/// <see cref="FormatResult.Declined"/> and <see cref="FormatResult.Message"/> saying which blocks were
	/// left as written and why.
	/// </returns>
	public FormatResult Format(string source, in FormatOptions options, bool produceText = true, bool verifyRoundTrip = true, bool component = true)
	{
		_blocks.Clear();
		RazorBlockScanner.Scan(source, _blocks, component);
		if (_blocks.Count == 0)
			return new FormatResult(FormatStatus.Formatted, false, produceText ? source : null, 1, null);

		var replacements = new string?[_blocks.Count];
		StringBuilder? refusals = null;
		var declined = 0;
		var changed = false;

		for (var i = 0; i < _blocks.Count; i++)
		{
			var block = _blocks[i];
			var refusal = block.Refusal;
			string? detail = null;

			if (refusal == RazorBlockRefusal.None)
			{
				var result = FormatBlock(source, block, options, verifyRoundTrip, out refusal, out detail);
				if (result.Status == FormatStatus.VerificationFailed)
				{
					return new FormatResult(
						FormatStatus.VerificationFailed, false, null, 1,
						$"line {RazorBlockScanner.LineOf(source, block.Directive)}: {block.Name} block: {result.Message}");
				}

				if (refusal == RazorBlockRefusal.None && result.Text is { } text
					&& !source.AsSpan(block.InnerStart, block.InnerEnd - block.InnerStart).SequenceEqual(text))
				{
					replacements[i] = text;
					changed = true;
				}
			}

			if (refusal != RazorBlockRefusal.None)
			{
				declined++;
				refusals ??= new StringBuilder();
				if (refusals.Length > 0)
					refusals.Append("; ");
				refusals
					.Append("line ").Append(RazorBlockScanner.LineOf(source, block.Directive)).Append(": ")
					.Append(block.Name).Append(" block left as written, ").Append(Describe(refusal));
				if (detail is not null)
					refusals.Append(" (").Append(detail).Append(')');
			}
		}

		var message = refusals?.ToString();
		if (!changed)
			return new FormatResult(FormatStatus.Formatted, false, produceText ? source : null, 1, message) { Declined = declined };

		var output = Splice(source, replacements);
		if (!MarkupUnchanged(source, _blocks, output, replacements, _rescan, component, out var failure))
			return new FormatResult(FormatStatus.VerificationFailed, false, null, 1, failure);

		return new FormatResult(FormatStatus.Formatted, true, produceText ? output : null, 1, message) { Declined = declined };
	}

	/// <summary>What a refusal says in a message. Stable text: corpus reports group on it.</summary>
	public static string Describe(RazorBlockRefusal refusal) => refusal switch
	{
		RazorBlockRefusal.CloseBraceNotAlone => "its closing brace is not alone on its line",
		RazorBlockRefusal.Unclosed => "no closing brace",
		RazorBlockRefusal.DoesNotParse => "it does not parse as C# class members",
		RazorBlockRefusal.TooDeep => "it nests too deeply",
		RazorBlockRefusal.WrapperReshaped => "formatting moved the braces around it",
		_ => "not refused",
	};

	/// <summary>
	/// Formats one block's inner text. On success the result's text is the replacement for the bytes
	/// from <see cref="RazorBlock.InnerStart"/> to <see cref="RazorBlock.InnerEnd"/>.
	/// </summary>
	private FormatResult FormatBlock(
		string source,
		in RazorBlock block,
		in FormatOptions options,
		bool verifyRoundTrip,
		out RazorBlockRefusal refusal,
		out string? detail)
	{
		refusal = RazorBlockRefusal.None;
		detail = null;

		var inner = source.AsSpan(block.InnerStart, block.InnerEnd - block.InnerStart);

		// Nothing to format, and an empty class is the one shape whose braces a width may join.
		if (inner.IsWhiteSpace())
			return new FormatResult(FormatStatus.Formatted, false, null, 1, null);

		// The block keeps the line break its own `{` line ends with; see the class remarks.
		var newLine = source[block.InnerStart - 1] == '\n'
			? block.InnerStart >= 2 && source[block.InnerStart - 2] == '\r' ? "\r\n" : "\n"
			: "\r";

		var indentColumns = Columns(source.AsSpan(block.LineStart, block.Directive - block.LineStart), options.TabWidth);
		var blockOptions = options with
		{
			// A header belongs at the top of a file, and the wrapper is not one.
			FileHeaderTemplate = null,
			EndOfLine = newLine switch { "\r\n" => EndOfLine.CrLf, "\n" => EndOfLine.Lf, _ => EndOfLine.Cr },

			// Every line ends up indentColumns further right than the formatter sees it.
			MaxLineLength = options.MaxLineLength == FormatOptions.Off
				? FormatOptions.Off
				: Math.Max(1, options.MaxLineLength - indentColumns),
		};

		_wrapped.Clear();
		_wrapped.Append("class ").Append(WrapperName).Append(newLine).Append('{').Append(newLine);
		_wrapped.Append(inner);
		_wrapped.Append('}').Append(newLine);
		var wrapped = _wrapped.ToString();

		var result = _csharp.Format(wrapped, blockOptions, fileName: null, produceText: true, verifyRoundTrip: verifyRoundTrip);
		switch (result.Status)
		{
			case FormatStatus.SyntaxError:
				refusal = RazorBlockRefusal.DoesNotParse;
				detail = result.Message;
				return result;

			case FormatStatus.TooDeep:
				refusal = RazorBlockRefusal.TooDeep;
				return result;

			case FormatStatus.VerificationFailed:
				return result;
		}

		var formatted = result.Text!;
		if (!TryUnwrap(formatted, out var start, out var end))
		{
			refusal = RazorBlockRefusal.WrapperReshaped;
			return result;
		}

		var text = indentColumns == 0
			? formatted[start..end]
			: Indent(formatted, start, end, source[block.LineStart..block.Directive]);
		return new FormatResult(FormatStatus.Formatted, true, text, 1, null);
	}

	/// <summary>
	/// Finds the members inside the wrapper in the formatter's output: from just past the line break
	/// after its <c>{</c> to the start of the line holding its <c>}</c>.
	/// </summary>
	/// <remarks>
	/// Nothing between the wrapper's name and its <c>{</c> can hold a brace, and nothing follows its
	/// closing brace but line breaks, so the first <c>{</c> after the name and the last <c>}</c> are the
	/// wrapper's. False when the formatter put anything else on either brace's line.
	/// </remarks>
	internal static bool TryUnwrap(string formatted, out int start, out int end)
	{
		start = end = 0;
		var name = formatted.IndexOf(WrapperName, StringComparison.Ordinal);
		var open = name < 0 ? -1 : formatted.IndexOf('{', name + WrapperName.Length);
		var close = formatted.LastIndexOf('}');
		if (open < 0 || close <= open)
			return false;

		if (formatted.AsSpan(close + 1).IndexOfAnyExcept("\r\n") >= 0)
			return false;

		start = open + 1;
		if (start < formatted.Length && formatted[start] == '\r')
			start++;
		if (start < formatted.Length && formatted[start] == '\n')
			start++;
		if (start == open + 1)
			return false;

		end = close;
		while (end > start && formatted[end - 1] is ' ' or '\t')
			end--;
		return end == start || formatted[end - 1] is '\n' or '\r';
	}

	/// <summary>Columns <paramref name="whitespace"/> occupies, with tabs to the next tab stop.</summary>
	private static int Columns(ReadOnlySpan<char> whitespace, int tabWidth)
	{
		var columns = 0;
		foreach (var c in whitespace)
			columns = c == '\t' ? ((columns / tabWidth) + 1) * tabWidth : columns + 1;
		return columns;
	}

	/// <summary>
	/// Prefixes each line of <paramref name="formatted"/> from <paramref name="start"/> to
	/// <paramref name="end"/> with <paramref name="indent"/>, except a blank line and a line that starts
	/// inside a token or trivia whose text the formatter keeps as written.
	/// </summary>
	/// <remarks>
	/// The exceptions are a multi-line string (verbatim, raw or interpolated), whose content would change;
	/// the inside of a multi-line comment and the inactive branch of an <c>#if</c>, which the formatter
	/// keeps where they are in a <c>.cs</c> file too, so shifting them would move them again on every run;
	/// and a directive in the first column. Found with Roslyn's lexer over the formatter's output, so they
	/// are exactly the parser's.
	/// </remarks>
	private static string Indent(string formatted, int start, int end, string indent)
	{
		List<(int Start, int End)>? kept = null;
		foreach (var token in SyntaxFactory.ParseTokens(formatted, 0, 0, CSharpSource.ParseOptions))
		{
			if (token.SpanStart >= end)
				break;
			Keep(token.LeadingTrivia, ref kept);
			if (Spans(formatted, token.SpanStart, token.Span.End))
				(kept ??= []).Add((token.SpanStart, token.Span.End));
			Keep(token.TrailingTrivia, ref kept);
		}

		var builder = new StringBuilder(end - start + (indent.Length * 32));
		var keptIndex = 0;
		var line = start;
		while (line < end)
		{
			var next = formatted.AsSpan(line, end - line).IndexOfAny('\r', '\n');
			var lineEnd = next < 0 ? end : line + next;
			var breakEnd = lineEnd < end && formatted[lineEnd] == '\r' && lineEnd + 1 < end && formatted[lineEnd + 1] == '\n'
				? lineEnd + 2
				: Math.Min(end, lineEnd + 1);

			while (kept is not null && keptIndex < kept.Count && kept[keptIndex].End < line)
				keptIndex++;
			var inside = kept is not null && keptIndex < kept.Count && kept[keptIndex].Start < line && line < kept[keptIndex].End;

			// A directive the formatter put in the first column stays there, as it does in a .cs file.
			if (!inside && formatted[line] != '#' && !formatted.AsSpan(line, lineEnd - line).IsWhiteSpace())
				builder.Append(indent);
			builder.Append(formatted, line, breakEnd - line);
			line = breakEnd;
		}

		return builder.ToString();

		void Keep(SyntaxTriviaList trivia, ref List<(int Start, int End)>? list)
		{
			foreach (var item in trivia)
			{
				if (!Spans(formatted, item.SpanStart, item.Span.End))
					continue;

				// Inactive text starts at its line's first column, so its own first line is kept too;
				// a comment starts after its line's indentation, which is re-indented with the line.
				if (item.IsKind(SyntaxKind.DisabledTextTrivia))
					(list ??= []).Add((item.SpanStart - 1, item.Span.End));
				else if (item.IsKind(SyntaxKind.MultiLineCommentTrivia) || item.IsKind(SyntaxKind.MultiLineDocumentationCommentTrivia))
					(list ??= []).Add((item.SpanStart, item.Span.End));
			}
		}
	}

	private static bool Spans(string text, int start, int end) => text.AsSpan(start, end - start).IndexOfAny('\r', '\n') >= 0;

	/// <summary>The source with each block that has a replacement spliced in over its inner lines.</summary>
	private string Splice(string source, string?[] replacements)
	{
		var builder = new StringBuilder(source.Length + 256);
		var position = 0;
		for (var i = 0; i < replacements.Length; i++)
		{
			if (replacements[i] is not { } replacement)
				continue;
			var block = _blocks[i];
			builder.Append(source, position, block.InnerStart - position);
			builder.Append(replacement);
			position = block.InnerEnd;
		}

		builder.Append(source, position, source.Length - position);
		return builder.ToString();
	}

	/// <summary>
	/// Re-scans <paramref name="output"/> and requires the same blocks in the same order, every byte
	/// outside the replaced ones identical to the source, and each replaced block holding exactly its
	/// replacement.
	/// </summary>
	/// <remarks>
	/// The splice cannot touch markup by construction. This holds it to that independently of the splice's
	/// own arithmetic: the positions compared come from scanning the output, not from the source. It also
	/// catches formatted C# that would change where a later block starts or ends.
	/// </remarks>
	/// <param name="source">The file as it was read.</param>
	/// <param name="blocks">What the scanner found in <paramref name="source"/>.</param>
	/// <param name="output">The file about to be returned.</param>
	/// <param name="replacements">Each block's new inner text, or null where it was left as written.</param>
	/// <param name="rescan">A buffer for scanning <paramref name="output"/>.</param>
	/// <param name="component">Scan as a component rather than a view.</param>
	/// <param name="failure">What differs, for the message.</param>
	internal static bool MarkupUnchanged(
		string source,
		List<RazorBlock> blocks,
		string output,
		string?[] replacements,
		List<RazorBlock> rescan,
		bool component,
		out string? failure)
	{
		failure = null;
		rescan.Clear();
		RazorBlockScanner.Scan(output, rescan, component);
		if (rescan.Count != blocks.Count)
		{
			failure = $"the formatted file has {rescan.Count} @code/@functions block(s) where the source had {blocks.Count}";
			return false;
		}

		var inSource = 0;
		var inOutput = 0;
		for (var i = 0; i < blocks.Count; i++)
		{
			var before = blocks[i];
			var after = rescan[i];
			if (before.Refusal != after.Refusal || before.IsFunctions != after.IsFunctions)
			{
				failure = $"line {RazorBlockScanner.LineOf(source, before.Directive)}: the {before.Name} block scans differently once formatted";
				return false;
			}

			if (replacements[i] is not { } replacement)
				continue;

			if (!source.AsSpan(inSource, before.InnerStart - inSource).SequenceEqual(output.AsSpan(inOutput, after.InnerStart - inOutput))
				|| !output.AsSpan(after.InnerStart, after.InnerEnd - after.InnerStart).SequenceEqual(replacement))
			{
				failure = $"line {RazorBlockScanner.LineOf(source, before.Directive)}: markup around the {before.Name} block changed";
				return false;
			}

			inSource = before.InnerEnd;
			inOutput = after.InnerEnd;
		}

		if (!source.AsSpan(inSource).SequenceEqual(output.AsSpan(inOutput)))
		{
			failure = "markup after the last @code/@functions block changed";
			return false;
		}

		return true;
	}

	/// <summary>Returns the pooled buffers this formatter holds.</summary>
	public void Dispose() => _csharp.Dispose();
}
