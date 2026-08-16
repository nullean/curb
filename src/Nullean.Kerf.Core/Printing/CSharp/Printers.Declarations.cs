using Microsoft.CodeAnalysis.CSharp.Syntax;
using Nullean.Kerf.Documents;
using Nullean.Kerf.Options;

namespace Nullean.Kerf.Printing.CSharp;

/// <summary>Printers for declarations and the statements that carry them.</summary>
internal static partial class Printers
{
	public static void FieldDeclaration(BaseFieldDeclarationSyntax node, PrintContext context)
	{
		if (context.Options.SpaceAroundDeclarationStatements == DeclarationSpacing.Ignore)
		{
			PrintVerbatim(node, context);
			return;
		}

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
		if (context.Options.SpaceAroundDeclarationStatements == DeclarationSpacing.Ignore)
		{
			PrintVerbatim(node, context);
			return;
		}

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

		// A value that brings its own braces or brackets already positions its contents, so adding a
		// continuation indent here would shift the whole construct one level right of where it
		// belongs. Only a plain expression needs the hanging indent.
		//
		// A value that starts on the `=` line and continues below counts too: breaking after the `=`
		// as well would push the whole thing right and add a line nobody asked for.
		//
		// The test is where the value *starts*, not whether it spans lines. Reflow may have broken
		// after the `=` itself on an earlier run, and asking "does this span lines" would then read
		// Kerf's own output back as intent and hug it — formatting twice would not settle.
		if (BringsOwnBlock(node.Value) || context.OnSameLine(node.EqualsToken.Span.End, node.Value.SpanStart))
		{
			arena.Synthetic(SyntheticText.Space);
			Node.Print(node.Value, context);
			return;
		}

		using (arena.Group())
		using (arena.Indent())
		{
			arena.Line();
			Node.Print(node.Value, context);
		}
	}

	/// <summary>True for values whose own layout supplies the indentation of their contents.</summary>
	internal static bool BringsOwnBlock(ExpressionSyntax? value) =>
		value switch
		{
			// A lambda or delegate with a block body; an expression-bodied one is a plain value.
			AnonymousFunctionExpressionSyntax function => function.Block is not null,
			ObjectCreationExpressionSyntax creation => creation.Initializer is not null,
			ImplicitObjectCreationExpressionSyntax creation => creation.Initializer is not null,
			ArrayCreationExpressionSyntax creation => creation.Initializer is not null,
			ImplicitArrayCreationExpressionSyntax => true,
			InitializerExpressionSyntax => true,
			CollectionExpressionSyntax => true,
			AnonymousObjectCreationExpressionSyntax => true,
			SwitchExpressionSyntax => true,
			QueryExpressionSyntax => true,
			WithExpressionSyntax => true,
			_ => false,
		};

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

		if (TryPrintExpressionProperty(node.AccessorList, context.Options.ExpressionBodiedProperties, context))
			return;

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

		// Written on one line, this is a block like any other, so turning preservation off expands it
		// rather than leaving it to whether it happens to fit.
		var expand = !context.Options.PreserveSingleLineBlocks
			&& context.OnSameLine(node.OpenBraceToken.SpanStart, node.CloseBraceToken.Span.End);

		using (arena.Group())
		{
			// The accessor list belongs to whatever declares it. An indexer's list answers to the
			// indexers flag, everything else's to properties — an event's accessor list included,
			// since Roslyn has no separate slot for it.
			var construct = node.Parent switch
			{
				IndexerDeclarationSyntax => BraceStyle.Indexers,
				EventDeclarationSyntax => BraceStyle.Events,
				_ => BraceStyle.Properties,
			};
			if (expand)
				BeforeOpenBrace(construct, context);
			else
				BeforeOpenBraceWhenBroken(construct, context);

			TokenPrinter.Print(node.OpenBraceToken, context);

			using (arena.Indent())
			{
				var previousEnd = node.OpenBraceToken.Span.End;
				for (var i = 0; i < node.Accessors.Count; i++)
				{
					var accessor = node.Accessors[i];

					// Expanding moves the accessors off the declaration's line but keeps ones the
					// author wrote together together: `{ get; set; }` becomes three lines, not four.
					if (i == 0)
						Edge();
					else if (expand && context.OnSameLine(previousEnd, accessor.SpanStart))
						arena.Synthetic(SyntheticText.Space);
					else
						arena.Line();

					Node.Print(accessor, context);
					previousEnd = accessor.Span.End;
				}
			}

			using (arena.IndentIf(context.Options.IndentBraces))
				Edge();
		}

		TokenPrinter.Print(node.CloseBraceToken, context);

		void Edge()
		{
			if (expand)
				arena.HardLine();
			else
				arena.Line();
		}
	}

	/// <summary>The <c>event T Name { add { } remove { } }</c> form; the field form is a FieldDeclaration.</summary>
	public static void EventDeclaration(EventDeclarationSyntax node, PrintContext context)
	{
		var arena = context.Arena;

		PrintAttributeLists(node.AttributeLists, context);
		PrintModifiers(node.Modifiers, context);
		TokenPrinter.Print(node.EventKeyword, context);
		arena.Synthetic(SyntheticText.Space);
		Node.Print(node.Type, context);
		arena.Synthetic(SyntheticText.Space);

		if (node.ExplicitInterfaceSpecifier is not null)
			Tokens(node.ExplicitInterfaceSpecifier, context);

		TokenPrinter.Print(node.Identifier, context);

		if (node.AccessorList is not null)
			Node.Print(node.AccessorList, context);

		TokenPrinter.PrintIfPresent(node.SemicolonToken, context);
	}

	public static void AccessorDeclaration(AccessorDeclarationSyntax node, PrintContext context)
	{
		var arena = context.Arena;

		PrintAttributeLists(node.AttributeLists, context);
		PrintModifiers(node.Modifiers, context);
		TokenPrinter.Print(node.Keyword, context);

		if (node.Body is not null)
		{
			if (!TryPrintExpressionBody(node.Body, context.Options.ExpressionBodiedAccessors, context))
				PrintBody(node.Body, BraceStyle.Accessors, context);
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
		Spacing.BeforeDeclarationParens(context);
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
				Spacing.BeforeCallParens(context);
				Node.Print(node.Initializer.ArgumentList, context);
			}
		}

		if (node.Body is not null)
		{
			if (!TryPrintExpressionBody(node.Body, context.Options.ExpressionBodiedConstructors, context))
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

		Spacing.BeforeCallParens(context);
		TokenPrinter.Print(node.ArgumentList.OpenParenToken, context);
		if (node.ArgumentList.Arguments.Count == 0)
			Spacing.InsideEmptyCallParens(context);
		else
			Spacing.InsideCallParens(context);

		for (var i = 0; i < node.ArgumentList.Arguments.Count; i++)
		{
			Node.Print(node.ArgumentList.Arguments[i], context);
			if (i >= node.ArgumentList.Arguments.SeparatorCount)
				continue;
			Spacing.BeforeComma(context);
			TokenPrinter.Print(node.ArgumentList.Arguments.GetSeparator(i), context);
			Spacing.AfterComma(context);
		}

		if (node.ArgumentList.Arguments.Count > 0)
			Spacing.InsideCallParens(context);

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
