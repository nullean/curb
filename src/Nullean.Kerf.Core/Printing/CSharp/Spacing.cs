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

	/// <summary>Before a comma in one of the lists these options govern.</summary>
	/// <remarks>
	/// <para>
	/// Not every comma in C# is one of them. dotnet format leaves the commas of a type
	/// <em>parameter</em> list, a constraint clause, an attribute list, a base list, a declarator
	/// list, an enum body, switch expression arms, a <c>for</c> header, an <c>orderby</c> and a
	/// pattern's subpatterns exactly as they are, while governing the commas of argument and
	/// parameter lists, type <em>argument</em> lists, initializers, tuples, array ranks, list
	/// patterns and deconstruction designations.
	/// </para>
	/// <para>
	/// The split looks arbitrary and is not one Kerf chose; matching it is what conformance means.
	/// Only call these from a position on the governed side.
	/// </para>
	/// </remarks>
	public static void BeforeComma(PrintContext context)
	{
		if (context.Options.SpaceBeforeComma)
			context.Arena.Synthetic(SyntheticText.Space);
	}

	/// <summary>After a comma, where the list cannot break.</summary>
	public static void AfterComma(PrintContext context)
	{
		if (context.Options.SpaceAfterComma)
			context.Arena.Synthetic(SyntheticText.Space);
	}

	/// <summary>
	/// After a comma, where the list breaks one item per line if it does not fit.
	/// </summary>
	/// <remarks>
	/// A normal line is a space when flat, which is the wrong answer under
	/// <c>space_after_comma = false</c>; a soft line is nothing when flat. Both still break, so
	/// turning the space off does not cost the list its ability to wrap.
	/// </remarks>
	public static void AfterCommaBreakable(PrintContext context)
	{
		if (context.Options.SpaceAfterComma)
			context.Arena.Line();
		else
			context.Arena.SoftLine();
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
