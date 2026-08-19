using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace Nullean.Curb.Cleanup.Rules;

/// <summary>
/// IDE0034 — takes the type out of <c>default(T)</c> where the target type already says it.
/// </summary>
/// <remarks>
/// <para>
/// Reported at the <c>default</c> keyword, spanning the whole expression — measured, <c>(17,17)</c> through
/// <c>(17,31)</c> covering <c>default(Int32)</c>. Only the parenthesised type goes.
/// </para>
/// <para>
/// A mistake does not compile. A bare <c>default</c> with no inferable target type is an error, not a
/// different program, which is what puts this among the loud rules.
/// </para>
/// </remarks>
internal sealed class SimplifiedDefaults : ICleanupRule
{
	public string RuleId => "IDE0034";

	// The keyword identifies one expression, so a start is enough.
	public bool NeedsSpan => false;

	public bool TryFix(CleanupContext context, in CleanupDiagnostic diagnostic, TextSpan span, ICollection<PlannedFix> into, out string? refusal)
	{
		var token = context.Root.FindToken(span.Start);

		if (token.SpanStart != span.Start || !token.IsKind(SyntaxKind.DefaultKeyword))
		{
			refusal = "the reported position is not a `default` keyword";
			return false;
		}

		if (token.Parent is not DefaultExpressionSyntax expression)
		{
			refusal = "the reported position is not a `default(T)` expression";
			return false;
		}

		if (Interior.CarriesContent(expression))
		{
			refusal = "the expression has a comment or a directive inside it, which dropping it would take too";
			return false;
		}

		var dropped = new List<TextSpan>
		{
			TextSpan.FromBounds(expression.OpenParenToken.SpanStart, expression.CloseParenToken.Span.End),
		};

		refusal = null;
		into.Add(PlannedFix.Delete(
			TextSpan.FromBounds(expression.Keyword.Span.End, expression.CloseParenToken.Span.End), dropped));

		return true;
	}
}

/// <summary>
/// IDE0071 — drops a redundant <c>.ToString()</c> from inside an interpolation.
/// </summary>
/// <remarks>
/// <para>
/// Reported at the <c>.</c>, spanning the call — measured, <c>(20,18)</c> through <c>(20,29)</c> covering
/// <c>.ToString()</c>.
/// </para>
/// <para>
/// <b>Only where the call takes no arguments.</b> <c>{x.ToString("N0")}</c> is also reported, but its fix
/// moves the argument into the interpolation's format clause — <c>{x:N0}</c> — which is a rewrite of two
/// places rather than a deletion of one, and deleting the call alone would silently lose the format. Not
/// derivable from the span, so refused.
/// </para>
/// </remarks>
internal sealed class SimplifiedInterpolations : ICleanupRule
{
	public string RuleId => "IDE0071";

	public bool NeedsSpan => false;

	public bool TryFix(CleanupContext context, in CleanupDiagnostic diagnostic, TextSpan span, ICollection<PlannedFix> into, out string? refusal)
	{
		var token = context.Root.FindToken(span.Start);

		if (token.SpanStart != span.Start || !token.IsKind(SyntaxKind.DotToken))
		{
			refusal = "the reported position is not the dot of a member access";
			return false;
		}

		if (token.Parent is not MemberAccessExpressionSyntax access
			|| access.Parent is not InvocationExpressionSyntax invocation
			|| invocation.Expression != access)
		{
			refusal = "the reported position is not a method call on a member access";
			return false;
		}

		if (access.Name.Identifier.ValueText != "ToString")
		{
			refusal = $"the call is to {access.Name.Identifier.ValueText}, not ToString";
			return false;
		}

		// The interpolation is what makes the call redundant; the same call anywhere else is not.
		if (invocation.Parent is not InterpolationSyntax interpolation || interpolation.Expression != invocation)
		{
			refusal = "the call is not the whole of an interpolation";
			return false;
		}

		if (invocation.ArgumentList.Arguments.Count > 0)
		{
			refusal = "the call takes arguments, and the fix for that moves them into a format clause";
			return false;
		}

		if (Interior.CarriesContent(access) || Interior.CarriesContent(invocation.ArgumentList))
		{
			refusal = "the call has a comment or a directive inside it, which dropping it would take too";
			return false;
		}

		var dropped = new List<TextSpan> { TextSpan.FromBounds(token.SpanStart, invocation.Span.End) };

		refusal = null;
		into.Add(PlannedFix.Delete(TextSpan.FromBounds(token.SpanStart, invocation.Span.End), dropped));
		return true;
	}
}
