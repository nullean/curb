using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Nullean.Kerf.Documents;
using Nullean.Kerf.Options;

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
	/// <remarks>
	/// A value that brings its own braces positions its own contents, so it takes a plain space
	/// rather than a hanging indent — otherwise the whole construct sits one level too deep.
	/// </remarks>
	private static void OperandOnRight(SyntaxNode? right, PrintContext context)
	{
		var arena = context.Arena;

		if (right is ExpressionSyntax expression && BringsOwnBlock(expression))
		{
			Spacing.BeforeOperator(context);
			Node.Print(right, context);
			return;
		}

		using (arena.Group())
		using (arena.Indent())
		{
			// A Line is a space when flat; under `none` the break must not bring one back.
			if (context.Options.SpaceAroundBinaryOperators == BinaryOperatorSpacing.BeforeAndAfter)
				arena.Line();
			else
				arena.SoftLine();
			Node.Print(right, context);
		}
	}

	public static void BinaryExpression(BinaryExpressionSyntax node, PrintContext context)
	{
		if (context.Options.SpaceAroundBinaryOperators == BinaryOperatorSpacing.Ignore)
		{
			PrintVerbatim(node, context);
			return;
		}

		Node.Print(node.Left, context);
		Spacing.BeforeOperator(context);
		TokenPrinter.Print(node.OperatorToken, context);
		OperandOnRight(node.Right, context);
	}

	public static void AssignmentExpression(AssignmentExpressionSyntax node, PrintContext context)
	{
		// dotnet format applies csharp_space_around_binary_operators to assignment too — `y=y+1`,
		// though the `=` of a declarator keeps its spaces, since that is not an operator.
		if (context.Options.SpaceAroundBinaryOperators == BinaryOperatorSpacing.Ignore)
		{
			PrintVerbatim(node, context);
			return;
		}

		Node.Print(node.Left, context);
		Spacing.BeforeOperator(context);
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
		Spacing.InsideExpressionParens(context);
		Node.Print(node.Expression, context);
		Spacing.InsideExpressionParens(context);
		TokenPrinter.Print(node.CloseParenToken, context);
	}

	public static void CastExpression(CastExpressionSyntax node, PrintContext context)
	{
		TokenPrinter.Print(node.OpenParenToken, context);
		Spacing.InsideCastParens(context);
		Node.Print(node.Type, context);
		Spacing.InsideCastParens(context);
		TokenPrinter.Print(node.CloseParenToken, context);
		Spacing.AfterCast(context);
		Node.Print(node.Expression, context);
	}

	public static void ObjectCreationExpression(ObjectCreationExpressionSyntax node, PrintContext context)
	{
		TokenPrinter.Print(node.NewKeyword, context);
		context.Arena.Synthetic(SyntheticText.Space);
		Node.Print(node.Type, context);

		if (node.ArgumentList is not null)
		{
			Spacing.BeforeCallParens(context);
			Node.Print(node.ArgumentList, context);
		}

		if (node.Initializer is null)
			return;

		InitializerExpression(node.Initializer, context, leadingLine: true);
	}

	public static void ImplicitObjectCreationExpression(ImplicitObjectCreationExpressionSyntax node, PrintContext context)
	{
		TokenPrinter.Print(node.NewKeyword, context);
		Spacing.BeforeCallParens(context);
		Node.Print(node.ArgumentList, context);

		if (node.Initializer is null)
			return;

		InitializerExpression(node.Initializer, context, leadingLine: true);
	}

	/// <summary>Object, collection, array and <c>with</c> initialisers.</summary>
	/// <param name="node">The initializer to print.</param>
	/// <param name="context">Per-file printing state.</param>
	/// <param name="leadingLine">
	/// Emit a break opportunity before the opening brace. True when the initializer follows a
	/// construct on the same line — <c>new C { … }</c> — so that the brace moves to its own line
	/// exactly when the contents break, which is what csharp_new_line_before_open_brace's
	/// object_collection_array_initializers flag describes. False for a nested initializer, whose
	/// parent has already emitted a separator.
	/// </param>
	public static void InitializerExpression(
		InitializerExpressionSyntax node,
		PrintContext context,
		bool leadingLine = false)
	{
		var arena = context.Arena;

		var oneMemberPerLine = context.Options.NewLineBeforeMembersInObjectInitializers;

		using (arena.Group())
		{
			if (leadingLine)
			{
				if (oneMemberPerLine)
					BeforeOpenBrace(BraceStyle.ObjectCollectionArrayInitializers, context);
				else
					BeforeOpenBraceWhenBroken(BraceStyle.ObjectCollectionArrayInitializers, context);
			}

			TokenPrinter.Print(node.OpenBraceToken, context);

			if (node.Expressions.Count > 0)
			{
				using (arena.Indent())
				{
					InsideBrace();
					for (var i = 0; i < node.Expressions.Count; i++)
					{
						Node.Print(node.Expressions[i], context);
						if (i >= node.Expressions.SeparatorCount)
							continue;

						Spacing.BeforeComma(context);
						TokenPrinter.Print(node.Expressions.GetSeparator(i), context);

						// A trailing separator is followed by the closing line, not by another one.
						if (i < node.Expressions.Count - 1)
							Separator();
					}
				}

				using (arena.IndentIf(context.Options.IndentBraces))
					InsideBrace();
			}
			else
			{
				// `{ }` — nothing to break around, so the brace pair just gets air between it.
				arena.Synthetic(SyntheticText.Space);
			}

			TokenPrinter.Print(node.CloseBraceToken, context);
		}

		// Between two members: a hard line, or the ordinary comma separator.
		void Separator()
		{
			if (oneMemberPerLine)
				arena.HardLine();
			else
				Spacing.AfterCommaBreakable(context);
		}

		// Just inside the braces, where no comma sits and the comma options do not reach.
		void InsideBrace()
		{
			if (oneMemberPerLine)
				arena.HardLine();
			else
				arena.Line();
		}
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
					Spacing.InsideBracketsBreakable(context);
					for (var i = 0; i < node.Elements.Count; i++)
					{
						Node.Print(node.Elements[i], context);
						if (i >= node.Elements.SeparatorCount)
							continue;

						Spacing.BeforeComma(context);
						TokenPrinter.Print(node.Elements.GetSeparator(i), context);

						// A trailing comma runs straight into the closing bracket, not `, ]`.
						if (i < node.Elements.Count - 1)
							Spacing.AfterCommaBreakable(context);
					}
				}

				Spacing.InsideBracketsBreakable(context);
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
			PrintBody(block, BraceStyle.Lambdas, context);
			return;
		}

		using (arena.Group())
		using (arena.Indent())
		{
			arena.Line();
			Node.Print(expression, context);
		}
	}

	/// <summary>The <c>delegate (T x) { }</c> form that predates lambdas.</summary>
	public static void AnonymousMethodExpression(AnonymousMethodExpressionSyntax node, PrintContext context)
	{
		var arena = context.Arena;

		foreach (var modifier in node.Modifiers)
		{
			TokenPrinter.Print(modifier, context);
			arena.Synthetic(SyntheticText.Space);
		}

		TokenPrinter.Print(node.DelegateKeyword, context);

		if (node.ParameterList is not null)
		{
			// `delegate (int x)`, with the space — dotnet format normalises to that shape, and the
			// delegate keyword is not a method name, so the method-paren spacing keys do not apply.
			arena.Synthetic(SyntheticText.Space);
			Node.Print(node.ParameterList, context);
		}

		PrintBody(node.Block, BraceStyle.AnonymousMethods, context);
	}

	public static void AnonymousObjectCreationExpression(AnonymousObjectCreationExpressionSyntax node, PrintContext context)
	{
		var arena = context.Arena;

		var oneMemberPerLine = context.Options.NewLineBeforeMembersInAnonymousTypes;

		TokenPrinter.Print(node.NewKeyword, context);

		using (arena.Group())
		{
			if (oneMemberPerLine)
				BeforeOpenBrace(BraceStyle.AnonymousTypes, context);
			else
				BeforeOpenBraceWhenBroken(BraceStyle.AnonymousTypes, context);

			TokenPrinter.Print(node.OpenBraceToken, context);

			using (arena.Indent())
			{
				InsideBrace();
				for (var i = 0; i < node.Initializers.Count; i++)
				{
					Node.Print(node.Initializers[i], context);
					if (i >= node.Initializers.SeparatorCount)
						continue;

					Spacing.BeforeComma(context);
					TokenPrinter.Print(node.Initializers.GetSeparator(i), context);
					if (i < node.Initializers.Count - 1)
						Separator();
				}
			}

			using (arena.IndentIf(context.Options.IndentBraces))
				InsideBrace();

			TokenPrinter.Print(node.CloseBraceToken, context);
		}

		void Separator()
		{
			if (oneMemberPerLine)
				arena.HardLine();
			else
				Spacing.AfterCommaBreakable(context);
		}

		void InsideBrace()
		{
			if (oneMemberPerLine)
				arena.HardLine();
			else
				arena.Line();
		}
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
		Spacing.BeforeOpenBracket(context);
		TokenPrinter.Print(node.OpenBracketToken, context);
		Spacing.InsideBrackets(context);

		for (var i = 0; i < node.Arguments.Count; i++)
		{
			Node.Print(node.Arguments[i], context);
			if (i >= node.Arguments.SeparatorCount)
				continue;
			Spacing.BeforeComma(context);
			TokenPrinter.Print(node.Arguments.GetSeparator(i), context);
			Spacing.AfterComma(context);
		}

		Spacing.InsideBrackets(context);
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

		using (arena.IndentIf(context.Options.IndentBraces))
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

		using (arena.IndentIf(context.Options.IndentBraces))
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
			Spacing.BeforeComma(context);
			TokenPrinter.Print(node.Arguments.GetSeparator(i), context);
			Spacing.AfterComma(context);
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
			ArrayRankSpecifier(rank, context);
	}

	public static void ArrayRankSpecifier(ArrayRankSpecifierSyntax node, PrintContext context)
	{
		Spacing.BeforeOpenBracket(context);
		TokenPrinter.Print(node.OpenBracketToken, context);

		// `int[]` and `int[,]` alike declare a rank without sizing it, so both count as empty
		// brackets: `int[ ]` and `int[ , ]`. Only `new int[2, 3]` holds expressions.
		var declaresRankOnly = node.Sizes.All(size => size.IsKind(SyntaxKind.OmittedArraySizeExpression));

		if (declaresRankOnly)
		{
			Spacing.InsideEmptyBrackets(context);
			for (var i = 0; i < node.Sizes.SeparatorCount; i++)
			{
				TokenPrinter.Print(node.Sizes.GetSeparator(i), context);
				Spacing.InsideEmptyBrackets(context);
			}

			TokenPrinter.Print(node.CloseBracketToken, context);
			return;
		}

		Spacing.InsideBrackets(context);
		for (var i = 0; i < node.Sizes.Count; i++)
		{
			Node.Print(node.Sizes[i], context);
			if (i >= node.Sizes.SeparatorCount)
				continue;
			Spacing.BeforeComma(context);
			TokenPrinter.Print(node.Sizes.GetSeparator(i), context);
			Spacing.AfterComma(context);
		}

		Spacing.InsideBrackets(context);
		TokenPrinter.Print(node.CloseBracketToken, context);
	}

	public static void BaseList(BaseListSyntax node, PrintContext context)
	{
		var arena = context.Arena;

		// The space before the colon is emitted by the caller, which knows what preceded it.
		TokenPrinter.Print(node.ColonToken, context);
		Spacing.AfterInheritanceColon(context);

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
