using EditorConfig.Core;
using Nullean.Curb.EditorConfig;
using Nullean.Curb.Xml;

namespace Nullean.Curb.Cli.Languages.Xml;

/// <summary>
/// MSBuild project files, solution files, and whatever other XML a repository opts in, formatted in
/// process by <see cref="XmlFormatter"/>.
/// </summary>
/// <remarks>
/// On by default for project, props, targets, solution and <c>nuget.config</c> files. A file whose
/// resolved <c>.editorconfig</c> sets <c>curb_xml_format = false</c> is not claimed at all: it is never
/// read and never counted. Other XML is claimed only when <c>curb_xml_extensions</c> names its extension.
/// </remarks>
internal sealed class XmlLanguage : IInProcessLanguage
{
	public static XmlLanguage Instance { get; } = new();

	private XmlLanguage() { }

	public string Id => "xml";

	/// <summary>What the walk hands over: the MSBuild and solution extensions, and every one a repository may opt in.</summary>
	public string[] Extensions { get; } = [.. XmlOptions.MsBuildExtensions, ".slnx", .. XmlOptions.OptInExtensions];

	public LanguageOptions BindOptions(FileConfiguration configuration, CurbEditorConfig editorConfig)
	{
		var explicitProperties = editorConfig.ExplicitFor(configuration.FileName);
		var options = XmlOptionsBinder.Bind(configuration, explicitProperties);
		return new XmlLanguageOptions(options, options.Enabled && Formats(configuration.FileName, options), explicitProperties);
	}

	/// <summary>
	/// Whether an enabled file is one XML formats: always for MSBuild, solution and <c>nuget.config</c>
	/// files, and for the rest only when <c>curb_xml_extensions</c> names its extension.
	/// </summary>
	private static bool Formats(string path, in XmlOptions options)
	{
		if (options.Profile != XmlProfile.Generic || Path.GetFileName(path.AsSpan()).Equals("nuget.config", StringComparison.OrdinalIgnoreCase))
			return true;

		foreach (var extension in options.Extensions)
		{
			if (path.EndsWith(extension, StringComparison.OrdinalIgnoreCase))
				return true;
		}

		return false;
	}

	public bool SkipSource(string source) => false;

	public ILanguageWorker CreateWorker(bool collectUnhandled) => new XmlWorker(new XmlFormatter());

	public string DescribeUnhandledKind(int kind) => "xml";
}

/// <summary>A file's bound <see cref="XmlOptions"/>.</summary>
internal sealed class XmlLanguageOptions(XmlOptions options, bool claimed, IReadOnlyDictionary<string, string> explicitProperties) : LanguageOptions
{
	public readonly XmlOptions Value = options;

	public override LayoutOptions Layout => Value.Layout;

	public override bool Excluded => Value.Excluded;

	public override bool Claimed { get; } = claimed;

	public override IReadOnlyDictionary<string, string>? AdditionalProperties { get; } = explicitProperties;

	public override bool ReportsCoverage => false;
}

/// <summary>One thread's <see cref="XmlFormatter"/>.</summary>
internal sealed class XmlWorker(XmlFormatter formatter) : ILanguageWorker
{
	public FormatResult Format(
		string source,
		LanguageOptions options,
		string fileName,
		bool produceText,
		bool expandUnhandled,
		bool verifyRoundTrip) =>
		formatter.Format(source, ((XmlLanguageOptions)options).Value, produceText);

	/// <summary>Always zero: every XML output is re-lexed and verified, which is not a second parse.</summary>
	public int RoundTripsChecked => 0;

	public Dictionary<int, int>? UnhandledByKind => null;

	public void Dispose() => formatter.Dispose();
}
