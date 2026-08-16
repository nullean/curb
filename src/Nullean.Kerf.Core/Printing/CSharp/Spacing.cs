using Nullean.Kerf.Documents;
using Nullean.Kerf.Options;

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

	/// <summary>Just inside a control-flow header's parentheses.</summary>
	public static void InsideControlFlowParens(PrintContext context)
	{
		if (context.Options.SpaceBetweenParentheses.HasFlag(ParenthesisSpacing.ControlFlowStatements))
			context.Arena.Synthetic(SyntheticText.Space);
	}

	/// <summary>
	/// Just inside a control-flow header's parentheses, where the condition can break.
	/// </summary>
	/// <remarks>
	/// Same soft-versus-normal line reasoning as <see cref="AfterCommaBreakable"/>. Only call this
	/// from inside a group — an ungrouped line always breaks, which would put every condition on a
	/// line of its own.
	/// </remarks>
	public static void InsideControlFlowParensBreakable(PrintContext context)
	{
		if (context.Options.SpaceBetweenParentheses.HasFlag(ParenthesisSpacing.ControlFlowStatements))
			context.Arena.Line();
		else
			context.Arena.SoftLine();
	}

	/// <summary>Just inside a parenthesised expression — <c>( a + b )</c>.</summary>
	public static void InsideExpressionParens(PrintContext context)
	{
		if (context.Options.SpaceBetweenParentheses.HasFlag(ParenthesisSpacing.Expressions))
			context.Arena.Synthetic(SyntheticText.Space);
	}

	/// <summary>Just inside a cast's parentheses — <c>( int )value</c>.</summary>
	public static void InsideCastParens(PrintContext context)
	{
		if (context.Options.SpaceBetweenParentheses.HasFlag(ParenthesisSpacing.TypeCasts))
			context.Arena.Synthetic(SyntheticText.Space);
	}

	/// <summary>Between a method's name and its opening parenthesis.</summary>
	/// <remarks>
	/// Emitted by the declaring printer rather than by the parameter list, because a lambda has no
	/// name for the space to sit after and must not get one.
	/// </remarks>
	public static void BeforeDeclarationParens(PrintContext context)
	{
		if (context.Options.SpaceBeforeDeclarationParameterList)
			context.Arena.Synthetic(SyntheticText.Space);
	}

	/// <summary>Just inside a parameter list's parentheses.</summary>
	public static void InsideDeclarationParens(PrintContext context)
	{
		if (context.Options.SpaceInDeclarationParameterList)
			context.Arena.Synthetic(SyntheticText.Space);
	}

	/// <summary>Just inside a parameter list's parentheses, where it can break.</summary>
	public static void InsideDeclarationParensBreakable(PrintContext context)
	{
		if (context.Options.SpaceInDeclarationParameterList)
			context.Arena.Line();
		else
			context.Arena.SoftLine();
	}

	/// <summary>Inside an empty parameter list — <c>M( )</c>.</summary>
	public static void InsideEmptyDeclarationParens(PrintContext context)
	{
		if (context.Options.SpaceInEmptyDeclarationParameterList)
			context.Arena.Synthetic(SyntheticText.Space);
	}

	/// <summary>Between a call's name and its opening parenthesis.</summary>
	public static void BeforeCallParens(PrintContext context)
	{
		if (context.Options.SpaceBeforeCallArgumentList)
			context.Arena.Synthetic(SyntheticText.Space);
	}

	/// <summary>Just inside an argument list's parentheses.</summary>
	public static void InsideCallParens(PrintContext context)
	{
		if (context.Options.SpaceInCallArgumentList)
			context.Arena.Synthetic(SyntheticText.Space);
	}

	/// <summary>Just inside an argument list's parentheses, where it can break.</summary>
	public static void InsideCallParensBreakable(PrintContext context)
	{
		if (context.Options.SpaceInCallArgumentList)
			context.Arena.Line();
		else
			context.Arena.SoftLine();
	}

	/// <summary>Inside an empty argument list — <c>M( )</c>.</summary>
	public static void InsideEmptyCallParens(PrintContext context)
	{
		if (context.Options.SpaceInEmptyCallArgumentList)
			context.Arena.Synthetic(SyntheticText.Space);
	}

	/// <summary>
	/// Before an opening square bracket.
	/// </summary>
	/// <remarks>
	/// Array types and creations, element access and indexer declarations only. An attribute list,
	/// a collection expression and a list pattern all keep their bracket where it is, since the
	/// thing before it is not something a bracket attaches to.
	/// </remarks>
	public static void BeforeOpenBracket(PrintContext context)
	{
		if (context.Options.SpaceBeforeOpenSquareBrackets)
			context.Arena.Synthetic(SyntheticText.Space);
	}

	/// <summary>
	/// Just inside a pair of brackets holding nothing — <c>int[ ]</c>.
	/// </summary>
	/// <remarks>
	/// A rank specifier of a multi-dimensional array counts as empty even though it holds commas:
	/// <c>int[,]</c> becomes <c>int[ , ]</c>, one space per slot. The comma options do not reach
	/// those commas — there is nothing on either side of them for a comma to separate — so this
	/// spaces them itself.
	/// </remarks>
	public static void InsideEmptyBrackets(PrintContext context)
	{
		if (context.Options.SpaceBetweenEmptySquareBrackets)
			context.Arena.Synthetic(SyntheticText.Space);
	}

	/// <summary>Just inside a pair of brackets holding something — <c>a[ 0 ]</c>.</summary>
	public static void InsideBrackets(PrintContext context)
	{
		if (context.Options.SpaceBetweenSquareBrackets)
			context.Arena.Synthetic(SyntheticText.Space);
	}

	/// <summary>
	/// Just inside a pair of brackets whose contents break one per line when they do not fit.
	/// </summary>
	/// <remarks>Same soft-versus-normal line reasoning as <see cref="AfterCommaBreakable"/>.</remarks>
	public static void InsideBracketsBreakable(PrintContext context)
	{
		if (context.Options.SpaceBetweenSquareBrackets)
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
		if (context.Options.SpaceAroundBinaryOperators == BinaryOperatorSpacing.BeforeAndAfter)
			context.Arena.Synthetic(SyntheticText.Space);
	}
}
