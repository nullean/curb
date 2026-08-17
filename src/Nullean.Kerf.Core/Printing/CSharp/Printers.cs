using System.Diagnostics;
using Microsoft.CodeAnalysis.Text;
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

		PrintFileHeader(node, context);

		// Source order is externs, then usings, then assembly-level attributes, then members.
		// Emitting the attributes first reorders the file, which the content verifier rightly
		// rejects -- and it moves the licence header that is attached to whatever comes first.
		foreach (var externAlias in node.Externs)
		{
			Separate(context, ref previousEnd, externAlias);
			Node.Print(externAlias, context);
		}

		// Held back when they are to be printed inside the namespace instead. Nothing is added or
		// removed, so the whole region from the first directive to the namespace's opening is a
		// permutation of itself, and that is what the content check is told.
		var moveInside = MovesUsingsInside(node, context);
		if (!moveInside)
			PrintUsings(node, node.Usings, context, ref previousEnd);

		foreach (var attributeList in node.AttributeLists)
		{
			Separate(context, ref previousEnd, attributeList);
			Node.Print(attributeList, context);
		}

		foreach (var member in node.Members)
		{
			Separate(context, ref previousEnd, member);

			if (moveInside && member is BaseNamespaceDeclarationSyntax)
			{
				context.UsingsToPlaceInside = node.Usings;
				context.Reordered(TextSpan.FromBounds(node.Usings[0].FullSpan.Start, InsertionPoint(member)));

				// The token stream genuinely reorders, so the comparer lifts the directives out of the
				// linear walk and checks them as a set — the same handling sorting them already needs.
				context.UsingsReordered = true;
			}

			PrintMember(member, context);
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

	/// <summary>
	/// Emits a using block, sorted if the file's <c>.editorconfig</c> asked for it.
	/// </summary>
	/// <remarks>
	/// A file banner — everything above the last blank line before the first directive — belongs to
	/// the file rather than to whichever directive happens to come first, so it is emitted verbatim
	/// where it was and that directive is printed without it. The span that moved is recorded so the
	/// content verifier can check the region as a multiset instead of in order.
	/// </remarks>
	private static void PrintUsings(
		SyntaxNode container,
		SyntaxList<UsingDirectiveSyntax> usings,
		PrintContext context,
		ref int previousEnd)
	{
		var ordered = UsingOrganiser.Order(container, usings, context.Options);

		if (ordered is null)
		{
			foreach (var directive in usings)
			{
				Separate(context, ref previousEnd, directive);
				Node.Print(directive, context);
			}

			return;
		}

		var arena = context.Arena;
		var firstInSource = usings[0];
		var bannerEnd = UsingOrganiser.BannerEnd(firstInSource);
		var hasBanner = bannerEnd > firstInSource.FullSpan.Start;

		if (hasBanner)
		{
			if (previousEnd >= 0)
				arena.HardLine(DocFlags.OnlyIfNotAtLineStart);

			TokenPrinter.EmitVerbatimRange(context, firstInSource.FullSpan.Start, bannerEnd - firstInSource.FullSpan.Start);
			previousEnd = bannerEnd;
		}

		for (var i = 0; i < ordered.Length; i++)
		{
			var directive = ordered[i];

			if (i == 0)
			{
				if (previousEnd >= 0)
					arena.HardLine(DocFlags.OnlyIfNotAtLineStart);
			}
			else
			{
				arena.HardLine(DocFlags.OnlyIfNotAtLineStart);
				if (context.Options.SeparateImportDirectiveGroups
					&& UsingOrganiser.StartsNewGroup(ordered[i - 1], directive))
					arena.HardLine();
			}

			UsingDirective(directive, context, skipBanner: hasBanner && directive == firstInSource);
		}

		previousEnd = usings[^1].Span.End;

		// To the end of the last directive's *full* span, so a trailing comment is inside the region
		// the verifier treats as permuted. Ending at Span.End leaves `using X; // note` half in and
		// half out, and the comment then fails a comparison it was never meant to be part of.
		context.Reordered(TextSpan.FromBounds(firstInSource.FullSpan.Start, usings[^1].FullSpan.End));
		context.UsingsReordered = true;
	}

	public static void UsingDirective(UsingDirectiveSyntax node, PrintContext context) =>
		UsingDirective(node, context, skipBanner: false);

	/// <param name="node">The directive.</param>
	/// <param name="context">Per-file printing state.</param>
	/// <param name="skipBanner">
	/// Drop the leading trivia of the first token. Set only for the directive that owned a file
	/// banner which has already been emitted in its original place.
	/// </param>
	private static void UsingDirective(UsingDirectiveSyntax node, PrintContext context, bool skipBanner)
	{
		if (node.GlobalKeyword.RawKind != 0)
		{
			if (skipBanner)
				TokenPrinter.PrintWithoutLeadingTrivia(node.GlobalKeyword, context);
			else
				TokenPrinter.Print(node.GlobalKeyword, context);

			context.Arena.Synthetic(SyntheticText.Space);
			skipBanner = false;
		}

		if (skipBanner)
			TokenPrinter.PrintWithoutLeadingTrivia(node.UsingKeyword, context);
		else
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

		// A name keeps the token path, which reproduces the author's layout through trivia — some
		// aliases target a generic spanning three lines, and the using-sort verifier compares the
		// directive's text, so reformatting one makes it unrecognisable.
		//
		// Anything else goes to a real printer. Since C# 12 an alias can target any type, and Tokens
		// is a raw token dump that only knows how to space a dotted name: it welded a tuple's element
		// type to its name, so `using X = (int Left, string Right)` came out as `intLeft`. That is the
		// fourth content bug traced to this helper and the first the release build could not catch,
		// GuardAgainstWelding being debug-only.
		if (node.NamespaceOrType is NameSyntax)
			Tokens(node.NamespaceOrType, context);
		else
			Node.Print(node.NamespaceOrType, context);
		TokenPrinter.Print(node.SemicolonToken, context);
	}

	public static void FileScopedNamespace(FileScopedNamespaceDeclarationSyntax node, PrintContext context)
	{
		TokenPrinter.Print(node.NamespaceKeyword, context);
		context.Arena.Synthetic(SyntheticText.Space);
		Tokens(node.Name, context);
		TokenPrinter.Print(node.SemicolonToken, context);

		var previousEnd = node.SemicolonToken.Span.End;

		// A blank line under the declaration, which dotnet format inserts and Kerf used to leave to
		// the author. Every file in the corpus already had one, so nothing there disagreed; the first
		// real project to consume the MSBuild package had a file that did not, and IDE0055 survived
		// the build — which is precisely the thing that integration exists to make impossible.
		//
		// How many is csharp_blank_lines_after_file_scoped_namespace_directive's business now, and it
		// defaults to the one dotnet format writes.
		if (node.Usings.Count > 0 || node.Members.Count > 0)
		{
			var arena = context.Arena;
			arena.HardLine();
			context.BlankLines(context.Options.BlankLinesAfterFileScopedNamespace);

			// Negative means "nothing precedes this", so the separator below adds nothing on top.
			previousEnd = -1;
		}

		var moved = context.TakeUsingsToPlaceInside();
		if (moved.Count > 0)
			PrintUsings(node, moved, context, ref previousEnd);

		// Usings may sit inside a file-scoped namespace, and this printer used to walk only the
		// members — so every such file was refused by the content verifier rather than formatted. None
		// of the 1,196 corpus files puts a using there, which is why it went unnoticed.
		PrintUsings(node, node.Usings, context, ref previousEnd);

		foreach (var member in node.Members)
		{
			Separate(context, ref previousEnd, member);
			PrintMember(member, context);
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
			PrintBaseList(node.BaseList, context);
		}
		foreach (var constraint in node.ConstraintClauses)
		{
			PrintConstraintClause(constraint, context);
		}

		if (node.OpenBraceToken.RawKind == 0)
		{
			TokenPrinter.PrintIfPresent(node.SemicolonToken, context);
			return;
		}

		// A `{ }` body the author kept on one line only stays there while the header is still one
		// line. Once a clause has been given a line of its own the brace follows it, which is what
		// dotnet format writes and what two corpus files caught. Safe to ask here because the clause
		// options force their break unconditionally — this is reading the options, not the layout.
		var oneLine = KeepsOneLine(node.OpenBraceToken, node.CloseBraceToken, context)
			&& !HeaderWasBroken(node.BaseList, node.ConstraintClauses.Count, context);

		using (arena.ForceFlatIf(oneLine))
		{
			if (oneLine)
				arena.Synthetic(SyntheticText.Space);
			else
				BeforeOpenBrace(BraceStyle.Types, context);

			PrintTypeBody(node, context);
		}
	}

	private static void PrintTypeBody(TypeDeclarationSyntax node, PrintContext context)
	{
		var arena = context.Arena;
		TokenPrinter.Print(node.OpenBraceToken, context);

		using (arena.Indent())
		{
			var previousEnd = node.OpenBraceToken.Span.End;
			var first = true;
			foreach (var member in node.Members)
			{
				arena.HardLine(DocFlags.OnlyIfNotAtLineStart);

				// The minimum parts members from each other; the first one is parted from the brace
				// above it by csharp_blank_lines_inside_type instead, so asking for air around fields
				// does not open a gap under every `{`.
				context.BlankLines(context.DeclarationSeparation(
					previousEnd,
					EffectiveStart(member),
					first ? context.Options.BlankLinesInsideType : MinimumBlankLinesFor(member, context)));
				first = false;
				PrintMember(member, context);
				previousEnd = member.Span.End;
			}

			if (TokenPrinter.HasLeadingContent(node.CloseBraceToken))
			{
				arena.HardLine(DocFlags.OnlyIfNotAtLineStart);
				context.BlankLines(context.DeclarationSeparation(
					previousEnd, EffectiveTriviaStart(node.CloseBraceToken), context.Options.BlankLinesInsideType));
				TokenPrinter.PrintLeadingTrivia(node.CloseBraceToken, context, trailingBreak: false);
			}
		}

		using (arena.IndentIf(context.Options.IndentBraces))
		{
			arena.HardLine(DocFlags.Reindent);
			TokenPrinter.PrintWithoutLeadingTrivia(node.CloseBraceToken, context);
		}

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
			PrintConstraintClause(constraint, context);
		}

		if (node.Body is not null)
		{
			if (!TryPrintExpressionBody(node.Body, context.Options.ExpressionBodiedMethods, context))
				PrintBody(node.Body, BraceStyle.Methods, context, context.ParameterListGroup);
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

		// csharp_wrap_before_arrow_with_expressions moves the arrow to the far side of the break, so
		// a body that wraps reads `M()` / `=> expr` rather than `M() =>` / `expr`. Only the break
		// moves: flat, both spellings print `M() => expr`, so the option costs nothing on a body that
		// fits and cannot make one wrap that would not have.
		// A break the author put around the arrow is theirs, exactly as a member chain's is, and which
		// side they put it on is theirs too. Without this the group simply fitted — with reflow off it
		// always fits — and every wrapped expression body was pulled back onto one line: 217
		// characters in one roslyn case, on a repository that sets no width at all. It also
		// contradicted the rule the rest of the printer is built on, that nothing joins lines the
		// author broke.
		//
		// Stable for the same reason the chain's is: Kerf reproduces the break, so the next run reads
		// the same answer back.
		// Both answer false under deterministic layout, where there is no author to ask — and this is the
		// one construct where that costs nothing at all, because csharp_wrap_before_arrow_with_expressions
		// and the group already decide the arrow's side of the break between them. It is the exemplar the
		// layout note uses for a rule aimed at a group rather than at the source.
		var hugs = node.Expression is SwitchExpressionSyntax or QueryExpressionSyntax;
		var breakBeforeArrow = !hugs
			&& context.AuthorBroke(node.ArrowToken.GetPreviousToken().Span.End, node.ArrowToken.SpanStart);
		var breakAfterArrow = !hugs
			&& context.AuthorBroke(node.ArrowToken.Span.End, node.Expression.SpanStart);

		var arrowLeadsTheBody = !hugs && (context.Options.WrapBeforeArrowWithExpressions || breakBeforeArrow);

		if (!arrowLeadsTheBody)
			TokenPrinter.Print(node.ArrowToken, context);

		// Narrower than an initializer's rule. dotnet format leaves `=> x switch { … }` level with
		// the member but indents `=> new() { … }` one further, so only the constructs that own a
		// whole block of their own hug here; an object or collection initializer does not.
		if (node.Expression is SwitchExpressionSyntax or QueryExpressionSyntax)
		{
			// dotnet format anchors this to the indent of the line the *arrow* ends up on, which is
			// not the same as whether the parameter list wrapped — an earlier note here said it was,
			// and aiming an IfBreak at that list's group made both corpus files worse.
			//
			// The line the arrow lands on is the line the closing paren ended. A list that wrapped
			// with `)` on a line of its own leaves the arrow at the member's indent; one that wrapped
			// with `)` still hugging the last parameter leaves it a level deeper, and the body follows
			// it down. Read from the source, where the two are already distinguishable, rather than
			// from a layout being decided on this same run.
			// Where the `)` is left to the source, the source answers. Where an option forces it to
			// hug, the arrow is a level deeper exactly when the list breaks — which is this run's
			// decision, so it is asked of the list's own group rather than of the text.
			var forced = context.Options.WrapBeforeDeclarationRpar;
			using (forced == false
				? arena.IndentIfBroken(context.ParameterListGroup)
				: arena.IndentIf(forced is null && ClosingParenHugsTheLastParameter(node, context)))
			{
				arena.Synthetic(SyntheticText.Space);
				Node.Print(node.Expression, context);
			}

			return;
		}

		using (arena.Group())
		using (arena.Indent())
		{
			// A soft line rather than a line: the caller has already put the space before the clause,
			// so flat this must collapse to nothing or the arrow ends up with two.
			if (arrowLeadsTheBody)
			{
				if (breakBeforeArrow)
					arena.HardLine();
				else
					arena.SoftLine();

				TokenPrinter.Print(node.ArrowToken, context);
				arena.Synthetic(SyntheticText.Space);
			}
			else if (breakAfterArrow)
			{
				arena.HardLine();
			}
			else
			{
				arena.Line();
			}

			Node.Print(node.Expression, context);
		}
	}

	/// <summary>
	/// True when the owner's parameter list wrapped but kept its <c>)</c> beside the last parameter.
	/// </summary>
	/// <remarks>
	/// Which is the one shape that leaves an expression body's arrow a level deeper than the member,
	/// and so the one shape where dotnet format puts a switch or query body a level deeper too.
	/// </remarks>
	private static bool ClosingParenHugsTheLastParameter(SyntaxNode arrow, PrintContext context)
	{
		var parameters = arrow.Parent switch
		{
			MethodDeclarationSyntax method => method.ParameterList,
			LocalFunctionStatementSyntax local => local.ParameterList,
			ConstructorDeclarationSyntax constructor => constructor.ParameterList,
			OperatorDeclarationSyntax op => op.ParameterList,
			ConversionOperatorDeclarationSyntax conversion => conversion.ParameterList,
			_ => null,
		};

		if (parameters is not { Parameters.Count: > 0 } || !SpansLines(parameters, context))
			return false;

		return context.AuthorJoined(parameters.Parameters[^1].Span.End, parameters.CloseParenToken.SpanStart);
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
		PrintInlineAttributeLists(node.AttributeLists, context);

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
			var asWritten = SpansLines(node, context);

			// Named, so an expression body hanging off this list can indent against whether it wrapped.
			var group = arena.NextGroupId();
			context.ParameterListGroup = group;

			using (arena.Group(group))
			{
				// A count, not a column: csharp_max_formal_parameters_on_line asks whether there are
				// too many parameters rather than whether the line is too long, so it breaks the
				// group outright instead of leaving it to fit measurement. chop_always is the same
				// decision with no count to reach.
				if (context.Options.WrapParametersStyle == WrapStyle.ChopAlways
					|| (context.Options.MaxParametersOnLine is { } limit && node.Parameters.Count > limit))
					arena.BreakParent();

				using (arena.Indent())
				{
					if (!asWritten)
						Spacing.InsideDeclarationParensBreakable(context);
					else if (context.AuthorBroke(node.SpanStart, node.Parameters[0].SpanStart))
						arena.HardLine();
					else
						Spacing.InsideDeclarationParens(context);

					PrintSeparated(node.Parameters, context, asWritten);
				}

				// csharp_wrap_before_declaration_rpar takes the decision away from both the author and
				// reflow when it is set: a breakable line puts the `)` on its own whenever the list
				// breaks, and the plain spacing keeps it beside the last parameter whatever happens.
				var rpar = context.Options.WrapBeforeDeclarationRpar;
				if (rpar == true || (rpar is null && !asWritten))
					Spacing.InsideDeclarationParensBreakable(context);
				else if (rpar == false)
					Spacing.InsideDeclarationParens(context);
				else if (context.AuthorBroke(node.Parameters[^1].Span.End, node.Span.End))
					arena.HardLine();
				else
					Spacing.InsideDeclarationParens(context);
			}
		}
		else
			Spacing.InsideEmptyDeclarationParens(context);

		TokenPrinter.Print(node.CloseParenToken, context);
	}

	public static void Parameter(ParameterSyntax node, PrintContext context)
	{
		PrintInlineAttributeLists(node.AttributeLists, context);

		// `this ref readonly` is the only order the grammar allows, so this is not a preference.
		PrintModifiers(node.Modifiers, context, reorder: false);

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

		using (arena.Indent(context.Options.IndentBlockContents ? 1 : 0))
		{
			var previousEnd = node.OpenBraceToken.Span.End;
			foreach (var statement in node.Statements)
			{
				// `int y = 1; int z = 2;` — two statements the author put on one line stay there
				// under csharp_preserve_single_line_statements.
				if (context.Options.PreserveSingleLineStatements
					&& previousEnd != node.OpenBraceToken.Span.End
					&& context.AuthorJoined(previousEnd, EffectiveStart(statement)))
				{
					arena.Synthetic(SyntheticText.Space);
					Node.Print(statement, context);
					previousEnd = statement.Span.End;
					continue;
				}

				// Reindent rather than OnlyIfNotAtLineStart. A trailing `// note` on a braceless `if`
				// body ends its own line from inside that body's indent, so the next statement
				// started already indented and came out a level too deep. Reindent trims whatever
				// was left and re-emits this block's own indent, whoever wrote the line ending.
				arena.HardLine(DocFlags.Reindent);
				context.BlankLines(context.CodeSeparation(previousEnd, EffectiveStart(statement)));
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

		using (arena.IndentIf(context.Options.IndentBraces))
		{
			arena.HardLine(DocFlags.Reindent);
			TokenPrinter.PrintWithoutLeadingTrivia(node.CloseBraceToken, context);
		}
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
		if (TryPrintChain(node, context))
			return;

		Node.Print(node.Expression, context);
		Spacing.BeforeCallParens(context);
		Node.Print(node.ArgumentList, context);
	}

	public static void MemberAccessExpression(MemberAccessExpressionSyntax node, PrintContext context)
	{
		if (TryPrintChain(node, context))
			return;

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

		// A sole argument that brings its own braces positions its own contents — `Returns(new C
		// { … })`, `Select(x => { … })` — so the list has nothing to lay out and adding its indent
		// would push those contents a level right of where dotnet format puts them.
		//
		// Only for a construct that brings its own block. Extending this to any argument the author
		// happened to spread over lines drops the group as well as the indent, leaving a long
		// argument no break opportunity at all — it came back out as one over-long line, which the
		// next run then broke again.
		if (node.Arguments.Count == 1 && EndsWithOwnBlock(node.Arguments[0].Expression))
		{
			Spacing.InsideCallParens(context);
			context.OwnBlockGroup = 0;
			Node.Print(node.Arguments[0], context);
			Spacing.InsideCallParens(context);
			TokenPrinter.Print(node.CloseParenToken, context);

			// This path opens no group of its own, so a trailing initializer on the creation aims at the
			// argument's instead: `new HttpClient(new SocketsHttpHandler { … }) { Timeout = t }` puts its
			// outer brace on a line of its own exactly when the inner one opened out.
			context.ArgumentListGroup = context.OwnBlockGroup;
			return;
		}

		// Named so that an initializer hanging off the creation this list belongs to can put its brace
		// on its own line exactly when this list wrapped. Published on the way out rather than on the
		// way in: these lists nest, and it is the outermost one the creation is asking about.
		var group = arena.NextGroupId();

		if (node.Arguments.Count > 0)
		{
			var asWritten = SpansLines(node, context);

			using (arena.Group(group))
			{
				// chop_always is the same decision as the count with no count to reach. Deterministic
				// mode only — the binder drops it otherwise, and records why.
				if (context.Options.WrapArgumentsStyle == WrapStyle.ChopAlways
					|| (context.Options.MaxArgumentsOnLine is { } limit && node.Arguments.Count > limit))
					arena.BreakParent();

				using (arena.Indent())
				{
					if (!asWritten)
						Spacing.InsideCallParensBreakable(context);
					else if (context.AuthorBroke(node.SpanStart, node.Arguments[0].SpanStart))
						arena.HardLine();
					else
						Spacing.InsideCallParens(context);

					PrintSeparated(node.Arguments, context, asWritten, node.OpenParenToken.Span.End);
				}

				var rpar = context.Options.WrapBeforeInvocationRpar;
				if (rpar == true || (rpar is null && !asWritten))
					Spacing.InsideCallParensBreakable(context);
				else if (rpar == false)
					Spacing.InsideCallParens(context);
				else if (context.AuthorBroke(node.Arguments[^1].Span.End, node.Span.End))
					arena.HardLine();
				else
					Spacing.InsideCallParens(context);
			}
		}
		else
			Spacing.InsideEmptyCallParens(context);

		TokenPrinter.Print(node.CloseParenToken, context);
		context.ArgumentListGroup = node.Arguments.Count > 0 ? group : (ushort)0;
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
	/// <summary>
	/// Emits attribute sections that share a line with what they decorate.
	/// </summary>
	/// <remarks>
	/// <para>
	/// Consecutive sections are glued — <c>[A][property: B] int x</c>, not <c>[A] [B]</c> — and only
	/// the last is followed by a space, to part it from what it decorates.
	/// </para>
	/// <para>
	/// Not configurable, and it was tried. ReSharper offers
	/// <c>space_between_attribute_sections</c> and defaults it the other way, but <c>dotnet format</c>
	/// does not merely decline to add the space — it actively removes one that is there. An option for
	/// it would therefore not be a fixed point, and Format Document would undo it on every save, which
	/// is the one thing Kerf's defaults exist to prevent. Glued is the only safe answer here.
	/// </para>
	/// </remarks>
	private static void PrintInlineAttributeLists(SyntaxList<AttributeListSyntax> attributeLists, PrintContext context)
	{
		if (attributeLists.Count == 0)
			return;

		foreach (var attributeList in attributeLists)
			Node.Print(attributeList, context);

		context.Arena.Synthetic(SyntheticText.Space);
	}

	/// <summary>
	/// Prints one member, inside a group when something needs to measure the whole of it.
	/// </summary>
	/// <remarks>
	/// The group exists for <c>csharp_place_*_attribute_on_same_line = if_owner_is_single_line</c> and
	/// nothing else, so it is opened only when that value is actually configured. A group changes which
	/// decision the plain lines inside it resolve against, and paying that for every member of every file
	/// to serve one option is the wrong trade — it also means the default configuration emits byte-for-byte
	/// the document it emitted before this family existed.
	/// </remarks>
	private static void PrintMember(MemberDeclarationSyntax member, PrintContext context)
	{
		if (!context.Options.MeasuresWholeMembers)
		{
			Node.Print(member, context);
			return;
		}

		var group = context.Arena.NextGroupId();
		context.MemberGroup = group;

		using (context.Arena.Group(group))
			Node.Print(member, context);
	}

	/// <summary>
	/// Emits a member's attribute sections and whatever separates them from the member.
	/// </summary>
	/// <remarks>
	/// The <c>csharp_place_*_attribute_on_same_line</c> family lives here rather than in each of the
	/// fourteen printers that has attributes: the owning node says which of the four keys applies, so
	/// wiring a new member kind into the family is a line in <see cref="PlacementFor"/> rather than a hunt.
	/// </remarks>
	private static void PrintAttributeLists(SyntaxList<AttributeListSyntax> attributeLists, PrintContext context)
	{
		if (attributeLists.Count == 0)
			return;

		// Consumed once, by the member it was set for — see PrintContext.MemberGroup.
		var memberGroup = context.MemberGroup;
		context.MemberGroup = 0;

		var placement = PlacementFor(attributeLists[0].Parent, context);
		var arena = context.Arena;

		for (var i = 0; i < attributeLists.Count; i++)
		{
			Node.Print(attributeLists[i], context);

			// Between two sections, never a space: dotnet format *removes* it and writes `[A][B]`, which is
			// one of the few layout questions it decides rather than declines — hence no option for it, and
			// hence ten corpus files lost to putting a space here.
			//
			// Whether the sections share a line at all is the same question as whether the attribute joins
			// the member, so it is aimed at the same group. Answering it separately collapsed a `[Theory]`
			// with nine `[InlineData]`s onto one line, which dotnet format then took apart again — it only
			// closes up a gap between sections already sharing a line, it never moves them onto one.
			var last = i == attributeLists.Count - 1;

			switch (placement)
			{
				// One decision for the whole unit. Between sections a bare aimed line, so they glue when the
				// unit is flat; before the member a space as well, which the break trims back off when it
				// fires — the same pairing the trailing-initializer brace uses. An aimed line is left out of
				// break propagation, so none of this can force the group it is asking about to break.
				case AttributePlacement.IfOwnerIsSingleLine when memberGroup != 0:
					if (last)
						arena.Synthetic(SyntheticText.Space);
					arena.LineIfBroken(memberGroup);
					break;

				// OwnLine, and the fallback for a member with no group of its own — a local function nested in
				// a method body, which has attributes but is not printed through PrintMember.
				default:
					arena.HardLine();
					break;
			}
		}
	}

	/// <summary>Which of the four <c>place_*_attribute_on_same_line</c> keys governs this owner.</summary>
	/// <remarks>
	/// The groupings are Roslyn's and ReSharper's, not Kerf's: a local function answers to the method key
	/// for the same reason it answers to <c>csharp_style_expression_bodied_local_functions</c>' sibling,
	/// and an indexer to the property key because that is how dotnet format treats it everywhere else.
	/// Anything unlisted keeps its own line — types and accessors are there deliberately, because dotnet
	/// format moves those onto their own line and so no other answer could be a fixed point.
	/// </remarks>
	private static AttributePlacement PlacementFor(SyntaxNode? owner, PrintContext context) => owner switch
	{
		MethodDeclarationSyntax
			or LocalFunctionStatementSyntax
			or ConstructorDeclarationSyntax
			or DestructorDeclarationSyntax
			or OperatorDeclarationSyntax
			or ConversionOperatorDeclarationSyntax => context.Options.PlaceMethodAttributeOnSameLine,
		FieldDeclarationSyntax => context.Options.PlaceFieldAttributeOnSameLine,
		PropertyDeclarationSyntax or IndexerDeclarationSyntax => context.Options.PlacePropertyAttributeOnSameLine,
		EventFieldDeclarationSyntax or EventDeclarationSyntax => context.Options.PlaceEventAttributeOnSameLine,
		_ => AttributePlacement.OwnLine,
	};

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
		if (!context.Options.NewLineBeforeOpenBrace.HasFlag(construct))
		{
			context.Arena.Synthetic(SyntheticText.Space);
			return;
		}

		// The brace lands wherever this break leaves the cursor, so csharp_indent_braces is applied
		// by emitting the break from a deeper scope rather than by moving the token afterwards.
		using (context.Arena.IndentIf(context.Options.IndentBraces))
			context.Arena.HardLine();
	}

	/// <summary>
	/// Emits a construct's braced body, keeping it on one line when the author wrote it on one.
	/// </summary>
	/// <remarks>
	/// <para>
	/// <c>csharp_preserve_single_line_blocks</c> beats <c>csharp_new_line_before_open_brace</c>: a
	/// preserved body keeps its brace on the header line rather than taking one of its own. The
	/// source span is a sufficient test on its own — a line comment inside the braces would end the
	/// line, so a body that occupies one line cannot contain one.
	/// </para>
	/// <para>
	/// <b>This one has no deterministic counterpart, and that is a conclusion rather than an omission.</b>
	/// Both candidate answers are wrong. Always-expand — what deterministic mode does today — throws the
	/// option's intent away. Flatten-if-it-fits, the accessor list's trick at
	/// <c>Printers.Declarations.cs</c>, would collapse every short <c>if</c> body into a one-liner, an
	/// opinion nobody asked for and one <c>dotnet format</c> never produces. Even restricting it to an
	/// empty <c>{ }</c> fails: <c>dotnet format</c> keeps a collapsed empty pair but never *joins* an
	/// expanded one, so that rule is preservation-flavoured too — tried, and it broke 20+ expectations
	/// that assert exactly that.
	/// </para>
	/// <para>
	/// The capability belongs to ReSharper's <c>place_simple_*_on_single_line</c> family, which asks about
	/// width rather than about the author. Kerf implements two of them:
	/// <c>place_simple_accessorholder_on_single_line</c> works in both modes, and
	/// <c>place_simple_enum_on_single_line</c> is still guarded on this predicate and so is preservation-only.
	/// Making the enum one deterministic would collapse every enum that fits, in both modes and by default —
	/// a real ReSharper behaviour with its own churn, and so its own measurement, not a rider on this.
	/// </para>
	/// </remarks>
	/// <summary>True when the author wrote this brace pair on a single line and asked to keep it.</summary>
	internal static bool KeepsOneLine(SyntaxToken openBrace, SyntaxToken closeBrace, PrintContext context) =>
		context.Options.PreserveSingleLineBlocks
		&& openBrace.RawKind != 0
		&& context.AuthorJoined(openBrace.SpanStart, closeBrace.Span.End);

	/// <summary>The same question of a body, so every caller goes through one predicate.</summary>
	/// <remarks>
	/// <see cref="PrintBody"/> and <see cref="PrintStatementBody"/> used to spell this test out themselves,
	/// which is the same condition written three times.
	/// </remarks>
	internal static bool KeepsOneLine(SyntaxNode body, PrintContext context) =>
		body is BlockSyntax block
			? KeepsOneLine(block.OpenBraceToken, block.CloseBraceToken, context)
			: context.Options.PreserveSingleLineBlocks && context.AuthorJoined(body.SpanStart, body.Span.End);

	/// <summary>
	/// True when an empty brace pair should print as <c>{ }</c> rather than opened out.
	/// </summary>
	/// <remarks>
	/// <para>
	/// The one single-line body deterministic layout can decide for itself. An expanded empty pair carries
	/// no information a collapsed one does not, so there is no question to answer differently on the next
	/// run, and it is the difference between <c>add { }</c> and a two-line <c>add</c> in every repository
	/// that names a width. Without it the mode's default output is visibly poor.
	/// </para>
	/// <para>
	/// It collapses the pair and <b>never moves it</b> — where the brace goes stays
	/// <c>csharp_new_line_before_open_brace</c>'s decision. That distinction is measured, not tidiness:
	/// collapsing onto the header as well put <c>), IAppDataFileSystem { }</c> on one line and
	/// <c>dotnet format</c> wrote <c>), IAppDataFileSystem</c> then <c>{ }</c>, costing three corpus files.
	/// It keeps a collapsed pair; it does not accept a joined header.
	/// </para>
	/// <para>
	/// Deterministic mode only, and still gated on <c>csharp_preserve_single_line_blocks</c>. Collapsing
	/// unconditionally would *join* a pair the author expanded, which <c>dotnet format</c> never does —
	/// 20-plus expectations assert exactly that.
	/// </para>
	/// </remarks>
	internal static bool CollapsesEmptyBraces(SyntaxNode body, PrintContext context) =>
		!context.Options.KeepExistingLinebreaks
		&& context.Options.PreserveSingleLineBlocks
		&& body is BlockSyntax { Statements.Count: 0 } block
		&& !TokenPrinter.HasLeadingContent(block.CloseBraceToken);

	/// <summary>
	/// Emits a block body as an expression body, when the configuration asked and the block allows.
	/// </summary>
	/// <remarks>
	/// <para>
	/// Eligible means exactly one statement — a <c>return</c> with a value, a <c>throw</c>, or a bare
	/// expression — and nothing anywhere in the block that a comment or a directive is attached to. A
	/// comment inside the braces has nowhere to go once the braces are gone, and dropping it is not a
	/// trade worth making for a shorter member.
	/// </para>
	/// <para>
	/// The declared delta: the two braces and any <c>return</c> keyword are recorded as dropped, and
	/// one <c>=&gt;</c> is declared as added. The statement's own semicolon carries over, so nothing
	/// is invented but the arrow.
	/// </para>
	/// </remarks>
	internal static bool TryPrintExpressionBody(BlockSyntax body, ExpressionBodyStyle style, PrintContext context)
	{
		if (style == ExpressionBodyStyle.AsWritten || body.Statements.Count != 1)
			return false;

		// Asked of the source, which reflow cannot move. Asking whether the result fits would let one
		// run's width decide the next run's tokens.
		//
		// With csharp_keep_existing_linebreaks off there is no source layout to ask, and the answer
		// becomes "decline" — when_on_single_line rewrites nothing in deterministic mode. It has to be
		// that way round rather than "rewrite everything": run 1 would join a block that fits, run 2
		// would then see a single-line block and turn it into an arrow. Reported; see KERF1004.
		if (style == ExpressionBodyStyle.WhenOnSingleLine && !context.AuthorJoined(body.SpanStart, body.Span.End))
			return false;

		var statement = body.Statements[0];
		var throws = statement is ThrowStatementSyntax { Expression: not null };

		ExpressionSyntax? value = statement switch
		{
			ReturnStatementSyntax { Expression: { } returned } => returned,
			ExpressionStatementSyntax expression => expression.Expression,
			_ => null,
		};

		if (value is null && !throws)
			return false;

		if (HasAnyTrivia(body.OpenBraceToken) || HasAnyTrivia(body.CloseBraceToken) || HasAnyTrivia(statement, context))
			return false;

		var arena = context.Arena;

		// The same arrow placement ArrowExpressionClause applies. A body synthesised here never
		// reaches that printer, so leaving it out made a converted member print the arrow trailing on
		// the run that converted it and leading on the next — thirteen corpus files, and none of the
		// unit tests, which all started from a body that already had its arrow.
		var arrowLeadsTheBody = context.Options.WrapBeforeArrowWithExpressions;

		arena.Synthetic(SyntheticText.Space);
		if (!arrowLeadsTheBody)
			arena.Synthetic(SyntheticText.Arrow);

		using (arena.Group())
		using (arena.Indent())
		{
			if (arrowLeadsTheBody)
			{
				arena.SoftLine();
				arena.Synthetic(SyntheticText.Arrow);
				arena.Synthetic(SyntheticText.Space);
			}
			else
			{
				arena.Line();
			}

			if (throws)
				Node.Print(statement, context);
			else
			{
				Node.Print(value, context);
				TokenPrinter.Print(SemicolonOf(statement), context);
			}
		}

		// In source order: the verifier walks these with a single cursor.
		context.Dropped(body.OpenBraceToken.Span);
		if (statement is ReturnStatementSyntax returnStatement)
			context.Dropped(returnStatement.ReturnKeyword.Span);
		context.Dropped(body.CloseBraceToken.Span);

		context.ArrowsAdded++;
		context.ExpressionBodyAdded = true;
		return true;
	}

	/// <summary>
	/// Emits a whole accessor list as an expression body, for a property or indexer that is only a
	/// getter.
	/// </summary>
	/// <remarks>
	/// Wider than <see cref="TryPrintExpressionBody"/>: the accessor list's own braces and the
	/// <c>get</c> keyword go as well, so it applies only where there is nothing else in the list to
	/// keep — one accessor, a getter, no attributes and no modifiers on it.
	/// </remarks>
	internal static bool TryPrintExpressionProperty(
		AccessorListSyntax? accessors,
		ExpressionBodyStyle style,
		PrintContext context)
	{
		if (style == ExpressionBodyStyle.AsWritten || accessors is null || accessors.Accessors.Count != 1)
			return false;

		var accessor = accessors.Accessors[0];
		if (!accessor.Keyword.IsKind(SyntaxKind.GetKeyword)
			|| accessor.AttributeLists.Count > 0
			|| accessor.Modifiers.Count > 0)
			return false;

		if (style == ExpressionBodyStyle.WhenOnSingleLine
			&& !context.AuthorJoined(accessors.SpanStart, accessors.Span.End))
			return false;

		// Either shape of getter: one already using an arrow, or a block simple enough to become one.
		ExpressionSyntax? value = null;
		SyntaxToken semicolon = default;
		ReturnStatementSyntax? returned = null;

		if (accessor.ExpressionBody is { } arrow)
		{
			value = arrow.Expression;
			semicolon = accessor.SemicolonToken;
		}
		else if (accessor.Body is { Statements: [ReturnStatementSyntax { Expression: { } inner } single] })
		{
			value = inner;
			semicolon = single.SemicolonToken;
			returned = single;
		}

		if (value is null || semicolon.RawKind == 0)
			return false;

		if (HasAnyTrivia(accessors.OpenBraceToken)
			|| HasAnyTrivia(accessors.CloseBraceToken)
			|| HasAnyTrivia(accessor, context))
			return false;

		var arena = context.Arena;

		// The third place an arrow is emitted, and it takes the same placement as the other two.
		var arrowLeadsTheBody = context.Options.WrapBeforeArrowWithExpressions;

		arena.Synthetic(SyntheticText.Space);
		if (!arrowLeadsTheBody)
			arena.Synthetic(SyntheticText.Arrow);

		using (arena.Group())
		using (arena.Indent())
		{
			if (arrowLeadsTheBody)
			{
				arena.SoftLine();
				arena.Synthetic(SyntheticText.Arrow);
				arena.Synthetic(SyntheticText.Space);
			}
			else
			{
				arena.Line();
			}

			Node.Print(value, context);
			TokenPrinter.Print(semicolon, context);
		}

		// A getter that already used an arrow carries its own across, so only the block form adds one.
		if (returned is not null)
			context.ArrowsAdded++;

		// In source order, which is what the verifier walks.
		context.Dropped(accessors.OpenBraceToken.Span);
		context.Dropped(accessor.Keyword.Span);

		if (returned is not null)
		{
			context.Dropped(accessor.Body!.OpenBraceToken.Span);
			context.Dropped(returned.ReturnKeyword.Span);
			context.Dropped(accessor.Body!.CloseBraceToken.Span);
		}

		context.Dropped(accessors.CloseBraceToken.Span);

		context.ExpressionBodyAdded = true;
		return true;
	}

	private static SyntaxToken SemicolonOf(StatementSyntax statement) => statement switch
	{
		ReturnStatementSyntax r => r.SemicolonToken,
		ExpressionStatementSyntax e => e.SemicolonToken,
		ThrowStatementSyntax t => t.SemicolonToken,
		_ => default,
	};

	/// <summary>True when a token carries a comment or directive on either side.</summary>
	private static bool HasAnyTrivia(SyntaxToken token) =>
		TokenPrinter.HasLeadingContent(token) || TokenPrinter.HasAnyContent(token);

	/// <summary>
	/// True when anything under a node carries a comment or directive.
	/// </summary>
	/// <remarks>
	/// Guarded by a character scan of the node's own text, because walking the descendants allocates
	/// an enumerator and this is asked of every body a <c>csharp_style_expression_bodied_*</c> key
	/// could reach. A comment needs a <c>/</c> and a directive a <c>#</c>; a body containing neither
	/// cannot have either.
	/// </remarks>
	private static bool HasAnyTrivia(SyntaxNode node, PrintContext context)
	{
		if (!MightCarryTrivia(context.Text, node.FullSpan))
			return false;

		foreach (var token in node.DescendantTokens())
		{
			if (HasAnyTrivia(token))
				return true;
		}

		return false;
	}

	private static bool MightCarryTrivia(SourceText text, TextSpan span)
	{
		for (var i = span.Start; i < span.End; i++)
		{
			if (text[i] is '/' or '#')
				return true;
		}

		return false;
	}

	/// <param name="body">The braced body to emit.</param>
	/// <param name="construct">Which csharp_new_line_before_open_brace flag governs its brace.</param>
	/// <param name="context">Per-file printing state.</param>
	/// <param name="ownerGroup">
	/// The group of the parameter list this body hangs off, or 0. A body kept on one line by
	/// csharp_preserve_single_line_blocks still takes a line of its own when the *header* wrapped —
	/// `)` and `{ }` do not share a line — and whether the header wrapped is this run's decision when
	/// a count or a width forced it, so it is asked of the list's group rather than of the source.
	/// </param>
	internal static void PrintBody(
		SyntaxNode body,
		BraceStyle construct,
		PrintContext context,
		ushort ownerGroup = 0)
	{
		if (KeepsOneLine(body, context))
		{
			var arena = context.Arena;

			if (ownerGroup != 0)
			{
				arena.LineIfBroken(ownerGroup);

				// The space only where that line printed nothing, so a body that moved down does not
				// arrive with one in front of it.
				using var ifBreak = arena.IfBreak(ownerGroup);
				using (ifBreak.Branch())
					arena.Synthetic(SyntheticText.Space);
				using (ifBreak.Branch())
				{
				}
			}
			else
				arena.Synthetic(SyntheticText.Space);

			using (arena.ForceFlat())
				Node.Print(body, context);
			return;
		}

		// The brace still goes wherever csharp_new_line_before_open_brace puts it; only the pair collapses.
		BeforeOpenBrace(construct, context);
		using (context.Arena.ForceFlatIf(CollapsesEmptyBraces(body, context)))
			Node.Print(body, context);
	}

	/// <summary>
	/// Emits a control-flow body that is not sharing its header's line.
	/// </summary>
	/// <remarks>
	/// Whether a statement body joins its header is <c>csharp_preserve_single_line_statements</c>'s
	/// decision and has already been taken by the caller; all that is left for
	/// <c>csharp_preserve_single_line_blocks</c> is whether the braces stay collapsed once the body
	/// is on its own line. dotnet format keeps them: with statements off and blocks on,
	/// <c>if (a) { return; }</c> becomes <c>if (a)</c> then <c>{ return; }</c>, not a full expansion.
	/// </remarks>
	internal static void PrintStatementBody(SyntaxNode body, BraceStyle construct, PrintContext context)
	{
		var flat = KeepsOneLine(body, context) || CollapsesEmptyBraces(body, context);

		BeforeOpenBrace(construct, context);
		using (context.Arena.ForceFlatIf(flat))
			Node.Print(body, context);
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
	internal static void BeforeOpenBraceWhenBroken(BraceStyle construct, PrintContext context, DocFlags flags = DocFlags.None)
	{
		if (!context.Options.NewLineBeforeOpenBrace.HasFlag(construct))
		{
			context.Arena.Synthetic(SyntheticText.Space);
			return;
		}

		using (context.Arena.IndentIf(context.Options.IndentBraces))
			context.Arena.Line(flags);
	}

	/// <summary>
	/// The <see cref="BeforeOpenBraceWhenBroken"/> variant aimed at the group of the construct the
	/// brace hangs off, rather than at the brace's own.
	/// </summary>
	/// <remarks>
	/// <para>
	/// For <c>new C(a, b) { X = 1 }</c>. dotnet format moves that <c>{</c> onto its own line whenever the
	/// creation opened out, whatever the initializer itself does — so the question is about the argument
	/// list's break, and a <see cref="DocArena.Line"/> cannot ask it: a line resolves against the group
	/// enclosing it, and the initializer's own group may well be flat inside a broken list.
	/// </para>
	/// <para>
	/// Emitted *outside* the initializer's group for that reason, and paired with
	/// <see cref="DocFlags.OnlyIfNotAtLineStart"/> on the in-group line so that the two cannot both fire
	/// and produce a blank line. Nothing at all when the owner stayed flat, which is what
	/// <see cref="DocArena.LineIfBroken"/> is for; an aimed line is excluded from break propagation, so
	/// this cannot force the list it is asking about to break.
	/// </para>
	/// </remarks>
	internal static void BeforeOpenBraceWhenOwnerBroke(BraceStyle construct, ushort ownerGroup, PrintContext context)
	{
		if (ownerGroup == 0 || !context.Options.NewLineBeforeOpenBrace.HasFlag(construct))
			return;

		using (context.Arena.IndentIf(context.Options.IndentBraces))
			context.Arena.LineIfBroken(ownerGroup);
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


	/// <summary>
	/// Emits a construct exactly as the author wrote it, for the two <c>= ignore</c> settings.
	/// </summary>
	/// <remarks>
	/// The same verbatim path an unknown syntax kind takes, which is the honest reading of
	/// <c>ignore</c>: reproduce the whitespace, do not reflow, and leave alignment the author put in
	/// alone. Leading and trailing trivia still print normally, so a comment above the construct is
	/// laid out like any other.
	/// </remarks>
	internal static void PrintVerbatim(SyntaxNode node, PrintContext context)
	{
		var first = node.GetFirstToken();
		var last = node.GetLastToken();

		if (first.RawKind != 0)
			TokenPrinter.PrintLeadingTrivia(first, context);

		var span = node.Span;
		TokenPrinter.EmitVerbatimRange(context, span.Start, span.Length);

		if (last.RawKind != 0)
			TokenPrinter.PrintTrailingTrivia(last, context);
	}

	private static void PrintModifiers(SyntaxTokenList modifiers, PrintContext context, bool reorder = true)
	{
		if (reorder && CanOrderModifiers(modifiers, context))
		{
			PrintOrderedModifiers(modifiers, context);
			return;
		}

		foreach (var modifier in modifiers)
		{
			TokenPrinter.Print(modifier, context);

			// A modifier followed by a comment has already been parted from the next one by the
			// trivia; adding the usual space as well doubled it.
			if (!EndsInWhitespace(modifier))
				context.Arena.Synthetic(SyntheticText.Space);
		}
	}

	/// <summary>True when a token's trailing trivia already supplies the space after it.</summary>
	/// <remarks>
	/// Plain whitespace after a token is not emitted — the caller's synthetic space is what parts it
	/// from the next one. A comment is emitted, with its own space on each side, so there the caller's
	/// space is one too many.
	/// </remarks>
	private static bool EndsInWhitespace(SyntaxToken token)
	{
		foreach (var trivia in token.TrailingTrivia)
		{
			if (trivia.Kind() is not (SyntaxKind.WhitespaceTrivia or SyntaxKind.EndOfLineTrivia))
				return true;
		}

		return false;
	}

	/// <summary>
	/// Whether this run of modifiers may be put in the configured order.
	/// </summary>
	/// <remarks>
	/// Declines whenever a modifier past the first carries a comment or directive. Reordering the
	/// tokens takes their trivia along, so a comment written between two modifiers would follow the
	/// wrong one — and the leading trivia of the first modifier is the declaration's doc comment,
	/// which must stay at the front whatever happens to the keywords after it.
	///
	/// Callers pass <c>reorder: false</c> for parameters and locals, whose modifier order is grammar
	/// rather than preference. Both were being sorted: `this ref readonly` came out as `readonly this
	/// ref`, which does not compile, because a stable sort moves the modifiers IDE0036 does not name
	/// to the back and only `readonly` is named.
	///
	/// It cannot be inferred from the modifiers alone — measured against `dotnet format style` at
	/// warning severity, a *member* with an unnamed modifier is still reordered (`async static public`
	/// becomes `public static async`), while a parameter or local is left exactly as written. So the
	/// distinction is the declaration, not the keywords.
	/// </remarks>
	private static bool CanOrderModifiers(SyntaxTokenList modifiers, PrintContext context)
	{
		if (context.Options.PreferredModifierOrder is null || modifiers.Count < 2)
			return false;

		for (var i = 0; i < modifiers.Count; i++)
		{
			if (i > 0 && TokenPrinter.HasLeadingContent(modifiers[i]))
				return false;

			foreach (var trivia in modifiers[i].TrailingTrivia)
			{
				if (trivia.Kind() is not (SyntaxKind.WhitespaceTrivia or SyntaxKind.EndOfLineTrivia))
					return false;
			}
		}

		return true;
	}

	/// <summary>
	/// Emits modifiers in the configured order, keeping the first one's leading trivia in front.
	/// </summary>
	/// <remarks>
	/// A stable sort by rank, so modifiers the configuration does not name keep their order relative
	/// to each other and land after the ones it does. The declaration's own leading trivia belongs to
	/// whichever modifier came first in the source, so it is emitted before the sort rather than
	/// travelling with that keyword.
	/// </remarks>
	private static void PrintOrderedModifiers(SyntaxTokenList modifiers, PrintContext context)
	{
		var order = context.Options.PreferredModifierOrder!;

		Span<int> indices = stackalloc int[modifiers.Count];
		Span<int> ranks = stackalloc int[modifiers.Count];
		for (var i = 0; i < modifiers.Count; i++)
		{
			indices[i] = i;
			ranks[i] = RankOf(modifiers[i], order);
		}

		// Insertion sort: a declaration has a handful of modifiers, and this keeps it stable and
		// allocation-free. Ranks are computed once above rather than inside the comparison — doing it
		// per comparison is quadratic in a keyword lookup, and it cost the corpus a measurable share
		// of its allocation.
		for (var i = 1; i < indices.Length; i++)
		{
			var current = indices[i];
			var rank = ranks[current];
			var j = i - 1;

			while (j >= 0 && ranks[indices[j]] > rank)
			{
				indices[j + 1] = indices[j];
				j--;
			}

			indices[j + 1] = current;
		}

		// With the break, not without it. A line comment runs to the end of its line, so emitting the
		// declaration's doc comment and then the keyword on the same line makes the whole declaration
		// part of the comment — which the re-parse check caught, being exactly what it is for.
		TokenPrinter.PrintLeadingTrivia(modifiers[0], context);

		var moved = false;
		for (var i = 0; i < indices.Length; i++)
		{
			if (indices[i] != i)
				moved = true;

			TokenPrinter.PrintWithoutLeadingTrivia(modifiers[indices[i]], context);
			context.Arena.Synthetic(SyntheticText.Space);
		}

		// Only when the sort actually moved something. Declaring a delta that did not happen is not
		// wrong, but it costs the whole file a second parse, and most declarations are already in the
		// configured order — on the corpus, declaring it unconditionally took the re-parse rate from
		// 1.3% of files to 77%.
		if (!moved)
			return;

		// The keywords only. Their leading trivia is printed above and is not permuted, so leaving it
		// outside the region keeps it under the strict linear compare.
		context.Reordered(TextSpan.FromBounds(modifiers[0].Span.Start, modifiers[^1].Span.End));
		context.ModifiersReordered = true;
	}

	private static int RankOf(SyntaxToken modifier, string[] order)
	{
		// SyntaxFacts.GetText hands back the interned keyword, where SyntaxToken.Text builds a string
		// from the green node every time it is asked.
		var text = SyntaxFacts.GetText((SyntaxKind)modifier.RawKind);
		if (text.Length == 0)
			return int.MaxValue;

		for (var i = 0; i < order.Length; i++)
		{
			if (order[i].Equals(text, StringComparison.Ordinal))
				return i;
		}

		// Not named in the configuration, so it sorts after everything that is.
		return int.MaxValue;
	}

	/// <summary>Emits a comma-separated list, breaking one-per-line when the group does not fit.</summary>
	/// <param name="list">The separated list.</param>
	/// <param name="context">Per-file printing state.</param>
	/// <param name="asWritten">
	/// Reproduce the author's line breaks instead of reflowing. Set when the list already spans more
	/// than one line: a break they put in is emitted as a hard one and a separator they left inline
	/// as a plain space, so the list comes out exactly as it went in.
	/// </param>
	/// <param name="anchorEnd">
	/// End of the token the list opens after. Used to tell an argument that sits inline from one the
	/// author put on a line of its own, which is what decides where a nested block anchors.
	/// </param>
	private static void PrintSeparated<T>(
		SeparatedSyntaxList<T> list,
		PrintContext context,
		bool asWritten = false,
		int anchorEnd = -1)
		where T : SyntaxNode
	{
		var arena = context.Arena;

		for (var i = 0; i < list.Count; i++)
		{
			// A construct bringing its own braces anchors to the indent of the line it starts on.
			// Inline after `M(` that is the statement's line, so the list's own level has to come
			// back off; on a line of its own it is already the right depth and must be left alone.
			// An argument inline and the same argument on its own line differ for that reason alone,
			// and dotnet format distinguishes them the same way.
			//
			// This is the one site where the deterministic answer is not the same shape as the
			// preserving one. Whether the argument ends up inline is the enclosing list's group
			// decision, so the rule wants aiming at that group rather than at the author — and under
			// csharp_keep_existing_linebreaks = false it currently stands down instead, never
			// compensating. That is stable and it is the conservative direction; whether it is also
			// right is a corpus question, not one to guess at here.
			var previousEnd = i == 0 ? anchorEnd : list[i - 1].Span.End;
			var ownBlock = list[i] is ArgumentSyntax { Expression: var value }
				&& EndsWithOwnBlock(value)
				&& previousEnd >= 0
				&& context.AuthorJoined(previousEnd, list[i].SpanStart);

			using (arena.IndentIf(ownBlock, -1))
				Node.Print(list[i], context);

			if (i >= list.SeparatorCount)
				continue;

			Spacing.BeforeComma(context);
			TokenPrinter.Print(list.GetSeparator(i), context);

			if (!asWritten)
			{
				Spacing.AfterCommaBreakable(context);
				continue;
			}

			if (context.AuthorJoined(list[i].Span.End, list[i + 1].SpanStart))
				Spacing.AfterComma(context);
			else
				arena.HardLine();
		}
	}

	/// <summary>
	/// Emits <c>file_header_template</c> at the top of a file that has no header yet.
	/// </summary>
	/// <remarks>
	/// Added, never replaced. Roslyn's fixer rewrites a header that differs from the template, but
	/// telling "the wrong header" from "a comment that happens to lead the file" needs more than the
	/// template to compare against, and deleting somebody's copyright notice because it was worded
	/// differently is not a mistake worth risking. A file that already opens with a comment is left
	/// alone.
	/// </remarks>
	private static void PrintFileHeader(CompilationUnitSyntax node, PrintContext context)
	{
		if (context.Options.FileHeaderTemplate is not { } template)
			return;

		var first = node.GetFirstToken(includeZeroWidth: true);
		if (OpensWithAComment(first))
			return;

		var arena = context.Arena;

		foreach (var line in template.Split("\\n"))
		{
			arena.HeaderLine(line);
			arena.HardLine();
		}

		// The blank line under the block, which is what Roslyn's fixer writes.
		arena.HardLine();
		context.FileHeaderAdded = true;
	}

	private static bool OpensWithAComment(SyntaxToken first)
	{
		foreach (var trivia in first.LeadingTrivia)
		{
			if (trivia.Kind() is SyntaxKind.WhitespaceTrivia or SyntaxKind.EndOfLineTrivia)
				continue;

			// A directive at the top is not a header, so a file opening with `#if` can still get one.
			return trivia.Kind() is SyntaxKind.SingleLineCommentTrivia
				or SyntaxKind.MultiLineCommentTrivia
				or SyntaxKind.SingleLineDocumentationCommentTrivia;
		}

		return false;
	}

	/// <summary>
	/// Whether the file's using directives should be printed inside its namespace.
	/// </summary>
	/// <remarks>
	/// Only where there is exactly one namespace and nothing beside it. A file with two would need to
	/// know which one a directive belonged to, and putting it in the wrong one changes what the names
	/// inside that namespace resolve to — a refusal rather than a guess, as with the file-scoped
	/// conversion.
	/// </remarks>
	private static bool MovesUsingsInside(CompilationUnitSyntax node, PrintContext context) =>
		context.Options.UsingPlacement == UsingPlacement.InsideNamespace
		&& node.Usings.Count > 0
		&& node.AttributeLists.Count == 0
		&& node.Members is [BaseNamespaceDeclarationSyntax];

	/// <summary>Where the directives land: just past the brace or semicolon that opens the namespace.</summary>
	private static int InsertionPoint(SyntaxNode member) => member switch
	{
		NamespaceDeclarationSyntax block => block.OpenBraceToken.Span.End,
		FileScopedNamespaceDeclarationSyntax scoped => scoped.SemicolonToken.Span.End,
		_ => member.SpanStart,
	};

	/// <summary>
	/// How many blank lines the configuration wants around a member of this kind.
	/// </summary>
	/// <remarks>
	/// Zero unless asked, which leaves the author's own spacing exactly as it was. An indexer and an
	/// event answer to the property setting, the same grouping dotnet format uses for their braces.
	/// </remarks>
	/// <summary>
	/// True when a clause option has put a line into a declaration's header, so nothing after it can
	/// still be on the declaration's own line.
	/// </summary>
	private static bool HeaderWasBroken(BaseListSyntax? baseList, int constraintCount, PrintContext context) =>
		(context.Options.WrapBeforeExtendsColon && baseList is not null)
		|| (context.Options.WrapBeforeFirstTypeParameterConstraint && constraintCount > 0);

	/// <summary>Prints a <c>where</c> clause, on the signature line or a line of its own.</summary>
	/// <remarks>
	/// <para>
	/// The break is unconditional when the option is on, so it never has to ask whether the
	/// declaration wrapped — a question whose answer this run's own output would change.
	/// </para>
	/// <para>
	/// The indent scope covers the clause and not just the line before it. Anything the clause brings
	/// its own braces for anchors to the line it starts on, and that line is now a level in; closing
	/// the scope after the break left a nested initializer a level short of where dotnet format puts
	/// it, which one corpus file caught.
	/// </para>
	/// </remarks>
	private static void PrintConstraintClause(TypeParameterConstraintClauseSyntax constraint, PrintContext context)
	{
		if (!context.Options.WrapBeforeFirstTypeParameterConstraint)
		{
			context.Arena.Synthetic(SyntheticText.Space);
			Node.Print(constraint, context);
			return;
		}

		using (context.Arena.Indent())
		{
			context.Arena.HardLine();
			Node.Print(constraint, context);
		}
	}

	/// <summary>Prints a base list, on the declaration line or a line of its own, colon first.</summary>
	private static void PrintBaseList(BaseListSyntax baseList, PrintContext context)
	{
		if (!context.Options.WrapBeforeExtendsColon)
		{
			Spacing.BeforeInheritanceColon(context);
			Node.Print(baseList, context);
			return;
		}

		using (context.Arena.Indent())
		{
			context.Arena.HardLine();
			Node.Print(baseList, context);
		}
	}

	private static int MinimumBlankLinesFor(MemberDeclarationSyntax member, PrintContext context)
	{
		var options = context.Options;

		return member switch
		{
			BaseNamespaceDeclarationSyntax => options.BlankLinesAroundNamespace,
			BaseTypeDeclarationSyntax or DelegateDeclarationSyntax => options.BlankLinesAroundType,
			MethodDeclarationSyntax or ConstructorDeclarationSyntax or DestructorDeclarationSyntax
				or OperatorDeclarationSyntax or ConversionOperatorDeclarationSyntax => options.BlankLinesAroundInvocable,
			PropertyDeclarationSyntax or IndexerDeclarationSyntax or EventDeclarationSyntax
				or EventFieldDeclarationSyntax => options.BlankLinesAroundProperty,
			FieldDeclarationSyntax => options.BlankLinesAroundField,
			_ => 0,
		};
	}

	/// <summary>
	/// True when a token is followed by a line comment, which has already ended the line.
	/// </summary>
	/// <remarks>
	/// A caller about to emit a break of its own has to ask this first. A line comment closes its line
	/// as part of being one, so a second break opens a blank line — and the next run preserves that
	/// blank line and is asked the same question again, which is how this class of bug stops a file
	/// settling rather than merely looking wrong once.
	/// </remarks>
	internal static bool EndsWithLineComment(SyntaxToken token)
	{
		foreach (var trivia in token.TrailingTrivia)
		{
			if (trivia.IsKind(SyntaxKind.SingleLineCommentTrivia))
				return true;
		}

		return false;
	}

	/// <summary>
	/// True when the printer, rather than the source, decides this list's trailing comma.
	/// </summary>
	/// <remarks>
	/// <para>
	/// Callers must only ask this of a list the C# grammar permits a trailing comma on — initializers,
	/// enum bodies, anonymous types, switch expressions, collection expressions and list patterns.
	/// Argument lists, parameter lists, type argument and type parameter lists and tuples forbid one,
	/// and adding it there produces source that does not compile, so those printers never call this
	/// and <see cref="PrintSeparated{T}"/>, which serves exactly those two forbidden shapes, has no
	/// trailing-comma path at all.
	/// </para>
	/// <para>
	/// A trailing separator carrying a comment is left to print itself as written. Suppressing the
	/// token would take its trivia with it, and a comma is not worth losing a comment over.
	/// </para>
	/// <para>
	/// A comment or directive sitting between the last element and the closer stands the list down
	/// too. The comma there would be legal and is what Rider writes, but it puts a token Kerf invented
	/// somewhere the content verifier cannot cheaply confirm — that check is a character scan which
	/// knows nothing of comments, and teaching the always-on safety net to parse them to widen an
	/// opinion is the wrong trade. Nine files in the corpus take this path.
	/// </para>
	/// </remarks>
	internal static bool RewritesTrailingComma<T>(SeparatedSyntaxList<T> list, SyntaxToken closer, PrintContext context)
		where T : SyntaxNode =>
		context.Options.RewritesTrailingCommas
		&& list.Count > 0
		&& !TokenPrinter.HasLeadingContent(closer)
		&& (list.SeparatorCount < list.Count || !TokenPrinter.HasAnyContent(list.GetSeparator(list.Count - 1)));

	/// <summary>
	/// Emits the trailing comma for a list <see cref="RewritesTrailingComma{T}"/> accepted.
	/// </summary>
	/// <remarks>
	/// Whether the list ends up broken is reflow's decision, taken on this same run, so the comma
	/// cannot be chosen from the source the way the preservation rules are. It goes in as an
	/// <c>IfBreak</c> against the enclosing group and is resolved when that group's fit is measured —
	/// which is also what makes the rule idempotent, since a second run measures the same group and
	/// reaches the same branch.
	/// </remarks>
	internal static void PrintTrailingComma(PrintContext context)
	{
		var options = context.Options;
		var arena = context.Arena;

		if (options.TrailingCommaInMultilineLists && options.TrailingCommaInSinglelineLists)
		{
			// Wanted either way, so there is nothing for the printer to decide.
			arena.Synthetic(SyntheticText.Comma);
			return;
		}

		using var ifBreak = arena.IfBreak();

		using (ifBreak.Branch())
		{
			if (options.TrailingCommaInSinglelineLists)
				arena.Synthetic(SyntheticText.Comma);
		}

		using (ifBreak.Branch())
		{
			if (options.TrailingCommaInMultilineLists)
				arena.Synthetic(SyntheticText.Comma);
		}
	}

	/// <summary>
	/// True when the author already spread this construct over more than one line.
	/// </summary>
	/// <remarks>
	/// dotnet format never joins lines; neither should Kerf. Reflow exists to break a line that is
	/// too long, not to gather up one somebody deliberately opened out — collapsing a laid-out
	/// argument list and then re-breaking it somewhere else is the single largest source of churn a
	/// formatter can inflict on a repository it is being introduced to.
	///
	/// False throughout under <c>csharp_keep_existing_linebreaks = false</c>, which is most of what
	/// deterministic mode means at the list printers: each of them already has a width-driven branch
	/// for a construct the author left on one line, and this makes that the only branch.
	/// </remarks>
	internal static bool SpansLines(SyntaxNode node, PrintContext context) =>
		context.AuthorBroke(node.SpanStart, node.Span.End);

	/// <summary>
	/// True when the block an expression finishes with is one that positions its own contents.
	/// </summary>
	/// <remarks>
	/// Looks through calls to reach it: in <c>Outer(Inner(new Options { … }))</c> the initializer is
	/// the tail of the whole argument, not of the outer call, and it anchors where dotnet format
	/// anchors it — to the line the statement began on — rather than gaining a level for every call
	/// it happens to be nested inside.
	/// </remarks>
	internal static bool EndsWithOwnBlock(ExpressionSyntax expression) =>
		expression switch
		{
			InvocationExpressionSyntax invocation => LastArgumentEndsWithOwnBlock(invocation.ArgumentList),

			// A creation with an initializer of its own already answers true through BringsOwnBlock;
			// these are the ones carrying only arguments, such as `new(new Limiter(new Options { … }))`.
			ObjectCreationExpressionSyntax { Initializer: null, ArgumentList: { } arguments } =>
				LastArgumentEndsWithOwnBlock(arguments),
			ImplicitObjectCreationExpressionSyntax { Initializer: null } implicitCreation =>
				LastArgumentEndsWithOwnBlock(implicitCreation.ArgumentList),

			_ => BringsOwnBlock(expression),
		};

	private static bool LastArgumentEndsWithOwnBlock(BaseArgumentListSyntax? arguments) =>
		arguments is { Arguments.Count: > 0 }
		&& EndsWithOwnBlock(arguments.Arguments[^1].Expression);

	/// <summary>Emits the separator between two top-level items, preserving at most one blank line.</summary>
	private static void Separate(PrintContext context, ref int previousEnd, SyntaxNode next)
	{
		if (previousEnd < 0)
		{
			previousEnd = next.Span.End;
			return;
		}

		context.Arena.HardLine(DocFlags.OnlyIfNotAtLineStart);

		// Through the same configuration as every other separator, so a compilation unit's members —
		// externs, usings, assembly attributes and the namespace itself — obey the caps and minimums
		// rather than being the one place that still hard-codes a single blank line.
		var minimum = next is MemberDeclarationSyntax member ? MinimumBlankLinesFor(member, context) : 0;
		context.BlankLines(context.DeclarationSeparation(previousEnd, EffectiveStart(next), minimum));

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
