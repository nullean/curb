using EditorConfig.Core;
using Nullean.Curb.EditorConfig;
using Nullean.Curb.Razor;

namespace Nullean.Curb.Cli.Languages.Razor;

/// <summary>
/// The C# in the <c>@code</c> and <c>@functions</c> blocks of <c>.razor</c> and <c>.cshtml</c> files,
/// formatted in process by <see cref="RazorFormatter"/>. Experimental, and off unless a repository opts in.
/// </summary>
/// <remarks>
/// <para>
/// A file is claimed only when <c>curb_razor_format_code_blocks = true</c> comes from a section that names
/// its files, such as <c>[*.razor]</c> or <c>[*.{cs,razor}]</c>; the same key in <c>[*]</c> does not
/// count, so a catch-all section written for something else cannot switch on an experimental feature.
/// Without it a Razor file is never read and never counted.
/// </para>
/// <para>
/// The C# options are bound from the file's whole resolved configuration, exactly as a <c>.cs</c> file's
/// are, so <c>[*.{cs,razor}]</c> gives both the same style.
/// </para>
/// </remarks>
internal sealed class RazorLanguage : IInProcessLanguage
{
	public static RazorLanguage Instance { get; } = new();

	private RazorLanguage() { }

	public string Id => "razor";

	public string[] Extensions { get; } = [".razor", ".cshtml"];

	/// <remarks>
	/// The merged settings are asked first: a key no section sets cannot come from an explicit one, and
	/// that is the answer for every Razor file in a repository that has not opted in, so declining them
	/// costs a dictionary lookup rather than a second walk over the sections.
	/// </remarks>
	public LanguageOptions BindOptions(FileConfiguration configuration, CurbEditorConfig editorConfig) =>
		configuration.Properties.ContainsKey(RazorFormatter.EnableKey) && IsEnabled(editorConfig.ExplicitFor(configuration.FileName))
			? new RazorOptions(EditorConfigOptionsBinder.Bind(configuration))
			: RazorOptions.Declined;

	/// <summary>A repository whose <c>.editorconfig</c> files never mention the key has not opted in.</summary>
	public bool DeclinesUnresolved(string path, CurbEditorConfig editorConfig) =>
		!editorConfig.Mentions(path, RazorFormatter.EnableKey);

	public string? NamedDeclineReason =>
		$"Razor formatting is not enabled for it; set {RazorFormatter.EnableKey} = true in a [*.razor] or [*.cshtml] section";

	/// <summary>Whether <paramref name="explicitProperties"/> turns Razor formatting on.</summary>
	internal static bool IsEnabled(IReadOnlyDictionary<string, string> explicitProperties) =>
		explicitProperties.TryGetValue(RazorFormatter.EnableKey, out var value)
		&& value.Equals("true", StringComparison.OrdinalIgnoreCase);

	public bool SkipSource(string source) => false;

	public ILanguageWorker CreateWorker(bool collectUnhandled) => new RazorWorker(new RazorFormatter());

	public string DescribeUnhandledKind(int kind) => "razor";
}

/// <summary>A Razor file's bound C# options.</summary>
internal sealed class RazorOptions : LanguageOptions
{
	/// <summary>For every file that did not opt in. Nothing reads it but <see cref="Claimed"/>.</summary>
	public static RazorOptions Declined { get; } = new(new FormatOptions(), claimed: false);

	public readonly FormatOptions Value;

	public RazorOptions(FormatOptions options, bool claimed = true)
	{
		Value = options;
		Claimed = claimed;

		// The byte-order mark is outside every block, so it stays as the file has it whatever `charset`
		// says: Curb changes nothing in a Razor file but the C# it formats.
		Layout = options.Layout with { Charset = Charset.Preserve };
	}

	public override LayoutOptions Layout { get; }

	public override bool Excluded => Value.Excluded;

	public override bool Claimed { get; }

	/// <summary>Printer coverage and the round-trip figure are a <c>.cs</c> file's; Razor stays out of both.</summary>
	public override bool ReportsCoverage => false;
}

/// <summary>One thread's <see cref="RazorFormatter"/>.</summary>
internal sealed class RazorWorker(RazorFormatter formatter) : ILanguageWorker
{
	public FormatResult Format(
		string source,
		LanguageOptions options,
		string fileName,
		bool produceText,
		bool expandUnhandled,
		bool verifyRoundTrip) =>
		formatter.Format(
			source, ((RazorOptions)options).Value, produceText, verifyRoundTrip,
			component: !fileName.EndsWith(".cshtml", StringComparison.OrdinalIgnoreCase));

	/// <summary>Zero, since the run's round-trip line counts <c>.cs</c> files only.</summary>
	public int RoundTripsChecked => 0;

	public Dictionary<int, int>? UnhandledByKind => null;

	public void Dispose() => formatter.Dispose();
}
