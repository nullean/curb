using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace Nullean.Kerf.Verification;

/// <summary>
/// Re-parses the formatted output and checks that it tokenises to exactly the same stream as the
/// input.
/// </summary>
/// <remarks>
/// <para>
/// The second safety net, and the one that catches what <see cref="ContentVerifier"/> cannot. That
/// check ignores whitespace by design, so it cannot see two tokens being welded together: dropping
/// the space in <c>out var x</c> yields the single identifier <c>outvar</c>, and moving a
/// <c>#endif</c> onto the end of a statement stops it being a directive at all. Both preserve every
/// non-whitespace character, and both stop the file compiling. Both were real bugs here, found only
/// by re-parsing.
/// </para>
/// <para>
/// Comparison is on <c>RawKind</c> plus the token's exact text, read as spans out of the two
/// sources, so it allocates nothing per token. Tokens are compared without descending into trivia —
/// comment content is <see cref="ContentVerifier"/>'s responsibility, while this establishes that
/// the code still means the same thing.
/// </para>
/// <para>
/// Reordering usings changes the token stream on purpose, so when it has happened the using block
/// is lifted out of the linear walk and checked separately: the two lists of directive texts are
/// sorted and compared, which is exact rather than lenient — a lost, duplicated or altered
/// directive all fail it — and the rest of the file is still compared token for token.
/// </para>
/// <para>
/// Costs one extra parse, so it is opt-in for <c>check</c> (which writes nothing and therefore
/// cannot corrupt anything) and on by default when formatting in place.
/// </para>
/// </remarks>
internal static class TokenStreamComparer
{
	public static bool Verify(
		SyntaxNode originalRoot,
		ReadOnlySpan<char> originalText,
		string formatted,
		out string? failure,
		bool usingsReordered = false,
		bool trailingCommas = false)
	{
		if (!CSharpSource.TryParse(formatted, out var reparsed, out var errors))
		{
			failure = errors.Count > 0
				? $"formatted output no longer parses: {errors[0].GetMessage(System.Globalization.CultureInfo.InvariantCulture)}"
				: "formatted output no longer parses";
			return false;
		}

		var formattedText = formatted.AsSpan();

		var originalUsings = TextSpan.FromBounds(0, 0);
		var producedUsings = TextSpan.FromBounds(0, 0);

		if (usingsReordered)
		{
			if (!SameDirectives(originalRoot, reparsed.Root, out failure))
				return false;

			originalUsings = DirectiveRegion(originalRoot);
			producedUsings = DirectiveRegion(reparsed.Root);
		}

		using var original = originalRoot.DescendantTokens().GetEnumerator();
		using var produced = reparsed.Root.DescendantTokens().GetEnumerator();

		var index = 0;

		while (true)
		{
			// The using block has been checked as a set; walking it in order would only re-discover
			// the reordering it was asked for.
			bool hasOriginal, hasProduced;
			do
			{
				hasOriginal = original.MoveNext();
			}
			while (hasOriginal && originalUsings.Contains(original.Current.SpanStart));

			do
			{
				hasProduced = produced.MoveNext();
			}
			while (hasProduced && producedUsings.Contains(produced.Current.SpanStart));

			if (!hasOriginal && !hasProduced)
			{
				failure = null;
				return true;
			}

			if (!hasOriginal)
			{
				failure = $"formatting introduced an extra token at position {index}: '{Text(formattedText, produced.Current)}'";
				return false;
			}

			if (!hasProduced)
			{
				failure = $"formatting lost the token at position {index}: '{Text(originalText, original.Current)}'";
				return false;
			}

			var before = original.Current;
			var after = produced.Current;

			// The declared token delta. A trailing comma that appeared or vanished shows up here as a
			// `,` on one side against the closer on the other, which needs no lookahead to spot: the
			// grammar allows at most one, so stepping over it once and re-comparing is exact. Any
			// other mismatch, including a comma anywhere but immediately before a `}` or `]`, still
			// fails.
			if (trailingCommas && Mismatch(originalText, before, formattedText, after))
			{
				if (IsComma(before) && IsCloser(after) && Next(original, originalUsings, out var nextBefore))
					before = nextBefore;
				else if (IsCloser(before) && IsComma(after) && Next(produced, producedUsings, out var nextAfter))
					after = nextAfter;
			}

			if (Mismatch(originalText, before, formattedText, after))
			{
				failure =
					$"formatting changed token {index}: '{Text(originalText, before)}' became "
					+ $"'{Text(formattedText, after)}'";
				return false;
			}

			index++;
		}
	}

	private static bool Mismatch(
		ReadOnlySpan<char> originalText,
		SyntaxToken before,
		ReadOnlySpan<char> formattedText,
		SyntaxToken after) =>
		before.RawKind != after.RawKind
		|| !Text(originalText, before).SequenceEqual(Text(formattedText, after));

	private static bool IsComma(SyntaxToken token) => token.RawKind == (int)SyntaxKind.CommaToken;

	/// <summary>The two closers the grammar permits a trailing comma before.</summary>
	private static bool IsCloser(SyntaxToken token) =>
		token.RawKind is (int)SyntaxKind.CloseBraceToken or (int)SyntaxKind.CloseBracketToken;

	/// <summary>Pulls the next token outside the region checked as a set.</summary>
	private static bool Next(IEnumerator<SyntaxToken> tokens, TextSpan skip, out SyntaxToken token)
	{
		while (tokens.MoveNext())
		{
			if (skip.Contains(tokens.Current.SpanStart))
				continue;

			token = tokens.Current;
			return true;
		}

		token = default;
		return false;
	}

	/// <summary>Compares the two using lists as multisets of their text.</summary>
	private static bool SameDirectives(SyntaxNode before, SyntaxNode after, out string? failure)
	{
		var original = Directives(before);
		var produced = Directives(after);

		if (original.Length != produced.Length)
		{
			failure = $"reordering usings changed how many there are: {original.Length} became {produced.Length}";
			return false;
		}

		Array.Sort(original, StringComparer.Ordinal);
		Array.Sort(produced, StringComparer.Ordinal);

		for (var i = 0; i < original.Length; i++)
		{
			if (string.Equals(original[i], produced[i], StringComparison.Ordinal))
				continue;

			failure = $"reordering usings altered one: '{original[i]}' is not among the directives written";
			return false;
		}

		failure = null;
		return true;
	}

	private static string[] Directives(SyntaxNode root) =>
		[.. root.DescendantNodes(descendIntoChildren: node => node is CompilationUnitSyntax or BaseNamespaceDeclarationSyntax)
			.OfType<UsingDirectiveSyntax>()
			.Select(directive => directive.ToString())];

	/// <summary>The span the using directives occupy, or an empty span at zero when there are none.</summary>
	private static TextSpan DirectiveRegion(SyntaxNode root)
	{
		var start = int.MaxValue;
		var end = 0;

		foreach (var directive in root
			.DescendantNodes(descendIntoChildren: node => node is CompilationUnitSyntax or BaseNamespaceDeclarationSyntax)
			.OfType<UsingDirectiveSyntax>())
		{
			start = Math.Min(start, directive.SpanStart);
			end = Math.Max(end, directive.Span.End);
		}

		return start == int.MaxValue ? TextSpan.FromBounds(0, 0) : TextSpan.FromBounds(start, end);
	}

	private static ReadOnlySpan<char> Text(ReadOnlySpan<char> source, SyntaxToken token) =>
		source.Slice(token.Span.Start, token.Span.Length);
}
