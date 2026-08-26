using System.Diagnostics;
using Microsoft.CodeAnalysis.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Nullean.Curb.Options;
using Nullean.Curb.Documents;

namespace Nullean.Curb.Printing.CSharp;

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
		var nextAfterUsings = node.AttributeLists.Count > 0 ? NextStartsWithDirective(node.AttributeLists[0])
			: node.Members.Count > 0 ? NextStartsWithDirective(node.Members[0])
			: NextStartsWithDirective(node.EndOfFileToken);
		AfterUsingList(context, ref previousEnd, !moveInside && node.Usings.Count > 0, nextAfterUsings);

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
		// the blank line above a trailing comment would be dropped. Treat it like an item — except
		// when it is a #region/#endregion, which TokenPrinter.PrintLeadingTrivia already gives its own
		// exact-count treatment; this at-most-one preservation running first would otherwise stack
		// with that force (both agreeing there should be a blank line here does not mean there should
		// be two), the same class of bug PrintTypeBody's closing-brace gap hit earlier.
		if (previousEnd >= 0 && TokenPrinter.HasLeadingContent(node.EndOfFileToken) && !NextStartsWithRegionBoundary(node.EndOfFileToken))
		{
			arena.HardLine(DocFlags.OnlyIfNotAtLineStart);
			if (context.BlankLinesBetween(previousEnd, EffectiveTriviaStart(node.EndOfFileToken)) > 0)
				arena.HardLine();
		}

		TokenPrinter.PrintIfPresent(node.EndOfFileToken, context);
	}

	/// <summary>
	/// Blank lines below the last using directive of a container, forced to an exact count rather than
	/// merely floored — same shape as <c>csharp_blank_lines_inside_type</c> (see <c>PrintTypeBody</c>)
	/// and the existing <c>csharp_blank_lines_after_file_scoped_namespace_directive</c> precedent just
	/// above <see cref="FileScopedNamespace"/>.
	/// </summary>
	/// <remarks>
	/// Called once per container, after every <see cref="PrintUsings"/> call for it has returned — a
	/// container can print two batches (usings moved in from the compilation unit, then its own), and
	/// only the second call's caller knows the whole using block is finished. <paramref name="hadUsings"/>
	/// covers both batches so the gap is forced exactly once when either contributed a directive.
	/// <paramref name="previousEnd"/> resets to the same negative sentinel <see cref="Separate"/> and
	/// <see cref="PrintContext.BlankLinesBetween"/> already treat as "nothing precedes this", so the
	/// immediately following separator adds nothing on top.
	/// <para>
	/// Skipped when <paramref name="nextStartsWithDirective"/> — the same reasoning
	/// <c>PrintTypeBody</c> ended up with for the closing-brace gap: a using block wrapped in its own
	/// <c>#region</c> is common, and forcing a blank line between the last <c>using</c> and
	/// <c>#endregion</c> regressed <c>UsingOrderTests.A_region_around_the_usings_also_stops_it</c>. The
	/// caller computes this rather than handing over a node, because what follows a using list is not
	/// always one — a file of nothing but conditionally-compiled usings ends at
	/// <c>CompilationUnitSyntax.EndOfFileToken</c> instead, which is a token, not a node.
	/// </para>
	/// </remarks>
	private static void AfterUsingList(PrintContext context, ref int previousEnd, bool hadUsings, bool nextStartsWithDirective = false)
	{
		if (!hadUsings || nextStartsWithDirective)
			return;

		context.Arena.HardLine(DocFlags.OnlyIfNotAtLineStart);
		context.BlankLines(context.Options.BlankLinesAfterUsingList);
		previousEnd = -1;
	}

	/// <summary>True when the first non-trivial thing about to print is a directive, such as <c>#endregion</c>.</summary>
	private static bool NextStartsWithDirective(SyntaxNode? next) =>
		next is not null && NextStartsWithDirective(next.GetFirstToken(includeZeroWidth: true));

	/// <summary>True when a token's own leading trivia starts with a directive, ignoring whitespace.</summary>
	private static bool NextStartsWithDirective(SyntaxToken token)
	{
		foreach (var trivia in token.LeadingTrivia)
		{
			if (trivia.Kind() is SyntaxKind.WhitespaceTrivia or SyntaxKind.EndOfLineTrivia)
				continue;
			return trivia.IsDirective;
		}

		return false;
	}

	/// <summary>
	/// True when a token's first leading trivia is specifically a <c>#region</c> or <c>#endregion</c>
	/// — narrower than <see cref="NextStartsWithDirective(SyntaxToken)"/>, for the one call site that
	/// needs to defer to TokenPrinter.PrintLeadingTrivia's own exact-count region handling rather than
	/// its ordinary at-most-one directive treatment (see CompilationUnit's end-of-file trivia check).
	/// </summary>
	private static bool NextStartsWithRegionBoundary(SyntaxToken token)
	{
		foreach (var trivia in token.LeadingTrivia)
		{
			if (trivia.Kind() is SyntaxKind.WhitespaceTrivia or SyntaxKind.EndOfLineTrivia)
				continue;
			return trivia.IsKind(SyntaxKind.RegionDirectiveTrivia) || trivia.IsKind(SyntaxKind.EndRegionDirectiveTrivia);
		}

		return false;
	}

	/// <summary>
	/// True when a token's own leading trivia starts, ignoring whitespace, with a <c>//</c> or
	/// <c>/* */</c> comment.
	/// </summary>
	/// <remarks>
	/// Guards <see cref="Block"/>'s statement loop against forcing a minimum blank line ahead of a
	/// comment that TokenPrinter.PrintLeadingTrivia's own trivia walk might align under a preceding
	/// trailing comment: that alignment decision reads its own <c>priorNewLines</c> count from the
	/// literal trivia in front of the comment, which a blank line this loop forced in *before* the
	/// walk ever starts is invisible to. Forcing one there anyway is how a statement-level blank-line
	/// minimum broke <c>AlignsUnderTrailingComment</c> — not on the first run, where the walk's own
	/// count still read zero, but on the second, once that forced blank line became literal source
	/// text the walk had to count too. Caught by the real corpus, not this suite's own unit tests: the
	/// exact author-aligned-comment shape it needs to exercise is not one a hand-written case reached.
	/// </remarks>
	private static bool NextStartsWithComment(SyntaxToken token)
	{
		foreach (var trivia in token.LeadingTrivia)
		{
			if (trivia.Kind() is SyntaxKind.WhitespaceTrivia or SyntaxKind.EndOfLineTrivia)
				continue;
			return trivia.IsKind(SyntaxKind.SingleLineCommentTrivia) || trivia.IsKind(SyntaxKind.MultiLineCommentTrivia);
		}

		return false;
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

		// A blank line under the declaration, which dotnet format inserts and Curb used to leave to
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
		AfterUsingList(context, ref previousEnd, moved.Count > 0 || node.Usings.Count > 0,
			node.Members.Count > 0 && NextStartsWithDirective(node.Members[0]));

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

				// The minimum parts members from each other, raising and capping whatever the author
				// wrote; the first one is parted from the brace above it by csharp_blank_lines_inside_type
				// instead, which is not layered under that same cap-and-preserve treatment — it names an
				// exact count for this one gap (both floor and ceiling), the way ReSharper's own
				// blank_lines_inside_type is documented to work, so the default of 0 actually means zero
				// rather than "leave up to one, whatever the author had." Only this one gap, deliberately:
				// the symmetric-looking gap before the closing brace below stays on the ordinary floor-
				// and-cap treatment, because what sits there is not only ever a blank line the author
				// left in front of a trailing comment — it is just as often a `#endregion` directive,
				// and stripping the blank line conventionally written above one regressed RegionTests.
				// Same directive guard as Separate: a member wrapped in its own #region right after
				// the previous one must not have a minimum forced in front of the #region marker. A
				// region boundary specifically is skipped outright rather than floored to zero — zero
				// is still a floor under whatever the author already wrote, which double-counts against
				// TokenPrinter.PrintLeadingTrivia's own exact-count force reached moments later inside
				// PrintMember (the region force owns this gap entirely), the same bug the statement
				// loop in Block hit.
				var memberStartsWithRegion = !first && NextStartsWithRegionBoundary(member.GetFirstToken(includeZeroWidth: true));
				if (!memberStartsWithRegion)
				{
					context.BlankLines(first
						? context.Options.BlankLinesInsideType
						: context.DeclarationSeparation(previousEnd, EffectiveStart(member),
							NextStartsWithDirective(member) ? 0 : MinimumBlankLinesFor(member, context)));
				}
				first = false;
				PrintMember(member, context);
				previousEnd = member.Span.End;
			}

			if (TokenPrinter.HasLeadingContent(node.CloseBraceToken))
			{
				// Skipped when a region boundary is what the close brace's leading trivia actually
				// starts with: TokenPrinter.PrintLeadingTrivia's own exact-count region handling takes
				// over from there, and running this floor-and-cap computation first would stack with
				// it — the same double-blank-line bug the end-of-file check hit, for the same reason.
				if (!NextStartsWithRegionBoundary(node.CloseBraceToken))
				{
					arena.HardLine(DocFlags.OnlyIfNotAtLineStart);
					context.BlankLines(context.DeclarationSeparation(
						previousEnd, EffectiveTriviaStart(node.CloseBraceToken), context.Options.BlankLinesInsideType));
				}
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
		// Stable for the same reason the chain's is: Curb reproduces the break, so the next run reads
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

		// Deliberately no "the author opened the body out, so keep the arrow inline" case here. It was
		// tried: it reads whether the expression spans lines, and Curb's own joining of a ternary
		// changes that answer, so the arrow sat at one indent on the first run and another on the
		// second — 217 further roslyn files that never settled. The trap in docs/layout-decisions.md,
		// walked straight into. Only the two arrow-adjacent breaks above are safe, because Curb
		// reproduces those exactly.
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

					PrintSeparated(node.Parameters, context, asWritten,
						fill: context.Options.WrapParametersStyle == WrapStyle.WrapIfLong);
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
			StatementSyntax? previousStatement = null;
			foreach (var statement in node.Statements)
			{
				var start = EffectiveStart(statement);

				// `int y = 1; int z = 2;` — two statements the author put on one line stay there
				// under csharp_preserve_single_line_statements.
				if (context.Options.PreserveSingleLineStatements
					&& previousEnd != node.OpenBraceToken.Span.End
					&& context.AuthorJoined(previousEnd, start))
				{
					arena.Synthetic(SyntheticText.Space);
					Node.Print(statement, context);
					previousEnd = statement.Span.End;
					previousStatement = statement;
					continue;
				}

				// Reindent rather than OnlyIfNotAtLineStart. A trailing `// note` on a braceless `if`
				// body ends its own line from inside that body's indent, so the next statement
				// started already indented and came out a level too deep. Reindent trims whatever
				// was left and re-emits this block's own indent, whoever wrote the line ending.
				arena.HardLine(DocFlags.Reindent);
				// Skipped when the statement's own leading trivia starts with a region boundary — the
				// same double-count risk as the closing-brace gaps above, just one statement over:
				// EffectiveStart(statement) measures to a #region/#endregion sitting in front of it,
				// so this would otherwise run its own at-most-one blank line ahead of
				// TokenPrinter.PrintLeadingTrivia's exact-count force, reached moments later inside
				// Node.Print(statement, context).
				var statementFirstToken = statement.GetFirstToken(includeZeroWidth: true);
				if (!NextStartsWithRegionBoundary(statementFirstToken))
				{
					// The statement-level minimum is skipped, not merely floored to it, when a comment
					// sits in front — see NextStartsWithComment for why forcing one in there is unsafe
					// regardless of what the comment turns out to be.
					var minimum = NextStartsWithComment(statementFirstToken)
						? 0
						: StatementSeparationMinimum(previousStatement, statement, context);
					context.BlankLines(context.CodeSeparation(previousEnd, start, minimum));
				}
				Node.Print(statement, context);
				previousEnd = statement.Span.End;
				previousStatement = statement;
			}

			// Trivia attached to the closing brace belongs with the statements it follows, not with
			// the brace, and the blank line above it is preserved like any other separator. A whole
			// `#if` block whose branch is disabled arrives here too, since none of it is parsed.
			// Skipped when a region boundary follows — see the type-body closing brace above for why.
			if (TokenPrinter.HasLeadingContent(node.CloseBraceToken))
			{
				if (!NextStartsWithRegionBoundary(node.CloseBraceToken))
				{
					arena.HardLine(DocFlags.OnlyIfNotAtLineStart);
					if (context.BlankLinesBetween(previousEnd, EffectiveTriviaStart(node.CloseBraceToken)) > 0)
						arena.HardLine();
				}
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

					PrintSeparated(node.Arguments, context, asWritten, node.OpenParenToken.Span.End,
						fill: context.Options.WrapArgumentsStyle == WrapStyle.WrapIfLong);
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
	/// is the one thing Curb's defaults exist to prevent. Glued is the only safe answer here.
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
	/// The groupings are Roslyn's and ReSharper's, not Curb's: a local function answers to the method key
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
	/// width rather than about the author. Curb implements two of them:
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
	/// <para>
	/// <see cref="PrintBody"/> and <see cref="PrintStatementBody"/> used to spell this test out themselves,
	/// which is the same condition written three times.
	/// </para>
	/// <para>
	/// An empty pair answers false here whenever <c>csharp_empty_block_style</c> is set, whatever its
	/// value: <see cref="CollapsesEmptyBraces"/> and <see cref="JoinsEmptyBracesToHeader"/> take over the
	/// whole question for that case, including <c>multiline</c>'s opposite one — forcing the pair apart
	/// even though preservation would otherwise have kept it joined.
	/// </para>
	/// </remarks>
	internal static bool KeepsOneLine(SyntaxNode body, PrintContext context)
	{
		if (context.Options.EmptyBlockStyle is not null && IsEmptyBraces(body))
			return false;

		return body is BlockSyntax block
			? KeepsOneLine(block.OpenBraceToken, block.CloseBraceToken, context)
			: context.Options.PreserveSingleLineBlocks && context.AuthorJoined(body.SpanStart, body.Span.End);
	}

	/// <summary>An empty block whose closing brace carries no trivia a collapse would have to drop.</summary>
	private static bool IsEmptyBraces(SyntaxNode body) =>
		body is BlockSyntax { Statements.Count: 0 } block && !TokenPrinter.HasLeadingContent(block.CloseBraceToken);

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
	/// By default it collapses the pair and <b>never moves it</b> — where the brace goes stays
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
	/// <para>
	/// <c>csharp_empty_block_style</c> overrides all of that once it is set: <c>together</c> and
	/// <c>together_same_line</c> both collapse unconditionally — in either layout mode and whatever
	/// <c>csharp_preserve_single_line_blocks</c> says, because this is an opt-in ReSharper opinion rather
	/// than dotnet format's — and <c>multiline</c> refuses to collapse at all, even a pair the author or
	/// preservation kept joined. <see cref="JoinsEmptyBracesToHeader"/> is what additionally moves the
	/// pair for <c>together_same_line</c>.
	/// </para>
	/// </remarks>
	internal static bool CollapsesEmptyBraces(SyntaxNode body, PrintContext context)
	{
		if (!IsEmptyBraces(body))
			return false;

		return context.Options.EmptyBlockStyle switch
		{
			EmptyBlockStyle.Multiline => false,
			EmptyBlockStyle.Together or EmptyBlockStyle.TogetherSameLine => true,
			_ => !context.Options.KeepExistingLinebreaks && context.Options.PreserveSingleLineBlocks,
		};
	}

	/// <summary>
	/// True when an empty brace pair should sit on its owner's line, overriding
	/// <c>csharp_new_line_before_open_brace</c> for that one pair.
	/// </summary>
	/// <remarks>
	/// <c>csharp_empty_block_style = together_same_line</c> only — <c>together</c> collapses the pair
	/// through <see cref="CollapsesEmptyBraces"/> but leaves it wherever the brace option puts it, which
	/// is the distinction ReSharper itself draws between the two values.
	/// </remarks>
	internal static bool JoinsEmptyBracesToHeader(SyntaxNode body, PrintContext context) =>
		context.Options.EmptyBlockStyle == EmptyBlockStyle.TogetherSameLine && IsEmptyBraces(body);

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
		// would then see a single-line block and turn it into an arrow. Reported; see CURB1004.
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

		// Every shape of getter: one already using an arrow, a block that returns, and a block that
		// throws. The throw case was missing, and its absence was not a missing feature but a
		// non-idempotency — the accessor-level rewrite turned the block into `get => throw …` on the
		// first run, and this printer only recognised the arrow form, so the property collapsed to
		// `=> throw …` on the second. Two rewrites that have to compose in one pass.
		ExpressionSyntax? value = null;
		SyntaxToken semicolon = default;
		ReturnStatementSyntax? returned = null;
		ThrowStatementSyntax? thrown = null;

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
		else if (accessor.Body is { Statements: [ThrowStatementSyntax { Expression: not null } onlyThrow] })
		{
			thrown = onlyThrow;
		}

		if (thrown is null && (value is null || semicolon.RawKind == 0))
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

			if (thrown is not null)
			{
				Node.Print(thrown, context);
			}
			else
			{
				Node.Print(value!, context);
				TokenPrinter.Print(semicolon, context);
			}
		}

		// A getter that already used an arrow carries its own across, so only the block forms add one.
		if (returned is not null || thrown is not null)
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
		else if (thrown is not null)
		{
			// The `throw` keyword stays — it is part of the statement carried across — so only the
			// accessor's own braces go.
			context.Dropped(accessor.Body!.OpenBraceToken.Span);
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
		if (KeepsOneLine(body, context) || JoinsEmptyBracesToHeader(body, context))
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
	/// <param name="body">The braced body to emit.</param>
	/// <param name="construct">Which csharp_new_line_before_open_brace flag governs its brace.</param>
	/// <param name="context">Per-file printing state.</param>
	/// <param name="alwaysJoinsEmpty">
	/// True for a <c>try</c>, <c>catch</c> or <c>finally</c> block: an empty one always prints as
	/// <c>{ }</c> glued to whatever precedes it (the keyword, or a catch's declaration/filter), whatever
	/// the source had and whatever <c>csharp_preserve_single_line_blocks</c> says. Curb's own opinion,
	/// not dotnet format's — dotnet format is lazy about all three in every direction, so there is
	/// nothing of its to match either way, and an unconditional <c>{ }</c> is the cleaner default. A
	/// non-empty body is unaffected: it follows the same rule as any other braced construct. Ignored
	/// when the body is not empty, and when <c>csharp_empty_block_style</c> is set — an explicit opinion
	/// beats an implicit one.
	/// </param>
	internal static void PrintStatementBody(
		SyntaxNode body,
		BraceStyle construct,
		PrintContext context,
		bool alwaysJoinsEmpty = false)
	{
		var forcesEmptyJoin = alwaysJoinsEmpty && context.Options.EmptyBlockStyle is null && IsEmptyBraces(body);
		var joinsHeader = forcesEmptyJoin || JoinsEmptyBracesToHeader(body, context);
		var flat = joinsHeader || KeepsOneLine(body, context) || CollapsesEmptyBraces(body, context);

		if (joinsHeader)
			context.Arena.Synthetic(SyntheticText.Space);
		else
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
	/// <param name="fill">
	/// <c>wrap_if_long</c>: pack elements onto a line until the next one does not fit, rather than the
	/// enclosing group's all-flat-or-all-broken choice. Mutually exclusive with <paramref name="asWritten"/>
	/// in practice — the binder restricts <c>wrap_if_long</c> to deterministic layout, which never asks
	/// for the author's own arrangement — so the ordinary per-element indent-and-break-position logic
	/// below is not needed here and is skipped rather than reused.
	/// </param>
	private static void PrintSeparated<T>(
		SeparatedSyntaxList<T> list,
		PrintContext context,
		bool asWritten = false,
		int anchorEnd = -1,
		bool fill = false)
		where T : SyntaxNode
	{
		var arena = context.Arena;

		if (fill)
		{
			using var f = arena.Fill();

			for (var i = 0; i < list.Count; i++)
			{
				using (f.Item())
					Node.Print(list[i], context);

				if (i >= list.SeparatorCount)
					continue;

				using (f.Separator())
				{
					Spacing.BeforeComma(context);
					TokenPrinter.Print(list.GetSeparator(i), context);
					Spacing.AfterCommaBreakable(context);
				}
			}

			return;
		}

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
	/// Emits <c>file_header_template</c> at the top of the file, inserting it if missing and
	/// correcting it if a leading <c>//</c> block does not match.
	/// </summary>
	/// <remarks>
	/// <para>
	/// Only a leading run of <c>//</c> comments is ever rewritten — the shape both Roslyn's fixer and
	/// this printer write, so it is the one shape Curb can tell "the wrong header" from "a comment
	/// that happens to lead the file" with nothing more than the template to compare against. A file
	/// that opens with a <c>/* */</c> block or a <c>///</c> doc comment is left alone entirely:
	/// rewriting those reliably needs more structure than a syntax-only check is willing to guess at.
	/// </para>
	/// <para>
	/// A directive at the top (<c>#if</c>) is not a header either way, so a file opening with one
	/// still gets a header inserted ahead of it.
	/// </para>
	/// </remarks>
	private static void PrintFileHeader(CompilationUnitSyntax node, PrintContext context)
	{
		if (context.Options.FileHeaderTemplate is not { } template)
			return;

		var first = node.GetFirstToken(includeZeroWidth: true);
		var leading = first.LeadingTrivia;
		var scan = ScanExistingHeader(leading, context);

		if (scan.CommentLines is { } existingLines)
		{
			if (HeaderMatches(existingLines, template, context.FileName))
				return;

			if (UsingOrganiserOwnsLeadingTrivia(node, first, context))
			{
				// The using organiser is about to split this very trivia into a "banner" it prints
				// verbatim ahead of the sorted block (see UsingOrganiser.BannerEnd). Two rewrites of
				// the same region cannot coexist, so this run leaves the mismatched header alone
				// rather than risk printing it twice.
				return;
			}

			SkipExistingTrivia(context, first, leading, scan.ConsumedCount);
		}
		else if (scan.StoppedAtUnreplaceableComment)
		{
			// A block or doc comment already opens the file. Leave it exactly as it is.
			return;
		}
		else if (scan.ConsumedCount > 0 && !UsingOrganiserOwnsLeadingTrivia(node, first, context))
		{
			// Nothing recognisable as a header, but the file started with blank lines (or those
			// blank lines led into something else, like a directive). Skip them so inserting the
			// header does not leave a blank line in front of it, matching Roslyn's fixer.
			SkipExistingTrivia(context, first, leading, scan.ConsumedCount);
		}

		var arena = context.Arena;

		foreach (var line in FileHeaderText.Lines(template, context.FileName))
		{
			arena.HeaderLine(line);
			arena.HardLine();
		}

		// The blank line under the block, which is what Roslyn's fixer writes.
		arena.HardLine();
		context.FileHeaderAdded = true;
	}

	private static void SkipExistingTrivia(PrintContext context, SyntaxToken first, SyntaxTriviaList leading, int count)
	{
		context.Dropped(TextSpan.FromBounds(leading[0].FullSpan.Start, leading[count - 1].FullSpan.End));
		context.HeaderSkipTokenPosition = first.SpanStart;
		context.HeaderSkipCount = count;
	}

	/// <summary>
	/// True when the file's first token is the first using directive's own first token, and sorting
	/// will actually reorder that block — the one case where <see cref="TokenPrinter"/>'s ordinary
	/// leading-trivia walk is not what prints that token's trivia. <see cref="PrintUsings"/> splits a
	/// leading comment-then-blank-line "banner" off the first directive and emits it verbatim itself
	/// (<see cref="UsingOrganiser.BannerEnd"/>), so a header rewrite here would either be printed
	/// twice or fight that logic for the same characters.
	/// </summary>
	private static bool UsingOrganiserOwnsLeadingTrivia(CompilationUnitSyntax node, SyntaxToken first, PrintContext context) =>
		node.Usings.Count > 0
		&& first == node.Usings[0].GetFirstToken(includeZeroWidth: true)
		&& UsingOrganiser.IsEligible(node, node.Usings, context.Options);

	/// <summary>What scanning the compilation unit's leading trivia from its very start found.</summary>
	/// <param name="ConsumedCount">
	/// How many leading trivia entries make up whatever was found — a run of blank lines, a run of
	/// blank lines followed by a <c>//</c> block, or (with <see cref="CommentLines"/> null and
	/// <see cref="StoppedAtUnreplaceableComment"/> false) just the blank lines in front of something
	/// else entirely. Zero when the very first trivia is already something other than whitespace.
	/// </param>
	/// <param name="CommentLines">
	/// The trimmed text of each <c>//</c> line in a header block found at the top, or null when there
	/// is none.
	/// </param>
	/// <param name="StoppedAtUnreplaceableComment">
	/// True when the scan stopped at a <c>/* */</c> or <c>///</c> comment — a header Curb will not
	/// rewrite.
	/// </param>
	private readonly record struct HeaderScan(int ConsumedCount, List<string>? CommentLines, bool StoppedAtUnreplaceableComment);

	/// <summary>
	/// Walks leading trivia from the start of the file, collecting a leading <c>//</c> block the same
	/// way Roslyn's fixer does: comments and single line breaks extend it, a second consecutive line
	/// break ends it (and is consumed with it), and anything else ends it without being consumed.
	/// </summary>
	private static HeaderScan ScanExistingHeader(SyntaxTriviaList leading, PrintContext context)
	{
		var onBlankLine = false;
		List<string>? lines = null;
		var i = 0;

		for (; i < leading.Count; i++)
		{
			var trivia = leading[i];
			switch (trivia.Kind())
			{
				case SyntaxKind.SingleLineCommentTrivia:
					(lines ??= []).Add(CommentContent(trivia, context));
					onBlankLine = false;
					break;

				case SyntaxKind.WhitespaceTrivia:
					break;

				case SyntaxKind.EndOfLineTrivia:
					if (onBlankLine)
					{
						i++;
						goto stop;
					}
					onBlankLine = true;
					break;

				case SyntaxKind.MultiLineCommentTrivia:
				case SyntaxKind.SingleLineDocumentationCommentTrivia:
				case SyntaxKind.MultiLineDocumentationCommentTrivia:
					if (lines is null)
						return new HeaderScan(i, null, StoppedAtUnreplaceableComment: true);
					goto stop;

				default:
					goto stop;
			}
		}

	stop:
		return new HeaderScan(i, lines, StoppedAtUnreplaceableComment: false);
	}

	/// <summary>The text of a <c>//</c> comment after its marker, trimmed on both ends.</summary>
	private static string CommentContent(SyntaxTrivia trivia, PrintContext context)
	{
		var span = trivia.Span;
		return span.Length <= 2 ? string.Empty : context.Text.ToString(new TextSpan(span.Start + 2, span.Length - 2)).Trim();
	}

	/// <summary>
	/// Compares an existing header's lines against the template, the way Roslyn's analyzer does:
	/// same number of lines, each equal to the corresponding template line once both are trimmed.
	/// </summary>
	private static bool HeaderMatches(List<string> existingLines, string template, string? fileName)
	{
		var expected = FileHeaderText.Lines(template, fileName);
		if (expected.Length != existingLines.Count)
			return false;

		for (var i = 0; i < expected.Length; i++)
		{
			if (!string.Equals(expected[i].Trim(), existingLines[i], StringComparison.Ordinal))
				return false;
		}

		return true;
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
	/// The minimum blank lines <see cref="Block"/>'s statement loop must raise the gap between two
	/// statements to — the larger of what the previous statement's own "after" rule asks for and what
	/// the next one's "before" rule asks for, since both opinions can apply to the same gap at once
	/// (a block statement immediately followed by a control-transfer one, say) and only one blank line
	/// is ever wanted regardless of how many rules would have asked for it.
	/// </summary>
	private static int StatementSeparationMinimum(StatementSyntax? previous, StatementSyntax next, PrintContext context)
	{
		var options = context.Options;

		// RendersOnOneLine only means something for a block statement — whether its header and body
		// collapsed onto one line together. A control-transfer statement like `return;` is inherently
		// one line on its own, always; asking the same question of it would trivially answer true for
		// every one and silence the option entirely, which is what an earlier version of this method
		// did before BlankLineOptionTests' own explicit-value case caught it.
		var after = previous switch
		{
			null => 0,
			_ when IsBlockStatement(previous) => RendersOnOneLine(previous, context) ? 0 : options.BlankLinesAfterBlockStatements,
			_ when IsControlTransferStatement(previous) => options.BlankLinesAfterControlTransferStatements,
			_ => 0,
		};
		var before = next switch
		{
			_ when IsBlockStatement(next) => RendersOnOneLine(next, context) ? 0 : options.BlankLinesBeforeBlockStatements,
			_ when IsControlTransferStatement(next) => options.BlankLinesBeforeControlTransferStatements,
			_ => 0,
		};
		return Math.Max(after, before);
	}

	/// <summary>
	/// True when preservation keeps a statement on the single line the author wrote it on — jb's own
	/// blank_lines_before/after_block_statements measurably does not reach a preserved single-line
	/// compound statement the way it reaches one that spans multiple lines: <c>if (a) { return; }</c>
	/// stays flush against whatever follows, and only expanding it (dropping preservation, or the
	/// author writing it that way originally) brings the blank-line rule into play.
	/// </summary>
	/// <remarks>
	/// The two preserve options are independent axes, not alternatives to OR together — an earlier
	/// version did exactly that and it was a real idempotency bug: PreserveSingleLineStatements decides
	/// whether the body joins the header line at all (<c>A_braced_body_moves_off_the_header_but_keeps_
	/// its_braces_collapsed</c> in PreserveSingleLineTests documents this precisely — with it off, the
	/// whole statement spans multiple lines even though PreserveSingleLineBlocks, on by default, would
	/// otherwise have kept the braces themselves collapsed), so it is required unconditionally; when the
	/// body is itself a block, PreserveSingleLineBlocks is required on top of that, since the braces
	/// only stay collapsed when it is. Getting this wrong meant a case with only
	/// csharp_preserve_single_line_statements = false set (blocks left at its true default) wrongly
	/// read as "still renders on one line" via the OR, skipped forcing the blank line on the first
	/// pass, and then forced it on the second once the now-multi-line source no longer looked joined —
	/// caught by the suite's own idempotency check, not by manual diffing.
	/// </remarks>
	private static bool RendersOnOneLine(StatementSyntax statement, PrintContext context)
	{
		if (!context.Options.PreserveSingleLineStatements)
			return false;
		if (HasBlockBody(statement) && !context.Options.PreserveSingleLineBlocks)
			return false;

		return context.AuthorJoined(statement.SpanStart, statement.Span.End);
	}

	/// <summary>True when a block/control-transfer statement's own governed body is a <c>{ }</c> block.</summary>
	private static bool HasBlockBody(StatementSyntax statement) =>
		statement switch
		{
			IfStatementSyntax s => s.Statement is BlockSyntax,
			WhileStatementSyntax s => s.Statement is BlockSyntax,
			ForStatementSyntax s => s.Statement is BlockSyntax,
			CommonForEachStatementSyntax s => s.Statement is BlockSyntax,
			DoStatementSyntax s => s.Statement is BlockSyntax,
			LockStatementSyntax s => s.Statement is BlockSyntax,
			UsingStatementSyntax s => s.Statement is BlockSyntax,
			FixedStatementSyntax s => s.Statement is BlockSyntax,
			// switch/try/checked/unsafe have no braceless form at all — always block-shaped — and every
			// other statement kind (including the control-transfer ones) has no nested body to ask
			// about, so PreserveSingleLineBlocks is simply irrelevant to it either way.
			_ => true,
		};

	/// <summary>
	/// The compound control-flow statements <c>csharp_blank_lines_before/after_block_statements</c>
	/// govern — every statement kind whose own body is a nested block or embedded statement, as
	/// opposed to a single simple statement.
	/// </summary>
	private static bool IsBlockStatement(StatementSyntax statement) =>
		statement is IfStatementSyntax or WhileStatementSyntax or ForStatementSyntax
			or CommonForEachStatementSyntax or DoStatementSyntax or SwitchStatementSyntax
			or UsingStatementSyntax or LockStatementSyntax or TryStatementSyntax
			or FixedStatementSyntax or CheckedStatementSyntax or UnsafeStatementSyntax;

	/// <summary>
	/// The statements <c>csharp_blank_lines_before/after_control_transfer_statements</c> govern —
	/// every statement kind that unconditionally leaves the block it sits in.
	/// </summary>
	private static bool IsControlTransferStatement(StatementSyntax statement) =>
		statement is ReturnStatementSyntax or ThrowStatementSyntax or BreakStatementSyntax
			or ContinueStatementSyntax or GotoStatementSyntax or YieldStatementSyntax;

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
	/// too. The comma there would be legal and is what Rider writes, but it puts a token Curb invented
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
	/// dotnet format never joins lines; neither should Curb. Reflow exists to break a line that is
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

			// An expression-bodied lambda whose tail is itself a callback ending in a block — `builder
			// => builder.Add(x => { … })` — hugs exactly like a block-bodied one once that block opens.
			// Delegates to the stricter EndsWithBlockBodiedCallback rather than recursing back through
			// this switch's BringsOwnBlock fallback: a `with` or object initializer at the tail can
			// still print flat, and unlike an actual block its own group cannot supply the break a long
			// preceding member chain needs once the outer one is skipped.
			AnonymousFunctionExpressionSyntax { Block: null } lambda =>
				EndsWithBlockBodiedCallback(lambda),

			_ => BringsOwnBlock(expression),
		};

	private static bool LastArgumentEndsWithOwnBlock(BaseArgumentListSyntax? arguments) =>
		arguments is { Arguments.Count: > 0 }
		&& EndsWithOwnBlock(arguments.Arguments[^1].Expression);

	/// <summary>
	/// True when an expression, followed strictly through calls and creations, resolves to a
	/// genuinely block-bodied lambda or anonymous method — the one tail shape guaranteed to hardline
	/// regardless of how long everything ahead of it is. Deliberately narrower than
	/// <see cref="EndsWithOwnBlock"/>: a `with` expression or object initializer can still print flat,
	/// so treating it the same way here would let a caller skip a breakable group that a long chain
	/// in front of it still needs.
	/// </summary>
	internal static bool EndsWithBlockBodiedCallback(ExpressionSyntax expression) =>
		expression switch
		{
			InvocationExpressionSyntax invocation =>
				LastArgumentEndsWithBlockBodiedCallback(invocation.ArgumentList),
			ObjectCreationExpressionSyntax { Initializer: null, ArgumentList: { } arguments } =>
				LastArgumentEndsWithBlockBodiedCallback(arguments),
			ImplicitObjectCreationExpressionSyntax { Initializer: null } implicitCreation =>
				LastArgumentEndsWithBlockBodiedCallback(implicitCreation.ArgumentList),
			AnonymousFunctionExpressionSyntax { Block: null, ExpressionBody: { } body } =>
				EndsWithBlockBodiedCallback(body),
			AnonymousFunctionExpressionSyntax function => function.Block is not null,
			_ => false,
		};

	private static bool LastArgumentEndsWithBlockBodiedCallback(BaseArgumentListSyntax? arguments) =>
		arguments is { Arguments.Count: > 0 }
		&& EndsWithBlockBodiedCallback(arguments.Arguments[^1].Expression);

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
		//
		// Suppressed when next starts with a directive: EffectiveStart measures to a member's first
		// comment or directive, not the member's own keyword, so a positive minimum (e.g.
		// csharp_blank_lines_around_type's default of 1) would otherwise force a blank line between
		// the previous item and a #region/#endregion wrapped immediately around this one — landing
		// inside the region rather than around it. Same reasoning as AfterUsingList above.
		// A region boundary is skipped outright rather than floored to zero — zero is still a floor
		// under whatever the author already wrote, which double-counts against the region force inside
		// the trivia walk this leads into moments later (see PrintTypeBody's member loop for the same
		// fix, and why).
		if (!NextStartsWithRegionBoundary(next.GetFirstToken(includeZeroWidth: true)))
		{
			var minimum = next is MemberDeclarationSyntax member && !NextStartsWithDirective(next)
				? MinimumBlankLinesFor(member, context) : 0;
			context.BlankLines(context.DeclarationSeparation(previousEnd, EffectiveStart(next), minimum));
		}

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

	private static int EffectiveStart(SyntaxNode node) =>
		EffectiveTriviaStart(node.GetFirstToken());
}
