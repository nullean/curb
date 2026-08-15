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
/// What it deliberately does <b>not</b> catch: two tokens being glued together by a missing space
/// (<c>a b</c> becoming <c>ab</c>), since whitespace is exactly what it ignores. That is the
/// re-parse token comparer's job, which is why both nets exist. Nor would it notice whitespace lost
/// from inside a string literal.
/// </para>
/// </remarks>
internal static class ContentVerifier
{
	public static bool Verify(ReadOnlySpan<char> source, ReadOnlySpan<char> output, out string? failure)
	{
		var sourceIndex = 0;
		var outputIndex = 0;

		while (true)
		{
			while (sourceIndex < source.Length && IsSkippable(source[sourceIndex]))
				sourceIndex++;
			while (outputIndex < output.Length && IsSkippable(output[outputIndex]))
				outputIndex++;

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
				failure =
					$"formatting changed content at source offset {sourceIndex}: "
					+ $"expected {Excerpt(source, sourceIndex)} but produced {Excerpt(output, outputIndex)}";
				return false;
			}

			sourceIndex++;
			outputIndex++;
		}
	}

	private static bool IsSkippable(char value) => value is ' ' or '\t' or '\r' or '\n';

	private static string Excerpt(ReadOnlySpan<char> text, int start)
	{
		var length = Math.Min(40, text.Length - start);
		return '"' + text.Slice(start, length).ToString().ReplaceLineEndings("\\n") + '"';
	}
}
