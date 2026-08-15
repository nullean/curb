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

	public override string ToString() => $"{Severity.ToString().ToLowerInvariant()} {Id}: {Message}";
}
