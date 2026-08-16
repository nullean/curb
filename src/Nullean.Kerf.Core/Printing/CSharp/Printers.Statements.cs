using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Nullean.Kerf.Documents;
using Nullean.Kerf.Options;

namespace Nullean.Kerf.Printing.CSharp;

/// <summary>Printers for statements.</summary>
internal static partial class Printers
{
	/// <summary>
	/// Emits the body of a control-flow construct.
	/// </summary>
	/// <remarks>
	/// A braced body opens on its own line (Allman, the Roslyn default, until
	/// <c>csharp_new_line_before_open_brace</c> governs it). A single unbraced statement is indented
	/// onto the next line instead.
	/// </remarks>
	private static void EmbeddedStatement(StatementSyntax? statement, PrintContext context)
	{
		if (statement is null)
			return;

		var arena = context.Arena;

		if (statement is BlockSyntax)
		{
			BeforeOpenBrace(BraceStyle.ControlBlocks, context);
			Node.Print(statement, context);
			return;
		}

		using (arena.Indent())
		{
			arena.HardLine();
			Node.Print(statement, context);
		}
	}

	/// <summary>Emits <c>keyword (expression)</c>, the shape every control-flow header shares.</summary>
	private static void ConditionHeader(
		SyntaxToken keyword,
		SyntaxToken openParen,
		SyntaxNode? condition,
		SyntaxToken closeParen,
		PrintContext context)
	{
		var arena = context.Arena;

		TokenPrinter.Print(keyword, context);
		Spacing.AfterControlFlowKeyword(context);
		TokenPrinter.Print(openParen, context);

		using (arena.Group())
		{
			using (arena.Indent())
			{
				Spacing.InsideControlFlowParensBreakable(context);
				Node.Print(condition, context);
			}

			Spacing.InsideControlFlowParensBreakable(context);
		}

		TokenPrinter.Print(closeParen, context);
	}

	public static void IfStatement(IfStatementSyntax node, PrintContext context)
	{
		ConditionHeader(node.IfKeyword, node.OpenParenToken, node.Condition, node.CloseParenToken, context);
		EmbeddedStatement(node.Statement, context);

		if (node.Else is null)
			return;

		BeforeContinuation(context.Options.NewLineBeforeElse, node.Statement is BlockSyntax, context);
		TokenPrinter.Print(node.Else.ElseKeyword, context);

		// `else if` stays on one line rather than nesting a whole new indent level.
		if (node.Else.Statement is IfStatementSyntax)
		{
			context.Arena.Synthetic(SyntheticText.Space);
			Node.Print(node.Else.Statement, context);
			return;
		}

		EmbeddedStatement(node.Else.Statement, context);
	}

	public static void WhileStatement(WhileStatementSyntax node, PrintContext context)
	{
		ConditionHeader(node.WhileKeyword, node.OpenParenToken, node.Condition, node.CloseParenToken, context);
		EmbeddedStatement(node.Statement, context);
	}

	public static void DoStatement(DoStatementSyntax node, PrintContext context)
	{
		TokenPrinter.Print(node.DoKeyword, context);
		EmbeddedStatement(node.Statement, context);
		context.Arena.HardLine();
		ConditionHeader(node.WhileKeyword, node.OpenParenToken, node.Condition, node.CloseParenToken, context);
		TokenPrinter.Print(node.SemicolonToken, context);
	}

	public static void ForStatement(ForStatementSyntax node, PrintContext context)
	{
		TokenPrinter.Print(node.ForKeyword, context);
		Spacing.AfterControlFlowKeyword(context);
		TokenPrinter.Print(node.OpenParenToken, context);
		Spacing.InsideControlFlowParens(context);

		Node.Print(node.Declaration, context);
		foreach (var initializer in node.Initializers)
			Node.Print(initializer, context);

		// The spacing goes around the semicolon whether or not a clause follows it, which is what
		// gives an empty header the `for (; ; )` shape dotnet format produces.
		Spacing.BeforeForSemicolon(context);
		TokenPrinter.Print(node.FirstSemicolonToken, context);
		Spacing.AfterForSemicolon(context);
		if (node.Condition is not null)
			Node.Print(node.Condition, context);

		Spacing.BeforeForSemicolon(context);
		TokenPrinter.Print(node.SecondSemicolonToken, context);
		Spacing.AfterForSemicolon(context);
		for (var i = 0; i < node.Incrementors.Count; i++)
		{
			Node.Print(node.Incrementors[i], context);
			if (i >= node.Incrementors.SeparatorCount)
				continue;
			TokenPrinter.Print(node.Incrementors.GetSeparator(i), context);
			context.Arena.Synthetic(SyntheticText.Space);
		}

		Spacing.InsideControlFlowParens(context);
		TokenPrinter.Print(node.CloseParenToken, context);
		EmbeddedStatement(node.Statement, context);
	}

	public static void ForEachStatement(ForEachStatementSyntax node, PrintContext context)
	{
		var arena = context.Arena;

		TokenPrinter.PrintIfPresent(node.AwaitKeyword, context);
		if (node.AwaitKeyword.RawKind != 0)
			arena.Synthetic(SyntheticText.Space);

		TokenPrinter.Print(node.ForEachKeyword, context);
		Spacing.AfterControlFlowKeyword(context);
		TokenPrinter.Print(node.OpenParenToken, context);
		Spacing.InsideControlFlowParens(context);

		Node.Print(node.Type, context);
		arena.Synthetic(SyntheticText.Space);
		TokenPrinter.Print(node.Identifier, context);
		arena.Synthetic(SyntheticText.Space);
		TokenPrinter.Print(node.InKeyword, context);
		arena.Synthetic(SyntheticText.Space);
		Node.Print(node.Expression, context);

		Spacing.InsideControlFlowParens(context);
		TokenPrinter.Print(node.CloseParenToken, context);
		EmbeddedStatement(node.Statement, context);
	}

	public static void ForEachVariableStatement(ForEachVariableStatementSyntax node, PrintContext context)
	{
		var arena = context.Arena;

		TokenPrinter.PrintIfPresent(node.AwaitKeyword, context);
		if (node.AwaitKeyword.RawKind != 0)
			arena.Synthetic(SyntheticText.Space);

		TokenPrinter.Print(node.ForEachKeyword, context);
		Spacing.AfterControlFlowKeyword(context);
		TokenPrinter.Print(node.OpenParenToken, context);
		Spacing.InsideControlFlowParens(context);

		Node.Print(node.Variable, context);
		arena.Synthetic(SyntheticText.Space);
		TokenPrinter.Print(node.InKeyword, context);
		arena.Synthetic(SyntheticText.Space);
		Node.Print(node.Expression, context);

		Spacing.InsideControlFlowParens(context);
		TokenPrinter.Print(node.CloseParenToken, context);
		EmbeddedStatement(node.Statement, context);
	}

	public static void TryStatement(TryStatementSyntax node, PrintContext context)
	{
		var arena = context.Arena;

		TokenPrinter.Print(node.TryKeyword, context);
		BeforeOpenBrace(BraceStyle.ControlBlocks, context);
		Node.Print(node.Block, context);

		foreach (var catchClause in node.Catches)
		{
			// Whatever precedes a catch is a block, so there is always a brace to join.
			BeforeContinuation(context.Options.NewLineBeforeCatch, true, context);
			TokenPrinter.Print(catchClause.CatchKeyword, context);

			if (catchClause.Declaration is not null)
			{
				Spacing.AfterControlFlowKeyword(context);
				TokenPrinter.Print(catchClause.Declaration.OpenParenToken, context);
				Spacing.InsideControlFlowParens(context);
				Node.Print(catchClause.Declaration.Type, context);
				if (catchClause.Declaration.Identifier.RawKind != 0)
				{
					arena.Synthetic(SyntheticText.Space);
					TokenPrinter.Print(catchClause.Declaration.Identifier, context);
				}

				Spacing.InsideControlFlowParens(context);
				TokenPrinter.Print(catchClause.Declaration.CloseParenToken, context);
			}

			if (catchClause.Filter is not null)
			{
				arena.Synthetic(SyntheticText.Space);
				TokenPrinter.Print(catchClause.Filter.WhenKeyword, context);
				Spacing.AfterControlFlowKeyword(context);
				TokenPrinter.Print(catchClause.Filter.OpenParenToken, context);
				Spacing.InsideControlFlowParens(context);
				Node.Print(catchClause.Filter.FilterExpression, context);
				Spacing.InsideControlFlowParens(context);
				TokenPrinter.Print(catchClause.Filter.CloseParenToken, context);
			}

			BeforeOpenBrace(BraceStyle.ControlBlocks, context);
			Node.Print(catchClause.Block, context);
		}

		if (node.Finally is null)
			return;

		BeforeContinuation(context.Options.NewLineBeforeFinally, true, context);
		TokenPrinter.Print(node.Finally.FinallyKeyword, context);
		BeforeOpenBrace(BraceStyle.ControlBlocks, context);
		Node.Print(node.Finally.Block, context);
	}

	public static void UsingStatement(UsingStatementSyntax node, PrintContext context)
	{
		var arena = context.Arena;

		TokenPrinter.PrintIfPresent(node.AwaitKeyword, context);
		if (node.AwaitKeyword.RawKind != 0)
			arena.Synthetic(SyntheticText.Space);

		TokenPrinter.Print(node.UsingKeyword, context);
		Spacing.AfterControlFlowKeyword(context);
		TokenPrinter.Print(node.OpenParenToken, context);
		Spacing.InsideControlFlowParens(context);
		Node.Print(node.Declaration, context);
		Node.Print(node.Expression, context);
		Spacing.InsideControlFlowParens(context);
		TokenPrinter.Print(node.CloseParenToken, context);

		// Chained `using (a) using (b) { }` keeps the inner using on the same line.
		if (node.Statement is UsingStatementSyntax)
		{
			arena.Synthetic(SyntheticText.Space);
			Node.Print(node.Statement, context);
			return;
		}

		EmbeddedStatement(node.Statement, context);
	}

	public static void FixedStatement(FixedStatementSyntax node, PrintContext context)
	{
		TokenPrinter.Print(node.FixedKeyword, context);
		Spacing.AfterControlFlowKeyword(context);
		TokenPrinter.Print(node.OpenParenToken, context);
		Spacing.InsideControlFlowParens(context);
		Node.Print(node.Declaration, context);
		Spacing.InsideControlFlowParens(context);
		TokenPrinter.Print(node.CloseParenToken, context);
		EmbeddedStatement(node.Statement, context);
	}

	public static void UnsafeStatement(UnsafeStatementSyntax node, PrintContext context)
	{
		TokenPrinter.Print(node.UnsafeKeyword, context);
		BeforeOpenBrace(BraceStyle.ControlBlocks, context);
		Node.Print(node.Block, context);
	}

	public static void LockStatement(LockStatementSyntax node, PrintContext context)
	{
		ConditionHeader(node.LockKeyword, node.OpenParenToken, node.Expression, node.CloseParenToken, context);
		EmbeddedStatement(node.Statement, context);
	}

	public static void SwitchStatement(SwitchStatementSyntax node, PrintContext context)
	{
		var arena = context.Arena;

		TokenPrinter.Print(node.SwitchKeyword, context);
		Spacing.AfterControlFlowKeyword(context);

		if (node.OpenParenToken.RawKind != 0)
		{
			TokenPrinter.Print(node.OpenParenToken, context);
			Spacing.InsideControlFlowParens(context);
			Node.Print(node.Expression, context);
			Spacing.InsideControlFlowParens(context);
			TokenPrinter.Print(node.CloseParenToken, context);
		}
		else
		{
			Node.Print(node.Expression, context);
		}

		BeforeOpenBrace(BraceStyle.ControlBlocks, context);
		TokenPrinter.Print(node.OpenBraceToken, context);

		foreach (var section in node.Sections)
		{
			// csharp_indent_switch_labels, default true.
			using (arena.Indent())
			{
				foreach (var label in section.Labels)
				{
					arena.HardLine();
					Node.Print(label, context);
				}

				// csharp_indent_case_contents, default true.
				using (arena.Indent())
				{
					foreach (var statement in section.Statements)
					{
						arena.HardLine(DocFlags.OnlyIfNotAtLineStart);
						Node.Print(statement, context);
					}
				}
			}
		}

		arena.HardLine();
		TokenPrinter.Print(node.CloseBraceToken, context);
	}

	public static void ThrowStatement(ThrowStatementSyntax node, PrintContext context)
	{
		TokenPrinter.Print(node.ThrowKeyword, context);
		if (node.Expression is not null)
		{
			context.Arena.Synthetic(SyntheticText.Space);
			Node.Print(node.Expression, context);
		}
		TokenPrinter.Print(node.SemicolonToken, context);
	}

	public static void YieldStatement(YieldStatementSyntax node, PrintContext context)
	{
		TokenPrinter.Print(node.YieldKeyword, context);
		context.Arena.Synthetic(SyntheticText.Space);
		TokenPrinter.Print(node.ReturnOrBreakKeyword, context);
		if (node.Expression is not null)
		{
			context.Arena.Synthetic(SyntheticText.Space);
			Node.Print(node.Expression, context);
		}
		TokenPrinter.Print(node.SemicolonToken, context);
	}

	/// <summary>Covers <c>break;</c>, <c>continue;</c> and the empty statement.</summary>
	public static void KeywordStatement(StatementSyntax node, PrintContext context) => Tokens(node, context);

	public static void LocalFunctionStatement(LocalFunctionStatementSyntax node, PrintContext context)
	{
		var arena = context.Arena;

		PrintAttributeLists(node.AttributeLists, context);
		PrintModifiers(node.Modifiers, context);
		Node.Print(node.ReturnType, context);
		arena.Synthetic(SyntheticText.Space);
		TokenPrinter.Print(node.Identifier, context);

		if (node.TypeParameterList is not null)
			Node.Print(node.TypeParameterList, context);

		Spacing.BeforeDeclarationParens(context);
		Node.Print(node.ParameterList, context);

		foreach (var constraint in node.ConstraintClauses)
		{
			arena.Synthetic(SyntheticText.Space);
			Node.Print(constraint, context);
		}

		if (node.Body is not null)
		{
			BeforeOpenBrace(BraceStyle.LocalFunctions, context);
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

	public static void GlobalStatement(GlobalStatementSyntax node, PrintContext context) =>
		Node.Print(node.Statement, context);

	public static void CaseSwitchLabel(CaseSwitchLabelSyntax node, PrintContext context)
	{
		TokenPrinter.Print(node.Keyword, context);
		context.Arena.Synthetic(SyntheticText.Space);
		Node.Print(node.Value, context);
		TokenPrinter.Print(node.ColonToken, context);
	}

	public static void CasePatternSwitchLabel(CasePatternSwitchLabelSyntax node, PrintContext context)
	{
		var arena = context.Arena;

		TokenPrinter.Print(node.Keyword, context);
		arena.Synthetic(SyntheticText.Space);
		Node.Print(node.Pattern, context);

		if (node.WhenClause is not null)
		{
			arena.Synthetic(SyntheticText.Space);
			TokenPrinter.Print(node.WhenClause.WhenKeyword, context);
			arena.Synthetic(SyntheticText.Space);
			Node.Print(node.WhenClause.Condition, context);
		}

		TokenPrinter.Print(node.ColonToken, context);
	}

	public static void DefaultSwitchLabel(DefaultSwitchLabelSyntax node, PrintContext context)
	{
		TokenPrinter.Print(node.Keyword, context);
		TokenPrinter.Print(node.ColonToken, context);
	}
}
