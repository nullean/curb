using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Nullean.Kerf.Documents;

namespace Nullean.Kerf.Printing.CSharp;

/// <summary>Printers for expressions and types.</summary>
internal static partial class Printers
{
	/// <summary>
	/// Emits a node exactly as written, but counts it as printed rather than as a coverage gap.
	/// </summary>
	/// <remarks>
	/// For constructs whose internal layout is <i>content</i>, not formatting — interpolated and raw
	/// string literals above all. Reproducing them verbatim is the correct output, not a fallback.
	/// </remarks>
	private static void VerbatimContent(SyntaxNode node, PrintContext context)
	{
		// node.Span excludes the trivia attached to the first and last tokens, so the comment above
		// an interpolated string would be dropped without printing it around the verbatim body.
		var first = node.GetFirstToken();
		var last = node.GetLastToken();

		if (first.RawKind != 0)
			TokenPrinter.PrintLeadingTrivia(first, context);

		var span = node.Span;
		TokenPrinter.EmitVerbatimRange(context, span.Start, span.Length);

		if (last.RawKind != 0)
			TokenPrinter.PrintTrailingTrivia(last, context);

		foreach (var _ in node.DescendantTokens())
			context.PrintedTokens++;
	}

	/// <summary>Right-hand side of an operator: breaks and indents when it will not fit.</summary>
	private static void OperandOnRight(SyntaxNode? right, PrintContext context)
	{
		var arena = context.Arena;
		using (arena.Group())
		using (arena.Indent())
		{
			arena.Line();
			Node.Print(right, context);
		}
	}

	public static void BinaryExpression(BinaryExpressionSyntax node, PrintContext context)
	{
		Node.Print(node.Left, context);
		// csharp_space_around_binary_operators, default before_and_after.
		context.Arena.Synthetic(SyntheticText.Space);
		TokenPrinter.Print(node.OperatorToken, context);
		OperandOnRight(node.Right, context);
	}

	public static void AssignmentExpression(AssignmentExpressionSyntax node, PrintContext context)
	{
		Node.Print(node.Left, context);
		context.Arena.Synthetic(SyntheticText.Space);
		TokenPrinter.Print(node.OperatorToken, context);
		OperandOnRight(node.Right, context);
	}

	public static void ConditionalExpression(ConditionalExpressionSyntax node, PrintContext context)
	{
		var arena = context.Arena;
		Node.Print(node.Condition, context);

		using (arena.Group())
		using (arena.Indent())
		{
			arena.Line();
			TokenPrinter.Print(node.QuestionToken, context);
			arena.Synthetic(SyntheticText.Space);
			Node.Print(node.WhenTrue, context);
			arena.Line();
			TokenPrinter.Print(node.ColonToken, context);
			arena.Synthetic(SyntheticText.Space);
			Node.Print(node.WhenFalse, context);
		}
	}

	public static void PrefixUnaryExpression(PrefixUnaryExpressionSyntax node, PrintContext context)
	{
		TokenPrinter.Print(node.OperatorToken, context);
		Node.Print(node.Operand, context);
	}

	public static void PostfixUnaryExpression(PostfixUnaryExpressionSyntax node, PrintContext context)
	{
		Node.Print(node.Operand, context);
		TokenPrinter.Print(node.OperatorToken, context);
	}

	public static void AwaitExpression(AwaitExpressionSyntax node, PrintContext context)
	{
		TokenPrinter.Print(node.AwaitKeyword, context);
		context.Arena.Synthetic(SyntheticText.Space);
		Node.Print(node.Expression, context);
	}

	public static void ParenthesizedExpression(ParenthesizedExpressionSyntax node, PrintContext context)
	{
		TokenPrinter.Print(node.OpenParenToken, context);
		Node.Print(node.Expression, context);
		TokenPrinter.Print(node.CloseParenToken, context);
	}

	public static void CastExpression(CastExpressionSyntax node, PrintContext context)
	{
		TokenPrinter.Print(node.OpenParenToken, context);
		Node.Print(node.Type, context);
		TokenPrinter.Print(node.CloseParenToken, context);
		// csharp_space_after_cast, default false.
		Node.Print(node.Expression, context);
	}

	public static void ObjectCreationExpression(ObjectCreationExpressionSyntax node, PrintContext context)
	{
		TokenPrinter.Print(node.NewKeyword, context);
		context.Arena.Synthetic(SyntheticText.Space);
		Node.Print(node.Type, context);

		if (node.ArgumentList is not null)
			Node.Print(node.ArgumentList, context);

		if (node.Initializer is null)
			return;

		context.Arena.Synthetic(SyntheticText.Space);
		Node.Print(node.Initializer, context);
	}

	public static void ImplicitObjectCreationExpression(ImplicitObjectCreationExpressionSyntax node, PrintContext context)
	{
		TokenPrinter.Print(node.NewKeyword, context);
		Node.Print(node.ArgumentList, context);

		if (node.Initializer is null)
			return;

		context.Arena.Synthetic(SyntheticText.Space);
		Node.Print(node.Initializer, context);
	}

	/// <summary>Object, collection, array and <c>with</c> initialisers.</summary>
	public static void InitializerExpression(InitializerExpressionSyntax node, PrintContext context)
	{
		var arena = context.Arena;
		TokenPrinter.Print(node.OpenBraceToken, context);

		if (node.Expressions.Count > 0)
		{
			using (arena.Group())
			{
				using (arena.Indent())
				{
					arena.Line();
					for (var i = 0; i < node.Expressions.Count; i++)
					{
						Node.Print(node.Expressions[i], context);
						if (i >= node.Expressions.SeparatorCount)
							continue;
						TokenPrinter.Print(node.Expressions.GetSeparator(i), context);
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

		TokenPrinter.Print(node.CloseBraceToken, context);
	}

	public static void CollectionExpression(CollectionExpressionSyntax node, PrintContext context)
	{
		var arena = context.Arena;
		TokenPrinter.Print(node.OpenBracketToken, context);

		if (node.Elements.Count > 0)
		{
			using (arena.Group())
			{
				using (arena.Indent())
				{
					arena.SoftLine();
					for (var i = 0; i < node.Elements.Count; i++)
					{
						Node.Print(node.Elements[i], context);
						if (i >= node.Elements.SeparatorCount)
							continue;
						TokenPrinter.Print(node.Elements.GetSeparator(i), context);
						arena.Line();
					}
				}
				arena.SoftLine();
			}
		}

		TokenPrinter.Print(node.CloseBracketToken, context);
	}

	public static void SimpleLambdaExpression(SimpleLambdaExpressionSyntax node, PrintContext context)
	{
		PrintModifiers(node.Modifiers, context);
		Node.Print(node.Parameter, context);
		LambdaBody(node.ArrowToken, node.Block, node.ExpressionBody, context);
	}

	public static void ParenthesizedLambdaExpression(ParenthesizedLambdaExpressionSyntax node, PrintContext context)
	{
		PrintAttributeLists(node.AttributeLists, context);
		PrintModifiers(node.Modifiers, context);

		if (node.ReturnType is not null)
		{
			Node.Print(node.ReturnType, context);
			context.Arena.Synthetic(SyntheticText.Space);
		}

		Node.Print(node.ParameterList, context);
		LambdaBody(node.ArrowToken, node.Block, node.ExpressionBody, context);
	}

	private static void LambdaBody(SyntaxToken arrow, BlockSyntax? block, ExpressionSyntax? expression, PrintContext context)
	{
		var arena = context.Arena;

		arena.Synthetic(SyntheticText.Space);
		TokenPrinter.Print(arrow, context);

		if (block is not null)
		{
			arena.HardLine();
			Node.Print(block, context);
			return;
		}

		using (arena.Group())
		using (arena.Indent())
		{
			arena.Line();
			Node.Print(expression, context);
		}
	}

	public static void AnonymousObjectCreationExpression(AnonymousObjectCreationExpressionSyntax node, PrintContext context)
	{
		var arena = context.Arena;

		TokenPrinter.Print(node.NewKeyword, context);
		arena.Synthetic(SyntheticText.Space);
		TokenPrinter.Print(node.OpenBraceToken, context);

		using (arena.Group())
		{
			using (arena.Indent())
			{
				arena.Line();
				for (var i = 0; i < node.Initializers.Count; i++)
				{
					Node.Print(node.Initializers[i], context);
					if (i >= node.Initializers.SeparatorCount)
						continue;
					TokenPrinter.Print(node.Initializers.GetSeparator(i), context);
					arena.Line();
				}
			}
			arena.Line();
		}

		TokenPrinter.Print(node.CloseBraceToken, context);
	}

	public static void AnonymousObjectMemberDeclarator(AnonymousObjectMemberDeclaratorSyntax node, PrintContext context)
	{
		if (node.NameEquals is not null)
		{
			TokenPrinter.Print(node.NameEquals.Name.Identifier, context);
			context.Arena.Synthetic(SyntheticText.Space);
			TokenPrinter.Print(node.NameEquals.EqualsToken, context);
			context.Arena.Synthetic(SyntheticText.Space);
		}

		Node.Print(node.Expression, context);
	}

	public static void ElementAccessExpression(ElementAccessExpressionSyntax node, PrintContext context)
	{
		Node.Print(node.Expression, context);
		Node.Print(node.ArgumentList, context);
	}

	public static void BracketedArgumentList(BracketedArgumentListSyntax node, PrintContext context)
	{
		TokenPrinter.Print(node.OpenBracketToken, context);

		for (var i = 0; i < node.Arguments.Count; i++)
		{
			Node.Print(node.Arguments[i], context);
			if (i >= node.Arguments.SeparatorCount)
				continue;
			TokenPrinter.Print(node.Arguments.GetSeparator(i), context);
			context.Arena.Synthetic(SyntheticText.Space);
		}

		TokenPrinter.Print(node.CloseBracketToken, context);
	}

	public static void ConditionalAccessExpression(ConditionalAccessExpressionSyntax node, PrintContext context)
	{
		Node.Print(node.Expression, context);
		TokenPrinter.Print(node.OperatorToken, context);
		Node.Print(node.WhenNotNull, context);
	}

	public static void SwitchExpression(SwitchExpressionSyntax node, PrintContext context)
	{
		var arena = context.Arena;

		Node.Print(node.GoverningExpression, context);
		arena.Synthetic(SyntheticText.Space);
		TokenPrinter.Print(node.SwitchKeyword, context);
		arena.HardLine();
		TokenPrinter.Print(node.OpenBraceToken, context);

		using (arena.Indent())
		{
			for (var i = 0; i < node.Arms.Count; i++)
			{
				arena.HardLine();
				Node.Print(node.Arms[i], context);
				if (i < node.Arms.SeparatorCount)
					TokenPrinter.Print(node.Arms.GetSeparator(i), context);
			}
		}

		arena.HardLine();
		TokenPrinter.Print(node.CloseBraceToken, context);
	}

	public static void SwitchExpressionArm(SwitchExpressionArmSyntax node, PrintContext context)
	{
		var arena = context.Arena;

		Node.Print(node.Pattern, context);

		if (node.WhenClause is not null)
		{
			arena.Synthetic(SyntheticText.Space);
			TokenPrinter.Print(node.WhenClause.WhenKeyword, context);
			arena.Synthetic(SyntheticText.Space);
			Node.Print(node.WhenClause.Condition, context);
		}

		arena.Synthetic(SyntheticText.Space);
		TokenPrinter.Print(node.EqualsGreaterThanToken, context);

		using (arena.Group())
		using (arena.Indent())
		{
			arena.Line();
			Node.Print(node.Expression, context);
		}
	}

	public static void GenericName(GenericNameSyntax node, PrintContext context)
	{
		TokenPrinter.Print(node.Identifier, context);
		Node.Print(node.TypeArgumentList, context);
	}

	public static void TypeArgumentList(TypeArgumentListSyntax node, PrintContext context)
	{
		TokenPrinter.Print(node.LessThanToken, context);

		for (var i = 0; i < node.Arguments.Count; i++)
		{
			Node.Print(node.Arguments[i], context);
			if (i >= node.Arguments.SeparatorCount)
				continue;
			TokenPrinter.Print(node.Arguments.GetSeparator(i), context);
			context.Arena.Synthetic(SyntheticText.Space);
		}

		TokenPrinter.Print(node.GreaterThanToken, context);
	}

	public static void NullableType(NullableTypeSyntax node, PrintContext context)
	{
		Node.Print(node.ElementType, context);
		TokenPrinter.Print(node.QuestionToken, context);
	}

	public static void ArrayType(ArrayTypeSyntax node, PrintContext context)
	{
		Node.Print(node.ElementType, context);
		foreach (var rank in node.RankSpecifiers)
			Tokens(rank, context);
	}

	public static void BaseList(BaseListSyntax node, PrintContext context)
	{
		var arena = context.Arena;

		// csharp_space_before_colon_in_inheritance_clause / _after_, both default true. The space
		// before is emitted by the caller, which knows what preceded the colon.
		TokenPrinter.Print(node.ColonToken, context);
		arena.Synthetic(SyntheticText.Space);

		for (var i = 0; i < node.Types.Count; i++)
		{
			Node.Print(node.Types[i], context);
			if (i >= node.Types.SeparatorCount)
				continue;
			TokenPrinter.Print(node.Types.GetSeparator(i), context);
			arena.Synthetic(SyntheticText.Space);
		}
	}

	public static void TypeParameterConstraintClause(TypeParameterConstraintClauseSyntax node, PrintContext context)
	{
		var arena = context.Arena;

		TokenPrinter.Print(node.WhereKeyword, context);
		arena.Synthetic(SyntheticText.Space);
		Tokens(node.Name, context);
		arena.Synthetic(SyntheticText.Space);
		TokenPrinter.Print(node.ColonToken, context);
		arena.Synthetic(SyntheticText.Space);

		for (var i = 0; i < node.Constraints.Count; i++)
		{
			Node.Print(node.Constraints[i], context);
			if (i >= node.Constraints.SeparatorCount)
				continue;
			TokenPrinter.Print(node.Constraints.GetSeparator(i), context);
			arena.Synthetic(SyntheticText.Space);
		}
	}

	/// <summary>An interpolated or raw string literal: its interior is content, not layout.</summary>
	public static void VerbatimExpression(SyntaxNode node, PrintContext context) => VerbatimContent(node, context);
}
