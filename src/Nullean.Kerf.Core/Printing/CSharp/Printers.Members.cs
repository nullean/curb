using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Nullean.Kerf.Documents;
using Nullean.Kerf.Options;

namespace Nullean.Kerf.Printing.CSharp;

/// <summary>Enums, operators, indexers, delegates and block-scoped namespaces.</summary>
internal static partial class Printers
{
	public static void EnumDeclaration(EnumDeclarationSyntax node, PrintContext context)
	{
		var arena = context.Arena;

		PrintAttributeLists(node.AttributeLists, context);
		PrintModifiers(node.Modifiers, context);
		TokenPrinter.Print(node.EnumKeyword, context);
		arena.Synthetic(SyntheticText.Space);
		TokenPrinter.Print(node.Identifier, context);

		if (node.BaseList is not null)
		{
			PrintBaseList(node.BaseList, context);
		}

		var oneLine = KeepsOneLine(node.OpenBraceToken, node.CloseBraceToken, context)
			&& !HeaderWasBroken(node.BaseList, constraintCount: 0, context);

		using (arena.ForceFlatIf(oneLine))
		{
			if (oneLine)
				arena.Synthetic(SyntheticText.Space);
			else
				BeforeOpenBrace(BraceStyle.Types, context);

			PrintEnumBody(node, context);
		}
	}

	private static void PrintEnumBody(EnumDeclarationSyntax node, PrintContext context)
	{
		var arena = context.Arena;
		TokenPrinter.Print(node.OpenBraceToken, context);

		var rewritesComma = RewritesTrailingComma(node.Members, node.CloseBraceToken, context);

		using (arena.Indent())
		{
			var previousEnd = node.OpenBraceToken.Span.End;
			for (var i = 0; i < node.Members.Count; i++)
			{
				arena.HardLine(DocFlags.OnlyIfNotAtLineStart);
				context.BlankLines(context.DeclarationSeparation(previousEnd, EffectiveStart(node.Members[i])));

				Node.Print(node.Members[i], context);
				previousEnd = node.Members[i].Span.End;

				if (rewritesComma && i == node.Members.Count - 1)
					continue;

				if (i < node.Members.SeparatorCount)
					TokenPrinter.Print(node.Members.GetSeparator(i), context);
			}

			if (rewritesComma)
				PrintTrailingComma(context);
		}

		using (arena.IndentIf(context.Options.IndentBraces))
			arena.HardLine();

		TokenPrinter.Print(node.CloseBraceToken, context);
		TokenPrinter.PrintIfPresent(node.SemicolonToken, context);
	}

	public static void EnumMemberDeclaration(EnumMemberDeclarationSyntax node, PrintContext context)
	{
		PrintAttributeLists(node.AttributeLists, context);
		TokenPrinter.Print(node.Identifier, context);
		Node.Print(node.EqualsValue, context);
	}

	public static void OperatorDeclaration(OperatorDeclarationSyntax node, PrintContext context)
	{
		var arena = context.Arena;

		PrintAttributeLists(node.AttributeLists, context);
		PrintModifiers(node.Modifiers, context);
		Node.Print(node.ReturnType, context);
		arena.Synthetic(SyntheticText.Space);
		TokenPrinter.Print(node.OperatorKeyword, context);
		arena.Synthetic(SyntheticText.Space);
		TokenPrinter.Print(node.OperatorToken, context);
		Spacing.BeforeDeclarationParens(context);
		Node.Print(node.ParameterList, context);

		if (node.Body is not null)
		{
			if (!TryPrintExpressionBody(node.Body, context.Options.ExpressionBodiedOperators, context))
				PrintBody(node.Body, BraceStyle.Methods, context);
			return;
		}

		if (node.ExpressionBody is not null)
		{
			arena.Synthetic(SyntheticText.Space);
			Node.Print(node.ExpressionBody, context);
		}

		TokenPrinter.PrintIfPresent(node.SemicolonToken, context);
	}

	public static void ConversionOperatorDeclaration(ConversionOperatorDeclarationSyntax node, PrintContext context)
	{
		var arena = context.Arena;

		PrintAttributeLists(node.AttributeLists, context);
		PrintModifiers(node.Modifiers, context);
		TokenPrinter.Print(node.ImplicitOrExplicitKeyword, context);
		arena.Synthetic(SyntheticText.Space);
		TokenPrinter.Print(node.OperatorKeyword, context);
		arena.Synthetic(SyntheticText.Space);
		Node.Print(node.Type, context);
		Spacing.BeforeDeclarationParens(context);
		Node.Print(node.ParameterList, context);

		if (node.Body is not null)
		{
			if (!TryPrintExpressionBody(node.Body, context.Options.ExpressionBodiedOperators, context))
				PrintBody(node.Body, BraceStyle.Methods, context);
			return;
		}

		if (node.ExpressionBody is not null)
		{
			arena.Synthetic(SyntheticText.Space);
			Node.Print(node.ExpressionBody, context);
		}

		TokenPrinter.PrintIfPresent(node.SemicolonToken, context);
	}

	public static void IndexerDeclaration(IndexerDeclarationSyntax node, PrintContext context)
	{
		var arena = context.Arena;

		PrintAttributeLists(node.AttributeLists, context);
		PrintModifiers(node.Modifiers, context);
		Node.Print(node.Type, context);
		arena.Synthetic(SyntheticText.Space);

		if (node.ExplicitInterfaceSpecifier is not null)
			Tokens(node.ExplicitInterfaceSpecifier, context);

		TokenPrinter.Print(node.ThisKeyword, context);
		Node.Print(node.ParameterList, context);

		Node.Print(node.AccessorList, context);

		if (node.ExpressionBody is not null)
		{
			arena.Synthetic(SyntheticText.Space);
			Node.Print(node.ExpressionBody, context);
		}

		TokenPrinter.PrintIfPresent(node.SemicolonToken, context);
	}

	public static void BracketedParameterList(BracketedParameterListSyntax node, PrintContext context)
	{
		Spacing.BeforeOpenBracket(context);
		TokenPrinter.Print(node.OpenBracketToken, context);
		Spacing.InsideBrackets(context);

		for (var i = 0; i < node.Parameters.Count; i++)
		{
			Node.Print(node.Parameters[i], context);
			if (i >= node.Parameters.SeparatorCount)
				continue;
			Spacing.BeforeComma(context);
			TokenPrinter.Print(node.Parameters.GetSeparator(i), context);
			Spacing.AfterComma(context);
		}

		Spacing.InsideBrackets(context);
		TokenPrinter.Print(node.CloseBracketToken, context);
	}

	public static void NamespaceDeclaration(NamespaceDeclarationSyntax node, PrintContext context)
	{
		var arena = context.Arena;

		TokenPrinter.Print(node.NamespaceKeyword, context);
		arena.Synthetic(SyntheticText.Space);
		Node.Print(node.Name, context);

		if (CanBeFileScoped(node, context))
		{
			PrintAsFileScoped(node, context);
			return;
		}

		var oneLine = KeepsOneLine(node.OpenBraceToken, node.CloseBraceToken, context);

		using (arena.ForceFlatIf(oneLine))
		{
			if (oneLine)
				arena.Synthetic(SyntheticText.Space);
			else
				BeforeOpenBrace(BraceStyle.Types, context);

			PrintNamespaceBody(node, context);
		}
	}

	/// <summary>
	/// Whether this block namespace may be rewritten as a file-scoped one.
	/// </summary>
	/// <remarks>
	/// A file-scoped namespace takes everything after it to the end of the file, so the conversion is
	/// only meaning-preserving when there is nothing else at the top level and nothing nested that
	/// declares a namespace of its own. Both are decidable from the tree, which is why Kerf can answer
	/// this without a compilation — and both are refusals rather than best guesses, because getting
	/// either wrong moves types into a namespace they were never in.
	/// </remarks>
	private static bool CanBeFileScoped(NamespaceDeclarationSyntax node, PrintContext context)
	{
		if (context.Options.NamespaceStyle != NamespaceStyle.FileScoped)
			return false;

		// Not the only thing in the file, so whatever sits beside it would be swallowed.
		if (node.Parent is not CompilationUnitSyntax unit || unit.Members.Count != 1)
			return false;

		foreach (var member in node.Members)
		{
			if (member is BaseNamespaceDeclarationSyntax)
				return false;
		}

		return true;
	}

	/// <summary>
	/// Emits a block namespace in the file-scoped form.
	/// </summary>
	/// <remarks>
	/// The declared token delta: the opening brace becomes a semicolon and the closing brace goes.
	/// Members print at the compilation unit's level rather than one deeper, which is where the whole
	/// file coming back an indent comes from — there is no re-indent pass, just an indent scope that
	/// is never opened.
	/// </remarks>
	private static void PrintAsFileScoped(NamespaceDeclarationSyntax node, PrintContext context)
	{
		var arena = context.Arena;

		arena.Synthetic(SyntheticText.Semicolon);
		context.NamespaceUnwrapped = true;

		// A blank line under the declaration whatever the author had under the brace, which is what
		// Roslyn's own fixer writes. Emitted once, up front, so the usings and the members that
		// follow are separated by the ordinary rules rather than by a special case for whichever
		// happens to come first.
		arena.HardLine();
		arena.HardLine();

		// Negative means "nothing precedes this", which is how Separate is told to emit no separator
		// of its own. Without it the blank line above is added to whatever the author already had
		// under the brace, and a file that had one came out with two.
		var previousEnd = -1;

		PrintUsings(node, node.Usings, context, ref previousEnd);

		foreach (var member in node.Members)
		{
			Separate(context, ref previousEnd, member);
			Node.Print(member, context);
		}

		// The closing brace is gone; a comment written above it is not, and neither is the blank line
		// the author left in front of it.
		if (TokenPrinter.HasLeadingContent(node.CloseBraceToken))
		{
			arena.HardLine(DocFlags.OnlyIfNotAtLineStart);
			if (context.BlankLinesBetween(previousEnd, LeadingContentStart(node.CloseBraceToken)) > 0)
				arena.HardLine();

			TokenPrinter.PrintLeadingTrivia(node.CloseBraceToken, context, trailingBreak: false);
		}

		TokenPrinter.PrintIfPresent(node.SemicolonToken, context);
	}

	/// <summary>Where a token's first comment or directive starts, or the token itself.</summary>
	private static int LeadingContentStart(SyntaxToken token)
	{
		foreach (var trivia in token.LeadingTrivia)
		{
			if (trivia.Kind() is not (SyntaxKind.WhitespaceTrivia or SyntaxKind.EndOfLineTrivia))
				return trivia.SpanStart;
		}

		return token.SpanStart;
	}

	private static void PrintNamespaceBody(NamespaceDeclarationSyntax node, PrintContext context)
	{
		var arena = context.Arena;
		TokenPrinter.Print(node.OpenBraceToken, context);

		using (arena.Indent())
		{
			var previousEnd = node.OpenBraceToken.Span.End;

			// The compilation unit's own directives, when csharp_using_directive_placement asked for
			// them to be in here. They print ahead of any this namespace already had.
			var moved = context.TakeUsingsToPlaceInside();
			if (moved.Count > 0)
				PrintUsings(node, moved, context, ref previousEnd);

			PrintUsings(node, node.Usings, context, ref previousEnd);

			// The first member is separated from the brace above it, not from a member — see the type
			// declaration for why the minimum does not apply there.
			var first = true;
			foreach (var member in node.Members)
			{
				arena.HardLine(DocFlags.OnlyIfNotAtLineStart);
				context.BlankLines(context.DeclarationSeparation(
					previousEnd, EffectiveStart(member), first ? 0 : MinimumBlankLinesFor(member, context)));
				first = false;
				Node.Print(member, context);
				previousEnd = member.Span.End;
			}
		}

		using (arena.IndentIf(context.Options.IndentBraces))
			arena.HardLine();

		TokenPrinter.Print(node.CloseBraceToken, context);
		TokenPrinter.PrintIfPresent(node.SemicolonToken, context);
	}

	public static void DelegateDeclaration(DelegateDeclarationSyntax node, PrintContext context)
	{
		var arena = context.Arena;

		PrintAttributeLists(node.AttributeLists, context);
		PrintModifiers(node.Modifiers, context);
		TokenPrinter.Print(node.DelegateKeyword, context);
		arena.Synthetic(SyntheticText.Space);
		Node.Print(node.ReturnType, context);
		arena.Synthetic(SyntheticText.Space);
		TokenPrinter.Print(node.Identifier, context);

		if (node.TypeParameterList is not null)
			Node.Print(node.TypeParameterList, context);

		Spacing.BeforeDeclarationParens(context);
		Node.Print(node.ParameterList, context);

		foreach (var constraint in node.ConstraintClauses)
		{
			PrintConstraintClause(constraint, context);
		}

		TokenPrinter.Print(node.SemicolonToken, context);
	}
}
