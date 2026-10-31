using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Nullean.Curb.CorpusGenerator;

/// <summary>
/// Rewrites the whitespace between tokens to a <see cref="LayoutProfile"/>, and proves it changed nothing else.
/// </summary>
/// <remarks>
/// <para>
/// Only gaps made entirely of spaces, tabs and line breaks are rewritten. A gap holding a comment, a directive or
/// disabled text keeps every character apart from the indentation in front of each line. Nothing inside an
/// interpolated string is touched, and token text never is.
/// </para>
/// <para>
/// Every result is re-parsed and its token stream compared with the original, kind and text. A file that differs
/// falls back to a profile without structural edits, then to the original layout with only its encoding changed.
/// So a permutation can never hand the formatting gates a file with different code in it, which would make a
/// conformance failure the generator's fault rather than Curb's.
/// </para>
/// </remarks>
public static class Permuter
{
	private static readonly CSharpParseOptions ParseOptions = new(LanguageVersion.Preview, DocumentationMode.Parse);

	private static readonly string[] Comments =
	[
		"TODO: revisit once the API settles",
		"keep in sync with the schema",
		"see the note above",
		"intentional",
		"cheap enough for now",
		"do not reorder",
		"callers rely on this",
		"null means not configured",
		"hot path",
		"FIXME",
		"legacy behaviour, kept for compatibility",
		"matches the reference implementation",
		"ok",
		"guard against an empty sequence",
		"this was a bug once",
		"measured, not guessed",
		"see issue 1234",
		"order matters here",
		"copied from the old service",
		"defaults",
		"may throw",
		"fast path",
		"slow path",
	];

	/// <summary>The result of a permutation, and which rung of the fallback ladder produced it.</summary>
	public readonly record struct Result(string Text, string Rung);

	public static Result Apply(string source, LayoutProfile profile, Rng r)
	{
		var normalised = source.Replace("\r\n", "\n").TrimStart('\uFEFF');
		var original = CSharpSyntaxTree.ParseText(normalised, ParseOptions);

		foreach (var (candidate, rung) in new[] { (profile, "full"), (LayoutProfile.WithoutStructure(profile), "no-structure") })
		{
			var text = Rewrite(original, candidate, r);
			if (SameTokens(original, text))
			{
				return new Result(Encode(text, candidate, r), rung);
			}
		}

		return new Result(Encode(normalised, profile with { NoFinalNewline = false }, r), "original");
	}

	private static string Encode(string lf, LayoutProfile p, Rng r)
	{
		var text = lf;
		if (p.Crlf)
		{
			if (!p.MixedLineEndings)
			{
				text = text.Replace("\n", "\r\n");
			}
			else
			{
				var b = new StringBuilder(text.Length + text.Length / 20);
				foreach (var c in text)
				{
					if (c == '\n' && !r.Chance(0.05))
					{
						b.Append('\r');
					}
					b.Append(c);
				}

				text = b.ToString();
			}
		}

		return p.Bom ? "\uFEFF" + text : text;
	}

	private static bool SameTokens(SyntaxTree original, string rewritten)
	{
		var tree = CSharpSyntaxTree.ParseText(rewritten, ParseOptions);
		var a = original.GetRoot().DescendantTokens().GetEnumerator();
		var b = tree.GetRoot().DescendantTokens().GetEnumerator();
		while (true)
		{
			var hasA = a.MoveNext();
			var hasB = b.MoveNext();
			if (hasA != hasB)
			{
				return false;
			}
			if (!hasA)
			{
				break;
			}
			if (a.Current.RawKind != b.Current.RawKind || a.Current.Text != b.Current.Text)
			{
				return false;
			}
		}

		return tree.GetDiagnostics().Count(d => d.Severity == DiagnosticSeverity.Error) <= original.GetDiagnostics().Count(
			d => d.Severity == DiagnosticSeverity.Error
		);
	}

	private static string Rewrite(SyntaxTree tree, LayoutProfile p, Rng r)
	{
		var tokens = tree.GetRoot().DescendantTokens().ToArray();
		var output = new StringBuilder(tree.Length + tree.Length / 4);
		var braces = 0;
		var parens = 0;

		for (var i = 0; i < tokens.Length; i++)
		{
			var token = tokens[i];
			var previous = i > 0 ? tokens[i - 1] : default;

			var level = braces + Math.Min(parens, 2);
			if (token.IsKind(SyntaxKind.CloseBraceToken))
			{
				level = Math.Max(0, level - 1);
			}
			var continuation = i > 0 && !previous.IsKind(SyntaxKind.SemicolonToken) && !previous.IsKind(SyntaxKind.OpenBraceToken) &&
				!previous.IsKind(SyntaxKind.CloseBraceToken) && !token.IsKind(SyntaxKind.OpenBraceToken) && !token.IsKind(SyntaxKind.CloseBraceToken) &&
				!IsAttributeEnd(previous) && !IsLabelColon(previous);
			if (continuation && parens == 0)
			{
				level++;
			}

			var gap = i > 0 ? previous.TrailingTrivia.AddRange(token.LeadingTrivia) : token.LeadingTrivia;

			if (InsideInterpolation(token) || KnownDefects.InsideVerbatimNode(previous, token))
			{
				AppendVerbatim(output, gap);
			}
			else if (KnownDefects.Avoid(previous, token) && gap.All(t => t.IsKind(SyntaxKind.WhitespaceTrivia) || t.IsKind(SyntaxKind.EndOfLineTrivia)))
			{
				AppendQuietGap(output, gap, level, i == 0, token, p, r);
			}
			else if (gap.All(t => t.IsKind(SyntaxKind.WhitespaceTrivia) || t.IsKind(SyntaxKind.EndOfLineTrivia)))
			{
				AppendWhitespaceGap(output, gap, previous, token, level, i == 0, p, r);
			}
			else
			{
				AppendCommentedGap(output, gap, level, i == 0 ? KnownDefects.InHeader(p) : p, r);
			}

			output.Append(token.Text);

			switch (token.Kind())
			{
				case SyntaxKind.OpenBraceToken:
					braces++;
					break;
				case SyntaxKind.CloseBraceToken:
					braces = Math.Max(0, braces - 1);
					break;
				case SyntaxKind.OpenParenToken or SyntaxKind.OpenBracketToken:
					parens++;
					break;
				case SyntaxKind.CloseParenToken or SyntaxKind.CloseBracketToken:
					parens = Math.Max(0, parens - 1);
					break;
			}

			// A block inside an argument list (a lambda body) starts a fresh statement context.
			if (token.IsKind(SyntaxKind.OpenBraceToken) && parens > 0)
			{
				parens = 0;
			}
		}

		return output.ToString();
	}

	/// <summary>Indentation and blank lines only, for a gap <see cref="KnownDefects"/> keeps away from.</summary>
	private static void AppendQuietGap(StringBuilder output, SyntaxTriviaList gap, int level, bool first, SyntaxToken token, LayoutProfile p, Rng r)
	{
		var newlines = gap.Count(t => t.IsKind(SyntaxKind.EndOfLineTrivia));
		if (newlines == 0)
		{
			output.Append(string.Concat(gap.Select(t => t.ToFullString())));
			return;
		}

		output.Append('\n', newlines);
		if (!first && !token.IsKind(SyntaxKind.EndOfFileToken))
		{
			output.Append(Indent(level, p, r));
		}
	}

	private static void AppendVerbatim(StringBuilder output, SyntaxTriviaList gap)
	{
		foreach (var t in gap)
		{
			output.Append(t.ToFullString().Replace("\r\n", "\n"));
		}
	}

	private static void AppendWhitespaceGap(
		StringBuilder output,
		SyntaxTriviaList gap,
		SyntaxToken previous,
		SyntaxToken token,
		int level,
		bool first,
		LayoutProfile p,
		Rng r
	)
	{
		var newlines = gap.Count(t => t.IsKind(SyntaxKind.EndOfLineTrivia));

		if (token.IsKind(SyntaxKind.EndOfFileToken))
		{
			if (!p.NoFinalNewline)
			{
				output.Append('\n', newlines > 1 && r.Chance(0.5) ? 2 : 1);
			}
			return;
		}

		if (first)
		{
			if (newlines > 0 || r.Chance(0.03))
			{
				output.Append('\n');
			}
			return;
		}

		if (newlines > 0)
		{
			if (CanJoin(previous, token) && r.Chance(p.Join))
			{
				output.Append(' ');
				return;
			}

			if (token.IsKind(SyntaxKind.OpenBraceToken) && r.Chance(p.BraceJoin))
			{
				output.Append(' ');
				return;
			}

			var lines = newlines;
			var boundary = previous.IsKind(SyntaxKind.SemicolonToken) || previous.IsKind(SyntaxKind.CloseBraceToken) || previous.IsKind(
				SyntaxKind.OpenBraceToken
			);
			if (lines > 1 && r.Chance(p.BlankDrop))
			{
				lines = 1;
			}
			else if (boundary && !token.IsKind(SyntaxKind.CloseBraceToken) && r.Chance(p.BlankAdd))
			{
				lines = Math.Min(lines + 1, 3);
			}

			if (r.Chance(p.TrailingComment) && KnownDefects.AllowsTrailingComment(previous, token))
			{
				output.Append(" // ").Append(r.Pick(Comments));
			}
			for (var n = 0; n < lines; n++)
			{
				if (r.Chance(p.TrailingWhitespace))
				{
					output.Append(r.Pick(" ", "  ", "\t", "    "));
				}
				output.Append('\n');
			}

			if (StartsStatementOrMember(token) && r.Chance(p.LeadingComment))
			{
				output.Append(Indent(level, p, r)).Append("// ").Append(r.Pick(Comments)).Append('\n');
			}

			output.Append(Indent(level, p, r));
			return;
		}

		var existing = string.Concat(gap.Select(t => t.ToFullString()));

		if (token.IsKind(SyntaxKind.OpenBraceToken) && r.Chance(p.BraceBreak))
		{
			output.Append('\n').Append(Indent(level, p, r));
			return;
		}

		if (CanBreak(previous, token) && r.Chance(p.Break))
		{
			output.Append('\n').Append(Indent(level + 1, p, r));
			return;
		}

		if (existing.Length > 0 && r.Chance(p.BlockComment) && IsQuietSpot(previous, token))
		{
			output.Append(" /* ").Append(r.Pick(Comments)).Append(" */ ");
			return;
		}

		if (existing.Length == 0)
		{
			if (r.Chance(p.SpaceAdd) && CanSpace(previous, token))
			{
				output.Append(' ');
			}
			return;
		}

		if (r.Chance(p.SpaceDrop) && CanTouch(previous, token) && KnownDefects.AllowsTouching(previous, token))
		{
			return;
		}

		output.Append(existing);
		if (r.Chance(p.SpaceDouble))
		{
			output.Append(' ');
		}
	}

	/// <summary>
	/// A gap with a comment, a directive or disabled text in it. Everything is kept but the whitespace in front of a
	/// line that starts with a comment or with the token itself, which is re-indented like any other line.
	/// </summary>
	private static void AppendCommentedGap(StringBuilder output, SyntaxTriviaList gap, int level, LayoutProfile p, Rng r)
	{
		for (var i = 0; i < gap.Count; i++)
		{
			var t = gap[i];
			var atLineStart = output.Length > 0 && output[^1] == '\n';
			if (t.IsKind(SyntaxKind.WhitespaceTrivia) && atLineStart)
			{
				var next = i + 1 < gap.Count ? gap[i + 1] : default;
				var reindent = i + 1 == gap.Count || next.IsKind(SyntaxKind.SingleLineCommentTrivia) || next.IsKind(
					SyntaxKind.MultiLineCommentTrivia
				) || next.IsKind(SyntaxKind.SingleLineDocumentationCommentTrivia);
				output.Append(reindent ? Indent(level, p, r) : t.ToFullString());
				continue;
			}

			if (t.IsKind(SyntaxKind.EndOfLineTrivia))
			{
				if (r.Chance(p.TrailingWhitespace))
				{
					output.Append(' ');
				}
				output.Append('\n');
				continue;
			}

			if (t.IsKind(SyntaxKind.SingleLineDocumentationCommentTrivia))
			{
				// Every line at the same indentation, the way people write them. Curb and dotnet format still disagree
				// when the indentation changes style (see KnownDefects); that is a conformance finding, left in.
				var lines = t.ToFullString().Replace("\r\n", "\n").Split('\n');
				var indent = Indent(level, p with { Jitter = 0, MixTabs = 0 }, r);
				for (var n = 0; n < lines.Length; n++)
				{
					if (lines[n].Length == 0 && n == lines.Length - 1)
					{
						break;
					}
					if (n > 0 || atLineStart)
					{
						output.Append(n > 0 || output[^1] == '\n' ? indent : "");
					}
					output.Append(lines[n].TrimStart());
					if (n < lines.Length - 1)
					{
						output.Append('\n');
					}
				}

				continue;
			}

			output.Append(t.ToFullString().Replace("\r\n", "\n"));
		}

		// A gap that ended in a line break leaves the token at column 0 unless it is indented here.
		if (gap.Count > 0 && output.Length > 0 && output[^1] == '\n')
		{
			output.Append(Indent(level, p, r));
		}
	}

	private static string Indent(int level, LayoutProfile p, Rng r)
	{
		var unit = p.IndentUnit != "\t" && r.Chance(p.MixTabs) ? "\t" : p.IndentUnit;
		var b = new StringBuilder(unit.Length * level + 3);
		for (var i = 0; i < level; i++)
		{
			b.Append(unit);
		}
		if (r.Chance(p.Jitter))
		{
			if (r.Chance(0.5) || b.Length == 0)
			{
				b.Append(' ', r.Next(1, 4));
			}
			else
			{
				b.Length = Math.Max(0, b.Length - r.Next(1, Math.Min(4, b.Length + 1)));
			}
		}

		return b.ToString();
	}

	private static bool InsideInterpolation(SyntaxToken token)
	{
		if (
			token.IsKind(SyntaxKind.InterpolatedStringStartToken) || token.IsKind(SyntaxKind.InterpolatedVerbatimStringStartToken) || token.IsKind(
				SyntaxKind.InterpolatedSingleLineRawStringStartToken
			) || token.IsKind(SyntaxKind.InterpolatedMultiLineRawStringStartToken)
		)
		{
			return false;
		}

		for (var node = token.Parent; node is not null; node = node.Parent)
		{
			if (node is InterpolatedStringExpressionSyntax)
			{
				return true;
			}
			if (node is StatementSyntax or MemberDeclarationSyntax)
			{
				return false;
			}
		}

		return false;
	}

	private static bool IsAttributeEnd(SyntaxToken token) => token.IsKind(SyntaxKind.CloseBracketToken) && token.Parent is AttributeListSyntax;

	private static bool IsLabelColon(SyntaxToken token) => token.IsKind(SyntaxKind.ColonToken) && token.Parent is SwitchLabelSyntax or LabeledStatementSyntax;

	private static bool StartsStatementOrMember(SyntaxToken token) =>
		token.Parent is { } parent &&
			(parent is StatementSyntax or MemberDeclarationSyntax || parent.Parent is StatementSyntax or MemberDeclarationSyntax) && (parent is StatementSyntax or MemberDeclarationSyntax
			? parent
			: parent.Parent!).GetFirstToken() == token && parent is not BlockSyntax;

	/// <summary>A line break inside a statement, which can become a space.</summary>
	private static bool CanJoin(SyntaxToken previous, SyntaxToken token) =>
		!previous.IsKind(SyntaxKind.SemicolonToken) && !previous.IsKind(SyntaxKind.OpenBraceToken) && !previous.IsKind(SyntaxKind.CloseBraceToken) &&
			!token.IsKind(SyntaxKind.OpenBraceToken) && !token.IsKind(SyntaxKind.CloseBraceToken) && !token.IsKind(SyntaxKind.EndOfFileToken) &&
			!IsAttributeEnd(previous);

	/// <summary>The places people break long lines: after <c>(</c> or <c>,</c>, before <c>.</c>, around operators.</summary>
	private static bool CanBreak(SyntaxToken previous, SyntaxToken token)
	{
		if (previous.IsKind(SyntaxKind.OpenParenToken) || previous.IsKind(SyntaxKind.CommaToken))
		{
			return previous.Parent is ArgumentListSyntax or ParameterListSyntax or BracketedArgumentListSyntax or InitializerExpressionSyntax or CollectionExpressionSyntax or TupleExpressionSyntax;
		}

		if (token.IsKind(SyntaxKind.DotToken))
		{
			return token.Parent is MemberAccessExpressionSyntax { Expression: InvocationExpressionSyntax or MemberAccessExpressionSyntax };
		}

		if (token.Parent is BinaryExpressionSyntax binary && binary.OperatorToken == token)
		{
			return true;
		}
		if (previous.Parent is BinaryExpressionSyntax prior && prior.OperatorToken == previous)
		{
			return true;
		}

		if (token.IsKind(SyntaxKind.EqualsGreaterThanToken) || previous.IsKind(SyntaxKind.EqualsGreaterThanToken))
		{
			return true;
		}

		return token.Parent is ConditionalExpressionSyntax conditional && (conditional.QuestionToken == token || conditional.ColonToken == token);
	}

	/// <summary>Where a <c>/* */</c> comment reads as something a person wrote.</summary>
	private static bool IsQuietSpot(SyntaxToken previous, SyntaxToken token) =>
		previous.IsKind(SyntaxKind.CommaToken) || token.IsKind(SyntaxKind.EqualsToken) || previous.IsKind(SyntaxKind.EqualsToken) || previous.Parent is
			ParameterSyntax;

	/// <summary>Whether a space can go between two tokens that touch. Never inside the punctuation people do not split.</summary>
	private static bool CanSpace(SyntaxToken previous, SyntaxToken token) =>
		!(previous.IsKind(SyntaxKind.QuestionToken) && previous.Parent is ConditionalAccessExpressionSyntax) && !token.IsKind(SyntaxKind.ExclamationToken) &&
			!previous.IsKind(SyntaxKind.DotToken) && !token.IsKind(SyntaxKind.DotToken) && !token.IsKind(SyntaxKind.PlusPlusToken) &&
			!token.IsKind(SyntaxKind.MinusMinusToken) && !previous.IsKind(SyntaxKind.PlusPlusToken) && !previous.IsKind(SyntaxKind.MinusMinusToken);

	/// <summary>Whether two tokens can touch without welding into one.</summary>
	private static bool CanTouch(SyntaxToken previous, SyntaxToken token)
	{
		var a = previous.Text;
		var b = token.Text;
		if (a.Length == 0 || b.Length == 0)
		{
			return false;
		}
		var last = a[^1];
		var head = b[0];

		static bool Word(char c) => char.IsLetterOrDigit(c) || c is '_' or '@' or '"' or '\'' or '$';

		static bool Operator(char c) => "+-*/%<>=!&|^?:.~".Contains(c);

		if (Word(last) && Word(head))
		{
			return false;
		}
		if (Operator(last) && Operator(head))
		{
			return false;
		}
		if (previous.IsKind(SyntaxKind.NumericLiteralToken))
		{
			return false;
		}
		return true;
	}
}
