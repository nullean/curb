using Microsoft.CodeAnalysis.Text;

namespace Nullean.Kerf.Verification;

/// <summary>
/// Checks that formatting preserved every non-whitespace character of the file, in order.
/// </summary>
/// <remarks>
/// <para>
/// This is Kerf's always-on safety net: O(n), allocation-free, and no re-parse. If it fails, the
/// file is reported and left untouched rather than written. "The formatter ate my code" is the
/// failure mode that ends a formatter's life, and it should be structurally impossible.
/// </para>
/// <para>
/// What it catches: dropped, duplicated, reordered or truncated tokens and comments — every way a
/// printer can lose content.
/// </para>
/// <para>
/// Reordering usings is the one thing Kerf does that legitimately changes the order of content, so
/// the caller declares the source span that moved and the verifier switches to a multiset compare
/// over exactly that region — still catching anything dropped, duplicated or altered there — and
/// stays strict over the whole of the rest of the file.
/// </para>
/// <para>
/// What it deliberately does <b>not</b> catch: two tokens being glued together by a missing space
/// (<c>a b</c> becoming <c>ab</c>), since whitespace is exactly what it ignores. That is the
/// re-parse token comparer's job, which is why both nets exist. Nor would it notice whitespace lost
/// from inside a string literal.
/// </para>
/// </remarks>
internal static class ContentVerifier
{
	/// <param name="source">The original text.</param>
	/// <param name="output">What was printed.</param>
	/// <param name="failure">Set when verification fails.</param>
	/// <param name="reordered">
	/// Source spans whose content was deliberately permuted, in source order, or null when nothing
	/// was.
	/// </param>
	/// <param name="trailingCommas">
	/// The trailing-comma options are on, so a <c>,</c> immediately before a closing brace or bracket
	/// may appear on one side and not the other.
	/// </param>
	public static bool Verify(
		ReadOnlySpan<char> source,
		ReadOnlySpan<char> output,
		out string? failure,
		IReadOnlyList<TextSpan>? reordered = null,
		bool trailingCommas = false)
	{
		var sourceIndex = 0;
		var outputIndex = 0;
		var nextReordered = 0;

		while (true)
		{
			while (sourceIndex < source.Length && IsSkippable(source[sourceIndex]))
				sourceIndex++;
			while (outputIndex < output.Length && IsSkippable(output[outputIndex]))
				outputIndex++;

			// A permuted region: take the same number of content characters from each side and compare
			// them as multisets, then carry on in order from where both left off. Regions arrive in
			// source order, so one cursor through them is enough.
			//
			// Tested after the whitespace skip, not before. A region starts at its first content
			// character, and the cursor steps over the run of whitespace in front of it in one go — so
			// testing first meant the cursor could jump from before the region to inside it without
			// ever comparing equal, and the permutation was then read as damage.
			if (reordered is not null && nextReordered < reordered.Count && sourceIndex >= reordered[nextReordered].Start)
			{
				var span = reordered[nextReordered++];
				var length = CountContent(source[span.Start..span.End]);

				if (length > 0
					&& !VerifyPermutation(source, output, ref sourceIndex, ref outputIndex, length, out failure))
					return false;

				continue;
			}

			var sourceDone = sourceIndex >= source.Length;
			var outputDone = outputIndex >= output.Length;

			if (sourceDone && outputDone)
			{
				failure = null;
				return true;
			}

			if (sourceDone)
			{
				failure = $"formatting added content at offset {outputIndex}: {Excerpt(output, outputIndex)}";
				return false;
			}

			if (outputDone)
			{
				failure = $"formatting dropped content from offset {sourceIndex}: {Excerpt(source, sourceIndex)}";
				return false;
			}

			if (source[sourceIndex] != output[outputIndex])
			{
				// The one declared token delta: a trailing comma the printer added or dropped. The
				// allowance is deliberately narrow — the comma has to be the last thing before a
				// closing brace or bracket on whichever side carries it — so it cannot excuse a
				// dropped element. `{ a, x }` printed as `{ a, }` still fails here, because the
				// mismatch is `x` against `}` and neither side is a comma.
				if (trailingCommas && SkipTrailingComma(output, ref outputIndex))
					continue;

				if (trailingCommas && SkipTrailingComma(source, ref sourceIndex))
					continue;

				failure =
					$"formatting changed content at source offset {sourceIndex}: "
					+ $"expected {Excerpt(source, sourceIndex)} but produced {Excerpt(output, outputIndex)}";
				return false;
			}

			sourceIndex++;
			outputIndex++;
		}
	}

	/// <summary>
	/// Steps over a trailing comma, and only a trailing one.
	/// </summary>
	/// <remarks>
	/// Advances <paramref name="index"/> past a <c>,</c> whose next content character closes a brace
	/// or a bracket, and leaves it alone otherwise. Those are the only closers the grammar permits a
	/// trailing comma before: a comma before <c>)</c> or <c>&gt;</c> is not legal C# and is not
	/// something the printer can have produced, so it is still a verification failure.
	/// </remarks>
	private static bool SkipTrailingComma(ReadOnlySpan<char> text, ref int index)
	{
		if (index >= text.Length || text[index] != ',')
			return false;

		var next = index + 1;
		while (next < text.Length && IsSkippable(text[next]))
			next++;

		if (next >= text.Length || text[next] is not ('}' or ']'))
			return false;

		index++;
		return true;
	}

	/// <summary>Compares the next <paramref name="count"/> content characters as multisets.</summary>
	/// <remarks>
	/// A permutation of usings preserves every character, only their order, so sorting both sides
	/// and comparing is exact rather than approximate: a dropped comment, a duplicated directive or
	/// a mangled name all change the multiset.
	/// </remarks>
	private static bool VerifyPermutation(
		ReadOnlySpan<char> source,
		ReadOnlySpan<char> output,
		ref int sourceIndex,
		ref int outputIndex,
		int count,
		out string? failure)
	{
		var expected = new char[count];
		var actual = new char[count];

		if (!Take(source, ref sourceIndex, expected) || !Take(output, ref outputIndex, actual))
		{
			failure = "formatting lost content while reordering";
			return false;
		}

		Array.Sort(expected);
		Array.Sort(actual);

		if (!expected.AsSpan().SequenceEqual(actual))
		{
			failure = "reordering changed content, not just order";
			return false;
		}

		failure = null;
		return true;
	}

	private static bool Take(ReadOnlySpan<char> text, ref int index, Span<char> into)
	{
		for (var taken = 0; taken < into.Length; taken++)
		{
			while (index < text.Length && IsSkippable(text[index]))
				index++;
			if (index >= text.Length)
				return false;
			into[taken] = text[index++];
		}

		return true;
	}

	private static int CountContent(ReadOnlySpan<char> text)
	{
		var count = 0;
		foreach (var value in text)
		{
			if (!IsSkippable(value))
				count++;
		}

		return count;
	}

	private static bool IsSkippable(char value) => value is ' ' or '\t' or '\r' or '\n';

	private static string Excerpt(ReadOnlySpan<char> text, int start)
	{
		var length = Math.Min(40, text.Length - start);
		return '"' + text.Slice(start, length).ToString().ReplaceLineEndings("\\n") + '"';
	}
}
