using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Nullean.Kerf.Documents;

namespace Nullean.Kerf.Printing.CSharp;

/// <summary>Patterns, designations, tuples and the smaller expression forms.</summary>
internal static partial class Printers
{
	public static void IsPatternExpression(IsPatternExpressionSyntax node, PrintContext context)
	{
		Node.Print(node.Expression, context);
		context.Arena.Synthetic(SyntheticText.Space);
		TokenPrinter.Print(node.IsKeyword, context);
		context.Arena.Synthetic(SyntheticText.Space);
		Node.Print(node.Pattern, context);
	}

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
			var positional = node.PositionalPatternClause;
			TokenPrinter.Print(positional.OpenParenToken, context);
			for (var i = 0; i < positional.Subpatterns.Count; i++)
			{
				Node.Print(positional.Subpatterns[i], context);
				if (i >= positional.Subpatterns.SeparatorCount)
					continue;
				TokenPrinter.Print(positional.Subpatterns.GetSeparator(i), context);
				arena.Synthetic(SyntheticText.Space);
			}
			TokenPrinter.Print(positional.CloseParenToken, context);
		}

		if (node.PropertyPatternClause is not null)
		{
			var property = node.PropertyPatternClause;
			if (node.Type is not null || node.PositionalPatternClause is not null)
				arena.Synthetic(SyntheticText.Space);

			TokenPrinter.Print(property.OpenBraceToken, context);
			if (property.Subpatterns.Count > 0)
			{
				using (arena.Group())
				{
					using (arena.Indent())
					{
						arena.Line();
						for (var i = 0; i < property.Subpatterns.Count; i++)
						{
							Node.Print(property.Subpatterns[i], context);
							if (i >= property.Subpatterns.SeparatorCount)
								continue;
							TokenPrinter.Print(property.Subpatterns.GetSeparator(i), context);
							arena.Line();
						}
					}
					arena.Line();
				}
			}
			else
			{
				arena.Synthetic(SyntheticText.Space);
			}
			TokenPrinter.Print(property.CloseBraceToken, context);
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
			context.Arena.Synthetic(SyntheticText.Space);
		}

		Node.Print(node.Pattern, context);
	}

	/// <summary>Covers <c>and</c>, <c>or</c> and relational patterns, all of which need spacing.</summary>
	public static void BinaryPattern(BinaryPatternSyntax node, PrintContext context)
	{
		Node.Print(node.Left, context);
		context.Arena.Synthetic(SyntheticText.Space);
		TokenPrinter.Print(node.OperatorToken, context);
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
		Spacing.InsideBrackets(context);

		for (var i = 0; i < node.Patterns.Count; i++)
		{
			Node.Print(node.Patterns[i], context);
			if (i >= node.Patterns.SeparatorCount)
				continue;
			Spacing.BeforeComma(context);
			TokenPrinter.Print(node.Patterns.GetSeparator(i), context);
			Spacing.AfterComma(context);
		}

		Spacing.InsideBrackets(context);
		TokenPrinter.Print(node.CloseBracketToken, context);

		if (node.Designation is null)
			return;

		arena.Synthetic(SyntheticText.Space);
		Node.Print(node.Designation, context);
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

			if (asWritten && !context.OnSameLine(node.Arguments[i].Span.End, node.Arguments[i + 1].SpanStart))
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
		Node.Print(node.Expression, context);
		context.Arena.Synthetic(SyntheticText.Space);
		TokenPrinter.Print(node.WithKeyword, context);
		InitializerExpression(node.Initializer, context, leadingLine: true);
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
