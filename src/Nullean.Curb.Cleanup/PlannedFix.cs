using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;

namespace Nullean.Curb.Cleanup;

/// <summary>
/// One edit a rule wants to make, together with the token-level damage it is declaring.
/// </summary>
/// <remarks>
/// <para>
/// The declaration is the point. Curb's verifiers compare the token stream before and after, and they
/// are parameterised by what a rewrite said it would do rather than switched off for it — so a fix that
/// removes more than it declared, or removes something somewhere else, still fails. A rule cannot buy
/// itself an exemption by being confident.
/// </para>
/// <para>
/// <see cref="DroppedTokens"/> is one span per token, not one per region. That is what
/// <c>TokenStreamComparer</c> consumes: it advances past a single span each time it skips a token, so a
/// span covering five tokens would excuse the first and fail on the rest.
/// </para>
/// </remarks>
/// <param name="Removed">The source span this fix deletes.</param>
/// <param name="Inserted">What replaces it. Empty for a pure deletion.</param>
/// <param name="DroppedTokens">Every token inside <paramref name="Removed"/> that the output will not carry, in source order.</param>
/// <param name="InsertedTokens">
/// Exact token texts the output will carry and the source does not, in output order. Given as the text
/// rather than a count so the verifiers permit precisely this word, once, here — not "extra content
/// somewhere".
/// </param>
internal readonly record struct PlannedFix(
	TextSpan Removed,
	string Inserted,
	IReadOnlyList<TextSpan> DroppedTokens,
	IReadOnlyList<string> InsertedTokens)
{
	/// <summary>A deletion of whole tokens, which is what removing a declaration or a directive is.</summary>
	public static PlannedFix Delete(TextSpan removed, IReadOnlyList<TextSpan> droppedTokens) =>
		new(removed, "", droppedTokens, []);

	/// <summary>
	/// A keyword added at <paramref name="at"/>, which is what the modifier rules do.
	/// </summary>
	/// <remarks>
	/// The trailing space belongs to the inserted text rather than to the formatter: the keyword has to be
	/// separated from whatever follows it before the output is re-parsed, and the token-stream comparer
	/// re-parses before any formatting happens.
	/// </remarks>
	public static PlannedFix InsertKeyword(int at, string keyword) =>
		new(new TextSpan(at, 0), keyword + " ", [], [keyword]);

	/// <summary>Collects the span of every token in <paramref name="node"/>, for a fix that deletes it whole.</summary>
	public static void CollectTokens(SyntaxNode node, List<TextSpan> into)
	{
		foreach (var token in node.DescendantTokens())
			into.Add(token.Span);
	}
}
