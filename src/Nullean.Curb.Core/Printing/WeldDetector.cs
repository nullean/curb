namespace Nullean.Curb.Printing;

/// <summary>
/// Decides whether two characters, placed next to each other, could lex as one token.
/// </summary>
/// <remarks>
/// <para>
/// C# lexes by maximal munch, so removing the space between two tokens can silently merge them:
/// <c>out var</c> becomes the identifier <c>outvar</c>, <c>a / /b</c> becomes a comment. This is the
/// only damage the re-parse comparer catches that the content verifier cannot, because the content
/// verifier ignores whitespace by design.
/// </para>
/// <para>
/// The test is deliberately conservative — it answers "could this possibly weld", not "does it".
/// A false positive costs one extra parse for that file; a false negative would let corrupted output
/// through, so every uncertain pairing is treated as a weld.
/// </para>
/// </remarks>
internal static class WeldDetector
{
	/// <summary>Characters that can begin or continue an identifier, keyword or number.</summary>
	private static bool IsWordChar(char value) =>
		char.IsLetterOrDigit(value) || value is '_' or '@' or '$';

	/// <summary>
	/// Characters that combine into longer operators — <c>==</c>, <c>&lt;&lt;</c>, <c>=&gt;</c>,
	/// <c>??</c>, <c>//</c>, <c>/*</c> and the rest. Brackets, commas and semicolons never combine
	/// with anything, so they are excluded and keep the check from firing on ordinary code.
	/// </summary>
	private static bool IsCombiningOperator(char value) =>
		value is '+' or '-' or '*' or '/' or '%' or '&' or '|' or '^' or '!' or '=' or '<' or '>'
			or '?' or ':' or '.' or '~';

	/// <summary>True when <paramref name="left"/> followed directly by <paramref name="right"/> might lex as one token.</summary>
	public static bool CanWeld(char left, char right)
	{
		if (IsWordChar(left) && IsWordChar(right))
			return true;

		if (IsCombiningOperator(left) && IsCombiningOperator(right))
			return true;

		// `1` followed by `.` becomes the numeric literal `1.`, and `.` followed by `5` likewise.
		if (char.IsDigit(left) && right == '.')
			return true;

		return left == '.' && char.IsDigit(right);
	}
}
