using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Nullean.Kerf.Documents;

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
			arena.HardLine();
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
		// csharp_space_after_keywords_in_control_flow_statements, default true.
		arena.Synthetic(SyntheticText.Space);
		TokenPrinter.Print(openParen, context);

		using (arena.Group())
		{
			using (arena.Indent())
			{
				arena.SoftLine();
				Node.Print(condition, context);
			}
			arena.SoftLine();
		}

		TokenPrinter.Print(closeParen, context);
	}

	public static void IfStatement(IfStatementSyntax node, PrintContext context)
	{
		ConditionHeader(node.IfKeyword, node.OpenParenToken, node.Condition, node.CloseParenToken, context);
		EmbeddedStatement(node.Statement, context);

		if (node.Else is null)
			return;

		// csharp_new_line_before_else, default true.
		context.Arena.HardLine();
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
		var arena = context.Arena;

		TokenPrinter.Print(node.ForKeyword, context);
		arena.Synthetic(SyntheticText.Space);
		TokenPrinter.Print(node.OpenParenToken, context);

		Node.Print(node.Declaration, context);
		foreach (var initializer in node.Initializers)
			Node.Print(initializer, context);

		TokenPrinter.Print(node.FirstSemicolonToken, context);
		if (node.Condition is not null)
		{
			// csharp_space_after_semicolon_in_for_statement, default true.
			arena.Synthetic(SyntheticText.Space);
			Node.Print(node.Condition, context);
		}

		TokenPrinter.Print(node.SecondSemicolonToken, context);
		for (var i = 0; i < node.Incrementors.Count; i++)
		{
			arena.Synthetic(SyntheticText.Space);
			Node.Print(node.Incrementors[i], context);
			if (i < node.Incrementors.SeparatorCount)
				TokenPrinter.Print(node.Incrementors.GetSeparator(i), context);
		}

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
		arena.Synthetic(SyntheticText.Space);
		TokenPrinter.Print(node.OpenParenToken, context);

		Node.Print(node.Type, context);
		arena.Synthetic(SyntheticText.Space);
		TokenPrinter.Print(node.Identifier, context);
		arena.Synthetic(SyntheticText.Space);
		TokenPrinter.Print(node.InKeyword, context);
		arena.Synthetic(SyntheticText.Space);
		Node.Print(node.Expression, context);

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
		arena.Synthetic(SyntheticText.Space);
		TokenPrinter.Print(node.OpenParenToken, context);

		Node.Print(node.Variable, context);
		arena.Synthetic(SyntheticText.Space);
		TokenPrinter.Print(node.InKeyword, context);
		arena.Synthetic(SyntheticText.Space);
		Node.Print(node.Expression, context);

		TokenPrinter.Print(node.CloseParenToken, context);
		EmbeddedStatement(node.Statement, context);
	}

	public static void TryStatement(TryStatementSyntax node, PrintContext context)
	{
		var arena = context.Arena;

		TokenPrinter.Print(node.TryKeyword, context);
		arena.HardLine();
		Node.Print(node.Block, context);

		foreach (var catchClause in node.Catches)
		{
			// csharp_new_line_before_catch, default true.
			arena.HardLine();
			TokenPrinter.Print(catchClause.CatchKeyword, context);

			if (catchClause.Declaration is not null)
			{
				arena.Synthetic(SyntheticText.Space);
				TokenPrinter.Print(catchClause.Declaration.OpenParenToken, context);
				Node.Print(catchClause.Declaration.Type, context);
				if (catchClause.Declaration.Identifier.RawKind != 0)
				{
					arena.Synthetic(SyntheticText.Space);
					TokenPrinter.Print(catchClause.Declaration.Identifier, context);
				}
				TokenPrinter.Print(catchClause.Declaration.CloseParenToken, context);
			}

			if (catchClause.Filter is not null)
			{
				arena.Synthetic(SyntheticText.Space);
				TokenPrinter.Print(catchClause.Filter.WhenKeyword, context);
				arena.Synthetic(SyntheticText.Space);
				TokenPrinter.Print(catchClause.Filter.OpenParenToken, context);
				Node.Print(catchClause.Filter.FilterExpression, context);
				TokenPrinter.Print(catchClause.Filter.CloseParenToken, context);
			}

			arena.HardLine();
			Node.Print(catchClause.Block, context);
		}

		if (node.Finally is null)
			return;

		// csharp_new_line_before_finally, default true.
		arena.HardLine();
		TokenPrinter.Print(node.Finally.FinallyKeyword, context);
		arena.HardLine();
		Node.Print(node.Finally.Block, context);
	}

	public static void UsingStatement(UsingStatementSyntax node, PrintContext context)
	{
		var arena = context.Arena;

		TokenPrinter.PrintIfPresent(node.AwaitKeyword, context);
		if (node.AwaitKeyword.RawKind != 0)
			arena.Synthetic(SyntheticText.Space);

		TokenPrinter.Print(node.UsingKeyword, context);
		arena.Synthetic(SyntheticText.Space);
		TokenPrinter.Print(node.OpenParenToken, context);
		Node.Print(node.Declaration, context);
		Node.Print(node.Expression, context);
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

	public static void LockStatement(LockStatementSyntax node, PrintContext context)
	{
		ConditionHeader(node.LockKeyword, node.OpenParenToken, node.Expression, node.CloseParenToken, context);
		EmbeddedStatement(node.Statement, context);
	}

	public static void SwitchStatement(SwitchStatementSyntax node, PrintContext context)
	{
		var arena = context.Arena;

		TokenPrinter.Print(node.SwitchKeyword, context);
		arena.Synthetic(SyntheticText.Space);

		if (node.OpenParenToken.RawKind != 0)
		{
			TokenPrinter.Print(node.OpenParenToken, context);
			Node.Print(node.Expression, context);
			TokenPrinter.Print(node.CloseParenToken, context);
		}
		else
		{
			Node.Print(node.Expression, context);
		}

		arena.HardLine();
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

		Node.Print(node.ParameterList, context);

		foreach (var constraint in node.ConstraintClauses)
		{
			arena.Synthetic(SyntheticText.Space);
			Node.Print(constraint, context);
		}

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
