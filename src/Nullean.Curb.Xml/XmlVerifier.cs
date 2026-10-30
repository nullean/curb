namespace Nullean.Curb.Xml;

/// <summary>
/// Proves an XML format changed nothing but layout, by re-lexing the output and comparing tokens.
/// </summary>
/// <remarks>
/// <para>
/// Runs on every format, in one forward pass over both token streams. Every token must come out
/// byte-identical and in the same order, with two exceptions, which are exactly what the printer is
/// allowed to rewrite:
/// </para>
/// <list type="bullet">
/// <item>whitespace inside a start tag (<see cref="XmlTokenKind.TagSpace"/>), which XML does not keep;</item>
/// <item>
/// whitespace-only text the tree marked as <see cref="XmlTokenFlags.Layout"/>, which may be
/// rewritten as other whitespace or dropped, but never turned into anything else. Whitespace the
/// output has where the source had none fails.
/// </item>
/// </list>
/// <para>
/// Line endings compare equal to each other, since an XML parser normalises every one of them to
/// <c>\n</c> before anything else reads the document, and <c>end_of_line</c> may rewrite them.
/// </para>
/// </remarks>
internal static class XmlVerifier
{
	/// <summary>Compares the source's tokens with the output's.</summary>
	/// <param name="source">The text that was formatted.</param>
	/// <param name="before">Its tokens, with the tree's layout flags.</param>
	/// <param name="output">What the printer wrote.</param>
	/// <param name="after">The output, re-lexed.</param>
	/// <param name="failure">What differed and where in the source, when the result is false.</param>
	public static bool Verify(
		ReadOnlySpan<char> source,
		ReadOnlySpan<XmlToken> before,
		ReadOnlySpan<char> output,
		ReadOnlySpan<XmlToken> after,
		out string? failure)
	{
		failure = null;
		var i = 0;
		var j = 0;

		while (true)
		{
			while (i < before.Length && before[i].Kind == XmlTokenKind.TagSpace)
				i++;
			while (j < after.Length && after[j].Kind == XmlTokenKind.TagSpace)
				j++;

			if (i < before.Length && before[i].Is(XmlTokenFlags.Layout))
			{
				if (j < after.Length && after[j].Kind == XmlTokenKind.Text && after[j].Is(XmlTokenFlags.WhitespaceOnly))
					j++;
				i++;
				continue;
			}

			if (i == before.Length || j == after.Length)
				break;

			var expected = before[i];
			var actual = after[j];
			if (expected.Kind != actual.Kind
				|| !EqualIgnoringLineEndings(source.Slice(expected.Start, expected.Length), output.Slice(actual.Start, actual.Length)))
			{
				failure = Describe(source, expected, "changed");
				return false;
			}

			i++;
			j++;
		}

		if (i < before.Length)
		{
			failure = Describe(source, before[i], "is missing from the output");
			return false;
		}

		// The file's last line break is insert_final_newline's, and lexes as top-level whitespace.
		if (j < after.Length && !(j == after.Length - 1 && after[j].Kind == XmlTokenKind.Text && after[j].Is(XmlTokenFlags.WhitespaceOnly)))
		{
			failure = "the output has content after the end of the source";
			return false;
		}

		return true;
	}

	private static string Describe(ReadOnlySpan<char> source, in XmlToken token, string what) =>
		"verification failed: " + new XmlError(token.Start, $"{token.Kind} {what}").Describe(source);

	/// <summary>True when the two are the same text once every <c>\r\n</c>, <c>\r</c> and <c>\n</c> reads as one line break.</summary>
	internal static bool EqualIgnoringLineEndings(ReadOnlySpan<char> left, ReadOnlySpan<char> right)
	{
		if (left.SequenceEqual(right))
			return true;

		var i = 0;
		var j = 0;
		while (i < left.Length && j < right.Length)
		{
			var a = left[i];
			var b = right[j];
			if (a is '\r' or '\n' && b is '\r' or '\n')
			{
				i += a == '\r' && i + 1 < left.Length && left[i + 1] == '\n' ? 2 : 1;
				j += b == '\r' && j + 1 < right.Length && right[j + 1] == '\n' ? 2 : 1;
				continue;
			}

			if (a != b)
				return false;
			i++;
			j++;
		}

		return i == left.Length && j == right.Length;
	}
}
