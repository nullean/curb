using System.Diagnostics;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Nullean.Kerf.Options;
using Nullean.Kerf.Documents;

namespace Nullean.Kerf.Printing.CSharp;

/// <summary>
/// The syntax printers.
/// </summary>
/// <remarks>
/// Grouped in one file while the set is small; each becomes its own file as coverage grows, so that
/// adding a node type stays an isolated change. Printers never read <see cref="FormatOptions"/>
/// directly — layout choices that an option will eventually govern go through a helper, so wiring
/// that option up later touches one place rather than every call site.
/// </remarks>
internal static partial class Printers
{
	public static void CompilationUnit(CompilationUnitSyntax node, PrintContext context)
	{
		var arena = context.Arena;
		var previousEnd = -1;

		// Source order is externs, then usings, then assembly-level attributes, then members.
		// Emitting the attributes first reorders the file, which the content verifier rightly
		// rejects -- and it moves the licence header that is attached to whatever comes first.
		foreach (var externAlias in node.Externs)
		{
			Separate(context, ref previousEnd, externAlias);
			Node.Print(externAlias, context);
		}

		foreach (var directive in node.Usings)
		{
			Separate(context, ref previousEnd, directive);
			Node.Print(directive, context);
		}

		foreach (var attributeList in node.AttributeLists)
		{
			Separate(context, ref previousEnd, attributeList);
			Node.Print(attributeList, context);
		}

		foreach (var member in node.Members)
		{
			Separate(context, ref previousEnd, member);
			Node.Print(member, context);
		}

		// Trivia hanging off the end-of-file token is not a member, so no separator ran for it and
		// the blank line above a trailing comment would be dropped. Treat it like an item.
		if (previousEnd >= 0 && TokenPrinter.HasLeadingContent(node.EndOfFileToken))
		{
			arena.HardLine(DocFlags.OnlyIfNotAtLineStart);
			if (context.BlankLinesBetween(previousEnd, EffectiveTriviaStart(node.EndOfFileToken)) > 0)
				arena.HardLine();
		}

		TokenPrinter.PrintIfPresent(node.EndOfFileToken, context);
	}

	public static void UsingDirective(UsingDirectiveSyntax node, PrintContext context)
	{
		if (node.GlobalKeyword.RawKind != 0)
		{
			TokenPrinter.Print(node.GlobalKeyword, context);
			context.Arena.Synthetic(SyntheticText.Space);
		}

		TokenPrinter.Print(node.UsingKeyword, context);
		context.Arena.Synthetic(SyntheticText.Space);

		if (node.StaticKeyword.RawKind != 0)
		{
			TokenPrinter.Print(node.StaticKeyword, context);
			context.Arena.Synthetic(SyntheticText.Space);
		}

		if (node.Alias is not null)
		{
			Tokens(node.Alias.Name, context);
			context.Arena.Synthetic(SyntheticText.Space);
			TokenPrinter.Print(node.Alias.EqualsToken, context);
			context.Arena.Synthetic(SyntheticText.Space);
		}

		Tokens(node.NamespaceOrType, context);
		TokenPrinter.Print(node.SemicolonToken, context);
	}

	public static void FileScopedNamespace(FileScopedNamespaceDeclarationSyntax node, PrintContext context)
	{
		TokenPrinter.Print(node.NamespaceKeyword, context);
		context.Arena.Synthetic(SyntheticText.Space);
		Tokens(node.Name, context);
		TokenPrinter.Print(node.SemicolonToken, context);

		var previousEnd = node.SemicolonToken.Span.End;
		foreach (var member in node.Members)
		{
			Separate(context, ref previousEnd, member);
			Node.Print(member, context);
		}
	}

	public static void TypeDeclaration(TypeDeclarationSyntax node, PrintContext context)
	{
		var arena = context.Arena;

		PrintAttributeLists(node.AttributeLists, context);
		PrintModifiers(node.Modifiers, context);
		TokenPrinter.Print(node.Keyword, context);
		if (node.Identifier.RawKind != 0)
			arena.Synthetic(SyntheticText.Space);

		// `record struct` and `record class` carry a second keyword that Keyword does not include.
		if (node is RecordDeclarationSyntax { ClassOrStructKeyword.RawKind: not 0 } record)
		{
			TokenPrinter.Print(record.ClassOrStructKeyword, context);
			arena.Synthetic(SyntheticText.Space);
		}

		// An extension block is shaped like a type declaration but has no name.
		TokenPrinter.PrintIfPresent(node.Identifier, context);

		if (node.TypeParameterList is not null)
			Node.Print(node.TypeParameterList, context);
		if (node.ParameterList is not null)
		{
			// A primary constructor's parameter list.
			Spacing.BeforeDeclarationParens(context);
			Node.Print(node.ParameterList, context);
		}

		if (node.BaseList is not null)
		{
			Spacing.BeforeInheritanceColon(context);
			Node.Print(node.BaseList, context);
		}
		foreach (var constraint in node.ConstraintClauses)
		{
			arena.Synthetic(SyntheticText.Space);
			Node.Print(constraint, context);
		}

		if (node.OpenBraceToken.RawKind == 0)
		{
			TokenPrinter.PrintIfPresent(node.SemicolonToken, context);
			return;
		}

		BeforeOpenBrace(BraceStyle.Types, context);
		TokenPrinter.Print(node.OpenBraceToken, context);

		using (arena.Indent())
		{
			var previousEnd = node.OpenBraceToken.Span.End;
			foreach (var member in node.Members)
			{
				arena.HardLine(DocFlags.OnlyIfNotAtLineStart);
				if (context.BlankLinesBetween(previousEnd, EffectiveStart(member)) > 0)
					arena.HardLine();
				Node.Print(member, context);
				previousEnd = member.Span.End;
			}

			if (TokenPrinter.HasLeadingContent(node.CloseBraceToken))
			{
				arena.HardLine(DocFlags.OnlyIfNotAtLineStart);
				if (context.BlankLinesBetween(previousEnd, EffectiveTriviaStart(node.CloseBraceToken)) > 0)
					arena.HardLine();
				TokenPrinter.PrintLeadingTrivia(node.CloseBraceToken, context, trailingBreak: false);
			}
		}

		arena.HardLine(DocFlags.Reindent);
		TokenPrinter.PrintWithoutLeadingTrivia(node.CloseBraceToken, context);
		TokenPrinter.PrintIfPresent(node.SemicolonToken, context);
	}

	public static void MethodDeclaration(MethodDeclarationSyntax node, PrintContext context)
	{
		var arena = context.Arena;

		PrintAttributeLists(node.AttributeLists, context);
		PrintModifiers(node.Modifiers, context);
		Node.Print(node.ReturnType, context);
		arena.Synthetic(SyntheticText.Space);

		if (node.ExplicitInterfaceSpecifier is not null)
			Tokens(node.ExplicitInterfaceSpecifier, context);

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
			BeforeOpenBrace(BraceStyle.Methods, context);
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

	public static void ArrowExpressionClause(ArrowExpressionClauseSyntax node, PrintContext context)
	{
		var arena = context.Arena;
		TokenPrinter.Print(node.ArrowToken, context);

		// Same rule as an initializer: a body that brings its own braces positions its own contents,
		// so `=> x switch { … }` keeps the header on one line rather than breaking after the arrow
		// and indenting the whole construct a level too deep.
		if (BringsOwnBlock(node.Expression))
		{
			arena.Synthetic(SyntheticText.Space);
			Node.Print(node.Expression, context);
			return;
		}

		using (arena.Group())
		using (arena.Indent())
		{
			arena.Line();
			Node.Print(node.Expression, context);
		}
	}

	public static void TypeParameterList(TypeParameterListSyntax node, PrintContext context)
	{
		TokenPrinter.Print(node.LessThanToken, context);

		for (var i = 0; i < node.Parameters.Count; i++)
		{
			Node.Print(node.Parameters[i], context);
			if (i >= node.Parameters.SeparatorCount)
				continue;
			TokenPrinter.Print(node.Parameters.GetSeparator(i), context);
			context.Arena.Synthetic(SyntheticText.Space);
		}

		TokenPrinter.Print(node.GreaterThanToken, context);
	}

	public static void TypeParameter(TypeParameterSyntax node, PrintContext context)
	{
		foreach (var attributeList in node.AttributeLists)
		{
			Node.Print(attributeList, context);
			context.Arena.Synthetic(SyntheticText.Space);
		}

		// `in` / `out` variance; without the space this becomes part of the parameter's name.
		if (node.VarianceKeyword.RawKind != 0)
		{
			TokenPrinter.Print(node.VarianceKeyword, context);
			context.Arena.Synthetic(SyntheticText.Space);
		}

		TokenPrinter.Print(node.Identifier, context);
	}

	public static void ParameterList(ParameterListSyntax node, PrintContext context)
	{
		var arena = context.Arena;
		TokenPrinter.Print(node.OpenParenToken, context);

		if (node.Parameters.Count > 0)
		{
			using (arena.Group())
			{
				using (arena.Indent())
				{
					Spacing.InsideDeclarationParensBreakable(context);
					PrintSeparated(node.Parameters, context);
				}

				Spacing.InsideDeclarationParensBreakable(context);
			}
		}
		else
			Spacing.InsideEmptyDeclarationParens(context);

		TokenPrinter.Print(node.CloseParenToken, context);
	}

	public static void Parameter(ParameterSyntax node, PrintContext context)
	{
		foreach (var attributeList in node.AttributeLists)
		{
			Node.Print(attributeList, context);
			context.Arena.Synthetic(SyntheticText.Space);
		}

		PrintModifiers(node.Modifiers, context);

		if (node.Type is not null)
		{
			Node.Print(node.Type, context);
			context.Arena.Synthetic(SyntheticText.Space);
		}

		TokenPrinter.Print(node.Identifier, context);

		// EqualsValueClause supplies its own leading space.
		Node.Print(node.Default, context);
	}

	public static void Block(BlockSyntax node, PrintContext context)
	{
		var arena = context.Arena;
		TokenPrinter.Print(node.OpenBraceToken, context);

		using (arena.Indent())
		{
			var previousEnd = node.OpenBraceToken.Span.End;
			foreach (var statement in node.Statements)
			{
				arena.HardLine(DocFlags.OnlyIfNotAtLineStart);
				if (context.BlankLinesBetween(previousEnd, EffectiveStart(statement)) > 0)
					arena.HardLine();
				Node.Print(statement, context);
				previousEnd = statement.Span.End;
			}

			// Trivia attached to the closing brace belongs with the statements it follows, not with
			// the brace, and the blank line above it is preserved like any other separator. A whole
			// `#if` block whose branch is disabled arrives here too, since none of it is parsed.
			if (TokenPrinter.HasLeadingContent(node.CloseBraceToken))
			{
				arena.HardLine(DocFlags.OnlyIfNotAtLineStart);
				if (context.BlankLinesBetween(previousEnd, EffectiveTriviaStart(node.CloseBraceToken)) > 0)
					arena.HardLine();
				TokenPrinter.PrintLeadingTrivia(node.CloseBraceToken, context, trailingBreak: false);
			}
		}

		arena.HardLine(DocFlags.Reindent);
		TokenPrinter.PrintWithoutLeadingTrivia(node.CloseBraceToken, context);
	}

	public static void ExpressionStatement(ExpressionStatementSyntax node, PrintContext context)
	{
		Node.Print(node.Expression, context);
		TokenPrinter.Print(node.SemicolonToken, context);
	}

	public static void ReturnStatement(ReturnStatementSyntax node, PrintContext context)
	{
		TokenPrinter.Print(node.ReturnKeyword, context);
		if (node.Expression is not null)
		{
			context.Arena.Synthetic(SyntheticText.Space);
			Node.Print(node.Expression, context);
		}
		TokenPrinter.Print(node.SemicolonToken, context);
	}

	public static void InvocationExpression(InvocationExpressionSyntax node, PrintContext context)
	{
		Node.Print(node.Expression, context);
		Spacing.BeforeCallParens(context);
		Node.Print(node.ArgumentList, context);
	}

	public static void MemberAccessExpression(MemberAccessExpressionSyntax node, PrintContext context)
	{
		Node.Print(node.Expression, context);
		Spacing.BeforeDot(context);
		TokenPrinter.Print(node.OperatorToken, context);
		Spacing.AfterDot(context);
		Node.Print(node.Name, context);
	}

	public static void ArgumentList(ArgumentListSyntax node, PrintContext context)
	{
		var arena = context.Arena;
		TokenPrinter.Print(node.OpenParenToken, context);

		if (node.Arguments.Count > 0)
		{
			using (arena.Group())
			{
				using (arena.Indent())
				{
					Spacing.InsideCallParensBreakable(context);
					PrintSeparated(node.Arguments, context);
				}

				Spacing.InsideCallParensBreakable(context);
			}
		}
		else
			Spacing.InsideEmptyCallParens(context);

		TokenPrinter.Print(node.CloseParenToken, context);
	}

	public static void Argument(ArgumentSyntax node, PrintContext context)
	{
		if (node.NameColon is not null)
		{
			Tokens(node.NameColon, context);
			context.Arena.Synthetic(SyntheticText.Space);
		}
		if (node.RefKindKeyword.RawKind != 0)
		{
			TokenPrinter.Print(node.RefKindKeyword, context);
			context.Arena.Synthetic(SyntheticText.Space);
		}

		Node.Print(node.Expression, context);
	}

	/// <summary>
	/// Emits every token of a node in order, with its trivia, and no layout decisions.
	/// </summary>
	/// <remarks>
	/// This concatenates tokens with NO separator, so it is correct only where no interior space is
	/// significant: dotted names, type parameter lists, literals, <c>name:</c> labels. Using it on
	/// anything containing a keyword produces garbage — <c>where T : class</c> would come out as
	/// <c>whereT:class</c>. Everything else goes through <see cref="Node.Print"/>, which falls back
	/// to emitting the node verbatim.
	/// </remarks>
	public static void Tokens(SyntaxNode? node, PrintContext context)
	{
		if (node is null)
			return;

		var previousEnd = -1;
		var previousLast = '\0';

		foreach (var token in node.DescendantTokens())
		{
			if (token.Span.Length == 0)
				continue;

			GuardAgainstWelding(node, token, previousEnd, previousLast, context);
			TokenPrinter.Print(token, context);

			previousEnd = token.Span.End;
			previousLast = context.Text[token.Span.End - 1];

			// Separators still need their space; csharp_space_after_comma will govern this once the
			// option is wired up, and its default is true.
			if (!token.IsKind(SyntaxKind.CommaToken))
				continue;

			context.Arena.Synthetic(SyntheticText.Space);
			previousLast = ' ';
		}
	}

	/// <summary>
	/// Fails loudly when <see cref="Tokens"/> is used somewhere it would merge two tokens.
	/// </summary>
	/// <remarks>
	/// Three content bugs have come from this helper: <c>out var</c> became <c>outvar</c>,
	/// <c>out TGrouping</c> became <c>outTGrouping</c>, and <c>case Colour.Red</c> became
	/// <c>caseColour.Red</c>. Each was caught downstream by the re-parse comparer, after the fact.
	/// This turns the next one into an immediate, located failure naming the node that needs a real
	/// printer. Debug-only: the release path pays nothing.
	/// </remarks>
	[Conditional("DEBUG")]
	private static void GuardAgainstWelding(
		SyntaxNode node,
		SyntaxToken token,
		int previousEnd,
		char previousLast,
		PrintContext context)
	{
		if (previousEnd < 0 || token.SpanStart <= previousEnd)
			return;

		if (!WeldDetector.CanWeld(previousLast, context.Text[token.SpanStart]))
			return;

		throw new InvalidOperationException(
			$"Printers.Tokens would merge '{previousLast}' and '{context.Text[token.SpanStart]}' in a "
			+ $"{node.Kind()}; it emits tokens with no separator and is only safe where the source had "
			+ "none either. This node needs a printer of its own.");
	}

	/// <summary>Emits attribute lists, each on its own line above the declaration.</summary>
	private static void PrintAttributeLists(SyntaxList<AttributeListSyntax> attributeLists, PrintContext context)
	{
		foreach (var attributeList in attributeLists)
		{
			Node.Print(attributeList, context);
			context.Arena.HardLine();
		}
	}

	/// <summary>
	/// Emits whatever separates a construct's header from its opening brace.
	/// </summary>
	/// <remarks>
	/// The one place <c>csharp_new_line_before_open_brace</c> is consulted. Its default is
	/// <c>all</c> — Allman — and <c>none</c> gives K&amp;R; a comma-separated list lets the two be
	/// mixed per construct. Printers call this rather than emitting a break of their own, so wiring
	/// a new construct into the option is a one-line change here rather than a hunt.
	/// </remarks>
	internal static void BeforeOpenBrace(BraceStyle construct, PrintContext context)
	{
		if (context.Options.NewLineBeforeOpenBrace.HasFlag(construct))
			context.Arena.HardLine();
		else
			context.Arena.Synthetic(SyntheticText.Space);
	}

	/// <summary>
	/// The <see cref="BeforeOpenBrace"/> variant for braces that only move to their own line when
	/// their group breaks — accessor lists, initializers, anonymous types.
	/// </summary>
	/// <remarks>
	/// A flat <c>{ get; set; }</c> keeps its brace on the line either way; the flag only decides what
	/// happens once the contents no longer fit. With the flag off the brace can never break away, so
	/// the separator degrades to a plain space.
	/// </remarks>
	internal static void BeforeOpenBraceWhenBroken(BraceStyle construct, PrintContext context)
	{
		if (context.Options.NewLineBeforeOpenBrace.HasFlag(construct))
			context.Arena.Line();
		else
			context.Arena.Synthetic(SyntheticText.Space);
	}

	/// <summary>
	/// Emits whatever separates a continuation keyword — <c>else</c>, <c>catch</c>, <c>finally</c> —
	/// from what came before it.
	/// </summary>
	/// <remarks>
	/// The three options behind this only ever pull the keyword up onto a closing brace: dotnet
	/// format leaves <c>else</c> on its own line after a braceless <c>if</c> whatever the option
	/// says, since there is no brace there to join.
	/// </remarks>
	internal static void BeforeContinuation(bool onItsOwnLine, bool followsABrace, PrintContext context)
	{
		if (onItsOwnLine || !followsABrace)
			context.Arena.HardLine();
		else
			context.Arena.Synthetic(SyntheticText.Space);
	}

	private static void PrintModifiers(SyntaxTokenList modifiers, PrintContext context)
	{
		foreach (var modifier in modifiers)
		{
			TokenPrinter.Print(modifier, context);
			context.Arena.Synthetic(SyntheticText.Space);
		}
	}

	/// <summary>Emits a comma-separated list, breaking one-per-line when the group does not fit.</summary>
	private static void PrintSeparated<T>(SeparatedSyntaxList<T> list, PrintContext context)
		where T : SyntaxNode
	{
		for (var i = 0; i < list.Count; i++)
		{
			Node.Print(list[i], context);

			if (i >= list.SeparatorCount)
				continue;

			Spacing.BeforeComma(context);
			TokenPrinter.Print(list.GetSeparator(i), context);
			Spacing.AfterCommaBreakable(context);
		}
	}

	/// <summary>Emits the separator between two top-level items, preserving at most one blank line.</summary>
	private static void Separate(PrintContext context, ref int previousEnd, SyntaxNode next)
	{
		if (previousEnd < 0)
		{
			previousEnd = next.Span.End;
			return;
		}

		context.Arena.HardLine(DocFlags.OnlyIfNotAtLineStart);
		if (context.BlankLinesBetween(previousEnd, EffectiveStart(next)) > 0)
			context.Arena.HardLine();
		previousEnd = next.Span.End;
	}

	/// <summary>
	/// Where a node effectively starts for blank-line purposes: its first comment or directive if it
	/// has one, otherwise the node itself. Measuring to the node would count a comment's own line as
	/// a blank one.
	/// </summary>
	/// <summary>Where a token's first comment or directive begins, for blank-line measurement.</summary>
	private static int EffectiveTriviaStart(SyntaxToken token)
	{
		foreach (var trivia in token.LeadingTrivia)
		{
			if (trivia.Kind() is not (SyntaxKind.WhitespaceTrivia or SyntaxKind.EndOfLineTrivia))
				return trivia.SpanStart;
		}
		return token.SpanStart;
	}

	private static int EffectiveStart(SyntaxNode node)
	{
		foreach (var trivia in node.GetLeadingTrivia())
		{
			if (trivia.Kind() is not (SyntaxKind.WhitespaceTrivia or SyntaxKind.EndOfLineTrivia))
				return trivia.SpanStart;
		}
		return node.SpanStart;
	}
}
