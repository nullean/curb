using Microsoft.CodeAnalysis;

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
		out string? failure)
	{
		if (!CSharpSource.TryParse(formatted, out var reparsed, out var errors))
		{
			failure = errors.Count > 0
				? $"formatted output no longer parses: {errors[0].GetMessage(System.Globalization.CultureInfo.InvariantCulture)}"
				: "formatted output no longer parses";
			return false;
		}

		var formattedText = formatted.AsSpan();

		using var original = originalRoot.DescendantTokens().GetEnumerator();
		using var produced = reparsed.Root.DescendantTokens().GetEnumerator();

		var index = 0;

		while (true)
		{
			var hasOriginal = original.MoveNext();
			var hasProduced = produced.MoveNext();

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

			if (before.RawKind != after.RawKind || !Text(originalText, before).SequenceEqual(Text(formattedText, after)))
			{
				failure =
					$"formatting changed token {index}: '{Text(originalText, before)}' became "
					+ $"'{Text(formattedText, after)}'";
				return false;
			}

			index++;
		}
	}

	private static ReadOnlySpan<char> Text(ReadOnlySpan<char> source, SyntaxToken token) =>
		source.Slice(token.Span.Start, token.Span.Length);
}
