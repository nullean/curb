namespace Nullean.Curb;

/// <summary>Why a file was not formatted.</summary>
public enum FormatStatus
{
	/// <summary>Formatted successfully.</summary>
	Formatted,

	/// <summary>The source does not parse. Curb never re-prints from a recovered tree.</summary>
	SyntaxError,

	/// <summary>Formatting produced output that lost or altered content, so it was discarded.</summary>
	VerificationFailed,

	/// <summary>The syntax nests deeper than Curb will follow.</summary>
	TooDeep,

	/// <summary>
	/// The formatter was asked and declined the file by its own rules, such as a match in Fantomas's
	/// <c>.fantomasignore</c>. Left as it was and counted as skipped.
	/// </summary>
	Ignored,
}

/// <summary>The outcome of formatting one file.</summary>
/// <param name="Status">Whether it worked, and if not, why.</param>
/// <param name="Changed">True when the output differs from the input.</param>
/// <param name="Text">The formatted text, or null when it was not requested or not produced.</param>
/// <param name="Coverage">Share of tokens emitted by a real printer rather than verbatim.</param>
/// <param name="Message">Detail for a non-successful status.</param>
public readonly record struct FormatResult(
	FormatStatus Status,
	bool Changed,
	string? Text,
	double Coverage,
	string? Message)
{
	public bool Success => Status == FormatStatus.Formatted;
}
