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
/// <para>
/// None at the moment: every shape the corpus has found so far is fixed, and the generator writes all of them.
/// </para>
/// <para>
/// Conformance with dotnet format is a percentage, so its findings stay in the corpus and set the floor instead.
/// Separately, the syntax Curb prints verbatim by design (<see cref="InsideVerbatimNode"/>) is kept free of untidy
/// spacing, since that measures a missing printer <c>curb check --coverage</c> already reports.
/// </para>
/// </remarks>
public static class KnownDefects
{
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
}
