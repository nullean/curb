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

		// Whether the author opened this query out. Read from the source, and stable there because
		// Kerf reproduces their breaks, so the next run reads the same answer back.
		var asWritten = !oneClausePerLine && SpansLines(node, context);

		// Clauses line up under the `from`, not at an indent level — `var x = from a in b` puts the
		// following `where` under the `f`, wherever that lands. Capture the column before printing.
		arena.Anchor(QueryAnchor);

		using (arena.Group())
		{
			QueryFromClause(node.FromClause, context);

			// No indent scope: a clause that breaks lands on the anchor's column, not on a level.
			PrintQueryBody(node.Body, node.FromClause.Span.End);
		}

		// A continuation carries a whole query body of its own, and it was never printed: everything
		// after `into g` — the clauses, the select, and any comment among them — was dropped. The
		// content verifier caught it, so no file was ever written that way, but any query using
		// `into` simply could not be formatted. Nothing in the corpus uses one.
		void PrintQueryBody(QueryBodySyntax body, int previousEnd)
		{
			foreach (var clause in body.Clauses)
			{
				Separator(previousEnd, clause.SpanStart);
				Node.Print(clause, context);
				previousEnd = clause.Span.End;
			}

			Separator(previousEnd, body.SelectOrGroup.SpanStart);
			Node.Print(body.SelectOrGroup, context);
			previousEnd = body.SelectOrGroup.Span.End;

			if (body.Continuation is null)
				return;

			Separator(previousEnd, body.Continuation.SpanStart);
			TokenPrinter.Print(body.Continuation.IntoKeyword, context);
			arena.Synthetic(SyntheticText.Space);
			TokenPrinter.Print(body.Continuation.Identifier, context);
			PrintQueryBody(body.Continuation.Body, body.Continuation.Identifier.Span.End);
		}

		void Separator(int previousEnd, int nextStart)
		{
			// Aligned either way. The option decides whether the break is taken at all; where it
			// lands when it is taken is the same question, and dotnet format answers it under the
			// `from` whether the break came from the option or from the width.
			//
			// A break the author already put between two clauses is kept, as it is in a member chain
			// and in an expression body. Without that the whole query closed up onto one line
			// whenever it fitted, which with reflow off is always.
			if (oneClausePerLine)
			{
				arena.AlignedLine(QueryAnchor);
				return;
			}

			// A query the author already opened out keeps their breaks exactly, clause by clause, and
			// a plain space where they joined two. A break opportunity will not do: one hard line
			// anywhere breaks the group, and every soft line in it with it, so `group x by x into g`
			// came apart at the `into`.
			if (asWritten)
			{
				if (context.AuthorJoined(previousEnd, nextStart))
					arena.Synthetic(SyntheticText.Space);
				else
					arena.AlignedLine(QueryAnchor);

				return;
			}

			arena.AlignedBreakOpportunity(QueryAnchor);
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
