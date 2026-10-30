namespace Nullean.Curb.Documents;

/// <summary>
/// The strings every language may insert that are not present in the source, and the ids they occupy.
/// </summary>
/// <remarks>
/// <para>
/// A <see cref="DocKind.SynText"/> stores an index into a table rather than a string, so emitting
/// synthesised text allocates nothing. Each language owns its table (see
/// <see cref="Printing.ILayoutLanguage.SyntheticTable"/>), because what a language may insert is part of
/// what its verifier has to allow: C# may add a brace or a comma, and XML must never add either. The set
/// is deliberately closed and small.
/// </para>
/// <para>
/// The ids below <see cref="LanguageBase"/> are the ones the printer itself interprets, so they mean the
/// same thing in every table. A language table starts with <see cref="Shared"/> and appends its own
/// strings from <see cref="LanguageBase"/> on.
/// </para>
/// </remarks>
internal static class SyntheticText
{
	public const int Empty = 0;
	public const int Space = 1;

	/// <summary>
	/// A space that is written only if the next thing printed is not already one.
	/// </summary>
	/// <remarks>
	/// For the gap after an inline <c>/* */</c> comment. The comment cannot know whether the printer
	/// will put a space in front of what follows: it does after <c>=</c> and does not after <c>(</c>.
	/// Writing its own space as well doubled the gap to <c>= /* c */  value</c>.
	/// </remarks>
	public const int MergingSpace = 2;

	/// <summary>
	/// Writes nothing, and holds back the next space unless the next text is not the source offset in
	/// <c>B</c>.
	/// </summary>
	/// <remarks>
	/// For an inline <c>/* */</c> comment the source wrote directly against the next token, as in
	/// <c>{ /* ignore */}</c>. dotnet format keeps that, but the printer puts its own space in front
	/// of a closing brace, an operand and so on, and the comment cannot stop it. Checking the offset
	/// keeps the space whenever something else lands there, such as a brace Curb inserts.
	/// </remarks>
	public const int Glue = 3;

	/// <summary>The first id a language may give a string of its own.</summary>
	public const int LanguageBase = 4;

	/// <summary>The strings behind the shared ids, which every language table begins with.</summary>
	public static readonly string[] Shared = ["", " ", " ", ""];
}
