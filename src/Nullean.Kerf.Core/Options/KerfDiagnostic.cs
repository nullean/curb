namespace Nullean.Kerf.Options;

public enum DiagnosticSeverity
{
	Info,
	Warning,
	Error,
}

/// <summary>Something Kerf wants to tell you about your configuration or a file it was given.</summary>
/// <param name="Id">Stable identifier, e.g. <c>KERF1001</c>, so it can be grepped and suppressed.</param>
/// <param name="Severity">How much it matters.</param>
/// <param name="Message">Human-readable text, already including the offending key or value.</param>
public readonly record struct KerfDiagnostic(string Id, DiagnosticSeverity Severity, string Message)
{
	/// <summary>A recognised key whose value Kerf could not parse. The default is kept.</summary>
	public static KerfDiagnostic UnrecognisedValue(string key, string value, string expected) =>
		new("KERF1001", DiagnosticSeverity.Warning,
			$"unrecognised value '{value}' for '{key}'; expected {expected}. Using the default.");

	/// <summary>A <c>csharp_*</c> / <c>dotnet_*</c> key Kerf has never heard of — most often a typo.</summary>
	public static KerfDiagnostic UnknownKey(string key, string? suggestion) =>
		new("KERF1002", DiagnosticSeverity.Warning,
			suggestion is null
				? $"unknown option '{key}'."
				: $"unknown option '{key}'. Did you mean '{suggestion}'?");

	/// <summary>
	/// A genuine IDE0055 option that Kerf recognises but has not implemented yet. Reporting this is
	/// the whole point of tracking known-but-unimplemented separately: silence here would look
	/// exactly like support.
	/// </summary>
	public static KerfDiagnostic NotImplemented(string key) =>
		new("KERF1003", DiagnosticSeverity.Warning,
			$"'{key}' is a supported .NET formatting option but Kerf does not implement it yet, so it has no effect.");

	/// <summary>
	/// A key whose meaning depends on the author's line breaks, set alongside
	/// <c>csharp_keep_existing_linebreaks = false</c>, which removes them from the input.
	/// </summary>
	/// <remarks>
	/// The same reasoning as <see cref="NotImplemented"/>: deterministic layout cannot honour a rule
	/// phrased as "whatever the author wrote", and reading the source anyway is what makes a formatter
	/// disagree with itself on the second run. Silence would be indistinguishable from support, so the
	/// degradation is named along with what it degrades to.
	/// </remarks>
	/// <summary>
	/// <c>csharp_keep_existing_linebreaks = false</c> without a <c>max_line_length</c> to reflow to.
	/// </summary>
	/// <remarks>
	/// Deterministic layout is a function of tokens and width, and the printer treats a missing width as
	/// infinite — so every group that fits would print flat and the whole repository would collapse onto
	/// long lines. Refused rather than given an implied width, because a repository that never named a
	/// width has not asked to be reflowed, and a key spelled "keep existing linebreaks" is not where anyone
	/// would look for the reason it was.
	/// </remarks>
	public static KerfDiagnostic DeterministicLayoutNeedsAWidth() =>
		new("KERF1007", DiagnosticSeverity.Warning,
			"'csharp_keep_existing_linebreaks = false' needs a 'max_line_length' to lay out against; with no "
			+ "width every construct that fits would be joined onto one line. Ignored, so your line breaks "
			+ "are preserved. Set a width and deterministic layout is the default anyway.");

	/// <summary>
	/// A recognised value Kerf refuses because <c>dotnet format</c> would undo it on the next save.
	/// </summary>
	/// <remarks>
	/// The "decides the other way" half of the classification in <c>docs/layout-decisions.md</c>, and the
	/// one failure mode no amount of care inside Kerf can fix: the option is expressible, idempotent under
	/// Kerf alone, and still wrong, because the reference implementation has an opinion of its own. Better
	/// to refuse it loudly than to write layout that Format Document reverts.
	/// </remarks>
	public static KerfDiagnostic OverruledByDotnetFormat(string key, string value, string instead) =>
		new("KERF1006", DiagnosticSeverity.Warning,
			$"'{key} = {value}' is not honoured: dotnet format undoes it, so the layout would not survive "
			+ $"the next Format Document. {instead}");

	/// <summary>
	/// A key Kerf honours only under <c>csharp_keep_existing_linebreaks = false</c>, set without it.
	/// </summary>
	/// <remarks>
	/// The mirror of <see cref="InertUnderDeterministicLayout"/>. Forcing a construct to break moves
	/// anything nested inside it, and in preservation mode other rules read that construct's
	/// indentation from the source — so the second pass formats the file differently. Measured at 140
	/// corpus files. The key is not wrong, it is asking for something only the other mode can give.
	/// </remarks>
	public static KerfDiagnostic RequiresDeterministicLayout(string key) =>
		new("KERF1005", DiagnosticSeverity.Warning,
			$"'{key}' is honoured only with 'csharp_keep_existing_linebreaks = false'. Forcing this "
			+ "construct to wrap moves what is nested inside it, which preserved layout elsewhere then "
			+ "disagrees with on the next run. Ignored here.");

	public static KerfDiagnostic InertUnderDeterministicLayout(string key, string effect) =>
		new("KERF1004", DiagnosticSeverity.Warning,
			$"'{key}' asks about the line breaks you wrote, and 'csharp_keep_existing_linebreaks = false' "
			+ $"means there are none to ask about; {effect}.");

	public override string ToString() => $"{Severity.ToString().ToLowerInvariant()} {Id}: {Message}";
}
