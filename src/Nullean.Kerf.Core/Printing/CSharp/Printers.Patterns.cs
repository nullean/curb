using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Nullean.Kerf.Documents;

namespace Nullean.Kerf.Printing.CSharp;

/// <summary>Printers for patterns, tuples and the remaining member forms.</summary>
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

		for (var i = 0; i < node.Patterns.Count; i++)
		{
			Node.Print(node.Patterns[i], context);
			if (i >= node.Patterns.SeparatorCount)
				continue;
			TokenPrinter.Print(node.Patterns.GetSeparator(i), context);
			arena.Synthetic(SyntheticText.Space);
		}

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
		var arena = context.Arena;
		TokenPrinter.Print(node.OpenParenToken, context);

		for (var i = 0; i < node.Variables.Count; i++)
		{
			Node.Print(node.Variables[i], context);
			if (i >= node.Variables.SeparatorCount)
				continue;
			TokenPrinter.Print(node.Variables.GetSeparator(i), context);
			arena.Synthetic(SyntheticText.Space);
		}

		TokenPrinter.Print(node.CloseParenToken, context);
	}

	/// <summary>A tuple expression, and the same shape for a tuple type.</summary>
	public static void TupleExpression(TupleExpressionSyntax node, PrintContext context)
	{
		var arena = context.Arena;
		TokenPrinter.Print(node.OpenParenToken, context);

		for (var i = 0; i < node.Arguments.Count; i++)
		{
			Node.Print(node.Arguments[i], context);
			if (i >= node.Arguments.SeparatorCount)
				continue;
			TokenPrinter.Print(node.Arguments.GetSeparator(i), context);
			arena.Synthetic(SyntheticText.Space);
		}

		TokenPrinter.Print(node.CloseParenToken, context);
	}

	public static void TupleType(TupleTypeSyntax node, PrintContext context)
	{
		var arena = context.Arena;
		TokenPrinter.Print(node.OpenParenToken, context);

		for (var i = 0; i < node.Elements.Count; i++)
		{
			Node.Print(node.Elements[i], context);
			if (i >= node.Elements.SeparatorCount)
				continue;
			TokenPrinter.Print(node.Elements.GetSeparator(i), context);
			arena.Synthetic(SyntheticText.Space);
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
		TokenPrinter.Print(node.OpenBracketToken, context);
		foreach (var comma in node.Commas)
			TokenPrinter.Print(comma, context);
		TokenPrinter.Print(node.CloseBracketToken, context);
		InitializerExpression(node.Initializer, context, leadingLine: true);
	}

	public static void EnumDeclaration(EnumDeclarationSyntax node, PrintContext context)
	{
		var arena = context.Arena;

		PrintAttributeLists(node.AttributeLists, context);
		PrintModifiers(node.Modifiers, context);
		TokenPrinter.Print(node.EnumKeyword, context);
		arena.Synthetic(SyntheticText.Space);
		TokenPrinter.Print(node.Identifier, context);

		if (node.BaseList is not null)
		{
			arena.Synthetic(SyntheticText.Space);
			Node.Print(node.BaseList, context);
		}

		arena.HardLine();
		TokenPrinter.Print(node.OpenBraceToken, context);

		using (arena.Indent())
		{
			var previousEnd = node.OpenBraceToken.Span.End;
			for (var i = 0; i < node.Members.Count; i++)
			{
				arena.HardLine(DocFlags.OnlyIfNotAtLineStart);
				if (context.BlankLinesBetween(previousEnd, node.Members[i].SpanStart) > 0)
					arena.HardLine();

				Node.Print(node.Members[i], context);
				if (i < node.Members.SeparatorCount)
					TokenPrinter.Print(node.Members.GetSeparator(i), context);
				previousEnd = node.Members[i].Span.End;
			}
		}

		arena.HardLine();
		TokenPrinter.Print(node.CloseBraceToken, context);
		TokenPrinter.PrintIfPresent(node.SemicolonToken, context);
	}

	public static void EnumMemberDeclaration(EnumMemberDeclarationSyntax node, PrintContext context)
	{
		PrintAttributeLists(node.AttributeLists, context);
		TokenPrinter.Print(node.Identifier, context);
		Node.Print(node.EqualsValue, context);
	}

	public static void OperatorDeclaration(OperatorDeclarationSyntax node, PrintContext context)
	{
		var arena = context.Arena;

		PrintAttributeLists(node.AttributeLists, context);
		PrintModifiers(node.Modifiers, context);
		Node.Print(node.ReturnType, context);
		arena.Synthetic(SyntheticText.Space);
		TokenPrinter.Print(node.OperatorKeyword, context);
		arena.Synthetic(SyntheticText.Space);
		TokenPrinter.Print(node.OperatorToken, context);
		Node.Print(node.ParameterList, context);

		if (node.Body is not null)
		{
			arena.HardLine();
			Node.Print(node.Body, context);
			return;
		}

		if (node.ExpressionBody is not null)
		{
			arena.Synthetic(SyntheticText.Space);
			Node.Print(node.ExpressionBody, context);
		}

		TokenPrinter.PrintIfPresent(node.SemicolonToken, context);
	}

	public static void ConversionOperatorDeclaration(ConversionOperatorDeclarationSyntax node, PrintContext context)
	{
		var arena = context.Arena;

		PrintAttributeLists(node.AttributeLists, context);
		PrintModifiers(node.Modifiers, context);
		TokenPrinter.Print(node.ImplicitOrExplicitKeyword, context);
		arena.Synthetic(SyntheticText.Space);
		TokenPrinter.Print(node.OperatorKeyword, context);
		arena.Synthetic(SyntheticText.Space);
		Node.Print(node.Type, context);
		Node.Print(node.ParameterList, context);

		if (node.Body is not null)
		{
			arena.HardLine();
			Node.Print(node.Body, context);
			return;
		}

		if (node.ExpressionBody is not null)
		{
			arena.Synthetic(SyntheticText.Space);
			Node.Print(node.ExpressionBody, context);
		}

		TokenPrinter.PrintIfPresent(node.SemicolonToken, context);
	}

	public static void IndexerDeclaration(IndexerDeclarationSyntax node, PrintContext context)
	{
		var arena = context.Arena;

		PrintAttributeLists(node.AttributeLists, context);
		PrintModifiers(node.Modifiers, context);
		Node.Print(node.Type, context);
		arena.Synthetic(SyntheticText.Space);

		if (node.ExplicitInterfaceSpecifier is not null)
			Tokens(node.ExplicitInterfaceSpecifier, context);

		TokenPrinter.Print(node.ThisKeyword, context);
		Node.Print(node.ParameterList, context);

		Node.Print(node.AccessorList, context);

		if (node.ExpressionBody is not null)
		{
			arena.Synthetic(SyntheticText.Space);
			Node.Print(node.ExpressionBody, context);
		}

		TokenPrinter.PrintIfPresent(node.SemicolonToken, context);
	}

	public static void BracketedParameterList(BracketedParameterListSyntax node, PrintContext context)
	{
		var arena = context.Arena;
		TokenPrinter.Print(node.OpenBracketToken, context);

		for (var i = 0; i < node.Parameters.Count; i++)
		{
			Node.Print(node.Parameters[i], context);
			if (i >= node.Parameters.SeparatorCount)
				continue;
			TokenPrinter.Print(node.Parameters.GetSeparator(i), context);
			arena.Synthetic(SyntheticText.Space);
		}

		TokenPrinter.Print(node.CloseBracketToken, context);
	}

	public static void NamespaceDeclaration(NamespaceDeclarationSyntax node, PrintContext context)
	{
		var arena = context.Arena;

		TokenPrinter.Print(node.NamespaceKeyword, context);
		arena.Synthetic(SyntheticText.Space);
		Node.Print(node.Name, context);
		arena.HardLine();
		TokenPrinter.Print(node.OpenBraceToken, context);

		using (arena.Indent())
		{
			var previousEnd = node.OpenBraceToken.Span.End;

			foreach (var directive in node.Usings)
			{
				arena.HardLine(DocFlags.OnlyIfNotAtLineStart);
				if (context.BlankLinesBetween(previousEnd, directive.SpanStart) > 0)
					arena.HardLine();
				Node.Print(directive, context);
				previousEnd = directive.Span.End;
			}

			foreach (var member in node.Members)
			{
				arena.HardLine(DocFlags.OnlyIfNotAtLineStart);
				if (context.BlankLinesBetween(previousEnd, member.SpanStart) > 0)
					arena.HardLine();
				Node.Print(member, context);
				previousEnd = member.Span.End;
			}
		}

		arena.HardLine();
		TokenPrinter.Print(node.CloseBraceToken, context);
		TokenPrinter.PrintIfPresent(node.SemicolonToken, context);
	}

	public static void DelegateDeclaration(DelegateDeclarationSyntax node, PrintContext context)
	{
		var arena = context.Arena;

		PrintAttributeLists(node.AttributeLists, context);
		PrintModifiers(node.Modifiers, context);
		TokenPrinter.Print(node.DelegateKeyword, context);
		arena.Synthetic(SyntheticText.Space);
		Node.Print(node.ReturnType, context);
		arena.Synthetic(SyntheticText.Space);
		TokenPrinter.Print(node.Identifier, context);

		if (node.TypeParameterList is not null)
			Node.Print(node.TypeParameterList, context);

		Node.Print(node.ParameterList, context);

		foreach (var constraint in node.ConstraintClauses)
		{
			arena.Synthetic(SyntheticText.Space);
			Node.Print(constraint, context);
		}

		TokenPrinter.Print(node.SemicolonToken, context);
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
		Node.Print(node.Pattern, context);
	}

	/// <summary>A `with` element in a collection expression.</summary>
	public static void WithElement(SyntaxNode node, PrintContext context) => Tokens(node, context);

	/// <summary>LINQ query syntax: one clause per line once it breaks.</summary>
	/// <remarks>
	/// The separator here is what <c>csharp_new_line_between_query_expression_clauses</c> will
	/// govern; its default is true, which is a hard line between clauses.
	/// </remarks>
	public static void QueryExpression(QueryExpressionSyntax node, PrintContext context)
	{
		var arena = context.Arena;

		QueryFromClause(node.FromClause, context);

		using (arena.Indent())
		{
			foreach (var clause in node.Body.Clauses)
			{
				arena.HardLine();
				Node.Print(clause, context);
			}

			arena.HardLine();
			Node.Print(node.Body.SelectOrGroup, context);

			if (node.Body.Continuation is null)
				return;

			arena.HardLine();
			TokenPrinter.Print(node.Body.Continuation.IntoKeyword, context);
			arena.Synthetic(SyntheticText.Space);
			TokenPrinter.Print(node.Body.Continuation.Identifier, context);
		}
	}

	private static void QueryFromClause(FromClauseSyntax node, PrintContext context)
	{
		var arena = context.Arena;

		TokenPrinter.Print(node.FromKeyword, context);
		arena.Synthetic(SyntheticText.Space);

		if (node.Type is not null)
		{
			Node.Print(node.Type, context);
			arena.Synthetic(SyntheticText.Space);
		}

		TokenPrinter.Print(node.Identifier, context);
		arena.Synthetic(SyntheticText.Space);
		TokenPrinter.Print(node.InKeyword, context);
		arena.Synthetic(SyntheticText.Space);
		Node.Print(node.Expression, context);
	}

	public static void FromClause(FromClauseSyntax node, PrintContext context) => QueryFromClause(node, context);

	public static void WhereClause(WhereClauseSyntax node, PrintContext context)
	{
		TokenPrinter.Print(node.WhereKeyword, context);
		context.Arena.Synthetic(SyntheticText.Space);
		Node.Print(node.Condition, context);
	}

	public static void SelectClause(SelectClauseSyntax node, PrintContext context)
	{
		TokenPrinter.Print(node.SelectKeyword, context);
		context.Arena.Synthetic(SyntheticText.Space);
		Node.Print(node.Expression, context);
	}

	public static void GroupClause(GroupClauseSyntax node, PrintContext context)
	{
		var arena = context.Arena;
		TokenPrinter.Print(node.GroupKeyword, context);
		arena.Synthetic(SyntheticText.Space);
		Node.Print(node.GroupExpression, context);
		arena.Synthetic(SyntheticText.Space);
		TokenPrinter.Print(node.ByKeyword, context);
		arena.Synthetic(SyntheticText.Space);
		Node.Print(node.ByExpression, context);
	}

	public static void LetClause(LetClauseSyntax node, PrintContext context)
	{
		var arena = context.Arena;
		TokenPrinter.Print(node.LetKeyword, context);
		arena.Synthetic(SyntheticText.Space);
		TokenPrinter.Print(node.Identifier, context);
		arena.Synthetic(SyntheticText.Space);
		TokenPrinter.Print(node.EqualsToken, context);
		arena.Synthetic(SyntheticText.Space);
		Node.Print(node.Expression, context);
	}

	public static void OrderByClause(OrderByClauseSyntax node, PrintContext context)
	{
		var arena = context.Arena;
		TokenPrinter.Print(node.OrderByKeyword, context);
		arena.Synthetic(SyntheticText.Space);

		for (var i = 0; i < node.Orderings.Count; i++)
		{
			Node.Print(node.Orderings[i], context);
			if (i >= node.Orderings.SeparatorCount)
				continue;
			TokenPrinter.Print(node.Orderings.GetSeparator(i), context);
			arena.Synthetic(SyntheticText.Space);
		}
	}

	public static void Ordering(OrderingSyntax node, PrintContext context)
	{
		Node.Print(node.Expression, context);
		if (node.AscendingOrDescendingKeyword.RawKind == 0)
			return;

		context.Arena.Synthetic(SyntheticText.Space);
		TokenPrinter.Print(node.AscendingOrDescendingKeyword, context);
	}

	public static void JoinClause(JoinClauseSyntax node, PrintContext context)
	{
		var arena = context.Arena;

		TokenPrinter.Print(node.JoinKeyword, context);
		arena.Synthetic(SyntheticText.Space);

		if (node.Type is not null)
		{
			Node.Print(node.Type, context);
			arena.Synthetic(SyntheticText.Space);
		}

		TokenPrinter.Print(node.Identifier, context);
		arena.Synthetic(SyntheticText.Space);
		TokenPrinter.Print(node.InKeyword, context);
		arena.Synthetic(SyntheticText.Space);
		Node.Print(node.InExpression, context);
		arena.Synthetic(SyntheticText.Space);
		TokenPrinter.Print(node.OnKeyword, context);
		arena.Synthetic(SyntheticText.Space);
		Node.Print(node.LeftExpression, context);
		arena.Synthetic(SyntheticText.Space);
		TokenPrinter.Print(node.EqualsKeyword, context);
		arena.Synthetic(SyntheticText.Space);
		Node.Print(node.RightExpression, context);

		if (node.Into is null)
			return;

		arena.Synthetic(SyntheticText.Space);
		TokenPrinter.Print(node.Into.IntoKeyword, context);
		arena.Synthetic(SyntheticText.Space);
		TokenPrinter.Print(node.Into.Identifier, context);
	}

	/// <summary>The C# 14 <c>field</c> contextual keyword. A single token.</summary>
	public static void FieldExpression(FieldExpressionSyntax node, PrintContext context) =>
		TokenPrinter.Print(node.Token, context);
}
