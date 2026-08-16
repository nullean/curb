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
	/// <param name="namespaceUnwrapped">
	/// A block namespace was rewritten as a file-scoped one, so its <c>{</c> reads as <c>;</c> in the
	/// output and its <c>}</c> is not there at all. Permitted once, and the two halves have to agree.
	/// </param>
	/// <param name="dropped">
	/// Source spans a rewrite deliberately did not emit, in source order. Expression bodies drop a
	/// block's braces and its <c>return</c>; naming the spans keeps the rest of the file under the
	/// strict compare instead of excusing the whole of it.
	/// </param>
	/// <param name="headerAdded">
	/// A <c>file_header_template</c> the output opens with and the source did not have. Given as the
	/// exact text, so the check is that the output starts with precisely this and nothing else was
	/// invented — not a licence to prepend anything.
	/// </param>
	/// <param name="arrowsAdded">
	/// How many <c>=&gt;</c> the output carries that the source does not. Exact rather than a flag,
	/// so an extra arrow is still damage.
	/// </param>
	/// <param name="bracesAdded">
	/// A control-flow body was given braces, so the output carries <c>{</c> and <c>}</c> the source
	/// does not. They are counted rather than merely allowed: an opening brace with no closing one, or
	/// a closing one that was never opened, is still a failure.
	/// </param>
	public static bool Verify(
		ReadOnlySpan<char> source,
		ReadOnlySpan<char> output,
		out string? failure,
		IReadOnlyList<TextSpan>? reordered = null,
		bool trailingCommas = false,
		bool bracesAdded = false,
		bool namespaceUnwrapped = false,
		IReadOnlyList<TextSpan>? dropped = null,
		int arrowsAdded = 0,
		string? headerAdded = null)
	{
		var sourceIndex = 0;
		var outputIndex = 0;
		var nextReordered = 0;

		// The header, matched exactly and only at the start, before the two are walked together.
		if (headerAdded is not null && !TakeHeader(output, headerAdded, ref outputIndex, out failure))
			return false;

		// Braces the output has and the source does not, counted so they have to balance.
		var braceDebt = 0;

		// The block namespace's closing brace, owed once its opening brace became a semicolon.
		var namespaceBraceOwed = false;

		var nextDropped = 0;
		var arrowsOwed = arrowsAdded;

		while (true)
		{
			while (sourceIndex < source.Length && IsSkippable(source[sourceIndex]))
				sourceIndex++;
			while (outputIndex < output.Length && IsSkippable(output[outputIndex]))
				outputIndex++;

			// Source a rewrite dropped on purpose. Stepping over it is only safe because what replaced
			// it is counted: an expression body drops a block and adds one arrow, so the arrows and
			// the dropped blocks have to balance by the end of the file.
			if (dropped is not null && nextDropped < dropped.Count && sourceIndex >= dropped[nextDropped].Start)
			{
				sourceIndex = Math.Max(sourceIndex, dropped[nextDropped].End);
				nextDropped++;

				// Round again rather than falling through: the cursor now sits on the whitespace that
				// followed the dropped token, and the compare below reads a character directly.
				continue;
			}

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

			// The namespace conversion, which is a substitution and a deletion rather than an
			// insertion: `namespace N {` became `namespace N;`, and the matching `}` is then owed.
			// Both halves are permitted exactly once, so a stray brace elsewhere is still damage.
			if (namespaceUnwrapped)
			{
				if (!sourceDone && !outputDone
					&& source[sourceIndex] == '{' && output[outputIndex] == ';' && !namespaceBraceOwed)
				{
					namespaceBraceOwed = true;
					sourceIndex++;
					outputIndex++;
					continue;
				}

				if (!sourceDone && source[sourceIndex] == '}' && namespaceBraceOwed
					&& (outputDone || output[outputIndex] != '}'))
				{
					namespaceBraceOwed = false;
					sourceIndex++;
					continue;
				}
			}

			// A brace the output added. Stepping over it is safe only because everything else still
			// has to match exactly and the pair has to balance: `if (a) Foo();` printed as `if (a) {}`
			// fails, since after the braces the source still has `Foo();` and the output has nothing.
			//
			// Which brace is "the added one" cannot be decided locally — braces are indistinguishable
			// characters, so an added `}` sitting in front of the enclosing block's own `}` matches it
			// and the pair is settled by the count instead, here and at the end of the file.
			if (bracesAdded && !outputDone)
			{
				if (output[outputIndex] == '{' && (sourceDone || source[sourceIndex] != '{'))
				{
					braceDebt++;
					outputIndex++;
					continue;
				}

				if (output[outputIndex] == '}' && braceDebt > 0 && (sourceDone || source[sourceIndex] != '}'))
				{
					braceDebt--;
					outputIndex++;
					continue;
				}
			}

			if (!outputDone && arrowsOwed > 0
				&& output[outputIndex] == '=' && outputIndex + 1 < output.Length && output[outputIndex + 1] == '>'
				&& (sourceDone || source[sourceIndex] != '='))
			{
				// The arrow that replaced a dropped block. One per opening brace dropped, no more.
				arrowsOwed--;
				outputIndex += 2;
				continue;
			}

			if (sourceDone && outputDone)
			{
				if (arrowsOwed != 0)
				{
					failure = "an expression body dropped a block without putting an arrow in its place";
					return false;
				}

				if (braceDebt != 0)
				{
					failure = $"formatting left {braceDebt} added brace(s) unbalanced";
					return false;
				}

				if (namespaceBraceOwed)
				{
					failure = "formatting opened a file-scoped namespace but left its block's brace unaccounted for";
					return false;
				}

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
	/// Consumes an inserted file header from the front of the output.
	/// </summary>
	/// <remarks>
	/// Compared character for character against what the configuration asked for, ignoring only
	/// whitespace, so this permits exactly the header the repository named and nothing else.
	/// </remarks>
	private static bool TakeHeader(ReadOnlySpan<char> output, string header, ref int outputIndex, out string? failure)
	{
		foreach (var expected in header)
		{
			if (IsSkippable(expected))
				continue;

			while (outputIndex < output.Length && IsSkippable(output[outputIndex]))
				outputIndex++;

			if (outputIndex >= output.Length || output[outputIndex] != expected)
			{
				failure = "the file header written does not match file_header_template";
				return false;
			}

			outputIndex++;
		}

		failure = null;
		return true;
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
