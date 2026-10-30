using EditorConfig.Core;
using Microsoft.CodeAnalysis.CSharp;
using Nullean.Curb.EditorConfig;

namespace Nullean.Curb.Cli.Languages.CSharp;

/// <summary>C#, formatted in process by <see cref="CSharpFormatter"/>.</summary>
internal sealed class CSharpLanguage : IInProcessLanguage
{
	public static CSharpLanguage Instance { get; } = new();

	/// <summary>
	/// The extensions without their dots, comma-separated, for the commands that only ever read C#
	/// (<c>doc-tree</c> and <c>print-config</c>). A constant, because an attribute argument has to be one.
	/// </summary>
	public const string ExtensionList = "cs";

	private CSharpLanguage() { }

	public string Id => "csharp";

	public string[] Extensions { get; } = [".cs"];

	public LanguageOptions BindOptions(FileConfiguration configuration, CurbEditorConfig editorConfig) =>
		new CSharpOptions(EditorConfigOptionsBinder.Bind(configuration));

	public bool SkipSource(string source) => CSharpSource.HasGeneratedHeader(source);

	public ILanguageWorker CreateWorker(bool collectUnhandled) =>
		new CSharpWorker(new CSharpFormatter { UnhandledByKind = collectUnhandled ? [] : null });

	public string DescribeUnhandledKind(int kind) => ((SyntaxKind)kind).ToString();
}

/// <summary>A file's bound <see cref="FormatOptions"/>, carried through the run's queue by reference.</summary>
internal sealed class CSharpOptions(FormatOptions options) : LanguageOptions
{
	public readonly FormatOptions Value = options;

	public override LayoutOptions Layout => Value.Layout;

	public override bool Excluded => Value.Excluded;
}

/// <summary>One thread's <see cref="CSharpFormatter"/>.</summary>
internal sealed class CSharpWorker(CSharpFormatter formatter) : ILanguageWorker
{
	public FormatResult Format(
		string source,
		LanguageOptions options,
		string fileName,
		bool produceText,
		bool expandUnhandled,
		bool verifyRoundTrip) =>
		formatter.Format(
			source, ((CSharpOptions)options).Value, fileName,
			produceText: produceText, expandUnhandled: expandUnhandled, verifyRoundTrip: verifyRoundTrip);

	public int RoundTripsChecked => formatter.RoundTripsChecked;

	public Dictionary<int, int>? UnhandledByKind => formatter.UnhandledByKind;

	public void Dispose() => formatter.Dispose();
}
