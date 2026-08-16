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
	/// <param name="statement">The body, or null for a header with none.</param>
	/// <param name="headerEnd">
	/// End of the token the body follows — usually the closing parenthesis. Needed to tell a body
	/// the author left on the header's line from one they put on its own, which is the whole of
	/// <c>csharp_preserve_single_line_statements</c>.
	/// </param>
	/// <param name="context">Per-file printing state.</param>
	private static void EmbeddedStatement(StatementSyntax? statement, int headerEnd, PrintContext context)
	{
		if (statement is null)
			return;

		var arena = context.Arena;

		// Braces first, because adding them expands the body onto its own lines whatever the
		// preservation options say — see FormatOptions.PreferBraces for why matching Roslyn here is
		// the whole point rather than a preference.
		if (WantsBraces(statement, headerEnd, context))
		{
			PrintSynthesisedBlock(statement, context);
			return;
		}

		// A statement that shared its header's line keeps sharing it, braces and all. This beats
		// csharp_preserve_single_line_blocks: `if (a) { return; }` stays whole even with that off.
		if (context.Options.PreserveSingleLineStatements && context.OnSameLine(headerEnd, statement.Span.End))
		{
			arena.Synthetic(SyntheticText.Space);
			using (arena.ForceFlat())
				Node.Print(statement, context);
			return;
		}

		if (statement is BlockSyntax)
		{
			PrintStatementBody(statement, BraceStyle.ControlBlocks, context);
			return;
		}

		using (arena.Indent())
		{
			arena.HardLine();
			Node.Print(statement, context);
		}
	}

	/// <summary>
	/// Whether this body should be given braces it was not written with.
	/// </summary>
	/// <remarks>
	/// Never for a body that already has them, and never for the <c>if</c> of an <c>else if</c> —
	/// Roslyn braces the chain's bodies, not the chain. <c>when_multiline</c> asks whether the author
	/// kept the body on the header's line, which is a fact about the source and so cannot change
	/// under reflow; asking whether the printed body breaks would let one run's layout decide the next
	/// run's tokens.
	/// </remarks>
	private static bool WantsBraces(StatementSyntax statement, int headerEnd, PrintContext context)
	{
		if (statement is BlockSyntax)
			return false;

		return context.Options.PreferBraces switch
		{
			BraceRequirement.Always => true,
			BraceRequirement.WhenMultiline => !context.OnSameLine(headerEnd, statement.Span.End),
			BraceRequirement.AsWritten => false,
			_ => false,
		};
	}

	/// <summary>
	/// Emits a body inside braces the source did not have.
	/// </summary>
	/// <remarks>
	/// The declared token delta: one <c>{</c> and one <c>}</c> that appear in the output and not in
	/// the source. Both verifiers are told, and both hold the pair to being balanced, so a brace that
	/// appears without its partner is still damage.
	/// </remarks>
	private static void PrintSynthesisedBlock(StatementSyntax statement, PrintContext context)
	{
		var arena = context.Arena;

		BeforeOpenBrace(BraceStyle.ControlBlocks, context);
		arena.Synthetic(SyntheticText.OpenBrace);

		using (arena.Indent(context.Options.IndentBlockContents ? 1 : 0))
		{
			arena.HardLine(DocFlags.Reindent);
			Node.Print(statement, context);
		}

		using (arena.IndentIf(context.Options.IndentBraces))
			arena.HardLine(DocFlags.Reindent);

		arena.Synthetic(SyntheticText.CloseBrace);
		context.BracesAdded = true;
	}

	/// <summary>
	/// True when a token is the last one inside a body that is about to be given braces.
	/// </summary>
	/// <remarks>
	/// Asked by the comment-alignment rule, which lines a standalone comment up with a trailing
	/// comment on the line above. A synthesised <c>}</c> lands between the two, so they are no longer
	/// on consecutive lines and the premise is gone. Asked of the source, never of the printed
	/// output: this is precisely the shape where the first run's braces changed the second run's
	/// answer and the file never settled.
	/// </remarks>
	internal static bool ClosesSynthesisedBlock(SyntaxToken token, PrintContext context)
	{
		if (context.Options.PreferBraces == BraceRequirement.AsWritten)
			return false;

		for (var node = token.Parent; node is not null; node = node.Parent)
		{
			if (node is not StatementSyntax statement)
				continue;

			var headerEnd = EmbeddedHeaderEnd(statement);
			if (headerEnd >= 0)
				return WantsBraces(statement, headerEnd, context);

			// A block of its own stops the walk: whatever braces it has are the source's.
			if (statement is BlockSyntax)
				return false;
		}

		return false;
	}

	/// <summary>
	/// End of the header a statement hangs off, or -1 when it is not an embedded body.
	/// </summary>
	private static int EmbeddedHeaderEnd(StatementSyntax statement) =>
		statement.Parent switch
		{
			IfStatementSyntax parent when parent.Statement == statement => parent.CloseParenToken.Span.End,
			ElseClauseSyntax parent when parent.Statement == statement => parent.ElseKeyword.Span.End,
			WhileStatementSyntax parent when parent.Statement == statement => parent.CloseParenToken.Span.End,
			DoStatementSyntax parent when parent.Statement == statement => parent.DoKeyword.Span.End,
			ForStatementSyntax parent when parent.Statement == statement => parent.CloseParenToken.Span.End,
			CommonForEachStatementSyntax parent when parent.Statement == statement => parent.CloseParenToken.Span.End,
			UsingStatementSyntax parent when parent.Statement == statement => parent.CloseParenToken.Span.End,
			LockStatementSyntax parent when parent.Statement == statement => parent.CloseParenToken.Span.End,
			FixedStatementSyntax parent when parent.Statement == statement => parent.CloseParenToken.Span.End,
			_ => -1,
		};

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
		EmbeddedStatement(node.Statement, node.CloseParenToken.Span.End, context);

		if (node.Else is null)
			return;

		// A body that just gained braces ends in one, so `else` continues from a block whatever the
		// source looked like.
		var afterBlock = node.Statement is BlockSyntax
			|| WantsBraces(node.Statement, node.CloseParenToken.Span.End, context);

		BeforeContinuation(context.Options.NewLineBeforeElse, afterBlock, context);
		TokenPrinter.Print(node.Else.ElseKeyword, context);

		// `else if` stays on one line rather than nesting a whole new indent level.
		if (node.Else.Statement is IfStatementSyntax)
		{
			context.Arena.Synthetic(SyntheticText.Space);
			Node.Print(node.Else.Statement, context);
			return;
		}

		EmbeddedStatement(node.Else.Statement, node.Else.ElseKeyword.Span.End, context);
	}

	public static void WhileStatement(WhileStatementSyntax node, PrintContext context)
	{
		ConditionHeader(node.WhileKeyword, node.OpenParenToken, node.Condition, node.CloseParenToken, context);
		EmbeddedStatement(node.Statement, node.CloseParenToken.Span.End, context);
	}

	public static void DoStatement(DoStatementSyntax node, PrintContext context)
	{
		TokenPrinter.Print(node.DoKeyword, context);
		EmbeddedStatement(node.Statement, node.DoKeyword.Span.End, context);
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
		EmbeddedStatement(node.Statement, node.CloseParenToken.Span.End, context);
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
		EmbeddedStatement(node.Statement, node.CloseParenToken.Span.End, context);
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
		EmbeddedStatement(node.Statement, node.CloseParenToken.Span.End, context);
	}

	public static void TryStatement(TryStatementSyntax node, PrintContext context)
	{
		// `try { Call(); } catch { }` written on one line stays on one line. A try whose blocks the
		// author broke does not, even where a brace and the following `catch` share a line — what
		// the option preserves is a single-line statement, not a single-line join.
		if (context.Options.PreserveSingleLineStatements && context.OnSameLine(node.SpanStart, node.Span.End))
		{
			using (context.Arena.ForceFlat())
				PrintTryStatement(node, context);
			return;
		}

		PrintTryStatement(node, context);
	}

	private static void PrintTryStatement(TryStatementSyntax node, PrintContext context)
	{
		var arena = context.Arena;

		TokenPrinter.Print(node.TryKeyword, context);
		PrintStatementBody(node.Block, BraceStyle.ControlBlocks, context);

		foreach (var catchClause in node.Catches)
		{
			// Whatever precedes a catch is a block, so there is always a brace to join.
			BeforeContinuation(context.Options.NewLineBeforeCatch, followsABrace: true, context);
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

			PrintStatementBody(catchClause.Block, BraceStyle.ControlBlocks, context);
		}

		if (node.Finally is null)
			return;

		BeforeContinuation(context.Options.NewLineBeforeFinally, followsABrace: true, context);
		TokenPrinter.Print(node.Finally.FinallyKeyword, context);
		PrintStatementBody(node.Finally.Block, BraceStyle.ControlBlocks, context);
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

		EmbeddedStatement(node.Statement, node.CloseParenToken.Span.End, context);
	}

	/// <summary>A <c>goto</c> target — <c>label:</c> followed by the statement it names.</summary>
	/// <remarks>
	/// The caller has already emitted the line the label sits on, at the ordinary statement indent,
	/// so moving the label left means re-emitting that indent from inside a shallower scope rather
	/// than adding a break of its own.
	/// </remarks>
	public static void LabeledStatement(LabeledStatementSyntax node, PrintContext context)
	{
		var arena = context.Arena;
		var outdent = context.Options.IndentLabels == LabelIndent.OneLessThanCurrent ? -1 : 0;

		using (arena.Indent(outdent))
		{
			arena.HardLine(DocFlags.Reindent);
			TokenPrinter.Print(node.Identifier, context);
			TokenPrinter.Print(node.ColonToken, context);
		}

		arena.HardLine();
		Node.Print(node.Statement, context);
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
		EmbeddedStatement(node.Statement, node.CloseParenToken.Span.End, context);
	}

	public static void UnsafeStatement(UnsafeStatementSyntax node, PrintContext context)
	{
		TokenPrinter.Print(node.UnsafeKeyword, context);
		PrintStatementBody(node.Block, BraceStyle.ControlBlocks, context);
	}

	public static void LockStatement(LockStatementSyntax node, PrintContext context)
	{
		ConditionHeader(node.LockKeyword, node.OpenParenToken, node.Expression, node.CloseParenToken, context);
		EmbeddedStatement(node.Statement, node.CloseParenToken.Span.End, context);
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

		// A switch on one line necessarily has each statement on its label's line, which is
		// csharp_preserve_single_line_statements' business rather than the block option's — so
		// dotnet format expands it as soon as either is off, and so does Kerf.
		var oneLine = context.Options.PreserveSingleLineStatements
			&& KeepsOneLine(node.OpenBraceToken, node.CloseBraceToken, context);

		using (arena.ForceFlatIf(oneLine))
		{
			if (oneLine)
				arena.Synthetic(SyntheticText.Space);
			else
				BeforeOpenBrace(BraceStyle.ControlBlocks, context);

			PrintSwitchBody(node, context);
		}
	}

	private static void PrintSwitchBody(SwitchStatementSyntax node, PrintContext context)
	{
		var arena = context.Arena;
		TokenPrinter.Print(node.OpenBraceToken, context);

		foreach (var section in node.Sections)
		{
			using (arena.Indent(context.Options.IndentSwitchLabels ? 1 : 0))
			{
				var labelEnd = 0;
				foreach (var label in section.Labels)
				{
					arena.HardLine();
					Node.Print(label, context);
					labelEnd = label.Span.End;
				}

				// A braced body answers to csharp_indent_case_contents_when_block and everything else
				// to csharp_indent_case_contents, so the two are decided per statement rather than
				// once for the section — a section may hold both.
				foreach (var statement in section.Statements)
				{
					// `case 1: break;` — a statement on its label's line stays there.
					if (context.Options.PreserveSingleLineStatements
						&& context.OnSameLine(labelEnd, statement.Span.End))
					{
						arena.Synthetic(SyntheticText.Space);
						using (arena.ForceFlat())
							Node.Print(statement, context);
						labelEnd = statement.Span.End;
						continue;
					}

					var indented = statement is BlockSyntax
						? context.Options.IndentCaseContentsWhenBlock
						: context.Options.IndentCaseContents;

					using (arena.Indent(indented ? 1 : 0))
					{
						arena.HardLine(DocFlags.OnlyIfNotAtLineStart);
						Node.Print(statement, context);
					}
				}
			}
		}

		using (arena.IndentIf(context.Options.IndentBraces))
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
			PrintBody(node.Body, BraceStyle.LocalFunctions, context);
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
