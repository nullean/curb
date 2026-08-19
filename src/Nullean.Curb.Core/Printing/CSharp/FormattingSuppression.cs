using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace Nullean.Curb.Printing.CSharp;

/// <summary>
/// The regions a file has asked Curb to leave alone with
/// <c>#pragma warning disable IDE0055</c>.
/// </summary>
/// <remarks>
/// <para>
/// This is the region-level opt-out, and it is .NET's own rather than one Curb invented. Roslyn
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
	/// Uses a two-phase approach to avoid walking the full directive chain. Phase one is a plain text
	/// scan that collects the source offsets of every <c>#pragma warning</c> occurrence — files with
	/// none pay only this scan. Phase two calls <c>root.FindTrivia(offset)</c> for each collected
	/// offset; <c>FindTrivia</c> is O(depth), not O(all directives), so it reaches each pragma
	/// directly without visiting unrelated <c>#if</c>, <c>#region</c>, or <c>#nullable</c> nodes.
	/// A false-positive offset (e.g. the text inside a string literal) is harmlessly rejected by the
	/// <c>is not PragmaWarningDirectiveTriviaSyntax</c> guard.
	/// </remarks>
	public static List<TextSpan>? Scan(SyntaxNode root, ReadOnlySpan<char> source)
	{
		// Fast text scan: collect offsets of all #pragma warning occurrences.
		// Files with none pay only this scan.
		var offsets = CollectPragmaOffsets(source);
		if (offsets is null)
			return null;
		if (!root.ContainsDirectives)
			return null;

		List<TextSpan>? spans = null;
		var openedAt = -1;

		foreach (var offset in offsets)
		{
			// FindTrivia is O(depth) — walks only the tree path to this offset, not all directives.
			var trivia = root.FindTrivia(offset);
			if (trivia.GetStructure() is not PragmaWarningDirectiveTriviaSyntax pragma)
				continue;
			if (!MentionsFormatting(pragma))
				continue;

			if (pragma.DisableOrRestoreKeyword.IsKind(SyntaxKind.DisableKeyword))
			{
				// Nested disables are not a thing; the first one opens the region.
				if (openedAt < 0)
					openedAt = pragma.FullSpan.Start;
			}
			else if (openedAt >= 0)
			{
				(spans ??= []).Add(TextSpan.FromBounds(openedAt, pragma.FullSpan.End));
				openedAt = -1;
			}
		}

		// A disable never restored runs to the end of the file, which is what the compiler does too.
		if (openedAt >= 0)
			(spans ??= []).Add(TextSpan.FromBounds(openedAt, root.FullSpan.End));

		return spans;
	}

	private static List<int>? CollectPragmaOffsets(ReadOnlySpan<char> source)
	{
		const string needle = "#pragma warning";
		List<int>? offsets = null;
		var remaining = source;
		var baseOffset = 0;
		while (true)
		{
			var idx = remaining.IndexOf(needle, StringComparison.Ordinal);
			if (idx < 0)
				break;
			(offsets ??= []).Add(baseOffset + idx);
			var advance = idx + needle.Length;
			remaining = remaining[advance..];
			baseOffset += advance;
		}
		return offsets;
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
