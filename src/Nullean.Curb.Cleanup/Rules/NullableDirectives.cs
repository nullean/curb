using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace Nullean.Curb.Cleanup.Rules;

/// <summary>
/// IDE0240 — removes a <c>#nullable</c> directive that says what the project already says.
/// </summary>
/// <remarks>
/// <para>
/// Reported at the <c>#</c>, spanning the directive — measured, <c>(1,1)</c> through <c>(1,17)</c> covering
/// <c>#nullable enable</c>.
/// </para>
/// <para>
/// <b>This rule is verified by one net rather than two, and that is worth knowing.</b> A directive lives in
/// trivia, and <c>SyntaxNode.DescendantTokens()</c> does not descend into trivia — so
/// <c>TokenStreamComparer</c> cannot see the directive at all, either before or after. Removing one is
/// invisible to it. <see cref="Nullean.Curb.Verification.ContentVerifier"/> does catch it, because it walks
/// characters, so the deletion is still declared and still held to the declared span. But the second net is
/// not watching here, which is why this rule does nothing but remove a whole line it has positively
/// identified as a nullable directive.
/// </para>
/// <para>
/// Refused where the file has a <c>#if</c>, for the same reason as the using rule: the compiler decided for
/// one symbol set, and a directive that is redundant under those symbols may be load-bearing under others.
/// </para>
/// </remarks>
internal sealed class RedundantNullableDirectives : ICleanupRule
{
	public string RuleId => "IDE0240";

	public bool NeedsSpan => false;

	public bool TryFix(CleanupContext context, in CleanupDiagnostic diagnostic, TextSpan span, ICollection<PlannedFix> into, out string? refusal)
	{
		if (context.HasConditionalDirectives)
		{
			refusal = "the file has a #if, so a directive redundant under one symbol set may not be under another";
			return false;
		}

		// FindTrivia rather than FindToken: a directive is trivia, and FindToken would hand back whichever
		// real token happens to carry it.
		var trivia = context.Root.FindTrivia(span.Start);

		if (trivia.SpanStart != span.Start || !trivia.IsKind(SyntaxKind.NullableDirectiveTrivia))
		{
			refusal = "the reported position is not the start of a #nullable directive";
			return false;
		}

		if (trivia.GetStructure() is not NullableDirectiveTriviaSyntax directive)
		{
			refusal = "the directive has no structure to read";
			return false;
		}

		// A directive the compiler could not make sense of is not one to delete on its word.
		if (!directive.IsActive || directive.ContainsDiagnostics)
		{
			refusal = "the directive is inactive or malformed";
			return false;
		}

		// A comment after the directive is *inside* the directive's span, not after it: the trailing comment
		// is leading trivia of the zero-width end-of-directive token, and Span only excludes the last token's
		// *trailing* trivia. So the line check below cannot see it and Interior has to.
		if (Interior.CarriesContent(directive))
		{
			refusal = "the directive shares its line with a comment, which removing it would take too";
			return false;
		}

		// The whole line, and only when the line holds nothing else.
		var line = context.Text.Lines.GetLineFromPosition(trivia.SpanStart);
		if (!IsBlank(context.Text, line.Start, trivia.SpanStart) || !IsBlank(context.Text, trivia.Span.End, line.End))
		{
			refusal = "the directive shares its line with something else";
			return false;
		}

		// One span for the whole directive rather than one per token. The containment matching handles a
		// multi-token span, and the tokens are not in the token stream anyway.
		var dropped = new List<TextSpan> { trivia.Span };

		refusal = null;
		into.Add(PlannedFix.Delete(TextSpan.FromBounds(line.Start, line.EndIncludingLineBreak), dropped));
		return true;
	}

	private static bool IsBlank(SourceText text, int start, int end)
	{
		for (var i = start; i < end; i++)
		{
			if (!char.IsWhiteSpace(text[i]))
				return false;
		}

		return true;
	}
}
