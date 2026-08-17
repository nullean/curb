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
	private static void OperandOnRight(SyntaxNode? right, PrintContext context, int operatorEnd = -1)
	{
		var arena = context.Arena;

		// Where the operand starts, not whether it spans lines — see EqualsValueClause for why the
		// difference decides whether formatting twice settles.
		if (right is ExpressionSyntax expression
			&& (BringsOwnBlock(expression)
				|| (operatorEnd >= 0 && context.OnSameLine(operatorEnd, expression.SpanStart))))
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

		if (TryPrintBinaryChain(node, context))
			return;

		Node.Print(node.Left, context);

		// A chain the author broke stays broken, with its operands level rather than gaining a
		// continuation indent — that is where dotnet format leaves them.
		//
		// The break can be on either side of the operator, and both shapes are common: `a &&` at the
		// end of a line, or `|| b` at the start of the next. Checking only after the operator missed
		// the operator-first style entirely and joined those chains, which is what left two corpus
		// files still reformatting themselves on a second run.
		if (!context.OnSameLine(node.Left.Span.End, node.OperatorToken.SpanStart))
			context.Arena.HardLine();
		else
			Spacing.BeforeOperator(context);

		TokenPrinter.Print(node.OperatorToken, context);

		if (!context.OnSameLine(node.OperatorToken.Span.End, node.Right.SpanStart))
		{
			context.Arena.HardLine();
			Node.Print(node.Right, context);
			return;
		}

		OperandOnRight(node.Right, context, node.OperatorToken.Span.End);
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
		OperandOnRight(node.Right, context, node.OperatorToken.Span.End);
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

		// Reproduce the author's own breaks rather than imposing one member per line: an initializer
		// written across lines with two members sharing one keeps them sharing it.
		var asWritten = !oneMemberPerLine && SpansLines(node, context);

		// A dictionary initializer — one whose elements are themselves brace-wrapped — is the one
		// shape Roslyn declines to re-indent: hand `dotnet format` a `{ { "k", "v" } }` block
		// indented four tabs too far and it leaves every line of it exactly where it is, in a way it
		// does for no other initializer. Reproducing the source verbatim is what parity means here.
		if (asWritten && HasBracedElements(node))
		{
			if (leadingLine)
				arena.Synthetic(SyntheticText.Space);

			PrintVerbatim(node, context);
			return;
		}

		var trailingTrivia = false;

		using (arena.Group())
		{
			if (leadingLine)
			{
				// `new HttpClient(new Handler { … }) { Timeout = t }` — once the creation it belongs to
				// has been opened out, the initializer takes a line of its own rather than trailing
				// the closing parenthesis.
				//
				// Asked of the source, not of what the printer is about to produce. dotnet format can
				// key this on the creation spanning lines because it never reflows, so its answer
				// cannot change; Kerf's reflow can open a creation out on one run, which would flip
				// the answer on the next and stop the file settling. Where the author put the brace
				// is fixed, so it is that which decides.
				var ownerSpansLines = node.Parent is not null
					&& !context.OnSameLine(node.Parent.SpanStart, node.SpanStart);

				if (oneMemberPerLine
					|| ownerSpansLines
					|| (asWritten && !context.OnSameLine(node.SpanStart, node.Parent!.SpanStart)))
					BeforeOpenBrace(BraceStyle.ObjectCollectionArrayInitializers, context);
				else
					BeforeOpenBraceWhenBroken(BraceStyle.ObjectCollectionArrayInitializers, context);
			}

			TokenPrinter.Print(node.OpenBraceToken, context);

			if (node.Expressions.Count > 0)
			{
				// A count rather than a column, the same question the parameter and argument limits
				// ask. Breaking only — an initializer that fits is never closed up by this.
				var isArray = node.IsKind(SyntaxKind.ArrayInitializerExpression);
				var limit = isArray
					? context.Options.MaxArrayInitializerElementsOnLine
					: context.Options.MaxInitializerElementsOnLine;


				// Not for an initializer sitting in an argument list. Where a construct with its own
				// braces anchors is read from the source — the line it starts on — and forcing a break
				// the source does not have puts it a level out, which the second run then corrects.
				// The shapes this rule is for, an assignment or a field, are not in that position.
				if (limit is { } elements && node.Expressions.Count > elements && !IsInsideArguments(node))
					arena.BreakParent();

				var rewritesComma = RewritesTrailingComma(node.Expressions, node.CloseBraceToken, context);

				using (arena.Indent())
				{
					InsideBrace(node.OpenBraceToken.Span.End, node.Expressions[0].SpanStart);
					for (var i = 0; i < node.Expressions.Count; i++)
					{
						Node.Print(node.Expressions[i], context);
						if (i >= node.Expressions.SeparatorCount)
							continue;

						// The source's own trailing comma is suppressed when the printer is deciding
						// this list's, so that the two cannot both emit one.
						if (rewritesComma && i == node.Expressions.Count - 1)
							continue;

						Spacing.BeforeComma(context);
						TokenPrinter.Print(node.Expressions.GetSeparator(i), context);

						// A trailing separator is followed by the closing line, not by another one.
						if (i < node.Expressions.Count - 1)
							Separator(node.Expressions[i].Span.End, node.Expressions[i + 1].SpanStart);
					}

					if (rewritesComma)
						PrintTrailingComma(context);

					// The last member may itself have ended in a line comment, which closed the line
					// already; the closing brace must then reuse that line rather than break again.
					if (EndsWithLineComment(node.CloseBraceToken.GetPreviousToken()))
						trailingTrivia = true;

					// A comment sitting above the closing brace belongs with the members, not with
					// the brace — the same rule Block already follows. Printed here, inside the
					// indent, it keeps their level instead of dropping back to the construct's.
					if (TokenPrinter.HasLeadingContent(node.CloseBraceToken))
					{
						arena.HardLine(DocFlags.OnlyIfNotAtLineStart);
						TokenPrinter.PrintLeadingTrivia(node.CloseBraceToken, context, trailingBreak: false);
						trailingTrivia = true;
					}
				}

				using (arena.IndentIf(context.Options.IndentBraces))
				{
					// Trivia has already ended its own line, and a directive ends two. Reindent
					// reuses whatever line is open instead of adding another, which is what stopped
					// a `#pragma restore` above the brace growing a blank line on every run.
					if (trailingTrivia)
						arena.HardLine(DocFlags.Reindent);
					else
						InsideBrace(node.Expressions[^1].Span.End, node.CloseBraceToken.SpanStart);
				}
			}
			else
			{
				// `{ }` — nothing to break around, so the brace pair just gets air between it.
				arena.Synthetic(SyntheticText.Space);
			}

			TokenPrinter.PrintWithoutLeadingTrivia(node.CloseBraceToken, context);
		}

		// Between two members: a hard line, or the ordinary comma separator.
		void Separator(int from, int to)
		{
			if (oneMemberPerLine || (asWritten && !context.OnSameLine(from, to)))
				arena.HardLine();
			else if (asWritten)
				Spacing.AfterComma(context);
			else
				Spacing.AfterCommaBreakable(context);
		}

		// Just inside the braces, where no comma sits and the comma options do not reach.
		void InsideBrace(int from, int to)
		{
			if (oneMemberPerLine || (asWritten && !context.OnSameLine(from, to)))
				arena.HardLine();
			else
				arena.Line();
		}
	}

	/// <summary>True for an initializer whose elements are brace-wrapped, as a dictionary's are.</summary>
	private static bool HasBracedElements(InitializerExpressionSyntax node)
	{
		foreach (var expression in node.Expressions)
		{
			if (expression is InitializerExpressionSyntax)
				return true;
		}

		return false;
	}

	public static void CollectionExpression(CollectionExpressionSyntax node, PrintContext context)
	{
		var arena = context.Arena;
		TokenPrinter.Print(node.OpenBracketToken, context);

		if (node.Elements.Count > 0)
		{
			var asWritten = SpansLines(node, context);
			var rewritesComma = RewritesTrailingComma(node.Elements, node.CloseBracketToken, context);

			using (arena.Group())
			{
				using (arena.Indent())
				{
					Edge(node.SpanStart, node.Elements[0].SpanStart);
					for (var i = 0; i < node.Elements.Count; i++)
					{
						Node.Print(node.Elements[i], context);
						if (i >= node.Elements.SeparatorCount)
							continue;

						if (rewritesComma && i == node.Elements.Count - 1)
							continue;

						Spacing.BeforeComma(context);
						TokenPrinter.Print(node.Elements.GetSeparator(i), context);

						// A trailing comma runs straight into the closing bracket, not `, ]`.
						if (i >= node.Elements.Count - 1)
							continue;

						if (!asWritten)
							Spacing.AfterCommaBreakable(context);
						else if (context.OnSameLine(node.Elements[i].Span.End, node.Elements[i + 1].SpanStart))
							Spacing.AfterComma(context);
						else
							arena.HardLine();
					}

					if (rewritesComma)
						PrintTrailingComma(context);
				}

				Edge(node.Elements[^1].Span.End, node.Span.End);
			}

			void Edge(int from, int to)
			{
				if (asWritten && !context.OnSameLine(from, to))
					arena.HardLine();
				else
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

		// A body the author put on the next line stays there. Joining it here defeated reflow further
		// out: the enclosing argument list saw nothing left to preserve, emitted no break
		// opportunity, and the statement came back as one over-long line that only the next run
		// broke — which is to say, formatting twice did not settle.
		if (expression is not null && !context.OnSameLine(arrow.Span.End, expression.SpanStart))
		{
			using (arena.Indent())
			{
				arena.HardLine();
				Node.Print(expression, context);
			}

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
		var asWritten = !oneMemberPerLine && SpansLines(node, context);
		var rewritesComma = RewritesTrailingComma(node.Initializers, node.CloseBraceToken, context);

		TokenPrinter.Print(node.NewKeyword, context);

		using (arena.Group())
		{
			if (oneMemberPerLine || (asWritten && !context.OnSameLine(node.NewKeyword.Span.End, node.OpenBraceToken.SpanStart)))
				BeforeOpenBrace(BraceStyle.AnonymousTypes, context);
			else
				BeforeOpenBraceWhenBroken(BraceStyle.AnonymousTypes, context);

			TokenPrinter.Print(node.OpenBraceToken, context);

			// `new { }` is valid C# and has nothing to lay out. Both ends of the block below index the
			// initializer list, so an empty one crashed the printer — and because the CLI formats in
			// parallel without isolating a file's exceptions, that one expression aborted the whole
			// run. Found on MassTransit, efcore and roslyn, which is to say the three largest
			// repositories measured.
			using (arena.Indent())
			{
				if (node.Initializers.Count > 0)
				{
					InsideBrace(node.OpenBraceToken.Span.End, node.Initializers[0].SpanStart);
					for (var i = 0; i < node.Initializers.Count; i++)
					{
						Node.Print(node.Initializers[i], context);
						if (i >= node.Initializers.SeparatorCount)
							continue;

						if (rewritesComma && i == node.Initializers.Count - 1)
							continue;

						Spacing.BeforeComma(context);
						TokenPrinter.Print(node.Initializers.GetSeparator(i), context);
						if (i < node.Initializers.Count - 1)
							Separator(node.Initializers[i].Span.End, node.Initializers[i + 1].SpanStart);
					}

					if (rewritesComma)
						PrintTrailingComma(context);
				}
			}

			using (arena.IndentIf(context.Options.IndentBraces))
				InsideBrace(
					node.Initializers.Count > 0 ? node.Initializers[^1].Span.End : node.OpenBraceToken.Span.End,
					node.CloseBraceToken.SpanStart);

			TokenPrinter.Print(node.CloseBraceToken, context);
		}

		void Separator(int from, int to)
		{
			if (oneMemberPerLine || (asWritten && !context.OnSameLine(from, to)))
				arena.HardLine();
			else if (asWritten)
				Spacing.AfterComma(context);
			else
				Spacing.AfterCommaBreakable(context);
		}

		void InsideBrace(int from, int to)
		{
			if (oneMemberPerLine || (asWritten && !context.OnSameLine(from, to)))
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

		var rewritesComma = RewritesTrailingComma(node.Arms, node.CloseBraceToken, context);

		using (arena.Indent())
		{
			for (var i = 0; i < node.Arms.Count; i++)
			{
				arena.HardLine();
				Node.Print(node.Arms[i], context);

				if (rewritesComma && i == node.Arms.Count - 1)
					continue;

				if (i < node.Arms.SeparatorCount)
					TokenPrinter.Print(node.Arms.GetSeparator(i), context);
			}

			// A switch expression is always laid out broken, so the flat branch never applies.
			if (rewritesComma)
				PrintTrailingComma(context);
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
