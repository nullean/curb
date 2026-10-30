using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Nullean.Curb.Documents;
using Nullean.Curb.Options;

namespace Nullean.Curb.Printing.CSharp;

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

		// preserveLineEndings: the run is a string literal, so its newlines are part of the value.
		var span = node.Span;
		TokenPrinter.EmitVerbatimRange(context, span.Start, span.Length, preserveLineEndings: true);

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
	private static void OperandOnRight(
		SyntaxNode? right,
		PrintContext context,
		int operatorEnd = -1,
		bool suppressIndent = false)
	{
		var arena = context.Arena;

		// Where the operand starts, not whether it spans lines — see EqualsValueClause for why the
		// difference decides whether formatting twice settles.
		//
		// BreaksWithoutHelp is EqualsValueClause's own fix for the same shape (issue #34), reused
		// rather than re-derived: `x = a.B().C()` needs exactly the same "has its own break
		// opportunity, so the operator needs no hanging indent" reasoning `var x = a.B().C()`
		// already gets. Without it, an assignment whose flat RHS did not fit broke after the
		// operator and hung the RHS on its own indented line — which then measured as fitting flat
		// at that new, shallower indent, so the RHS's own chain or argument list never broke either:
		// the assignment ate the only break the line needed.
		if (right is ExpressionSyntax expression
			&& (BringsOwnBlock(expression)
				|| (operatorEnd >= 0 && context.AuthorJoined(operatorEnd, expression.SpanStart))
				|| (!context.Options.KeepExistingLinebreaks && BreaksWithoutHelp(expression))))
		{
			Spacing.BeforeOperator(context);
			Node.Print(right, context);
			return;
		}

		using (arena.Group())
		using (arena.IndentIf(!suppressIndent))
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

		// Consumed here, unconditionally: this is the first printer every BinaryExpressionSyntax
		// reaches, so a match can only mean ConditionHeader indented this exact node — or an
		// enclosing link of the same chain relayed that below — and is waiting for this node to use
		// that indent instead of adding its own. Cleared either way, so a chain nested behind a cast
		// or a prefix operator's own parentheses — a different node — still gets its own.
		var alreadyIndented = ReferenceEquals(context.IndentedCondition, node);
		if (alreadyIndented)
			context.IndentedCondition = null;

		if (TryPrintBinaryChain(node, context, alreadyIndented))
			return;

		// Relayed one more link down: the rest of a uniform chain prints through node.Left at the
		// same ambient indent, so its own continuation — reached once Left's BinaryExpression call
		// gets here in turn — is still the condition's, not a nested construct's.
		if (alreadyIndented
			&& node.Left is BinaryExpressionSyntax left
			&& left.OperatorToken.RawKind == node.OperatorToken.RawKind)
			context.IndentedCondition = left;

		// A chain the author broke stays broken, and each continuation line keeps the offset the
		// author gave it from the line the expression starts on — that is where dotnet format leaves
		// it, whether the operands are aligned under the first or one level in. Breaking to the
		// ambient indent put every operand after the first at the statement's own column (issue
		// #100). The offset is relative rather than the source column, so a statement Curb
		// re-indents takes its continuation lines along with it.
		//
		// The break can be on either side of the operator, and both shapes are common: `a &&` at the
		// end of a line, or `|| b` at the start of the next. Checking only after the operator missed
		// the operator-first style entirely and joined those chains, which is what left two corpus
		// files still reformatting themselves on a second run.
		var breakBefore = context.AuthorBroke(node.Left.Span.End, node.OperatorToken.SpanStart);
		var breakAfter = context.AuthorBroke(node.OperatorToken.Span.End, node.Right.SpanStart);

		// How far the line the expression starts on moved is only known once it is printed, so it is
		// captured there — in a register of this chain's own, since an operand may hold a chain of
		// its own that captures another before this one's continuation lines read theirs. Where Curb
		// moves the first operand onto a line of its own there is nothing to measure from, and the
		// continuation lines break to the ambient indent instead. A chain in a case label measures
		// from the switch, which captured its register already — see CaseLabelContinuation.
		var anchor = -1;
		if (breakBefore || breakAfter)
		{
			anchor = context.CaseLabelContinuation;
			if (anchor < 0)
			{
				anchor = context.EnterContinuation();
				context.Arena.LineShiftAnchor(anchor);
			}
		}

		Node.Print(node.Left, context);

		// A trailing `// note` on the left operand has already ended its own line; the aligned line
		// reuses it rather than making a blank one, which then moved the comment run below it out of
		// reach of the align-under-a-trailing-comment rule on the next run.
		var offset = 0;
		if (breakBefore)
			offset = ContinuationLine(node.OperatorToken, anchor, context);
		else
			Spacing.BeforeOperator(context);

		TokenPrinter.Print(node.OperatorToken, context);

		// The right operand's own lines — an initializer's braces, a call's arguments, a lambda's
		// block — sit as far in from its continuation line as they would from the indent. Printed
		// at the indent itself, they fell back to the left of the line they belong to.
		if (breakAfter)
		{
			offset = ContinuationLine(node.Right.GetFirstToken(), anchor, context);
			using (context.Arena.AlignedIndent(anchor, offset))
				Node.Print(node.Right, context);
		}
		else if (breakBefore)
		{
			using (context.Arena.AlignedIndent(anchor, offset))
				OperandOnRight(node.Right, context, node.OperatorToken.Span.End, alreadyIndented);
		}
		else
			OperandOnRight(node.Right, context, node.OperatorToken.Span.End, alreadyIndented);

		if (anchor >= 0 && anchor != context.CaseLabelContinuation)
			context.ExitContinuation();
	}

	/// <summary>
	/// Breaks before <paramref name="next"/>, moving it as far as the line the expression starts on
	/// moved, and returns the offset the break reads <paramref name="anchor"/> with.
	/// </summary>
	/// <remarks>
	/// A comment ahead of <paramref name="next"/> goes on the same column, as dotnet format puts it,
	/// rather than at the ambient indent with the operand stranded to its right (issue #115). A
	/// conditional-compilation directive still goes to column 0.
	/// </remarks>
	private static int ContinuationLine(SyntaxToken next, int anchor, PrintContext context, bool element = false)
	{
		var offset = context.SourceIndent(next.SpanStart);
		context.Arena.AlignedLine(anchor, offset);
		if (TokenPrinter.HasLeadingContent(next))
			context.AlignLeadingTrivia(next, anchor, offset, element);
		return offset;
	}

	/// <summary>
	/// <see cref="ContinuationLine"/> between two collection or initializer members the author put on
	/// separate lines, with the blank line they left there, as <see cref="BreakBetweenMembers"/> keeps one.
	/// </summary>
	/// <remarks>
	/// The break and the blank lines go first, and the aligned line then reuses the line they end on,
	/// since an aligned break reuses a line nothing has been written to.
	/// </remarks>
	private static int MemberContinuationLine(int previousEnd, SyntaxNode next, int anchor, PrintContext context)
	{
		BreakBetweenMembers(previousEnd, next, context);
		return ContinuationLine(next.GetFirstToken(), anchor, context, element: true);
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

		// A right side the author put below the operator stays there, as EqualsValueClause keeps one
		// below an initializer's `=` — issue #102. A value with brackets of its own included (#107).
		if (context.AuthorBroke(node.OperatorToken.Span.End, node.Right.SpanStart))
		{
			ValueBelowOperator(node.OperatorToken, node.Right, context);
			return;
		}

		TokenPrinter.Print(node.OperatorToken, context);
		OperandOnRight(node.Right, context, node.OperatorToken.Span.End);
	}

	public static void ConditionalExpression(ConditionalExpressionSyntax node, PrintContext context)
	{
		var arena = context.Arena;
		var spaced = context.Options.SpaceAroundTernaryOperator;

		// Consumed as BinaryExpression consumes it: an if/while/do/lock condition already sits inside
		// the indent that opens out its parentheses, so a broken ternary there takes no second level
		// — issue #109. One deeper in the condition, behind parentheses of its own, needs none either
		// while it still starts on the `(` line: that indent is already one level in from its line.
		var alreadyIndented = ReferenceEquals(context.IndentedCondition, node);
		if (alreadyIndented)
			context.IndentedCondition = null;
		else
			alreadyIndented = StartsOnConditionLine(node, context);

		Node.Print(node.Condition, context);

		// A ternary the author broke keeps exactly their breaks, before or after either operator. The
		// group below cannot: with no width it never breaks, so it joined them all — issue #102. And
		// once one break is hard, every Line in the group breaks with it, so the joined positions
		// need a plain space rather than a Line.
		//
		// One that spans lines only inside a branch, such as a block-bodied lambda, keeps its operators
		// inline and takes no indent: the block anchors to its line, as dotnet format leaves it.
		//
		// A block-bodied lambda that opens out as the true branch ends its own line: dotnet format puts
		// the `:` after its closing brace on a new line at the indent the ternary started from, wherever
		// the author had it — issue #106. That break is not the author's, so it neither indents the
		// ternary nor moves the block a level right of the line the lambda starts on.
		if (SpansLines(node, context))
		{
			var colonAfterBlock = OpensOutBlock(node.WhenTrue, context);
			var broken = context.AuthorBroke(node.Condition.Span.End, node.QuestionToken.SpanStart)
				|| context.AuthorBroke(node.QuestionToken.Span.End, node.WhenTrue.SpanStart)
				|| (!colonAfterBlock && context.AuthorBroke(node.WhenTrue.Span.End, node.ColonToken.SpanStart))
				|| context.AuthorBroke(node.ColonToken.Span.End, node.WhenFalse.SpanStart);
			var indented = broken && !alreadyIndented;

			using (arena.IndentIf(indented))
			{
				AuthorTernaryBreak(node.Condition.Span.End, node.QuestionToken.SpanStart, spaced, context);
				TokenPrinter.Print(node.QuestionToken, context);
				AuthorTernaryBreak(node.QuestionToken.Span.End, node.WhenTrue.SpanStart, spaced, context);
				Node.Print(node.WhenTrue, context);

				if (!colonAfterBlock)
				{
					AuthorTernaryBreak(node.WhenTrue.Span.End, node.ColonToken.SpanStart, spaced, context);
					TokenPrinter.Print(node.ColonToken, context);
					AuthorTernaryBreak(node.ColonToken.Span.End, node.WhenFalse.SpanStart, spaced, context);
					Node.Print(node.WhenFalse, context);
				}
			}

			if (colonAfterBlock)
			{
				ColonAfterBlockLine(node, context);
				TokenPrinter.Print(node.ColonToken, context);
				using (arena.IndentIf(indented && context.AuthorBroke(node.ColonToken.Span.End, node.WhenFalse.SpanStart)))
				{
					AuthorTernaryBreak(node.ColonToken.Span.End, node.WhenFalse.SpanStart, spaced, context);
					Node.Print(node.WhenFalse, context);
				}
			}
			return;
		}

		using (arena.Group())
		using (arena.Indent())
		{
			// A SoftLine renders as nothing when flat and a newline when broken — the same wrap point
			// a Line gives, minus the space a Line renders flat. Wrapping is unaffected either way;
			// only which the flat form reads as changes.
			if (spaced)
				arena.Line();
			else
				arena.SoftLine();
			TokenPrinter.Print(node.QuestionToken, context);
			if (spaced)
				arena.Synthetic(SyntheticText.Space);
			Node.Print(node.WhenTrue, context);
			if (spaced)
				arena.Line();
			else
				arena.SoftLine();
			TokenPrinter.Print(node.ColonToken, context);
			if (spaced)
				arena.Synthetic(SyntheticText.Space);
			Node.Print(node.WhenFalse, context);
		}
	}

	/// <summary>
	/// True for an operand of an if/while/do/lock condition, reached only through parentheses, operators
	/// and <c>!</c>, that starts on the line of the condition's <c>(</c>.
	/// </summary>
	private static bool StartsOnConditionLine(ExpressionSyntax node, PrintContext context)
	{
		SyntaxNode current = node;
		while (current.Parent is ParenthesizedExpressionSyntax or BinaryExpressionSyntax or PrefixUnaryExpressionSyntax)
			current = current.Parent;

		var openParen = current.Parent switch
		{
			IfStatementSyntax statement when statement.Condition == current => statement.OpenParenToken,
			WhileStatementSyntax statement when statement.Condition == current => statement.OpenParenToken,
			DoStatementSyntax statement when statement.Condition == current => statement.OpenParenToken,
			LockStatementSyntax statement when statement.Expression == current => statement.OpenParenToken,
			_ => default,
		};

		return openParen.RawKind != 0 && context.AuthorJoined(openParen.Span.End, node.SpanStart);
	}

	/// <summary>
	/// Breaks before the <c>:</c> that follows a block lambda, to the indent of the line the ternary's
	/// enclosing statement, lambda, member, switch arm or initializer element starts on.
	/// </summary>
	/// <remarks>
	/// That is where dotnet format puts it, however deep the ternary sits: an argument list adds no
	/// level of its own, so a ternary on a later argument line still has its <c>:</c> at the
	/// statement's indent rather than the argument's — issue #134. Where that construct starts
	/// mid-line there is no such line, and the break goes to the ternary's own indent instead.
	/// </remarks>
	private static void ColonAfterBlockLine(ConditionalExpressionSyntax node, PrintContext context)
	{
		var start = IndentationContextStart(node);
		if (start < 0)
		{
			context.Arena.HardLine(DocFlags.OnlyIfNotAtLineStart);
			return;
		}

		var register = context.EnterContinuation();
		context.Arena.SourceLineAnchor(register, start);
		context.Arena.AlignedLine(register);
		context.ExitContinuation();
	}

	/// <summary>
	/// Where the nearest construct that dotnet format indents its own lines from starts: a statement,
	/// a lambda, a switch expression arm, an initializer or collection element, or a member.
	/// </summary>
	private static int IndentationContextStart(SyntaxNode node)
	{
		for (var current = node.Parent; current is not null; current = current.Parent)
		{
			switch (current)
			{
				case StatementSyntax or AnonymousFunctionExpressionSyntax or SwitchExpressionArmSyntax
					or ExpressionElementSyntax or AnonymousObjectMemberDeclaratorSyntax:
					return current.SpanStart;
				case ExpressionSyntax when current.Parent is InitializerExpressionSyntax:
					return current.SpanStart;
				case MemberDeclarationSyntax member:
					return member.AttributeLists.Count == 0
						? member.SpanStart
						: member.AttributeLists[^1].GetLastToken().GetNextToken().SpanStart;
			}
		}

		return -1;
	}

	/// <summary>
	/// True for a lambda or anonymous method whose block prints opened out, the same question
	/// <see cref="PrintBody"/> answers when it prints it.
	/// </summary>
	/// <remarks>
	/// A block <c>csharp_place_simple_blocks_on_single_line</c> may collapse answers false: whether it
	/// does is measured against the width, and a hard break after a block that collapsed would strand
	/// the <c>:</c> on a line of its own.
	/// </remarks>
	private static bool OpensOutBlock(ExpressionSyntax expression, PrintContext context) =>
		expression is AnonymousFunctionExpressionSyntax { Block: { } block }
		&& !KeepsOneLine(block, context)
		&& !CollapsesEmptyBraces(block, context)
		&& !JoinsEmptyBracesToHeader(block, context)
		&& !context.Options.CollapsesSimpleBlocks;

	private static void AuthorTernaryBreak(int from, int to, bool spaced, PrintContext context)
	{
		if (context.AuthorBroke(from, to))
			context.Arena.HardLine(DocFlags.OnlyIfNotAtLineStart);
		else if (spaced)
			context.Arena.Synthetic(SyntheticText.Space);
	}

	/// <summary>Operators csharp_space_after_unary_operator does not reach even when on — measured
	/// directly against jb: bitwise complement and prefix increment/decrement stay glued to their
	/// operand regardless.</summary>
	private static readonly SyntaxKind[] UnspacedPrefixOperators =
		[SyntaxKind.TildeToken, SyntaxKind.PlusPlusToken, SyntaxKind.MinusMinusToken];

	public static void PrefixUnaryExpression(PrefixUnaryExpressionSyntax node, PrintContext context)
	{
		TokenPrinter.Print(node.OperatorToken, context);
		if (context.Options.SpaceAfterUnaryOperator && Array.IndexOf(UnspacedPrefixOperators, node.OperatorToken.Kind()) < 0)
			context.Arena.Synthetic(SyntheticText.Space);
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

		ushort argumentGroup = 0;
		if (node.ArgumentList is not null)
		{
			Spacing.BeforeCallParens(context);
			context.ArgumentListGroup = 0;
			Node.Print(node.ArgumentList, context);
			argumentGroup = context.ArgumentListGroup;
		}

		if (node.Initializer is null)
			return;

		InitializerExpression(node.Initializer, context, leadingLine: true, ownerGroup: argumentGroup);
	}

	public static void ImplicitObjectCreationExpression(ImplicitObjectCreationExpressionSyntax node, PrintContext context)
	{
		TokenPrinter.Print(node.NewKeyword, context);
		Spacing.BeforeCallParens(context);
		context.ArgumentListGroup = 0;
		Node.Print(node.ArgumentList, context);
		var argumentGroup = context.ArgumentListGroup;

		if (node.Initializer is null)
			return;

		InitializerExpression(node.Initializer, context, leadingLine: true, ownerGroup: argumentGroup);
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
	/// <param name="ownerGroup">
	/// The group of the argument list this initializer's creation carries, or 0 when it has none. The
	/// brace takes a line of its own whenever that list wrapped, which is the one thing dotnet format
	/// decides here and the only version of the question that survives deterministic layout.
	/// </param>
	public static void InitializerExpression(
		InitializerExpressionSyntax node,
		PrintContext context,
		bool leadingLine = false,
		ushort ownerGroup = 0)
	{
		var arena = context.Arena;

		var spansLines = SpansLines(node, context);
		var oneMemberPerLine = context.Options.NewLineBeforeMembersInObjectInitializers;

		// Reproduce the author's own breaks rather than imposing one member per line: an initializer
		// written across lines with two members sharing one keeps them sharing it.
		var asWritten = !oneMemberPerLine && spansLines;

		// The option says one member per line *when the initializer is opened out*, not that it must
		// be opened out. Measured: with csharp_new_line_before_members_in_object_initializers = true
		// and Allman braces, dotnet format leaves `new[] { a, b }`, `new List<int> { 1, 2, 3 }`,
		// `new Thing { X = 1 }` and `new { P = 1 }` every one of them on its line. Curb exploded all
		// four, which on roslyn — where both keys are set — was the single largest source of churn,
		// and produced things like a `new[]` at one indent with its brace at another.
		var openItOut = oneMemberPerLine && spansLines;

		// An array or collection initializer the author broke keeps exactly their breaks, as a
		// collection expression does. dotnet format leaves both as written, where it opens out an
		// object initializer that spans lines. The breakable lines below broke as soon as any member
		// did, so `new[] { a ||` / `b }` came back with its braces on lines of their own — issue #113.
		var exactly = asWritten
			&& node.Kind() is SyntaxKind.ArrayInitializerExpression or SyntaxKind.CollectionInitializerExpression;

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

		// Each line the author broke keeps the offset they gave it from the line the initializer starts
		// on, as a collection expression's does — see there.
		var anchor = -1;
		if (exactly)
		{
			anchor = context.EnterContinuation();
			arena.LineShiftAnchor(anchor);
		}

		// The creation is an element of a collection kept as the author broke it, and they put this brace
		// on a line of its own: the brace keeps that line's offset and the lines inside follow it. The
		// indent here put them a level in from the collection's line — see BrokenCollectionElement. One
		// that keeps exactly its breaks already places every line it has against an anchor of its own.
		var elementBrace = -1;
		if (leadingLine
			&& context.TryTakeElementBraceAlignment(node.OpenBraceToken, out var elementRegister)
			&& !exactly)
			elementBrace = elementRegister;

		// Outside the group below, because it has to resolve against the owner's break rather than this
		// initializer's — see BeforeOpenBraceWhenOwnerBroke. This is the deterministic half of the
		// `ownerSpansLines` question further down: that one reads where the author put the brace, which
		// is right in preservation mode and unavailable in deterministic mode, and this one reads the
		// decision the argument list took on this run, which is available in both.
		if (leadingLine && !oneMemberPerLine && elementBrace < 0)
			BeforeOpenBraceWhenOwnerBroke(BraceStyle.ObjectCollectionArrayInitializers, ownerGroup, context);

		// Named and published so an enclosing construct can aim at it — see PrintContext.OwnBlockGroup.
		var group = arena.NextGroupId();
		context.OwnBlockGroup = group;

		// Its line breaks outside the group, so that an initializer the author kept on that one line,
		// `{ Name = "a" }`, is not opened out by it.
		var aligned = default(DocScope);
		if (elementBrace >= 0)
			aligned = arena.AlignedIndent(elementBrace, ContinuationLine(node.OpenBraceToken, elementBrace, context));

		using (aligned)
		using (arena.Group(group))
		{
			// An element's brace is already on its line, above.
			if (leadingLine && elementBrace < 0)
			{
				// `new HttpClient(new Handler { … }) { Timeout = t }` — once the creation it belongs to
				// has been opened out, the initializer takes a line of its own rather than trailing
				// the closing parenthesis.
				//
				// Asked of the source, not of what the printer is about to produce. dotnet format can
				// key this on the creation spanning lines because it never reflows, so its answer
				// cannot change; Curb's reflow can open a creation out on one run, which would flip
				// the answer on the next and stop the file settling. Where the author put the brace
				// is fixed, so it is that which decides.
				var ownerSpansLines = node.Parent is not null
					&& context.AuthorBroke(node.Parent.SpanStart, node.SpanStart);

				if (exactly && ownerSpansLines)
					ContinuationLine(node.OpenBraceToken, anchor, context);
				else if (openItOut
					|| ownerSpansLines
					|| (asWritten && context.AuthorBroke(node.SpanStart, node.Parent!.SpanStart)))
					BeforeOpenBrace(BraceStyle.ObjectCollectionArrayInitializers, context);
				else if (exactly)
					arena.Synthetic(SyntheticText.Space);
				else
					BeforeOpenBraceWhenBroken(
						BraceStyle.ObjectCollectionArrayInitializers, context, DocFlags.OnlyIfNotAtLineStart);
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
				// The IsInsideArguments guard is preservation's, not the rule's: a break the source does
				// not have puts a brace-bringing construct a level out, and only a rule that reads the
				// source can be wrong about that. Deterministic mode has no such rule, so chop_always
				// applies everywhere and the count keeps its guard.
				var chopAlways = context.Options.WrapObjectAndCollectionInitializerStyle == WrapStyle.ChopAlways;
				if (chopAlways || (limit is { } elements && node.Expressions.Count > elements && !IsInsideArguments(node)))
					arena.BreakParent();

				var rewritesComma = RewritesTrailingComma(node.Expressions, node.CloseBraceToken, context);

				using (arena.Indent())
				{
					var offset = InsideBrace(node.OpenBraceToken.Span.End, node.Expressions[0].GetFirstToken());
					for (var i = 0; i < node.Expressions.Count; i++)
					{
						if (anchor < 0)
							Node.Print(node.Expressions[i], context);
						else
							BrokenCollectionElement(node.Expressions[i], anchor, offset, context);

						offset = int.MinValue;
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
							offset = Separator(node.Expressions[i].Span.End, node.Expressions[i + 1]);
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
						InsideBrace(node.Expressions[^1].Span.End, node.CloseBraceToken);
				}
			}
			else if (TokenPrinter.HasLeadingContent(node.CloseBraceToken))
			{
				// `{ // X = x }` — no members, but comments above the closing brace. They are its leading
				// trivia, which PrintWithoutLeadingTrivia below skips, so the `{ }` shape dropped them and
				// the verifier refused the file. Printed at the members' level, as the non-empty case does.
				using (arena.Indent())
				{
					arena.HardLine(DocFlags.OnlyIfNotAtLineStart);
					TokenPrinter.PrintLeadingTrivia(node.CloseBraceToken, context, trailingBreak: false);
				}

				using (arena.IndentIf(context.Options.IndentBraces))
					arena.HardLine(DocFlags.Reindent);
			}
			else
			{
				// `{ }` — nothing to break around, so the brace pair just gets air between it.
				arena.Synthetic(SyntheticText.Space);
			}

			TokenPrinter.PrintWithoutLeadingTrivia(node.CloseBraceToken, context);
		}

		if (anchor >= 0)
			context.ExitContinuation();

		// Between two members: a hard line, or the ordinary comma separator. Returns the offset of a
		// line it kept where the author broke it, for the member after it to follow.
		int Separator(int from, ExpressionSyntax next)
		{
			if (exactly && context.AuthorBroke(from, next.SpanStart))
				return MemberContinuationLine(from, next, anchor, context);

			if (openItOut || (asWritten && context.AuthorBroke(from, next.SpanStart)))
				BreakBetweenMembers(from, next, context);
			else if (asWritten)
				Spacing.AfterComma(context);
			else
				Spacing.AfterCommaBreakable(context);

			return int.MinValue;
		}

		// Just inside the braces, where no comma sits and the comma options do not reach.
		int InsideBrace(int from, SyntaxToken next)
		{
			if (exactly && context.AuthorBroke(from, next.SpanStart))
				return ContinuationLine(next, anchor, context, element: true);

			if (openItOut || (asWritten && context.AuthorBroke(from, next.SpanStart)))
				arena.HardLine();
			else if (exactly)
				arena.Synthetic(SyntheticText.Space);
			else
				arena.Line();

			return int.MinValue;
		}
	}

	/// <summary>
	/// Prints one element of a collection expression or initializer that keeps the author's breaks, whose
	/// lines read <paramref name="anchor"/>.
	/// </summary>
	/// <param name="element">The element to print.</param>
	/// <param name="anchor">The register the collection's broken lines read.</param>
	/// <param name="offset">
	/// The offset of the line of its own the author put the element on, or <see cref="int.MinValue"/> when it
	/// shares a line with the bracket or with the element before it.
	/// </param>
	/// <param name="context">Per-file printing state.</param>
	/// <remarks>
	/// A creation whose initializer brace the author put on a line of its own keeps that line where they put
	/// it too, as dotnet format does: `{ new Person` / `{ Name = "a" } }` left the brace a level in from
	/// the line the collection opens on, where the source has it at the line's own level.
	/// </remarks>
	private static void BrokenCollectionElement(SyntaxNode element, int anchor, int offset, PrintContext context)
	{
		var expression = element switch
		{
			ExpressionElementSyntax item => item.Expression,
			ExpressionSyntax value => value,
			_ => null,
		};

		if (expression is BaseObjectCreationExpressionSyntax { Initializer: { } initializer }
			&& context.AuthorBroke(initializer.OpenBraceToken.GetPreviousToken().Span.End, initializer.OpenBraceToken.SpanStart))
			context.AlignElementBrace(initializer.OpenBraceToken, anchor);

		var indent = ElementIndent(expression, offset, context);
		if (indent == int.MinValue)
		{
			Node.Print(element, context);
			return;
		}

		using (context.Arena.AlignedIndent(anchor, indent))
			Node.Print(element, context);
	}

	/// <summary>
	/// The offset an element's own lines are based on, for an element the author put on a line of its own
	/// at <paramref name="offset"/>, or that shares a line when <paramref name="offset"/> is
	/// <see cref="int.MinValue"/>.
	/// </summary>
	/// <remarks>
	/// <para>
	/// dotnet format leaves an element's own lines where the author put them. It does not move them with
	/// the element: `Create(` one, two or three columns off the grid keeps its arguments where they were
	/// (issue #138). Basing them on the element's column moved them off the grid with it.
	/// </para>
	/// <para>
	/// So a call or creation whose own arguments the author opened out after its `(` is based on where
	/// they put the first argument, a level to its left, the way an argument's continuation is read on the
	/// argument-list side. That holds for an element sharing the bracket's line as well: `[Make(` / `1),`
	/// moved `1` to a level in from the statement wherever the author aligned it. Only its own list: a
	/// nested call's arguments are a level further in than the outer list says, and reading them as the
	/// outer list's moved them a level right on every run. Any other element's lines follow the element,
	/// which is <see cref="int.MinValue"/> for one that shares a line: it follows that line.
	/// </para>
	/// </remarks>
	private static int ElementIndent(ExpressionSyntax? expression, int offset, PrintContext context)
	{
		var arguments = expression switch
		{
			InvocationExpressionSyntax invocation => invocation.ArgumentList,
			BaseObjectCreationExpressionSyntax creation => creation.ArgumentList,
			_ => null,
		};

		if (arguments is not { Arguments.Count: > 0 }
			|| !context.AuthorJoined(expression!.SpanStart, arguments.OpenParenToken.SpanStart)
			|| !context.AuthorBroke(arguments.OpenParenToken.Span.End, arguments.Arguments[0].SpanStart))
			return offset;

		return context.SourceIndent(arguments.Arguments[0].SpanStart) - context.LevelColumns;
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

		if (node.Elements.Count == 0)
		{
			TokenPrinter.Print(node.OpenBracketToken, context);
			TokenPrinter.Print(node.CloseBracketToken, context);
			return;
		}

		var asWritten = SpansLines(node, context);
		var rewritesComma = RewritesTrailingComma(node.Elements, node.CloseBracketToken, context);

		// Published for the same reason an initializer's is: `new C([…]) { X = 1 }` hands the list no
		// group to break, so the trailing initializer aims at this one instead.
		var group = arena.NextGroupId();
		context.OwnBlockGroup = group;
		var anchor = -1;

		using (arena.Group(group))
		{
			// A collection expression is the third spelling of a collection initializer, so it answers
			// to the same key. Deterministic mode only; see FormatOptions.
			if (context.Options.WrapObjectAndCollectionInitializerStyle == WrapStyle.ChopAlways)
				arena.BreakParent();

			// Only for the value of an assignment or declarator — `int[] values = [...]` — where the
			// bracket has somewhere else to go. A nested element (`[[1, 2], [3, 4]]`) or an argument
			// (`Call([1, 2])`) is already positioned by its own container, and giving it a leading
			// line of its own would put it a level out from what dotnet format produces, exactly as
			// InitializerExpression's leadingLine is only ever true for the same kind of position.
			//
			// Soft, not the brace helpers' Normal line: EqualsValueClause and OperandOnRight already
			// print the space that belongs here when this stays flat, so only the broken case needs
			// anything from this line at all — a Normal line would double that space.
			var equals = node.Parent switch
			{
				EqualsValueClauseSyntax clause => clause.EqualsToken,
				AssignmentExpressionSyntax assignment => assignment.OperatorToken,
				_ => default,
			};

			// Deterministic mode only. In preservation mode the author decided: a bracket they put below
			// the `=` is EqualsValueClause's to place, and one they started on the `=` line stays on it
			// however its contents break — the line would break with them and open out `= [1,` / `2]`
			// (issue #113).
			if (equals.RawKind != 0
				&& BraceOnOwnLine(BraceStyle.ObjectCollectionArrayInitializers, context)
				&& !context.AuthorBroke(equals.Span.End, node.SpanStart)
				&& !context.AuthorJoined(equals.Span.End, node.SpanStart))
			{
				using (arena.IndentIf(context.Options.IndentBraces))
					arena.SoftLine(DocFlags.OnlyIfNotAtLineStart);
			}

			// Each line the author broke keeps the offset they gave it from the line the bracket opens
			// on, as dotnet format leaves it: `2` aligned under `1`, or one level in, or wherever they
			// put it. Breaking to the indent instead moved an aligned element to one level.
			if (asWritten)
			{
				anchor = context.EnterContinuation();
				arena.LineShiftAnchor(anchor);
			}

			TokenPrinter.Print(node.OpenBracketToken, context);

			using (arena.Indent())
			{
				var offset = Edge(node.SpanStart, node.Elements[0].GetFirstToken());
				for (var i = 0; i < node.Elements.Count; i++)
				{
					PrintElement(node.Elements[i], offset);
					offset = int.MinValue;
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
					else if (context.AuthorJoined(node.Elements[i].Span.End, node.Elements[i + 1].SpanStart))
						Spacing.AfterComma(context);
					else
						offset = MemberContinuationLine(node.Elements[i].Span.End, node.Elements[i + 1], anchor, context);
				}

				if (rewritesComma)
					PrintTrailingComma(context);
			}

			Edge(node.Elements[^1].Span.End, node.CloseBracketToken);
		}

		TokenPrinter.Print(node.CloseBracketToken, context);

		if (anchor >= 0)
			context.ExitContinuation();

		void PrintElement(CollectionElementSyntax element, int offset)
		{
			if (anchor < 0)
				Node.Print(element, context);
			else
				BrokenCollectionElement(element, anchor, offset, context);
		}

		// An edge the author left joined stays joined. A breakable line there broke as soon as any element
		// did, which opened out `[1,` / `2]` in full — issue #113. Returns the offset of a line it broke.
		int Edge(int from, SyntaxToken next)
		{
			if (!asWritten)
				Spacing.InsideBracketsBreakable(context);
			else if (context.AuthorBroke(from, next.SpanStart))
				return ContinuationLine(next, anchor, context, element: true);
			else
				Spacing.InsideBrackets(context);

			return int.MinValue;
		}
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
			PrintAnonymousFunctionBody(block, BraceStyle.Lambdas, context);
			return;
		}

		// A body the author put on the next line stays there. Joining it here defeated reflow further
		// out: the enclosing argument list saw nothing left to preserve, emitted no break
		// opportunity, and the statement came back as one over-long line that only the next run
		// broke — which is to say, formatting twice did not settle.
		// Reindent: a trailing `// note` after the arrow has already ended its line, and a second break
		// made a blank one. The body still takes its indent on the line the comment left open.
		if (expression is not null && context.AuthorBroke(arrow.Span.End, expression.SpanStart))
		{
			using (arena.Indent())
			{
				arena.HardLine(DocFlags.Reindent);
				Node.Print(expression, context);
			}

			return;
		}

		// A body the author started on the arrow's line stays there, as an expression-bodied member's
		// does — the group below broke as soon as the body kept a break of its own, so `x => x.Flag` /
		// `? a` / `: b` gained a break after the arrow (issue #102).
		if (expression is not null && context.AuthorJoined(arrow.Span.End, expression.SpanStart))
		{
			BodyOnArrowLine(expression, context);
			return;
		}

		// An expression body that itself ends with a block-bodied callback — `builder =>
		// builder.Add(x => { … })` — already positions its own contents once that block opens.
		// Wrapping it in a breakable group as well would let the block's hardline propagate outward
		// and break the group unconditionally, pushing the whole expression to its own indented line
		// for no reason: the block was always going to supply the next line. EndsWithBlockBodiedCallback
		// rather than EndsWithOwnBlock: a `with` or object initializer at the tail can still print
		// flat, and without this group it would lose the only break a long chain ahead of it has.
		if (expression is not null && EndsWithBlockBodiedCallback(expression))
		{
			arena.Synthetic(SyntheticText.Space);
			Node.Print(expression, context);
			return;
		}

		// Same reasoning as OperandOnRight's own BreaksWithoutHelp check, one step further out: a
		// lambda arrow is an operator like any other, and a body that is itself a chain or a call
		// has somewhere to break already — `s => s.Indices(…).Query(…).Size(1)` should break at
		// those dots, not hang the whole flattened body on a line of its own where it then measures
		// as fitting and never breaks at all.
		if (expression is not null && !context.Options.KeepExistingLinebreaks && BreaksWithoutHelp(expression))
		{
			arena.Synthetic(SyntheticText.Space);
			Node.Print(expression, context);
			return;
		}

		using (arena.Group())
		using (arena.Indent())
		{
			arena.Line();
			Node.Print(expression, context);
		}
	}

	/// <summary>Prints an arrow's body on the arrow's own line, where the author started it.</summary>
	/// <remarks>
	/// Only a body whose continuation lines would otherwise sit at the arrow's own level takes an indent
	/// here. A call, a member chain, a ternary or a construct with braces of its own already positions
	/// its continuation one level in from the line it starts on, and indenting it again put
	/// `=> Call(` / `1)` two levels in, where dotnet format leaves it at one.
	/// </remarks>
	private static void BodyOnArrowLine(ExpressionSyntax body, PrintContext context)
	{
		var hangs = body is BinaryExpressionSyntax || !(BringsOwnBlock(body) || BreaksWithoutHelp(body));

		context.Arena.Synthetic(SyntheticText.Space);
		using (context.Arena.IndentIf(hangs))
			Node.Print(body, context);
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

		PrintAnonymousFunctionBody(node.Block, BraceStyle.AnonymousMethods, context);
	}

	/// <summary>
	/// Prints a lambda's or anonymous method's block, keeping a brace the author put on a line of its
	/// own at the offset they gave it from the line the function starts on.
	/// </summary>
	/// <remarks>
	/// dotnet format leaves such a block where the author put it, one level in, two or flush with the
	/// line, and moves it as far as that line moves. Curb anchored it to the line's indent and pulled
	/// it back — issue #135. The block's statements and closing brace follow the opening brace, as they
	/// would from the indent.
	/// </remarks>
	private static void PrintAnonymousFunctionBody(BlockSyntax block, BraceStyle construct, PrintContext context)
	{
		var column = OwnLineBraceColumn(block, construct, context);
		if (column < 0)
		{
			PrintBody(block, construct, context);
			return;
		}

		var register = context.EnterContinuation();
		context.Arena.LineShiftAnchor(register);
		using (context.Arena.AlignedIndent(register, column))
			PrintBody(block, construct, context);
		context.ExitContinuation();
	}

	public static void AnonymousObjectCreationExpression(AnonymousObjectCreationExpressionSyntax node, PrintContext context)
	{
		var arena = context.Arena;

		// Either key opens an anonymous type out, which is Roslyn's behaviour rather than its
		// documentation: with only csharp_new_line_before_members_in_object_initializers set,
		// dotnet format still puts an opened-out `new { First = 1, Second = 2 }` one member per
		// line. Curb treated the two keys as independent and kept the members together, which is
		// the one expectation in the suite that dotnet format moved.
		var oneMemberPerLine = context.Options.NewLineBeforeMembersInAnonymousTypes
			|| context.Options.NewLineBeforeMembersInObjectInitializers;

		// Same rule as the object initializer above: one member per line *when the type is
		// opened out*, not a reason to open it. dotnet format leaves `new { P = 1, Q = 2 }` alone
		// with either key set.
		var openItOut = oneMemberPerLine && SpansLines(node, context);
		var asWritten = !oneMemberPerLine && SpansLines(node, context);
		var rewritesComma = RewritesTrailingComma(node.Initializers, node.CloseBraceToken, context);

		TokenPrinter.Print(node.NewKeyword, context);

		using (arena.Group())
		{
			if (openItOut || (asWritten && context.AuthorBroke(node.NewKeyword.Span.End, node.OpenBraceToken.SpanStart)))
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
			if (openItOut || (asWritten && context.AuthorBroke(from, to)))
				arena.HardLine();
			else if (asWritten)
				Spacing.AfterComma(context);
			else
				Spacing.AfterCommaBreakable(context);
		}

		void InsideBrace(int from, int to)
		{
			if (openItOut || (asWritten && context.AuthorBroke(from, to)))
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

		// A bracketed list the author broke keeps their breaks, as an invocation's or an attribute's
		// does. Printed flat it was joined onto one line however it was written — issue #124.
		var arguments = node.Arguments;
		if (arguments.Count > 0 && SpansLines(node, context))
		{
			using (context.Arena.Indent())
			{
				if (context.AuthorBroke(node.OpenBracketToken.Span.End, arguments[0].SpanStart))
					context.Arena.HardLine(DocFlags.OnlyIfNotAtLineStart);
				else
					Spacing.InsideBrackets(context);

				PrintSeparated(arguments, context, asWritten: true, node.OpenBracketToken.Span.End);
			}

			if (context.AuthorBroke(arguments[^1].Span.End, node.CloseBracketToken.SpanStart))
				context.Arena.HardLine(DocFlags.OnlyIfNotAtLineStart);
			else
				Spacing.InsideBrackets(context);

			TokenPrinter.Print(node.CloseBracketToken, context);
			return;
		}

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

		// dotnet format files this brace under the initializers flag, and moves it either way regardless
		// of where the author put it.
		BeforeOpenBrace(BraceStyle.ObjectCollectionArrayInitializers, context);

		TokenPrinter.Print(node.OpenBraceToken, context);

		var rewritesComma = RewritesTrailingComma(node.Arms, node.CloseBraceToken, context);

		// The arms and the closing brace indent from the line the opening brace sits on, so when
		// csharp_indent_braces moved that brace in, everything after it moves in with it.
		using (arena.IndentIf(IndentsOpenBrace(BraceStyle.ObjectCollectionArrayInitializers, context)))
		{
			using (arena.Indent())
			{
				for (var i = 0; i < node.Arms.Count; i++)
				{
					// Arms always break, so the first is the only one with no blank line to keep.
					if (i == 0)
						arena.HardLine();
					else
						BreakBetweenMembers(node.Arms[i - 1].Span.End, node.Arms[i], context);
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

		// An arrow the author put below the pattern stays below it, one level in, as dotnet format leaves
		// it. Joined, `_ when a > 3` / `=> 2` lost a break the author chose. The body then lays out from
		// the arrow's line.
		var arrowBroken = context.AuthorBroke(
			node.WhenClause?.Condition.Span.End ?? node.Pattern.Span.End, node.EqualsGreaterThanToken.SpanStart);

		using (arena.IndentIf(arrowBroken))
		{
			if (arrowBroken)
				arena.HardLine(DocFlags.Reindent);
			else
				arena.Synthetic(SyntheticText.Space);

			TokenPrinter.Print(node.EqualsGreaterThanToken, context);
			SwitchExpressionArmBody(node, context);
		}
	}

	/// <summary>The part of a switch expression arm after its <c>=&gt;</c>.</summary>
	private static void SwitchExpressionArmBody(SwitchExpressionArmSyntax node, PrintContext context)
	{
		var arena = context.Arena;

		// A nested switch expression hugs the arrow, as it does an expression body's. It always breaks,
		// so the group below would always push it onto a line of its own, which dotnet format never does.
		// A break the author put there is still theirs.
		if (node.Expression is SwitchExpressionSyntax
			&& !context.AuthorBroke(node.EqualsGreaterThanToken.Span.End, node.Expression.SpanStart))
		{
			arena.Synthetic(SyntheticText.Space);
			Node.Print(node.Expression, context);
			return;
		}

		// Same as a lambda's body: one the author started on the arrow's line stays there.
		if (context.AuthorJoined(node.EqualsGreaterThanToken.Span.End, node.Expression.SpanStart))
		{
			BodyOnArrowLine(node.Expression, context);
			return;
		}

		// And one the author put below the arrow stays below it, as a lambda's does. The group below never
		// breaks with no width, so it joined `0 =>` / `[1]`, and a trailing `// note` after the arrow
		// left the body stranded one column in from the arm.
		if (context.AuthorBroke(node.EqualsGreaterThanToken.Span.End, node.Expression.SpanStart))
		{
			using (arena.Indent())
			{
				arena.HardLine(DocFlags.Reindent);
				Node.Print(node.Expression, context);
			}

			return;
		}

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
