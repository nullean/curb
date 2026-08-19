using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace Nullean.Curb.Cleanup.Rules;

/// <summary>
/// IDE0090 — drops the type name from <c>new Widget()</c> where the target type already says it.
/// </summary>
/// <remarks>
/// <para>
/// Reported at the <c>new</c> keyword — measured, `(24,36)` through `(24,39)` on
/// <c>private static Widget Create() =&gt; new Widget();</c> — so the fixer walks from there to the
/// creation expression.
/// </para>
/// <para>
/// A pure deletion, and a mistake does not compile: if the target type were not in fact known, <c>new()</c>
/// is an error rather than a different program. That is what puts this above the <c>var</c> rule in the
/// order.
/// </para>
/// </remarks>
internal sealed class ImplicitObjectCreation : ICleanupRule
{
	public string RuleId => "IDE0090";

	// The position identifies one `new`, so a start is enough.
	public bool NeedsSpan => false;

	public bool TryFix(CleanupContext context, in CleanupDiagnostic diagnostic, TextSpan span, ICollection<PlannedFix> into, out string? refusal)
	{

		var token = context.Root.FindToken(span.Start);

		if (token.SpanStart != span.Start || !token.IsKind(SyntaxKind.NewKeyword))
		{
			refusal = "the reported position is not a `new` keyword";
			return false;
		}

		if (token.Parent is not ObjectCreationExpressionSyntax creation)
		{
			refusal = "the reported position is not an object creation";
			return false;
		}

		// `new Widget { X = 1 }` has no argument list, and dropping the type there would leave
		// `new { X = 1 }` — an anonymous object, which is a different program that also compiles. The one
		// shape in this rule where a mistake would be quiet, so it is refused.
		if (creation.ArgumentList is null)
		{
			refusal = "the creation has no argument list, and `new { … }` would be an anonymous object";
			return false;
		}

		if (Interior.CarriesContent(creation.Type))
		{
			refusal = "the type has a comment or a directive inside it, which dropping it would take too";
			return false;
		}

		var dropped = new List<TextSpan>();
		PlannedFix.CollectTokens(creation.Type, dropped);

		if (dropped.Count == 0)
		{
			refusal = "the creation names no type to drop";
			return false;
		}

		// From the end of `new` to the end of the type, so the space between them goes with it.
		refusal = null;
		into.Add(PlannedFix.Delete(TextSpan.FromBounds(token.Span.End, creation.Type.Span.End), dropped));
		return true;
	}
}

/// <summary>
/// IDE0007 — replaces an explicit local type with <c>var</c>.
/// </summary>
/// <remarks>
/// <para>
/// The one rule in the slice whose mistake is <b>quiet</b>. Every other fix here fails to compile if the
/// verdict was wrong; this one compiles and changes the declared type, so
/// <c>IEnumerable&lt;int&gt; x = list;</c> becoming <c>var x = list;</c> silently narrows <c>x</c> to
/// <c>List&lt;int&gt;</c>. Nothing Curb can check catches that, which is why it leans hardest on the
/// analyser having been right and on the freshness gate.
/// </para>
/// <para>
/// Scoped to the two shapes where the rewrite is unambiguous — a local declaration with exactly one
/// initialised variable, and a <c>foreach</c> variable. Everything else is refused rather than reasoned
/// about: <c>int a = 1, b = 2;</c> cannot become <c>var</c> at all, and <c>string s;</c> without an
/// initialiser has nothing to infer from.
/// </para>
/// </remarks>
internal sealed class ImplicitTypes : ICleanupRule
{
	public string RuleId => "IDE0007";

	// The position identifies the type node, so a start is enough.
	public bool NeedsSpan => false;

	public bool TryFix(CleanupContext context, in CleanupDiagnostic diagnostic, TextSpan span, ICollection<PlannedFix> into, out string? refusal)
	{

		var token = context.Root.FindToken(span.Start);
		if (token.SpanStart != span.Start)
		{
			refusal = "the reported position is not the start of a token";
			return false;
		}

		if (token.Parent?.FirstAncestorOrSelf<TypeSyntax>() is not { } type)
		{
			refusal = "the reported position is not the start of a type";
			return false;
		}

		// Out to the outermost type at this position. `string?` is a NullableTypeSyntax wrapping a
		// PredefinedTypeSyntax, and stopping at the inner one made every nullable local refuse — the type's
		// parent was the wrapper rather than the declaration. Arrays and pointers nest the same way.
		while (type.Parent is TypeSyntax outer && outer.Span.Start == type.Span.Start)
			type = outer;

		if (type.Span.Start != span.Start)
		{
			refusal = "the reported position is not the start of a type";
			return false;
		}

		if (type.IsVar)
		{
			refusal = "the declaration already uses var, so the log describes a file that has changed";
			return false;
		}

		switch (type.Parent)
		{
			case VariableDeclarationSyntax declaration when declaration.Parent is LocalDeclarationStatementSyntax local:
				if (local.UsingKeyword != default || local.Modifiers.Any(SyntaxKind.ConstKeyword))
				{
					refusal = "a const or using declaration keeps its explicit type";
					return false;
				}

				if (declaration.Variables.Count != 1)
				{
					refusal = "the statement declares more than one variable, which var cannot express";
					return false;
				}

				if (declaration.Variables[0].Initializer is null)
				{
					refusal = "the declaration has no initialiser, so there is nothing to infer from";
					return false;
				}

				break;

			case ForEachStatementSyntax:
				break;

			default:
				refusal = $"a type in a {type.Parent?.Kind()} is not a local declaration";
				return false;
		}

		if (Interior.CarriesContent(type))
		{
			refusal = "the type has a comment or a directive inside it, which replacing it would take too";
			return false;
		}

		// An initialiser that infers nothing. `var x = default;` and `var x = null;` do not compile whatever
		// the analyser thought, so this is a syntactic invariant of `var` rather than second-guessing a
		// verdict — and owning the rewrite means owning that.
		//
		// `default(T)` is the case a real build caught. On its own `var flag = default(bool)` is fine, and on
		// its own IDE0034 turning `default(bool)` into `default` is fine. Applied together in one pass they
		// produce `var flag = default`, which is CS8716. The two rules are in tension on exactly this shape,
		// and `var` is the one that stands down: its mistake is the quiet one, and keeping an explicit type
		// next to a `default` is what anyone would have written anyway.
		if (Initialiser(type) is { } value && InfersNothing(value))
		{
			refusal = "the initialiser infers no type, so var would not compile — `default` and `null` need the type written";
			return false;
		}

		var dropped = new List<TextSpan>();
		PlannedFix.CollectTokens(type, dropped);

		if (dropped.Count == 0)
		{
			refusal = "there is no type token to replace";
			return false;
		}

		// A deletion and an insertion rather than a substitution, because that is what the verifiers
		// already understand: the type's tokens are declared dropped and `var` is declared inserted.
		refusal = null;
		into.Add(new PlannedFix(type.Span, "var", dropped, ["var"]));
		return true;
	}

	/// <summary>The initialiser a type belongs to, when it has one.</summary>
	private static ExpressionSyntax? Initialiser(TypeSyntax type) =>
		type.Parent is VariableDeclarationSyntax { Variables.Count: 1 } declaration
			? declaration.Variables[0].Initializer?.Value
			: null;

	/// <summary>True for an initialiser <c>var</c> cannot take its type from.</summary>
	private static bool InfersNothing(ExpressionSyntax value) =>
		value is DefaultExpressionSyntax
			|| value.IsKind(SyntaxKind.DefaultLiteralExpression)
			|| value.IsKind(SyntaxKind.NullLiteralExpression);
}
