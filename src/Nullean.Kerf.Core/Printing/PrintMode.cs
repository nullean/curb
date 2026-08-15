namespace Nullean.Kerf.Printing;

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
}
