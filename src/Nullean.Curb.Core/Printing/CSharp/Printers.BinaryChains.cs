using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Nullean.Curb.Documents;

namespace Nullean.Curb.Printing.CSharp;

/// <summary>Binary chains — <c>a &amp;&amp; b &amp;&amp; c</c> — when asked to break at the operators.</summary>
internal static partial class Printers
{
	/// <summary>
	/// Prints <paramref name="node"/> as a broken binary chain, if the configuration asked for one.
	/// </summary>
	/// <remarks>
	/// <para>
	/// Off unless <c>csharp_wrap_chained_binary_expressions</c> is set, and then only for the
	/// outermost link of a chain of one repeated operator. Without it a binary chain has no break
	/// opportunity anywhere along its length, so a condition too long for the line overflows it — the
	/// parentheses around the condition move and the operands do not.
	/// </para>
	/// <para>
	/// One operator only. C# binds <c>a &amp;&amp; b || c</c> as <c>(a &amp;&amp; b) || c</c>, and
	/// flattening that to three operands at one level would print something that reads as though it
	/// binds the other way. A mixed chain is left to the ordinary path.
	/// </para>
	/// </remarks>
	/// <param name="node">The outermost link of the chain, if it is one.</param>
	/// <param name="context">The print context.</param>
	/// <param name="callerAlreadyIndented">
	/// True when <see cref="BinaryExpression"/> already found <paramref name="node"/> to be the
	/// node <see cref="PrintContext.IndentedCondition"/> named — an enclosing <c>if</c>/<c>while</c>/
	/// <c>do</c> condition already sitting inside its own indent — so the chain must not add a
	/// second one on top of it.
	/// </param>
	public static bool TryPrintBinaryChain(BinaryExpressionSyntax node, PrintContext context, bool callerAlreadyIndented)
	{
		if (context.Options.WrapChainedBinaryExpressions is null)
			return false;

		// Not inside a call's arguments. A chain there is measured by whatever encloses it — a member
		// chain, an argument list — and the break opportunities this adds change that measurement
		// without necessarily being taken, which left one corpus file long on the first run and
		// broken on the second. The shapes this rule exists for, a condition or an assignment, are
		// not in that position.
		if (IsInsideArguments(node))
			return false;

		// Only the outermost link prints the chain; the rest are its operands.
		if (node.Parent is BinaryExpressionSyntax parent && parent.OperatorToken.RawKind == node.OperatorToken.RawKind)
			return false;

		var operands = new List<ExpressionSyntax>();
		var operators = new List<SyntaxToken>();
		Flatten(node, operands, operators);

		// A two-operand chain used to fall through to the ordinary per-operator path instead, on the
		// reasoning that it reads fine on one line and the group there already offers a break. It
		// does, but that path never reads csharp_wrap_before_binary_opsign — it only ever reproduces
		// whichever side the author already broke on, which left a two-operand chain unable to have
		// its operator normalised at all once csharp_wrap_chained_binary_expressions asked for it
		// (issue #46). A genuine BinaryExpressionSyntax always flattens to at least two operands, so
		// this is defensive rather than a real exclusion.
		if (operands.Count < 2)
			return false;

		var arena = context.Arena;
		var before = context.Options.WrapBeforeBinaryOpsign;

		void PrintOperator(int operatorIndex)
		{
			if (before)
			{
				arena.Line();
				TokenPrinter.Print(operators[operatorIndex], context);
				arena.Synthetic(SyntheticText.Space);
			}
			else
			{
				arena.Synthetic(SyntheticText.Space);
				TokenPrinter.Print(operators[operatorIndex], context);
				arena.Line();
			}
		}

		using (arena.IndentIf(!callerAlreadyIndented))
		{
			// wrap_if_long packs operands onto a line until the next one would not fit, rather than
			// the group's all-flat-or-all-broken choice the other two values make — see DocKind.Fill.
			if (context.Options.WrapChainedBinaryExpressions == WrapStyle.WrapIfLong)
			{
				using var fill = arena.Fill();

				using (fill.Item())
					Node.Print(operands[0], context);

				for (var i = 1; i < operands.Count; i++)
				{
					using (fill.Separator())
						PrintOperator(i - 1);

					using (fill.Item())
						Node.Print(operands[i], context);
				}

				return true;
			}

			using (arena.Group())
			{
				// A count, not a fit measurement: chop_always is the same decision
				// csharp_wrap_arguments_style's chop_always makes, forcing the break outright instead
				// of leaving it to whether the chain fits.
				if (context.Options.WrapChainedBinaryExpressions == WrapStyle.ChopAlways)
					arena.BreakParent();

				Node.Print(operands[0], context);

				for (var i = 1; i < operands.Count; i++)
				{
					PrintOperator(i - 1);
					Node.Print(operands[i], context);
				}
			}
		}

		return true;
	}

	/// <summary>True when an expression sits inside an argument list or a lambda's body.</summary>
	private static bool IsInsideArguments(SyntaxNode node)
	{
		for (var current = node.Parent; current is not null; current = current.Parent)
		{
			if (current is StatementSyntax or MemberDeclarationSyntax)
				return false;

			if (current is ArgumentSyntax or AnonymousFunctionExpressionSyntax)
				return true;
		}

		return false;
	}

	/// <summary>
	/// The operands and operators of a chain of one repeated operator, in source order.
	/// </summary>
	/// <remarks>
	/// Both collected in the same walk. Looking an operator up from its operand afterwards works only
	/// for a left-associative chain, where every operand but the first is a right-hand child —
	/// <c>??</c> associates the other way, so its operands are left-hand children and every operator
	/// came back empty. The content check caught it on one corpus file.
	/// </remarks>
	private static void Flatten(
		BinaryExpressionSyntax node,
		List<ExpressionSyntax> operands,
		List<SyntaxToken> operators)
	{
		void Walk(ExpressionSyntax expression)
		{
			if (expression is BinaryExpressionSyntax binary
				&& binary.OperatorToken.RawKind == node.OperatorToken.RawKind)
			{
				Walk(binary.Left);
				operators.Add(binary.OperatorToken);
				Walk(binary.Right);
				return;
			}

			operands.Add(expression);
		}

		Walk(node);
	}
}
