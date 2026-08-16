using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace Nullean.Kerf.Printing.CSharp;

/// <summary>
/// The regions a file has asked Kerf to leave alone with
/// <c>#pragma warning disable IDE0055</c>.
/// </summary>
/// <remarks>
/// <para>
/// This is the region-level opt-out, and it is .NET's own rather than one Kerf invented. Roslyn
/// honours the pragma in Format Document — issue 38587, closed as fixed — so a hand-aligned table
/// or a generated block wrapped in it stays as written. There is no ignore comment to learn.
/// </para>
/// <para>
/// A bare <c>#pragma warning disable</c> with no rule ids disables everything, IDE0055 included, so
/// it counts too.
/// </para>
/// </remarks>
internal static class FormattingSuppression
{
	/// <summary>The rule that names formatting.</summary>
	private const string FormattingRule = "IDE0055";

	/// <summary>
	/// Finds the suppressed spans in a file, or null when there are none.
	/// </summary>
	/// <remarks>
	/// Guarded by a plain text scan first. Almost no file contains a pragma at all — none of the
	/// 1,196 in the corpus do — so the usual cost is one scan of the source rather than a walk of
	/// every trivia node in the tree.
	/// </remarks>
	public static List<TextSpan>? Scan(SyntaxNode root, ReadOnlySpan<char> source)
	{
		if (!source.Contains("#pragma warning", StringComparison.Ordinal))
			return null;

		List<TextSpan>? spans = null;
		var openedAt = -1;

		foreach (var trivia in root.DescendantTrivia(descendIntoTrivia: true))
		{
			if (!trivia.IsKind(SyntaxKind.PragmaWarningDirectiveTrivia))
				continue;

			var pragma = (PragmaWarningDirectiveTriviaSyntax)trivia.GetStructure()!;
			if (!MentionsFormatting(pragma))
				continue;

			if (pragma.DisableOrRestoreKeyword.IsKind(SyntaxKind.DisableKeyword))
			{
				// Nested disables are not a thing; the first one opens the region.
				if (openedAt < 0)
					openedAt = pragma.FullSpan.Start;

				continue;
			}

			if (openedAt < 0)
				continue;

			(spans ??= []).Add(TextSpan.FromBounds(openedAt, pragma.FullSpan.End));
			openedAt = -1;
		}

		// A disable never restored runs to the end of the file, which is what the compiler does too.
		if (openedAt >= 0)
			(spans ??= []).Add(TextSpan.FromBounds(openedAt, root.FullSpan.End));

		return spans;
	}

	/// <summary>True when a node sits wholly inside a suppressed region.</summary>
	public static bool Covers(List<TextSpan> spans, TextSpan node)
	{
		foreach (var span in spans)
		{
			if (span.Contains(node))
				return true;
		}

		return false;
	}

	private static bool MentionsFormatting(PragmaWarningDirectiveTriviaSyntax pragma)
	{
		// No ids at all means every rule, so formatting with it.
		if (pragma.ErrorCodes.Count == 0)
			return true;

		foreach (var code in pragma.ErrorCodes)
		{
			if (code is IdentifierNameSyntax identifier
				&& identifier.Identifier.ValueText.Equals(FormattingRule, StringComparison.OrdinalIgnoreCase))
				return true;
		}

		return false;
	}
}
