using Nullean.Kerf.Documents;

namespace Nullean.Kerf.Printing.CSharp;

/// <summary>
/// The one place the <c>csharp_space_*</c> options are consulted.
/// </summary>
/// <remarks>
/// <para>
/// Every printer that would otherwise emit a bare space at an option-controlled position calls one
/// of these instead, so wiring a new construct into an existing option is a matter of swapping one
/// call rather than hunting for every place the space is written.
/// </para>
/// <para>
/// Each method is named for the position rather than the option, because several options share a
/// position and a printer should not have to know which is which.
/// </para>
/// </remarks>
internal static class Spacing
{
	/// <summary>Between a cast's closing parenthesis and the value it casts.</summary>
	public static void AfterCast(PrintContext context)
	{
		if (context.Options.SpaceAfterCast)
			context.Arena.Synthetic(SyntheticText.Space);
	}

	/// <summary>
	/// Between a control-flow keyword and its opening parenthesis — <c>if (a)</c> against
	/// <c>if(a)</c>.
	/// </summary>
	public static void AfterControlFlowKeyword(PrintContext context)
	{
		if (context.Options.SpaceAfterKeywordsInControlFlowStatements)
			context.Arena.Synthetic(SyntheticText.Space);
	}

	/// <summary>Before the colon introducing a base list.</summary>
	public static void BeforeInheritanceColon(PrintContext context)
	{
		if (context.Options.SpaceBeforeColonInInheritanceClause)
			context.Arena.Synthetic(SyntheticText.Space);
	}

	/// <summary>After the colon introducing a base list.</summary>
	public static void AfterInheritanceColon(PrintContext context)
	{
		if (context.Options.SpaceAfterColonInInheritanceClause)
			context.Arena.Synthetic(SyntheticText.Space);
	}

	/// <summary>Before a <c>for</c> statement's semicolon.</summary>
	public static void BeforeForSemicolon(PrintContext context)
	{
		if (context.Options.SpaceBeforeSemicolonInForStatement)
			context.Arena.Synthetic(SyntheticText.Space);
	}

	/// <summary>After a <c>for</c> statement's semicolon.</summary>
	public static void AfterForSemicolon(PrintContext context)
	{
		if (context.Options.SpaceAfterSemicolonInForStatement)
			context.Arena.Synthetic(SyntheticText.Space);
	}

	/// <summary>Before a member-access dot.</summary>
	public static void BeforeDot(PrintContext context)
	{
		if (context.Options.SpaceBeforeDot)
			context.Arena.Synthetic(SyntheticText.Space);
	}

	/// <summary>After a member-access dot.</summary>
	public static void AfterDot(PrintContext context)
	{
		if (context.Options.SpaceAfterDot)
			context.Arena.Synthetic(SyntheticText.Space);
	}

	/// <summary>
	/// Before a binary or assignment operator.
	/// </summary>
	/// <remarks>
	/// Nothing at all under <c>none</c>, not a soft line. A break here would sit outside any group —
	/// the operand's group starts after the operator — and an ungrouped line breaks, which would put
	/// every operand of every expression on a line of its own. The break opportunity the option must
	/// not destroy lives after the operator instead, inside the group the right-hand operand already
	/// opens, so reflow keeps working under <c>none</c>.
	/// </remarks>
	public static void BeforeOperator(PrintContext context)
	{
		if (context.Options.SpaceAroundBinaryOperators)
			context.Arena.Synthetic(SyntheticText.Space);
	}
}
