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
			Spacing.BeforeInheritanceColon(context);
			Node.Print(node.BaseList, context);
		}

		BeforeOpenBrace(BraceStyle.Types, context);
		TokenPrinter.Print(node.OpenBraceToken, context);

		using (arena.Indent())
		{
			var previousEnd = node.OpenBraceToken.Span.End;
			for (var i = 0; i < node.Members.Count; i++)
			{
				arena.HardLine(DocFlags.OnlyIfNotAtLineStart);
				if (context.BlankLinesBetween(previousEnd, node.Members[i].SpanStart) > 0)
					arena.HardLine();

				Node.Print(node.Members[i], context);
				if (i < node.Members.SeparatorCount)
					TokenPrinter.Print(node.Members.GetSeparator(i), context);
				previousEnd = node.Members[i].Span.End;
			}
		}

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
		Node.Print(node.ParameterList, context);

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
		Node.Print(node.ParameterList, context);

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
		TokenPrinter.Print(node.OpenBracketToken, context);

		for (var i = 0; i < node.Parameters.Count; i++)
		{
			Node.Print(node.Parameters[i], context);
			if (i >= node.Parameters.SeparatorCount)
				continue;
			Spacing.BeforeComma(context);
			TokenPrinter.Print(node.Parameters.GetSeparator(i), context);
			Spacing.AfterComma(context);
		}

		TokenPrinter.Print(node.CloseBracketToken, context);
	}

	public static void NamespaceDeclaration(NamespaceDeclarationSyntax node, PrintContext context)
	{
		var arena = context.Arena;

		TokenPrinter.Print(node.NamespaceKeyword, context);
		arena.Synthetic(SyntheticText.Space);
		Node.Print(node.Name, context);
		BeforeOpenBrace(BraceStyle.Types, context);
		TokenPrinter.Print(node.OpenBraceToken, context);

		using (arena.Indent())
		{
			var previousEnd = node.OpenBraceToken.Span.End;

			foreach (var directive in node.Usings)
			{
				arena.HardLine(DocFlags.OnlyIfNotAtLineStart);
				if (context.BlankLinesBetween(previousEnd, directive.SpanStart) > 0)
					arena.HardLine();
				Node.Print(directive, context);
				previousEnd = directive.Span.End;
			}

			foreach (var member in node.Members)
			{
				arena.HardLine(DocFlags.OnlyIfNotAtLineStart);
				if (context.BlankLinesBetween(previousEnd, member.SpanStart) > 0)
					arena.HardLine();
				Node.Print(member, context);
				previousEnd = member.Span.End;
			}
		}

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

		Node.Print(node.ParameterList, context);

		foreach (var constraint in node.ConstraintClauses)
		{
			arena.Synthetic(SyntheticText.Space);
			Node.Print(constraint, context);
		}

		TokenPrinter.Print(node.SemicolonToken, context);
	}
}
