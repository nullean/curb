namespace Nullean.Curb.Printing;

/// <summary>How a region of the document is being laid out.</summary>
internal enum PrintMode : byte
{
	/// <summary>Everything on one line: normal lines become spaces, soft lines vanish.</summary>
	Flat = 0,

	/// <summary>Lines become newlines followed by the current indent.</summary>
	Break = 1,

	/// <summary>
	/// Flat, and not reconsidered even if it overflows the width. Distinct from
	/// <see cref="Flat"/> because a hard line inside it stays flat, which is what makes
	/// <c>csharp_preserve_single_line_*</c> work.
	/// </summary>
	ForceFlat = 2,

	/// <summary>
	/// No mode decided yet. Only ever seen in the printer's group table, for a group the walk has
	/// not reached.
	/// </summary>
	/// <remarks>
	/// Given a value of its own rather than letting a cleared table read as <see cref="Flat"/>,
	/// because an <c>IfBreak</c> aimed at another group has to tell "that group chose flat" from
	/// "that group has not been measured yet" — and answering the second as though it were the first
	/// is how such a construct silently lays itself out against a decision nobody made. Numbered
	/// after the real modes so that <c>default(PrintMode)</c> keeps meaning <see cref="Flat"/>.
	/// </remarks>
	Unset = 3,
}
