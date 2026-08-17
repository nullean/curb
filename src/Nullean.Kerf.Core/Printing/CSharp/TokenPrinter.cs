using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Nullean.Kerf.Documents;

namespace Nullean.Kerf.Printing.CSharp;

/// <summary>
/// Emits a token together with the trivia attached to it.
/// </summary>
/// <remarks>
/// <para>
/// All trivia handling lives here rather than being sprinkled through the node printers. That is the
/// single most important structural decision in a formatter: comments, blank lines and preprocessor
/// directives attach to tokens, and every formatter that handles them ad hoc in node printers ends
/// up losing or duplicating them somewhere.
/// </para>
/// <para>
/// Whitespace trivia is dropped, because layout is the printer's job. Everything else is preserved:
/// comments keep their text, disabled <c>#if</c> branches pass through untouched, and directives are
/// emitted but excluded from width measurement so they cannot push real code into wrapping.
/// </para>
/// </remarks>
internal static class TokenPrinter
{
	public static void Print(SyntaxToken token, PrintContext context)
	{
		PrintLeadingTrivia(token, context);

		var span = token.Span;
		if (span.Length > 0)
			context.Arena.SourceText(span.Start, span.Length);

		PrintTrailingTrivia(token, context);
		context.PrintedTokens++;
		context.PreviousToken = token;
	}

	/// <summary>Emits a token and its trailing trivia, but not its leading trivia.</summary>
	/// <remarks>
	/// For callers that need the leading trivia printed at a different indent than the token, which
	/// is the case for a comment sitting just above a closing brace: the comment belongs with the
	/// block's contents, the brace does not.
	/// </remarks>
	public static void PrintWithoutLeadingTrivia(SyntaxToken token, PrintContext context)
	{
		var span = token.Span;
		if (span.Length > 0)
			context.Arena.SourceText(span.Start, span.Length);

		PrintTrailingTrivia(token, context);
		context.PrintedTokens++;
		context.PreviousToken = token;
	}

	/// <summary>True when a token carries a comment or directive ahead of it.</summary>
	public static bool HasLeadingContent(SyntaxToken token)
	{
		foreach (var trivia in token.LeadingTrivia)
		{
			if (trivia.Kind() is not (SyntaxKind.WhitespaceTrivia or SyntaxKind.EndOfLineTrivia))
				return true;
		}
		return false;
	}

	/// <summary>True when a token carries a comment or directive on either side of it.</summary>
	/// <remarks>
	/// Asked of a trailing comma the printer is considering dropping. Dropping the token drops its
	/// trivia with it, and a comma either side of a comment is not worth losing the comment for, so
	/// such a separator is printed exactly as written instead.
	/// </remarks>
	public static bool HasAnyContent(SyntaxToken token)
	{
		if (HasLeadingContent(token))
			return true;

		foreach (var trivia in token.TrailingTrivia)
		{
			if (trivia.Kind() is not (SyntaxKind.WhitespaceTrivia or SyntaxKind.EndOfLineTrivia))
				return true;
		}
		return false;
	}

	/// <summary>Emits a token only if it is actually present, for optional syntax like a trailing semicolon.</summary>
	public static void PrintIfPresent(SyntaxToken token, PrintContext context)
	{
		if (token.RawKind != 0)
			Print(token, context);
	}

	/// <param name="token">The token whose leading trivia to emit.</param>
	/// <param name="context">Per-file printing state.</param>
	/// <param name="trailingBreak">
	/// Emit a break after each comment. Every comment is already preceded by a conditional break, so
	/// a run of them stays correct without this; it exists for the caller that supplies its own break
	/// afterwards — a closing brace, whose comment belongs at the inner indent but whose own line
	/// must be at the outer one. Emitting both breaks would insert a blank line, and the next run
	/// would then preserve it and add another.
	/// </param>
	internal static void PrintLeadingTrivia(SyntaxToken token, PrintContext context, bool trailingBreak = true)
	{
		var leading = token.LeadingTrivia;
		if (leading.Count == 0)
			return;

		var arena = context.Arena;

		// Blank lines are only preserved *between* things, never introduced at the start of a run,
		// and never more than one however many the source had.
		var pendingNewLines = 0;
		var emittedAnything = false;

		// True while emitting a run of comments that began aligned under a trailing comment.
		var alignedRun = false;

		foreach (var trivia in leading)
		{
			switch (trivia.Kind())
			{
				case SyntaxKind.WhitespaceTrivia:
					break;

				case SyntaxKind.EndOfLineTrivia:
					pendingNewLines++;
					break;

				case SyntaxKind.SingleLineCommentTrivia:
				case SyntaxKind.MultiLineCommentTrivia:
				case SyntaxKind.SingleLineDocumentationCommentTrivia:
				case SyntaxKind.MultiLineDocumentationCommentTrivia:
					// Roslyn puts everything up to and including the end of a line into the *trailing*
					// trivia of the token before it, so anything reaching leading trivia genuinely
					// starts a fresh line and must be emitted at one. Without this the comment glues
					// onto whatever the enclosing printer emitted last, and the next run then reads it
					// back as trailing trivia — so the output never settles.
					// Captured before the flush, which zeroes it — the old check read it afterwards
					// and so was always true.
					var priorNewLines = pendingNewLines;
					FlushBlankLine(arena, ref pendingNewLines, emittedAnything);

					// dotnet format aligns a comment sitting directly under a trailing comment to
					// that comment's column, and normalises every other comment to the statement
					// indent. Kerf used to normalise both, which pulled hand-aligned continuations
					// back to the left.
					//
					// The whole run aligns, not only the comment that starts it. Aligning the first
					// and dropping the rest to the statement indent is neither what dotnet format
					// writes nor stable: the ragged result read back differently on the next run.
					var startsAligned = !emittedAnything
						&& priorNewLines == 0
						&& AlignsUnderTrailingComment(trivia, context);

					if (startsAligned || (alignedRun && priorNewLines <= 1))
					{
						arena.AlignedLine(TrailingCommentAnchor);
						alignedRun = true;
					}
					else
					{
						arena.HardLine(DocFlags.OnlyIfNotAtLineStart);
						alignedRun = false;
					}

					pendingNewLines = EmitTriviaText(trivia, context, CommentFlags(trivia));
					if (trailingBreak)
						arena.HardLine();
					emittedAnything = true;
					break;

				case SyntaxKind.DisabledTextTrivia:
					// Code inside a false #if branch is never reformatted; it is not even parsed.
					FlushBlankLine(arena, ref pendingNewLines, emittedAnything);
					arena.Trim();
					arena.LiteralLine(DocFlags.OnlyIfNotAtLineStart);
					EmitVerbatimBlock(trivia, context);
					emittedAnything = true;
					pendingNewLines = 0;
					break;

				default:
					if (!trivia.IsDirective)
						break;

					// A directive must be the first non-whitespace on its line, so break first unless
					// we are already at a line start. It is emitted but never counted against the
					// width of the code around it.
					FlushBlankLine(arena, ref pendingNewLines, emittedAnything);

					// Regions are indented with the code they wrap; conditional-compilation
					// directives are not, and sit at column 0.
					if (trivia.IsKind(SyntaxKind.RegionDirectiveTrivia) || trivia.IsKind(SyntaxKind.EndRegionDirectiveTrivia))
					{
						arena.HardLine(DocFlags.OnlyIfNotAtLineStart);
					}
					else
					{
						arena.Trim();
						arena.LiteralLine(DocFlags.OnlyIfNotAtLineStart);
					}

					pendingNewLines = EmitTriviaText(trivia, context, DocFlags.IsDirective);
					arena.HardLine();
					emittedAnything = true;
					break;
			}
		}

		// Blank lines immediately before the token itself.
		FlushBlankLine(arena, ref pendingNewLines, emittedAnything);
	}

	/// <summary>Register holding the column of the most recent trailing comment.</summary>
	private const int TrailingCommentAnchor = 0;

	/// <summary>
	/// True when this comment sits on the line directly below one that ended in a trailing comment.
	/// </summary>
	/// <remarks>
	/// Walking to the previous token allocates, so it is asked only of a token that actually carries
	/// a leading comment — which is uncommon, and never in the middle of an expression.
	/// </remarks>
	private static bool AlignsUnderTrailingComment(SyntaxTrivia comment, PrintContext context)
	{
		var previous = context.PreviousToken;
		if (previous.RawKind == 0)
			return false;

		var trailing = previous.TrailingTrivia;
		if (trailing.Count == 0)
			return false;

		var last = trailing[^1];
		if (!last.IsKind(SyntaxKind.SingleLineCommentTrivia) && !last.IsKind(SyntaxKind.MultiLineCommentTrivia))
		{
			// A line ending stays in the trailing trivia after the comment, so look past one.
			if (trailing.Count < 2)
				return false;

			last = trailing[^2];
			if (!last.IsKind(SyntaxKind.SingleLineCommentTrivia) && !last.IsKind(SyntaxKind.MultiLineCommentTrivia))
				return false;
		}

		// A brace Kerf is about to synthesise would land between the two comments.
		if (Printers.ClosesSynthesisedBlock(previous, context))
			return false;

		var lines = context.Text.Lines;
		return lines.GetLineFromPosition(comment.SpanStart).LineNumber
			== lines.GetLineFromPosition(last.SpanStart).LineNumber + 1;
	}

	internal static void PrintTrailingTrivia(SyntaxToken token, PrintContext context)
	{
		var trailing = token.TrailingTrivia;
		if (trailing.Count == 0)
			return;

		// Only separate a trailing comment from the token before it if the source did. Otherwise
		// `Call(/* c */ arg)` gains a space after the parenthesis that it never had, while
		// `Call(); // why` still keeps the one it did.
		var separated = false;

		foreach (var trivia in trailing)
		{
			switch (trivia.Kind())
			{
				case SyntaxKind.WhitespaceTrivia:
					separated = true;
					continue;

				case SyntaxKind.SingleLineCommentTrivia:
				case SyntaxKind.MultiLineCommentTrivia:
					if (separated)
						context.Arena.Synthetic(SyntheticText.Space);
					separated = false;

					// Where this comment starts is where a comment on the next line aligns to.
					context.Arena.Anchor(TrailingCommentAnchor);
					EmitTriviaText(trivia, context, CommentFlags(trivia));

					if (trivia.IsKind(SyntaxKind.SingleLineCommentTrivia))
					{
						// A // comment runs to end of line, so whatever follows must start a new one.
						context.Arena.HardLine();
					}
					else
					{
						// A /* */ comment can have code after it on the same line; keep them apart.
						context.Arena.Synthetic(SyntheticText.Space);
					}
					break;

				default:
					break;
			}
		}
	}

	/// <summary>
	/// A <c>//</c> comment swallows the rest of its line, so the printer has to know it wrote one.
	/// </summary>
	private static DocFlags CommentFlags(SyntaxTrivia trivia) =>
		trivia.IsKind(SyntaxKind.SingleLineCommentTrivia) || trivia.IsKind(SyntaxKind.SingleLineDocumentationCommentTrivia)
			? DocFlags.LineComment
			: DocFlags.None;

	/// <summary>Emits at most one blank line, and never before anything has been written.</summary>
	private static void FlushBlankLine(DocArena arena, ref int pendingNewLines, bool emittedAnything)
	{
		if (pendingNewLines > 1 && emittedAnything)
			arena.HardLine();
		pendingNewLines = 0;
	}

	/// <summary>Emits trivia text, less any line ending it carries — the printer supplies those.</summary>
	/// <remarks>
	/// Uses <c>FullSpan</c>, not <c>Span</c>. Documentation comments are structured trivia: the
	/// <c>///</c> and <c>/**</c> markers are exterior trivia inside the structure, so <c>Span</c>
	/// begins after them and emitting it would silently strip the marker off every doc comment.
	/// </remarks>
	/// <returns>
	/// The number of line endings the trivia carried and this method trimmed. Directives and
	/// documentation comments hold their own terminating newline inside the span, whereas a <c>//</c>
	/// comment is followed by a separate end-of-line trivia. Blank-line counting has to know which,
	/// or a blank line after a directive is silently swallowed.
	/// </returns>
	private static int EmitTriviaText(SyntaxTrivia trivia, PrintContext context, DocFlags flags)
	{
		var span = trivia.FullSpan;
		var length = TrimTrailingNewLines(context, span.Start, span.Length);
		if (length > 0)
			EmitSourceLines(span.Start, length, context, flags);

		return CountNewLines(context, span.Start + length, span.Length - length);
	}

	/// <summary>
	/// Emits a span of source, re-issuing any line ending inside it through the printer.
	/// </summary>
	/// <remarks>
	/// Roslyn groups consecutive <c>///</c> lines into a single trivia node, so a doc comment arrives
	/// here as one span with its own newlines in it. Emitted verbatim, those keep whatever the source
	/// used while every break Kerf writes uses <c>end_of_line</c> — which left files with both, and
	/// cost efcore 3,189 of its fixed points and log4net 322 of theirs.
	///
	/// A literal line is the right break: it carries no indent, so the continuation lines keep the
	/// leading whitespace they already have inside the span. Single-line trivia never finds a newline
	/// and takes the same single call it always did.
	/// </remarks>
	private static void EmitSourceLines(int start, int length, PrintContext context, DocFlags flags)
	{
		var text = context.Text;
		var end = start + length;
		var chunk = start;

		for (var i = start; i < end; i++)
		{
			if (text[i] != '\n')
				continue;

			// Back off a CR so it is not emitted as content; the printer supplies the ending.
			var stop = i > chunk && text[i - 1] == '\r' ? i - 1 : i;
			if (stop > chunk)
				context.Arena.SourceText(chunk, stop - chunk, flags);

			context.Arena.LiteralLine();
			chunk = i + 1;
		}

		if (end > chunk)
			context.Arena.SourceText(chunk, end - chunk, flags);
	}

	/// <summary>Counts the line endings in a range, treating CRLF as one.</summary>
	private static int CountNewLines(PrintContext context, int start, int length)
	{
		var newLines = 0;
		for (var i = start; i < start + length; i++)
		{
			if (context.Text[i] == '\n')
				newLines++;
		}
		return newLines;
	}

	/// <summary>
	/// Emits a multi-line run verbatim, keeping its own line structure and its own indentation.
	/// </summary>
	private static void EmitVerbatimBlock(SyntaxTrivia trivia, PrintContext context)
	{
		var span = trivia.FullSpan;
		var length = TrimTrailingNewLines(context, span.Start, span.Length);
		if (length <= 0)
			return;

		EmitVerbatimRange(context, span.Start, length);
	}

	/// <summary>
	/// Emits <paramref name="length"/> characters from <paramref name="start"/> exactly as written,
	/// splitting on newlines so that the printer does not re-indent them.
	/// </summary>
	internal static void EmitVerbatimRange(PrintContext context, int start, int length)
	{
		var source = context.Text;
		var arena = context.Arena;
		var lineStart = start;
		var end = start + length;

		for (var i = start; i < end; i++)
		{
			if (source[i] != '\n')
				continue;

			var lineLength = i - lineStart;
			// Drop a \r that belongs to this \n; the printer emits the configured line ending.
			if (lineLength > 0 && source[lineStart + lineLength - 1] == '\r')
				lineLength--;

			if (lineLength > 0)
				arena.SourceText(lineStart, lineLength);
			arena.LiteralLine();
			lineStart = i + 1;
		}

		if (end > lineStart)
			arena.SourceText(lineStart, end - lineStart);
	}

	private static int TrimTrailingNewLines(PrintContext context, int start, int length)
	{
		var source = context.Text;
		while (length > 0 && source[start + length - 1] is '\n' or '\r')
			length--;
		return length;
	}
}
