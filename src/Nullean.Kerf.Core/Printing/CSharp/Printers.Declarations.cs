using Microsoft.CodeAnalysis.CSharp.Syntax;
using Nullean.Kerf.Documents;

namespace Nullean.Kerf.Printing.CSharp;

/// <summary>Printers for declarations and the statements that carry them.</summary>
internal static partial class Printers
{
	public static void FieldDeclaration(BaseFieldDeclarationSyntax node, PrintContext context)
	{
		PrintAttributeLists(node.AttributeLists, context);
		PrintModifiers(node.Modifiers, context);

		// An event field carries `event` as its own token rather than as a modifier, so printing
		// only the modifiers drops the keyword entirely.
		if (node is EventFieldDeclarationSyntax eventField)
		{
			TokenPrinter.Print(eventField.EventKeyword, context);
			context.Arena.Synthetic(SyntheticText.Space);
		}

		Node.Print(node.Declaration, context);
		TokenPrinter.Print(node.SemicolonToken, context);
	}

	public static void LocalDeclarationStatement(LocalDeclarationStatementSyntax node, PrintContext context)
	{
		PrintAttributeLists(node.AttributeLists, context);

		if (node.AwaitKeyword.RawKind != 0)
		{
			TokenPrinter.Print(node.AwaitKeyword, context);
			context.Arena.Synthetic(SyntheticText.Space);
		}

		if (node.UsingKeyword.RawKind != 0)
		{
			TokenPrinter.Print(node.UsingKeyword, context);
			context.Arena.Synthetic(SyntheticText.Space);
		}

		PrintModifiers(node.Modifiers, context);
		Node.Print(node.Declaration, context);
		TokenPrinter.Print(node.SemicolonToken, context);
	}

	public static void VariableDeclaration(VariableDeclarationSyntax node, PrintContext context)
	{
		Node.Print(node.Type, context);
		context.Arena.Synthetic(SyntheticText.Space);

		for (var i = 0; i < node.Variables.Count; i++)
		{
			Node.Print(node.Variables[i], context);
			if (i >= node.Variables.SeparatorCount)
				continue;
			TokenPrinter.Print(node.Variables.GetSeparator(i), context);
			context.Arena.Synthetic(SyntheticText.Space);
		}
	}

	public static void VariableDeclarator(VariableDeclaratorSyntax node, PrintContext context)
	{
		TokenPrinter.Print(node.Identifier, context);

		if (node.ArgumentList is not null)
			Node.Print(node.ArgumentList, context);

		if (node.Initializer is not null)
			Node.Print(node.Initializer, context);
	}

	public static void EqualsValueClause(EqualsValueClauseSyntax node, PrintContext context)
	{
		var arena = context.Arena;
		arena.Synthetic(SyntheticText.Space);
		TokenPrinter.Print(node.EqualsToken, context);

		using (arena.Group())
		using (arena.Indent())
		{
			arena.Line();
			Node.Print(node.Value, context);
		}
	}

	public static void PropertyDeclaration(PropertyDeclarationSyntax node, PrintContext context)
	{
		var arena = context.Arena;

		PrintAttributeLists(node.AttributeLists, context);
		PrintModifiers(node.Modifiers, context);
		Node.Print(node.Type, context);
		arena.Synthetic(SyntheticText.Space);

		if (node.ExplicitInterfaceSpecifier is not null)
			Tokens(node.ExplicitInterfaceSpecifier, context);

		TokenPrinter.Print(node.Identifier, context);

		Node.Print(node.AccessorList, context);

		if (node.ExpressionBody is not null)
		{
			arena.Synthetic(SyntheticText.Space);
			Node.Print(node.ExpressionBody, context);
		}

		if (node.Initializer is not null)
			Node.Print(node.Initializer, context);

		TokenPrinter.PrintIfPresent(node.SemicolonToken, context);
	}

	/// <summary>
	/// Accessors collapse onto one line when they fit — <c>{ get; set; }</c> — and break otherwise.
	/// </summary>
	/// <remarks>
	/// With reflow off, which is the default, a group is always flat, so accessor lists stay on one
	/// line. That matches csharp_preserve_single_line_blocks, whose default is also true.
	/// </remarks>
	public static void AccessorList(AccessorListSyntax node, PrintContext context)
	{
		var arena = context.Arena;

		using (arena.Group())
		{
			// csharp_new_line_before_open_brace covers accessors and properties; its default puts
			// the brace on its own line, but only where the list actually breaks.
			arena.Line();
			TokenPrinter.Print(node.OpenBraceToken, context);

			using (arena.Indent())
			{
				for (var i = 0; i < node.Accessors.Count; i++)
				{
					arena.Line();
					Node.Print(node.Accessors[i], context);
				}
			}
			arena.Line();
		}

		TokenPrinter.Print(node.CloseBraceToken, context);
	}

	public static void AccessorDeclaration(AccessorDeclarationSyntax node, PrintContext context)
	{
		var arena = context.Arena;

		PrintAttributeLists(node.AttributeLists, context);
		PrintModifiers(node.Modifiers, context);
		TokenPrinter.Print(node.Keyword, context);

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

	public static void ConstructorDeclaration(ConstructorDeclarationSyntax node, PrintContext context)
	{
		var arena = context.Arena;

		PrintAttributeLists(node.AttributeLists, context);
		PrintModifiers(node.Modifiers, context);
		TokenPrinter.Print(node.Identifier, context);
		Node.Print(node.ParameterList, context);

		if (node.Initializer is not null)
		{
			// ` : base(...)` — the colon needs air on both sides.
			using (arena.Indent())
			{
				arena.Synthetic(SyntheticText.Space);
				TokenPrinter.Print(node.Initializer.ColonToken, context);
				arena.Synthetic(SyntheticText.Space);
				TokenPrinter.Print(node.Initializer.ThisOrBaseKeyword, context);
				Node.Print(node.Initializer.ArgumentList, context);
			}
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

	public static void AttributeList(AttributeListSyntax node, PrintContext context)
	{
		TokenPrinter.Print(node.OpenBracketToken, context);

		if (node.Target is not null)
		{
			// `assembly:` / `return:` — the keyword is a contextual one and must not weld to the name.
			TokenPrinter.Print(node.Target.Identifier, context);
			TokenPrinter.Print(node.Target.ColonToken, context);
			context.Arena.Synthetic(SyntheticText.Space);
		}

		for (var i = 0; i < node.Attributes.Count; i++)
		{
			Node.Print(node.Attributes[i], context);
			if (i >= node.Attributes.SeparatorCount)
				continue;
			TokenPrinter.Print(node.Attributes.GetSeparator(i), context);
			context.Arena.Synthetic(SyntheticText.Space);
		}

		TokenPrinter.Print(node.CloseBracketToken, context);
	}

	public static void Attribute(AttributeSyntax node, PrintContext context)
	{
		Tokens(node.Name, context);

		if (node.ArgumentList is null)
			return;

		TokenPrinter.Print(node.ArgumentList.OpenParenToken, context);
		for (var i = 0; i < node.ArgumentList.Arguments.Count; i++)
		{
			Node.Print(node.ArgumentList.Arguments[i], context);
			if (i >= node.ArgumentList.Arguments.SeparatorCount)
				continue;
			TokenPrinter.Print(node.ArgumentList.Arguments.GetSeparator(i), context);
			context.Arena.Synthetic(SyntheticText.Space);
		}
		TokenPrinter.Print(node.ArgumentList.CloseParenToken, context);
	}

	public static void AttributeArgument(AttributeArgumentSyntax node, PrintContext context)
	{
		if (node.NameEquals is not null)
		{
			TokenPrinter.Print(node.NameEquals.Name.Identifier, context);
			context.Arena.Synthetic(SyntheticText.Space);
			TokenPrinter.Print(node.NameEquals.EqualsToken, context);
			context.Arena.Synthetic(SyntheticText.Space);
		}

		if (node.NameColon is not null)
		{
			Tokens(node.NameColon, context);
			context.Arena.Synthetic(SyntheticText.Space);
		}

		Node.Print(node.Expression, context);
	}
}
