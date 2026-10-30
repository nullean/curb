using Nullean.Curb.Documents;

namespace Nullean.Curb.Printing;

/// <summary>What the shared layout printer needs to know about C#.</summary>
/// <remarks>
/// An empty struct, so <see cref="DocPrinter{TLanguage}"/> is specialised for it and both members inline
/// at their call sites — see <see cref="ILayoutLanguage"/>.
/// </remarks>
internal readonly struct CSharpLayout : ILayoutLanguage
{
	public static string[] SyntheticTable => CSharpSyntheticText.Table;

	public static bool CanWeld(char left, char right) => WeldDetector.CanWeld(left, right);
}

/// <summary>
/// The strings C# printers may insert beyond the shared ones in <see cref="SyntheticText"/>.
/// </summary>
/// <remarks>
/// Each corresponds to a token some option adds: a brace for <c>csharp_prefer_braces</c>, a comma for
/// the trailing-comma keys, an arrow for an expression body. The verifier allows exactly these.
/// </remarks>
internal static class CSharpSyntheticText
{
	public const int Comma = SyntheticText.LanguageBase;
	public const int OpenBrace = SyntheticText.LanguageBase + 1;
	public const int CloseBrace = SyntheticText.LanguageBase + 2;
	public const int Semicolon = SyntheticText.LanguageBase + 3;
	public const int Arrow = SyntheticText.LanguageBase + 4;

	public static readonly string[] Table =
	[
		.. SyntheticText.Shared,
		",",
		"{",
		"}",
		";",
		"=>",
	];
}

/// <summary>The one <see cref="DocArena"/> overload that speaks Roslyn, kept out of the language-neutral arena.</summary>
internal static class DocArenaExtensions
{
	/// <summary>Emits a span of the original source verbatim.</summary>
	public static void SourceText(this DocArena arena, Microsoft.CodeAnalysis.Text.TextSpan span) =>
		arena.SourceText(span.Start, span.Length);
}
