namespace Nullean.Curb.Options;

/// <summary>Spacing around binary and assignment operators.</summary>
public enum BinaryOperatorSpacing : byte
{
	/// <summary>A space on each side. The default.</summary>
	BeforeAndAfter = 0,

	/// <summary>No space on either side — <c>a+b</c>.</summary>
	None = 1,

	/// <summary>Reproduce whatever the author wrote. See <see cref="DeclarationSpacing.Ignore"/>.</summary>
	Ignore = 2,
}

/// <summary>Spacing within a declaration statement.</summary>
public enum DeclarationSpacing : byte
{
	/// <summary>Normalise it like anything else. The default.</summary>
	Normalise = 0,

	/// <summary>
	/// Reproduce whatever the author wrote, exactly.
	/// </summary>
	/// <remarks>
	/// <para>
	/// The one option that asks Curb not to format. The construct is emitted from the source
	/// verbatim, so alignment the author put in — a column of <c>=</c> signs, say — survives
	/// unchanged, and nothing inside it is reflowed however narrow <c>max_line_length</c> is.
	/// </para>
	/// <para>
	/// It applies to the whole construct, initializer included: under this setting dotnet format
	/// leaves the operators inside <c>var b = x   &gt;   1;</c> alone as well as the declaration's
	/// own spacing.
	/// </para>
	/// </remarks>
	Ignore = 1,
}
