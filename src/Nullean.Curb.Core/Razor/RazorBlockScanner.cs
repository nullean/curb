using System.Buffers;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace Nullean.Curb.Razor;

/// <summary>Why a <c>@code</c> or <c>@functions</c> block was left as written.</summary>
public enum RazorBlockRefusal : byte
{
	/// <summary>Not refused.</summary>
	None,

	/// <summary>The closing brace shares its line with something else.</summary>
	CloseBraceNotAlone,

	/// <summary>The C# lexer found no brace that closes the block.</summary>
	Unclosed,

	/// <summary>The inner text does not parse as the members of a class: Razor markup, usually.</summary>
	DoesNotParse,

	/// <summary>The C# nests deeper than Curb follows.</summary>
	TooDeep,

	/// <summary>The formatter moved the braces of the class the block was wrapped in.</summary>
	WrapperReshaped,
}

/// <summary>One <c>@code { … }</c> or <c>@functions { … }</c> block in a Razor file.</summary>
/// <remarks>
/// Only the bytes from <see cref="InnerStart"/> to <see cref="InnerEnd"/> are ever replaced: whole lines
/// between the line that opens the block and the line that closes it. Everything else in the file is
/// markup as far as Curb is concerned.
/// </remarks>
/// <param name="LineStart">Where the directive's line starts.</param>
/// <param name="Directive">Where the <c>@</c> of the directive is.</param>
/// <param name="IsFunctions">True for <c>@functions</c>, false for <c>@code</c>.</param>
/// <param name="OpenBrace">Where the block's <c>{</c> is.</param>
/// <param name="InnerStart">Just past the line break that ends the <c>{</c> line.</param>
/// <param name="InnerEnd">The start of the line holding the closing <c>}</c>; -1 when there is none.</param>
/// <param name="CloseBrace">Where the closing <c>}</c> is; -1 when there is none.</param>
/// <param name="Refusal">Why the scanner already refused it, or <see cref="RazorBlockRefusal.None"/>.</param>
public readonly record struct RazorBlock(
	int LineStart,
	int Directive,
	bool IsFunctions,
	int OpenBrace,
	int InnerStart,
	int InnerEnd,
	int CloseBrace,
	RazorBlockRefusal Refusal)
{
	/// <summary>The directive's name, for messages.</summary>
	public string Name => IsFunctions ? "@functions" : "@code";
}

/// <summary>
/// Finds the <c>@code</c> and <c>@functions</c> blocks in a <c>.razor</c> or <c>.cshtml</c> file without
/// parsing Razor.
/// </summary>
/// <remarks>
/// <para>
/// A candidate is a line whose first non-whitespace text is <c>@code</c> or <c>@functions</c>, then
/// optional spaces or tabs, then <c>{</c> and the end of the line. The <c>{</c> may also stand alone on
/// the next line. Lines inside a Razor comment (<c>@* *@</c>) or an HTML comment (<c>&lt;!-- --&gt;</c>)
/// are never candidates, and <c>@@code</c> is an escaped <c>@</c>, not a directive. <c>@code</c> is a
/// directive in a component (<c>.razor</c>) only; in a view (<c>.cshtml</c>) it is an expression followed
/// by markup, so a view's candidates are <c>@functions</c> blocks alone.
/// </para>
/// <para>
/// The closing brace is found by Roslyn's own lexer, run from the <c>{</c> and counting brace tokens, so
/// a brace inside a string of any kind (verbatim, interpolated with holes, raw), a character literal or
/// a comment is never counted. The closing brace has to be alone on its line; a block whose brace is not
/// is reported with <see cref="RazorBlockRefusal.CloseBraceNotAlone"/>.
/// </para>
/// <para>
/// Cheap by design: one pass over the characters that can start something (<c>@</c>, <c>&lt;</c> and
/// line breaks), and the lexer only from a candidate's brace to its end.
/// </para>
/// </remarks>
public static class RazorBlockScanner
{
	private static readonly SearchValues<char> Transitions = SearchValues.Create("@<");

	/// <summary>Appends every candidate block in <paramref name="source"/> to <paramref name="blocks"/>, in order.</summary>
	/// <remarks>
	/// <para>
	/// Candidates are only looked for in markup. Every region Razor parses as C# is skipped whole, matched
	/// with the same lexer as a block's own braces: <c>@{ }</c>, <c>@( )</c>, an implicit expression with its
	/// calls and indexers, <c>@if</c>/<c>@else</c>, <c>@for</c>, <c>@foreach</c>, <c>@while</c>, <c>@do</c>,
	/// <c>@switch</c>, <c>@using ( )</c>, <c>@lock</c> and <c>@try</c>/<c>@catch</c>/<c>@finally</c> with their
	/// bodies, and a <c>@code</c> or <c>@functions</c> block that is not a candidate. A line that looks like a
	/// block inside a string in any of them is never taken for one.
	/// </para>
	/// <para>
	/// When a region's end cannot be found (an unclosed <c>@{</c>, a statement whose shape it does not know),
	/// scanning stops: nothing after that point is a candidate, so nothing after it is formatted.
	/// </para>
	/// </remarks>
	/// <param name="source">The file's text.</param>
	/// <param name="blocks">Where the blocks go.</param>
	/// <param name="component">True for a <c>.razor</c> component, where <c>@code</c> is a directive too.</param>
	public static void Scan(string source, List<RazorBlock> blocks, bool component = true)
	{
		var text = source.AsSpan();
		var i = 0;

		while (i < text.Length)
		{
			var next = text[i..].IndexOfAny(Transitions);
			if (next < 0)
				return;

			i += next;
			if (text[i] == '<')
			{
				i = text[i..].StartsWith("<!--") ? SkipPast(text, i + 4, "-->") : i + 1;
				continue;
			}

			if (i + 1 >= text.Length)
				return;

			switch (text[i + 1])
			{
				case '@':
					// An escaped @: whatever follows it is text, never a directive or a comment.
					i += 2;
					continue;

				case '*':
					i = SkipPast(text, i + 2, "*@");
					continue;
			}

			// `name@example.com` is text, as it is to Razor.
			if (i > 0 && char.IsLetterOrDigit(text[i - 1]))
			{
				i++;
				continue;
			}

			var lineStart = i;
			while (lineStart > 0 && text[lineStart - 1] is ' ' or '\t')
				lineStart--;
			var firstOnLine = lineStart == 0 || text[lineStart - 1] is '\n' or '\r';

			if (firstOnLine && TryMatch(source, lineStart, i, component, out var block, out var resume))
			{
				blocks.Add(block);
				if (resume < 0)
					return;
				i = resume;
				continue;
			}

			var end = SkipCode(source, i, component);
			if (end < 0)
				return;
			i = end;
		}
	}

	/// <summary>
	/// The position just past the C# that the transition at <paramref name="at"/> starts, <c>at + 1</c> when
	/// it starts none, or -1 when its end cannot be found.
	/// </summary>
	private static int SkipCode(string source, int at, bool component)
	{
		var text = source.AsSpan();
		var start = at + 1;
		switch (text[start])
		{
			case '{':
				return PastMatch(source, start, SyntaxKind.OpenBraceToken, SyntaxKind.CloseBraceToken);
			case '(':
				return PastMatch(source, start, SyntaxKind.OpenParenToken, SyntaxKind.CloseParenToken);
		}

		if (!IsIdentifierStart(text[start]))
			return start;

		var nameEnd = start;
		while (nameEnd < text.Length && IsIdentifierPart(text[nameEnd]))
			nameEnd++;

		var name = text[start..nameEnd];
		if ((component && name.SequenceEqual("code")) || name.SequenceEqual("functions"))
		{
			// Not a candidate (text after its `{`, or not first on its line), but its body is still C#.
			var brace = BraceAfterDirective(text, nameEnd);
			return brace < 0 ? nameEnd : PastMatch(source, brace, SyntaxKind.OpenBraceToken, SyntaxKind.CloseBraceToken);
		}

		if (name is "if" or "for" or "foreach" or "while" or "switch" or "lock" or "try" or "do"
			|| (name.SequenceEqual("using") && SkipBlanks(text, nameEnd) < text.Length && text[SkipBlanks(text, nameEnd)] == '('))
			return PastStatement(source, nameEnd, name.SequenceEqual("do"));

		// An implicit expression: the name, then member accesses, calls and indexers with nothing between.
		var i = nameEnd;
		while (i < text.Length)
		{
			if (text[i] is '(' or '[')
			{
				i = text[i] == '('
					? PastMatch(source, i, SyntaxKind.OpenParenToken, SyntaxKind.CloseParenToken)
					: PastMatch(source, i, SyntaxKind.OpenBracketToken, SyntaxKind.CloseBracketToken);
				if (i < 0)
					return -1;
			}
			else if (text[i] == '.' && i + 1 < text.Length && IsIdentifierStart(text[i + 1]))
			{
				i++;
				while (i < text.Length && IsIdentifierPart(text[i]))
					i++;
			}
			else if (text[i] == '?' && i + 2 < text.Length && text[i + 1] == '.' && IsIdentifierStart(text[i + 2]))
				i++;
			else
				break;
		}

		return i;
	}

	private static bool IsIdentifierStart(char c) => char.IsLetter(c) || c == '_';

	private static bool IsIdentifierPart(char c) => char.IsLetterOrDigit(c) || c == '_';

	/// <summary>The <c>{</c> after a directive's name, on its line or alone at the start of the next; -1 when there is none.</summary>
	private static int BraceAfterDirective(ReadOnlySpan<char> text, int afterName)
	{
		var brace = SkipBlanks(text, afterName);
		if (brace < text.Length && text[brace] == '{')
			return brace;
		var nextLine = PastLineBreak(text, brace);
		if (nextLine < 0)
			return -1;
		brace = SkipBlanks(text, nextLine);
		return brace < text.Length && text[brace] == '{' ? brace : -1;
	}

	/// <summary>Just past the token that closes the one at <paramref name="open"/>, or -1.</summary>
	private static int PastMatch(string source, int open, SyntaxKind openKind, SyntaxKind closeKind)
	{
		var close = FindClose(source, open, openKind, closeKind);
		return close < 0 ? -1 : close + 1;
	}

	/// <summary>
	/// Just past a Razor statement whose keyword ends at <paramref name="afterKeyword"/>: its header, its
	/// body, and any <c>else</c>, <c>catch</c>, <c>finally</c> or a <c>do</c> loop's <c>while ( );</c>. -1 for
	/// a shape it does not know.
	/// </summary>
	private static int PastStatement(string source, int afterKeyword, bool isDo)
	{
		using var tokens = SyntaxFactory.ParseTokens(source, afterKeyword, afterKeyword, CSharpSource.ParseOptions).GetEnumerator();
		if (!tokens.MoveNext())
			return -1;

		while (true)
		{
			// The header: parenthesised conditions, and the words `if`, `when` or a catch's names between them.
			while (!tokens.Current.IsKind(SyntaxKind.OpenBraceToken))
			{
				if (tokens.Current.IsKind(SyntaxKind.OpenParenToken))
				{
					if (SkipGroup(tokens, SyntaxKind.OpenParenToken, SyntaxKind.CloseParenToken) < 0)
						return -1;
				}
				else if (!tokens.Current.IsKind(SyntaxKind.IdentifierToken) && !tokens.Current.IsKind(SyntaxKind.IfKeyword))
					return -1;

				if (!tokens.MoveNext())
					return -1;
			}

			var end = SkipGroup(tokens, SyntaxKind.OpenBraceToken, SyntaxKind.CloseBraceToken);
			if (end < 0)
				return -1;

			if (!tokens.MoveNext())
				return end;

			var kind = (SyntaxKind)tokens.Current.RawKind;
			if (kind is SyntaxKind.ElseKeyword or SyntaxKind.CatchKeyword or SyntaxKind.FinallyKeyword)
			{
				if (!tokens.MoveNext())
					return -1;
				continue;
			}

			if (isDo && kind == SyntaxKind.WhileKeyword)
			{
				if (!tokens.MoveNext() || !tokens.Current.IsKind(SyntaxKind.OpenParenToken))
					return -1;
				end = SkipGroup(tokens, SyntaxKind.OpenParenToken, SyntaxKind.CloseParenToken);
				if (end >= 0 && tokens.MoveNext() && tokens.Current.IsKind(SyntaxKind.SemicolonToken))
					end = tokens.Current.Span.End;
				return end;
			}

			return end;
		}
	}

	/// <summary>
	/// With <paramref name="tokens"/> on an opening token, advances to the one that closes it and returns the
	/// position just past it; -1 at the end of the text.
	/// </summary>
	private static int SkipGroup(IEnumerator<SyntaxToken> tokens, SyntaxKind openKind, SyntaxKind closeKind)
	{
		var depth = 0;
		do
		{
			var kind = (SyntaxKind)tokens.Current.RawKind;
			if (kind == SyntaxKind.EndOfFileToken)
				return -1;
			if (kind == openKind)
				depth++;
			else if (kind == closeKind && --depth == 0)
				return tokens.Current.Span.End;
		}
		while (tokens.MoveNext());

		return -1;
	}

	private static int SkipBlanks(ReadOnlySpan<char> text, int i)
	{
		while (i < text.Length && text[i] is ' ' or '\t')
			i++;
		return i;
	}

	/// <summary>The position just past <paramref name="terminator"/>, or the end of the text when it never comes.</summary>
	private static int SkipPast(ReadOnlySpan<char> text, int from, string terminator)
	{
		var end = text[from..].IndexOf(terminator, StringComparison.Ordinal);
		return end < 0 ? text.Length : from + end + terminator.Length;
	}

	/// <summary>The position just past the line break at <paramref name="i"/>, or -1 when there is none there.</summary>
	private static int PastLineBreak(ReadOnlySpan<char> text, int i)
	{
		if (i >= text.Length)
			return -1;
		if (text[i] == '\r')
			return i + 1 < text.Length && text[i + 1] == '\n' ? i + 2 : i + 1;
		return text[i] == '\n' ? i + 1 : -1;
	}

	private static bool TryMatch(
		string source,
		int lineStart,
		int first,
		bool component,
		out RazorBlock block,
		out int resume)
	{
		block = default;
		resume = 0;

		var text = source.AsSpan();
		var rest = text[first..];
		bool isFunctions;
		int afterName;
		if (component && rest.StartsWith("@code"))
		{
			isFunctions = false;
			afterName = first + 5;
		}
		else if (rest.StartsWith("@functions"))
		{
			isFunctions = true;
			afterName = first + 10;
		}
		else
			return false;

		// `{` on the same line, or alone on the next one. Anything else after the name, an identifier
		// character in particular (`@codeBehind`), is not this directive.
		var brace = SkipBlanks(text, afterName);
		if (brace >= text.Length)
			return false;
		if (text[brace] != '{')
		{
			var nextLine = PastLineBreak(text, brace);
			if (nextLine < 0)
				return false;
			brace = SkipBlanks(text, nextLine);
			if (brace >= text.Length || text[brace] != '{')
				return false;
		}

		var innerStart = PastLineBreak(text, SkipBlanks(text, brace + 1));
		if (innerStart < 0)
			return false;

		var close = FindClose(source, brace);
		if (close < 0)
		{
			// Nothing after a block that never closes can be told apart from its body.
			block = new RazorBlock(lineStart, first, isFunctions, brace, innerStart, -1, -1, RazorBlockRefusal.Unclosed);
			resume = -1;
			return true;
		}

		var closeLineStart = close;
		var alone = true;
		while (closeLineStart > 0 && text[closeLineStart - 1] is not ('\n' or '\r'))
		{
			if (text[closeLineStart - 1] is not (' ' or '\t'))
				alone = false;
			closeLineStart--;
		}

		var afterClose = SkipBlanks(text, close + 1);
		if (afterClose < text.Length && text[afterClose] is not ('\n' or '\r'))
			alone = false;

		block = new RazorBlock(
			lineStart, first, isFunctions, brace, innerStart, closeLineStart, close,
			alone ? RazorBlockRefusal.None : RazorBlockRefusal.CloseBraceNotAlone);
		resume = close + 1;
		return true;
	}

	/// <summary>The position of the <c>}</c> that closes the <c>{</c> at <paramref name="openBrace"/>, or -1.</summary>
	/// <remarks>
	/// The lexer is the parser's own, at the language version Curb parses at, so what it calls a brace
	/// token is exactly what the C# parser will. Positions it reports are absolute in
	/// <paramref name="source"/>.
	/// </remarks>
	internal static int FindClose(string source, int openBrace) =>
		FindClose(source, openBrace, SyntaxKind.OpenBraceToken, SyntaxKind.CloseBraceToken);

	/// <summary>The position of the token that closes the <paramref name="openKind"/> at <paramref name="open"/>, or -1.</summary>
	private static int FindClose(string source, int open, SyntaxKind openKind, SyntaxKind closeKind)
	{
		var depth = 0;
		foreach (var token in SyntaxFactory.ParseTokens(source, open, open, CSharpSource.ParseOptions))
		{
			var kind = (SyntaxKind)token.RawKind;
			if (kind == SyntaxKind.EndOfFileToken)
				return -1;
			if (kind == openKind)
				depth++;
			else if (kind == closeKind && --depth == 0)
				return token.SpanStart;
		}

		return -1;
	}

	/// <summary>The 1-based line <paramref name="position"/> is on, for messages.</summary>
	public static int LineOf(string source, int position)
	{
		var line = 1;
		var text = source.AsSpan(0, Math.Min(position, source.Length));
		for (var i = 0; i < text.Length; i++)
		{
			if (text[i] == '\n' || (text[i] == '\r' && (i + 1 >= text.Length || text[i + 1] != '\n')))
				line++;
		}

		return line;
	}
}
