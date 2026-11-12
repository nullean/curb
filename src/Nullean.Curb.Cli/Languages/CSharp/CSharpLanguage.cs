using System.Globalization;
using EditorConfig.Core;
using Microsoft.CodeAnalysis.CSharp;
using Nullean.Curb.EditorConfig;

namespace Nullean.Curb.Cli.Languages.CSharp;

/// <summary>C#, formatted in process by <see cref="CSharpFormatter"/>.</summary>
internal sealed class CSharpLanguage : IInProcessLanguage
{
	public static CSharpLanguage Instance { get; } = new(LanguageVersion.Default);

	/// <summary>
	/// The extensions without their dots, comma-separated, for the commands that only ever read C#
	/// (<c>doc-tree</c> and <c>print-config</c>). A constant, because an attribute argument has to be one.
	/// </summary>
	public const string ExtensionList = "cs";

	private readonly LanguageVersion _languageVersion;

	private CSharpLanguage(LanguageVersion languageVersion) => _languageVersion = languageVersion;

	/// <summary>C# whose rewrites stop at <paramref name="languageVersion"/>; <see cref="Instance"/> when nothing caps them.</summary>
	public static CSharpLanguage For(LanguageVersion languageVersion) => languageVersion == LanguageVersion.Default ? Instance : new(languageVersion);

	/// <summary>
	/// The lowest of the versions <c>--lang-version</c> was given, as <see cref="FormatOptions.LanguageVersion"/>
	/// reads it; <see cref="LanguageVersion.Default"/> when it was given none. False, after saying why, for
	/// a value that is not a C# version.
	/// </summary>
	/// <remarks>
	/// The lowest, because a multi-targeting project passes one per target framework, and a file every
	/// framework compiles has to compile in the oldest. A number newer than the parser Curb ships with
	/// knows (an SDK that came out after this build of Curb) caps nothing, since every rewrite Curb has
	/// is older than it. <c>latest</c>, <c>preview</c> and <c>default</c> cap nothing either.
	/// </remarks>
	public static bool TryResolveLanguageVersion(IReadOnlyList<string>? values, out LanguageVersion resolved)
	{
		resolved = LanguageVersion.Default;
		if (values is null)
			return true;

		var lowest = LanguageVersion.Preview;
		var any = false;

		foreach (var value in values)
		{
			var trimmed = value.Trim();
			if (trimmed.Length == 0)
				continue;

			if (!LanguageVersionFacts.TryParse(trimmed, out var version))
			{
				if (!double.TryParse(trimmed, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var number) || number < 1)
				{
					Console.Error.WriteLine($"--lang-version: '{value}' is not a C# language version");
					return false;
				}

				version = LanguageVersion.Latest;
			}

			var effective = version.MapSpecifiedToEffectiveVersion();
			if (effective < lowest)
				lowest = effective;
			any = true;
		}

		// Preview and every "latest" spelling map to versions at least as new as anything Curb rewrites to,
		// so a run that named only those resolves to the same uncapped default as one that named nothing.
		if (any && lowest < LanguageVersion.LatestMajor.MapSpecifiedToEffectiveVersion())
			resolved = lowest;

		return true;
	}

	public string Id => "csharp";

	public string[] Extensions { get; } = [".cs"];

	public LanguageOptions BindOptions(FileConfiguration configuration, CurbEditorConfig editorConfig) =>
		_languageVersion == LanguageVersion.Default
			? new CSharpOptions(EditorConfigOptionsBinder.Bind(configuration))
			: new CSharpOptions(EditorConfigOptionsBinder.Bind(configuration) with { LanguageVersion = _languageVersion }, CacheKey(_languageVersion));

	/// <summary>
	/// What the cache key adds for a capped version, which the merged settings cannot show: the same file
	/// under the same <c>.editorconfig</c> formats differently at C# 7.3.
	/// </summary>
	private static Dictionary<string, string> CacheKey(LanguageVersion version) =>
		new(StringComparer.Ordinal) { ["curb_lang_version"] = version.ToDisplayString() };

	public bool SkipSource(string source) => CSharpSource.HasGeneratedHeader(source);

	public ILanguageWorker CreateWorker(bool collectUnhandled) => new CSharpWorker(new CSharpFormatter { UnhandledByKind = collectUnhandled ? [] : null });

	public string DescribeUnhandledKind(int kind) => ((SyntaxKind)kind).ToString();
}

/// <summary>A file's bound <see cref="FormatOptions"/>, carried through the run's queue by reference.</summary>
internal sealed class CSharpOptions(FormatOptions options, IReadOnlyDictionary<string, string>? additionalProperties = null) : LanguageOptions
{
	public readonly FormatOptions Value = options;

	public override IReadOnlyDictionary<string, string>? AdditionalProperties => additionalProperties;

	public override LayoutOptions Layout => Value.Layout;

	public override bool Excluded => Value.Excluded;
}

/// <summary>One thread's <see cref="CSharpFormatter"/>.</summary>
internal sealed class CSharpWorker(CSharpFormatter formatter) : ILanguageWorker
{
	public FormatResult Format(string source, LanguageOptions options, string fileName, bool produceText, bool expandUnhandled, bool verifyRoundTrip) =>
		formatter.Format(
			source,
			((CSharpOptions)options).Value,
			fileName,
			produceText: produceText,
			expandUnhandled: expandUnhandled,
			verifyRoundTrip: verifyRoundTrip
		);

	public int RoundTripsChecked => formatter.RoundTripsChecked;

	public Dictionary<int, int>? UnhandledByKind => formatter.UnhandledByKind;

	public void Dispose() => formatter.Dispose();
}
