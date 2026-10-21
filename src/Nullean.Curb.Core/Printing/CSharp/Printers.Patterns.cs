using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Nullean.Curb.Documents;
using Nullean.Curb.Options;

namespace Nullean.Curb.Printing.CSharp;

/// <summary>Patterns, designations, tuples and the smaller expression forms.</summary>
internal static partial class Printers
{
	public static void IsPatternExpression(IsPatternExpressionSyntax node, PrintContext context)
	{
		Node.Print(node.Expression, context);
		context.Arena.Synthetic(SyntheticText.Space);
		TokenPrinter.Print(node.IsKeyword, context);

		// A bare recursive pattern (`is { ... }`, no type or positional clause ahead of the brace) that
		// the author kept on the `is` line owns the gap to its own opening brace: that gap answers the
		// same fits question as whether the property list wraps, so RecursivePattern controls it with a
		// fits-aware line instead of a fixed space — see BareRecursivePatternOwnsLeadingBrace.
		if (BareRecursivePatternOwnsLeadingBrace(node.Pattern, context))
		{
			Node.Print(node.Pattern, context);
			return;
		}

		// A break the author put after `is` is theirs, the same as one between two alternatives of the
		// pattern that follows. Without this, `x is` / `A or` / `B` kept its `or` breaks and lost the
		// first one, which reads worse than either preserving all of them or none.
		if (context.AuthorBroke(node.IsKeyword.Span.End, node.Pattern.SpanStart))
			context.Arena.HardLine();
		else
			context.Arena.Synthetic(SyntheticText.Space);

		Node.Print(node.Pattern, context);
	}

	/// <summary>
	/// True for the exact shape RecursivePattern defers its leading brace-line to: a property-only
	/// pattern directly under an <c>is</c> expression, with nothing between the `is` keyword and the
	/// pattern in the source. Author-broken input keeps the caller's forced line instead, matching every
	/// other pattern shape.
	/// </summary>
	private static bool BareRecursivePatternOwnsLeadingBrace(PatternSyntax pattern, PrintContext context) =>
		pattern is RecursivePatternSyntax
		{
			Type: null,
			PositionalPatternClause: null,
			PropertyPatternClause.Subpatterns.Count: > 0,
			Parent: IsPatternExpressionSyntax isExpression,
		}
		&& !context.AuthorBroke(isExpression.IsKeyword.Span.End, pattern.SpanStart);

	public static void DeclarationPattern(DeclarationPatternSyntax node, PrintContext context)
	{
		Node.Print(node.Type, context);
		context.Arena.Synthetic(SyntheticText.Space);
		Node.Print(node.Designation, context);
	}

	public static void RecursivePattern(RecursivePatternSyntax node, PrintContext context)
	{
		var arena = context.Arena;

		if (node.Type is not null)
			Node.Print(node.Type, context);

		if (node.PositionalPatternClause is not null)
		{
			// A break the author put inside the parentheses is theirs; dotnet format leaves them all.
			var positional = node.PositionalPatternClause;
			var subpatterns = positional.Subpatterns;
			var asWritten = SpansLines(positional, context);
			TokenPrinter.Print(positional.OpenParenToken, context);
			using (arena.Indent())
			{
				if (subpatterns.Count > 0 && asWritten && context.AuthorBroke(positional.OpenParenToken.Span.End, subpatterns[0].SpanStart))
					arena.HardLine();

				for (var i = 0; i < subpatterns.Count; i++)
				{
					Node.Print(subpatterns[i], context);
					if (i >= subpatterns.SeparatorCount)
						continue;
					TokenPrinter.Print(subpatterns.GetSeparator(i), context);
					if (asWritten && context.AuthorBroke(subpatterns[i].Span.End, subpatterns[i + 1].SpanStart))
						arena.HardLine();
					else
						arena.Synthetic(SyntheticText.Space);
				}
			}

			if (subpatterns.Count > 0 && asWritten && context.AuthorBroke(subpatterns[^1].Span.End, positional.CloseParenToken.SpanStart))
				arena.HardLine();
			TokenPrinter.Print(positional.CloseParenToken, context);
		}

		if (node.PropertyPatternClause is not null)
		{
			var property = node.PropertyPatternClause;
			var hasPrecedingContent = node.Type is not null || node.PositionalPatternClause is not null;

			if (property.Subpatterns.Count > 0)
			{
				// The brace has to answer the same fits question as the property list behind it — dotnet
				// format moves it to its own line whenever that list doesn't fit, the same way an
				// initializer's brace does. A plain space before an unconditionally-printed brace, as
				// before, could never move: the group guarding the list only ever affected what came
				// after the brace, not the brace itself. A bare pattern directly under `is` gets the same
				// treatment for the `is`-to-brace gap — see BareRecursivePatternOwnsLeadingBrace, whose
				// caller (IsPatternExpression) skips its own separator in exactly this case.
				var ownsLeadingGap = hasPrecedingContent || BareRecursivePatternOwnsLeadingBrace(node, context);

				// A pattern the author opened out keeps their breaks, as an initializer does. Without
				// this the group only ever asked whether the list fits, and with no width everything
				// fits, so preservation mode joined a pattern dotnet format leaves alone.
				var asWritten = SpansLines(property, context);
				var subpatterns = property.Subpatterns;
				var rewritesComma = RewritesTrailingComma(subpatterns, property.CloseBraceToken, context);

				using (arena.Group())
				{
					// dotnet format places this brace by the initializer flag of csharp_new_line_before_open_brace,
					// and only once the braces span lines: `Version { Major: 1,` / `Minor: 2 }` opens out fully.
					if (ownsLeadingGap)
					{
						var beforeBrace = property.OpenBraceToken.GetPreviousToken().Span.End;
						if (asWritten && context.AuthorBroke(beforeBrace, property.OpenBraceToken.SpanStart))
							BeforeOpenBrace(BraceStyle.ObjectCollectionArrayInitializers, context);
						else
							BeforeOpenBraceWhenBroken(BraceStyle.ObjectCollectionArrayInitializers, context);
					}

					TokenPrinter.Print(property.OpenBraceToken, context);
					using (arena.Indent())
					{
						InsideBrace(property.OpenBraceToken.Span.End, subpatterns[0].SpanStart);
						for (var i = 0; i < subpatterns.Count; i++)
						{
							Node.Print(subpatterns[i], context);
							if (i >= subpatterns.SeparatorCount)
								continue;

							// The source's own trailing comma gives way when the options decide this one.
							if (rewritesComma && i == subpatterns.Count - 1)
								continue;

							TokenPrinter.Print(subpatterns.GetSeparator(i), context);

							// A trailing separator is followed by the closing line, not by another one —
							// both used to print, which joined as `Minor: 2,  }`.
							if (i >= subpatterns.Count - 1)
								continue;

							if (!asWritten)
								arena.Line();
							else if (context.AuthorBroke(subpatterns[i].Span.End, subpatterns[i + 1].SpanStart))
								arena.HardLine();
							else
								arena.Synthetic(SyntheticText.Space);
						}

						if (rewritesComma)
							PrintTrailingComma(context);
					}

					InsideBrace(property.CloseBraceToken.GetPreviousToken().Span.End, property.CloseBraceToken.SpanStart);
					TokenPrinter.Print(property.CloseBraceToken, context);
				}

				// Just inside the braces, which open out with the group: dotnet format moves both edges of
				// `{ Major: 1,` / `Minor: 2 }` onto lines of their own, as it does an initializer's.
				void InsideBrace(int from, int to)
				{
					if (asWritten && context.AuthorBroke(from, to))
						arena.HardLine();
					else
						arena.Line();
				}
			}
			else
			{
				if (hasPrecedingContent)
					arena.Synthetic(SyntheticText.Space);
				TokenPrinter.Print(property.OpenBraceToken, context);
				arena.Synthetic(SyntheticText.Space);
				TokenPrinter.Print(property.CloseBraceToken, context);
			}
		}

		if (node.Designation is null)
			return;

		arena.Synthetic(SyntheticText.Space);
		Node.Print(node.Designation, context);
	}

	public static void Subpattern(SubpatternSyntax node, PrintContext context)
	{
		if (node.ExpressionColon is not null)
		{
			Node.Print(node.ExpressionColon.Expression, context);
			TokenPrinter.Print(node.ExpressionColon.ColonToken, context);

			// `Address:` / `{ … }` — a nested pattern the author put on a line of its own stays there.
			if (context.AuthorBroke(node.ExpressionColon.ColonToken.Span.End, node.Pattern.SpanStart))
				context.Arena.HardLine();
			else
				context.Arena.Synthetic(SyntheticText.Space);
		}

		Node.Print(node.Pattern, context);
	}

	/// <summary>Covers <c>and</c>, <c>or</c> and relational patterns, all of which need spacing.</summary>
	public static void BinaryPattern(BinaryPatternSyntax node, PrintContext context)
	{
		Node.Print(node.Left, context);

		// The same rule BinaryExpression follows: a chain the author broke stays broken, on whichever
		// side of the operator they broke it. A long `is A or B or C` written one alternative per line
		// was closed up onto a single line, because nothing here asked. That shape is everywhere in
		// analyzer code and was among the largest sources of churn measured on roslyn.
		if (context.AuthorBroke(node.Left.Span.End, node.OperatorToken.SpanStart))
			context.Arena.HardLine(DocFlags.OnlyIfNotAtLineStart);
		else
			context.Arena.Synthetic(SyntheticText.Space);

		TokenPrinter.Print(node.OperatorToken, context);

		if (context.AuthorBroke(node.OperatorToken.Span.End, node.Right.SpanStart))
			context.Arena.HardLine();
		else
			context.Arena.Synthetic(SyntheticText.Space);

		Node.Print(node.Right, context);
	}

	public static void UnaryPattern(UnaryPatternSyntax node, PrintContext context)
	{
		TokenPrinter.Print(node.OperatorToken, context);
		context.Arena.Synthetic(SyntheticText.Space);
		Node.Print(node.Pattern, context);
	}

	public static void RelationalPattern(RelationalPatternSyntax node, PrintContext context)
	{
		TokenPrinter.Print(node.OperatorToken, context);
		context.Arena.Synthetic(SyntheticText.Space);
		Node.Print(node.Expression, context);
	}

	public static void ListPattern(ListPatternSyntax node, PrintContext context)
	{
		var arena = context.Arena;
		TokenPrinter.Print(node.OpenBracketToken, context);

		var rewritesComma = RewritesTrailingComma(node.Patterns, node.CloseBracketToken, context);

		// Printed flat unless the author opened it out, in which case their breaks stay — the same rule
		// a collection expression follows. The group is what a trailing comma's IfBreak resolves against.
		var asWritten = SpansLines(node, context);

		using (arena.Group())
		{
			using (arena.Indent())
			{
				if (node.Patterns.Count > 0)
					Edge(node.OpenBracketToken.Span.End, node.Patterns[0].SpanStart);
				else
					Spacing.InsideBrackets(context);

				for (var i = 0; i < node.Patterns.Count; i++)
				{
					Node.Print(node.Patterns[i], context);
					if (i >= node.Patterns.SeparatorCount)
						continue;

					if (rewritesComma && i == node.Patterns.Count - 1)
						continue;

					Spacing.BeforeComma(context);
					TokenPrinter.Print(node.Patterns.GetSeparator(i), context);

					// A trailing comma runs straight into the closing bracket, not `, ]`.
					if (i >= node.Patterns.Count - 1)
						continue;

					if (asWritten && context.AuthorBroke(node.Patterns[i].Span.End, node.Patterns[i + 1].SpanStart))
						arena.HardLine();
					else
						Spacing.AfterComma(context);
				}

				if (rewritesComma)
					PrintTrailingComma(context);
			}

			if (node.Patterns.Count > 0)
				Edge(node.CloseBracketToken.GetPreviousToken().Span.End, node.CloseBracketToken.SpanStart);
			else
				Spacing.InsideBrackets(context);
		}

		TokenPrinter.Print(node.CloseBracketToken, context);

		if (node.Designation is not null)
		{
			arena.Synthetic(SyntheticText.Space);
			Node.Print(node.Designation, context);
		}

		void Edge(int from, int to)
		{
			if (asWritten && context.AuthorBroke(from, to))
				arena.HardLine();
			else
				Spacing.InsideBrackets(context);
		}
	}

	public static void ParenthesizedPattern(ParenthesizedPatternSyntax node, PrintContext context)
	{
		TokenPrinter.Print(node.OpenParenToken, context);
		Node.Print(node.Pattern, context);
		TokenPrinter.Print(node.CloseParenToken, context);
	}

	public static void DeclarationExpression(DeclarationExpressionSyntax node, PrintContext context)
	{
		Node.Print(node.Type, context);
		context.Arena.Synthetic(SyntheticText.Space);
		Node.Print(node.Designation, context);
	}

	public static void ParenthesizedVariableDesignation(ParenthesizedVariableDesignationSyntax node, PrintContext context)
	{
		TokenPrinter.Print(node.OpenParenToken, context);

		for (var i = 0; i < node.Variables.Count; i++)
		{
			Node.Print(node.Variables[i], context);
			if (i >= node.Variables.SeparatorCount)
				continue;
			Spacing.BeforeComma(context);
			TokenPrinter.Print(node.Variables.GetSeparator(i), context);
			Spacing.AfterComma(context);
		}

		TokenPrinter.Print(node.CloseParenToken, context);
	}

	/// <summary>A tuple expression, and the same shape for a tuple type.</summary>
	public static void TupleExpression(TupleExpressionSyntax node, PrintContext context)
	{
		// Every list that can hold a line break has to keep the author's, or it joins and the
		// enclosing construct — which preserved its own layout and so emitted no break opportunity —
		// has no way to break the result. A tuple was the last one still joining unconditionally.
		var asWritten = SpansLines(node, context);

		TokenPrinter.Print(node.OpenParenToken, context);

		for (var i = 0; i < node.Arguments.Count; i++)
		{
			Node.Print(node.Arguments[i], context);
			if (i >= node.Arguments.SeparatorCount)
				continue;

			Spacing.BeforeComma(context);
			TokenPrinter.Print(node.Arguments.GetSeparator(i), context);

			if (asWritten && context.AuthorBroke(node.Arguments[i].Span.End, node.Arguments[i + 1].SpanStart))
				context.Arena.HardLine();
			else
				Spacing.AfterComma(context);
		}

		TokenPrinter.Print(node.CloseParenToken, context);
	}

	public static void TupleType(TupleTypeSyntax node, PrintContext context)
	{
		TokenPrinter.Print(node.OpenParenToken, context);

		for (var i = 0; i < node.Elements.Count; i++)
		{
			Node.Print(node.Elements[i], context);
			if (i >= node.Elements.SeparatorCount)
				continue;
			Spacing.BeforeComma(context);
			TokenPrinter.Print(node.Elements.GetSeparator(i), context);
			Spacing.AfterComma(context);
		}

		TokenPrinter.Print(node.CloseParenToken, context);
	}

	public static void TupleElement(TupleElementSyntax node, PrintContext context)
	{
		Node.Print(node.Type, context);
		if (node.Identifier.RawKind == 0)
			return;

		context.Arena.Synthetic(SyntheticText.Space);
		TokenPrinter.Print(node.Identifier, context);
	}

	public static void WithExpression(WithExpressionSyntax node, PrintContext context)
	{
		// `X.CreateContext(a, b) with { … }` is the same shape as a creation with a trailing initializer,
		// and dotnet format treats it the same way: once the call has opened out, the `{` takes its own
		// line. So the initializer aims at the call's argument list, not at the source.
		context.ArgumentListGroup = 0;
		Node.Print(node.Expression, context);
		var ownerGroup = context.ArgumentListGroup;

		context.Arena.Synthetic(SyntheticText.Space);
		TokenPrinter.Print(node.WithKeyword, context);
		InitializerExpression(node.Initializer, context, leadingLine: true, ownerGroup: ownerGroup);
	}

	public static void ThrowExpression(ThrowExpressionSyntax node, PrintContext context)
	{
		TokenPrinter.Print(node.ThrowKeyword, context);
		context.Arena.Synthetic(SyntheticText.Space);
		Node.Print(node.Expression, context);
	}

	public static void TypeOfExpression(TypeOfExpressionSyntax node, PrintContext context)
	{
		TokenPrinter.Print(node.Keyword, context);
		TokenPrinter.Print(node.OpenParenToken, context);
		Node.Print(node.Type, context);
		TokenPrinter.Print(node.CloseParenToken, context);
	}

	public static void SimpleBaseType(SimpleBaseTypeSyntax node, PrintContext context) =>
		Node.Print(node.Type, context);

	public static void PrimaryConstructorBaseType(PrimaryConstructorBaseTypeSyntax node, PrintContext context)
	{
		Node.Print(node.Type, context);
		Node.Print(node.ArgumentList, context);
	}

	public static void ImplicitElementAccess(ImplicitElementAccessSyntax node, PrintContext context) =>
		Node.Print(node.ArgumentList, context);

	public static void RangeExpression(RangeExpressionSyntax node, PrintContext context)
	{
		Node.Print(node.LeftOperand, context);
		TokenPrinter.Print(node.OperatorToken, context);
		Node.Print(node.RightOperand, context);
	}

	public static void SpreadElement(SpreadElementSyntax node, PrintContext context)
	{
		TokenPrinter.Print(node.OperatorToken, context);
		// Roslyn spaces a spread: `.. items`. A range expression does not, and prints elsewhere.
		context.Arena.Synthetic(SyntheticText.Space);
		Node.Print(node.Expression, context);
	}

	public static void ExpressionElement(ExpressionElementSyntax node, PrintContext context) =>
		Node.Print(node.Expression, context);

	public static void ArrayCreationExpression(ArrayCreationExpressionSyntax node, PrintContext context)
	{
		TokenPrinter.Print(node.NewKeyword, context);
		context.Arena.Synthetic(SyntheticText.Space);
		Node.Print(node.Type, context);

		if (node.Initializer is null)
			return;

		InitializerExpression(node.Initializer, context, leadingLine: true);
	}

	public static void ImplicitArrayCreationExpression(ImplicitArrayCreationExpressionSyntax node, PrintContext context)
	{
		TokenPrinter.Print(node.NewKeyword, context);
		Spacing.BeforeOpenBracket(context);
		TokenPrinter.Print(node.OpenBracketToken, context);
		Spacing.InsideEmptyBrackets(context);
		foreach (var comma in node.Commas)
		{
			TokenPrinter.Print(comma, context);
			Spacing.InsideEmptyBrackets(context);
		}

		TokenPrinter.Print(node.CloseBracketToken, context);
		InitializerExpression(node.Initializer, context, leadingLine: true);
	}

	public static void ElementBindingExpression(ElementBindingExpressionSyntax node, PrintContext context) =>
		Node.Print(node.ArgumentList, context);

	public static void VarPattern(VarPatternSyntax node, PrintContext context)
	{
		TokenPrinter.Print(node.VarKeyword, context);
		context.Arena.Synthetic(SyntheticText.Space);
		Node.Print(node.Designation, context);
	}

	public static void DefaultExpression(DefaultExpressionSyntax node, PrintContext context)
	{
		TokenPrinter.Print(node.Keyword, context);
		TokenPrinter.Print(node.OpenParenToken, context);
		Node.Print(node.Type, context);
		TokenPrinter.Print(node.CloseParenToken, context);
	}

	public static void StackAllocArrayCreationExpression(StackAllocArrayCreationExpressionSyntax node, PrintContext context)
	{
		TokenPrinter.Print(node.StackAllocKeyword, context);
		context.Arena.Synthetic(SyntheticText.Space);
		Node.Print(node.Type, context);

		if (node.Initializer is null)
			return;

		InitializerExpression(node.Initializer, context, leadingLine: true);
	}

	public static void SlicePattern(SlicePatternSyntax node, PrintContext context)
	{
		TokenPrinter.Print(node.DotDotToken, context);

		if (node.Pattern is null)
			return;

		// Spaced like a spread element in a collection expression: `[1, 2, .. var rest]`.
		context.Arena.Synthetic(SyntheticText.Space);
		Node.Print(node.Pattern, context);
	}

	/// <summary>A `with` element in a collection expression.</summary>
	public static void WithElement(SyntaxNode node, PrintContext context) => Tokens(node, context);

	/// <summary>The C# 14 <c>field</c> contextual keyword. A single token.</summary>
	public static void FieldExpression(FieldExpressionSyntax node, PrintContext context) =>
		TokenPrinter.Print(node.Token, context);
}
