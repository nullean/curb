using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Nullean.Curb.CorpusGenerator;

/// <summary>
/// Shapes the generated corpus found Curb refusing, not idempotent on, or breaking the build after cleanup. They are
/// kept out of the corpus until each is fixed, because the gates they fail are all-or-nothing: <c>curb check</c> must
/// report no failure, a second format must change nothing, and the cleaned solution must build.
/// </summary>
/// <remarks>
/// <para>
/// Delete a rule here in the change that fixes its defect, and the corpus covers the shape again. Counts are files in
/// the seed-1 corpus unless another seed is named. Every refusal was Curb's own verifier declining to write; none
/// damaged a file.
/// </para>
/// <list type="number">
/// <item><c>curb check</c>: extra whitespace inside a using directive, <c>using System.Text ;</c> or
/// <c>using  System;</c>, makes sorting the directives refuse, "reordering usings altered one" (66).</item>
/// <item><c>curb check</c>: a comment as the first thing inside a block namespace is lost by file-scoped conversion,
/// "formatting changed content" (5).</item>
/// <item><c>curb check</c>: a trailing <c>//</c> comment after the <c>{</c> of an empty block; collapsing the block
/// puts the <c>}</c> inside the comment, "formatted output no longer parses" (4).</item>
/// <item><c>curb check</c>: comments among using directives that move into a block namespace, "formatting lost
/// content while reordering" (2).</item>
/// <item><c>curb check</c>: two snippets' own <c>csharp_using_directive_placement = inside_namespace</c> on top of the
/// corpus's <c>file_header_template</c> and file-scoped namespaces, refused with no layout change at all (2). Those
/// snippets keep the corpus settings (<see cref="SettingsConflicts"/>).</item>
/// <item>Idempotency: a block body converted to an expression body under
/// <c>csharp_style_expression_bodied_methods = true</c> comes out as <c>=&gt;</c> with the expression on the next line,
/// and a second pass joins it (210, nearly all <c>{ return x switch { ... }; }</c>). The corpus configuration leaves
/// that one key out.</item>
/// <item>Idempotency: trailing whitespace on a file header line, or on the blank line after it, survives the first pass
/// and goes on the second (18). None is written into a file's leading trivia.</item>
/// <item>Idempotency: a trailing <c>//</c> comment on a line a width decision covers (a method header, an expression
/// body, an accessor list, a switch arm) is counted by the first pass, which breaks the construct, and not by the
/// second, which joins it back (1; 2 each at seeds 2 and 3). <c>curb check</c>: a trailing comment after a <c>}</c>
/// that <c>csharp_new_line_before_catch</c> or <c>_finally = false</c> joins to the next keyword swallows it (1 each at
/// seeds 2 and 4). Generated trailing comments only follow the <c>{</c> of a non-empty block; snippets keep every
/// comment their tests wrote.</item>
/// <item>Cleanup: a multi-targeted project writes one log per target framework, and a site reported in both gets its
/// modifier inserted twice, <c>readonly readonly</c> for IDE0044, IDE0250 and IDE0251 and <c>private private</c> for
/// IDE0040, so the cleaned solution stops building (CS1004). The multi-targeted project gets no site for those four
/// rules; its sites for the rules that remove or replace text stay.</item>
/// <item>Cleanup: IDE0007 on a type that touches its variable, <c>List&lt;string&gt;names</c>, writes <c>varnames</c>;
/// the verifier refuses and <c>curb cleanup</c> exits non-zero (6). No space is dropped between a type's closing
/// <c>&gt;</c>, <c>]</c> or <c>?</c> and an identifier.</item>
/// </list>
/// <para>
/// Conformance with dotnet format is a percentage, so its findings stay in the corpus and set the floor instead: for
/// one, converting space indentation to tabs re-indents only the first line of a <c>///</c> doc comment. Separately,
/// the syntax Curb prints verbatim by design (<see cref="InsideVerbatimNode"/>) is kept free of untidy spacing, since
/// that measures a missing printer <c>curb check --coverage</c> already reports.
/// </para>
/// </remarks>
public static class KnownDefects
{
	/// <summary>Test cases whose own settings, combined with the corpus's, Curb refuses (item 5).</summary>
	public static readonly IReadOnlySet<string> SettingsConflicts = new HashSet<string>(StringComparer.Ordinal)
	{
		"UsingPlacementAndHeaderTests.Directives_move_inside_a_block_namespace",
		"UsingPlacementAndHeaderTests.The_header_lands_above_directives_that_moved_inside",
	};

	/// <summary>The profile for a file's leading trivia: no trailing whitespace (item 7).</summary>
	public static LayoutProfile InHeader(LayoutProfile p) => p with { TrailingWhitespace = 0 };

	/// <summary>
	/// Syntax Curb prints verbatim today (<c>curb check --coverage</c>), by design rather than by defect: an unhandled
	/// node keeps its source text. Untidy spacing inside one would measure that coverage gap rather than a formatting
	/// decision, so the gaps inside these keep their source text too. Shrink the list as printers land.
	/// </summary>
	private static readonly HashSet<SyntaxKind> VerbatimKinds =
	[
		SyntaxKind.RefExpression,
		SyntaxKind.CheckedExpression,
		SyntaxKind.UncheckedExpression,
		SyntaxKind.GotoStatement,
		SyntaxKind.GotoCaseStatement,
		SyntaxKind.GotoDefaultStatement,
		SyntaxKind.RefType,
		SyntaxKind.SizeOfExpression,
		SyntaxKind.PointerType,
		SyntaxKind.FunctionPointerType,
		SyntaxKind.ExternAliasDirective,
	];

	/// <summary>Whether two neighbouring tokens sit inside one node Curb prints verbatim.</summary>
	public static bool InsideVerbatimNode(SyntaxToken previous, SyntaxToken token)
	{
		for (var node = token.Parent; node is not null &&
			node is not StatementSyntax
			{
				RawKind: not (int)SyntaxKind.GotoStatement and not (int)SyntaxKind.GotoDefaultStatement and not (int)SyntaxKind.GotoCaseStatement
			}; node = node.Parent)
		{
			if (VerbatimKinds.Contains(node.Kind()) && previous.Span.Start >= node.SpanStart && previous.Span.End <= node.Span.End)
			{
				return true;
			}
		}

		return false;
	}

	/// <summary>Whether the space between two tokens may be dropped (item 10).</summary>
	public static bool AllowsTouching(SyntaxToken previous, SyntaxToken token) =>
		!(token.IsKind(SyntaxKind.IdentifierToken) &&
			(previous.IsKind(SyntaxKind.GreaterThanToken) || previous.IsKind(SyntaxKind.CloseBracketToken) || previous.IsKind(SyntaxKind.QuestionToken)));

	/// <summary>Whether a generated trailing comment may end the line between two tokens (item 8).</summary>
	public static bool AllowsTrailingComment(SyntaxToken previous, SyntaxToken token) =>
		previous.IsKind(SyntaxKind.OpenBraceToken) && !token.IsKind(SyntaxKind.CloseBraceToken);

	/// <summary>Whether the gap between two tokens may only have its indentation and blank lines changed (items 1 to 4).</summary>
	public static bool Avoid(SyntaxToken previous, SyntaxToken token) =>
		InUsing(previous) || InUsing(token) || previous.IsKind(SyntaxKind.OpenBraceToken) && previous.Parent is NamespaceDeclarationSyntax || previous.IsKind(
			SyntaxKind.OpenBraceToken
		) && token.IsKind(SyntaxKind.CloseBraceToken);

	private static bool InUsing(SyntaxToken token) => token.Parent?.AncestorsAndSelf().Any(n => n is UsingDirectiveSyntax) == true;
}
