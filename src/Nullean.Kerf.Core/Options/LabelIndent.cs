namespace Nullean.Kerf.Options;

/// <summary>Where a <c>goto</c> label sits relative to the statements around it.</summary>
/// <remarks>
/// The value set of <c>csharp_indent_labels</c>, default <see cref="OneLessThanCurrent"/>.
/// </remarks>
public enum LabelIndent : byte
{
	/// <summary><c>one_less_than_current</c>: one level left of the surrounding statements. The default.</summary>
	OneLessThanCurrent = 0,

	/// <summary><c>no_change</c>: level with the surrounding statements, where the author left it.</summary>
	NoChange = 1,

	/// <summary><c>flush_left</c>: at column zero, whatever the surrounding indent.</summary>
	FlushLeft = 2,
}
