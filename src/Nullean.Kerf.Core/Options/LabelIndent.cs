namespace Nullean.Kerf.Options;

/// <summary>Where a <c>goto</c> label sits relative to the statements around it.</summary>
/// <remarks>
/// The value set of <c>csharp_indent_labels</c>, default <see cref="OneLessThanCurrent"/>.
/// </remarks>
public enum LabelIndent : byte
{
	/// <summary>One indent level left of the surrounding statements. The default.</summary>
	OneLessThanCurrent = 0,

	/// <summary>Level with the surrounding statements.</summary>
	NoIndent = 1,

	/// <summary>
	/// Documented as depending on whether the label sits in a block.
	/// </summary>
	/// <remarks>
	/// dotnet format produced output identical to <see cref="NoIndent"/> for every position tested —
	/// directly in a method body, inside a nested block, inside an if body, inside a switch section
	/// and as a loop's embedded statement — so Kerf treats the two the same. If a shape is found
	/// where they differ, this is where the difference goes.
	/// </remarks>
	FlipWhenBlock = 2,
}
