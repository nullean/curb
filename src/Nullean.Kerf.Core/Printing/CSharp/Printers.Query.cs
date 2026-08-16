using Microsoft.CodeAnalysis.CSharp.Syntax;
using Nullean.Kerf.Documents;

namespace Nullean.Kerf.Printing.CSharp;

/// <summary>LINQ query syntax.</summary>
internal static partial class Printers
{
	/// <summary>Register holding the column a query expression starts at.</summary>
	private const int QueryAnchor = 1;

	/// <summary>
	/// A query expression, one clause per line or flowing until the line runs out.
	/// </summary>
	/// <remarks>
	/// Under <c>csharp_new_line_between_query_expression_clauses</c> every clause takes a line of
	/// its own. Otherwise the clauses sit in a group and break only when they do not fit, which is
	/// what leaves a one-line query on one line — the shape dotnet format produces.
	/// </remarks>
	public static void QueryExpression(QueryExpressionSyntax node, PrintContext context)
	{
		var arena = context.Arena;
		var oneClausePerLine = context.Options.NewLineBetweenQueryExpressionClauses;

		// Clauses line up under the `from`, not at an indent level — `var x = from a in b` puts the
		// following `where` under the `f`, wherever that lands. Capture the column before printing.
		arena.Anchor(QueryAnchor);

		using (arena.Group())
		{
			QueryFromClause(node.FromClause, context);

			using (arena.Indent())
			{
				foreach (var clause in node.Body.Clauses)
				{
					Separator();
					Node.Print(clause, context);
				}

				Separator();
				Node.Print(node.Body.SelectOrGroup, context);

				if (node.Body.Continuation is null)
					return;

				Separator();
				TokenPrinter.Print(node.Body.Continuation.IntoKeyword, context);
				arena.Synthetic(SyntheticText.Space);
				TokenPrinter.Print(node.Body.Continuation.Identifier, context);
			}
		}

		void Separator()
		{
			if (oneClausePerLine)
				arena.AlignedLine(QueryAnchor);
			else
				arena.Line();
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
}
