namespace Nullean.Curb.Documents;

/// <summary>
/// The fixed set of strings Curb may insert that are not present in the source.
/// </summary>
/// <remarks>
/// A <see cref="DocKind.SynText"/> stores an index into <see cref="Table"/> rather than a string, so
/// emitting synthesised text allocates nothing. The set is deliberately closed and small: it is also
/// the whitelist the token-coverage verifier checks against when deciding whether output that does
/// not appear in the source is legitimate.
/// </remarks>
internal static class SyntheticText
{
	public const int Empty = 0;
	public const int Space = 1;
	public const int Comma = 2;
	public const int OpenBrace = 3;
	public const int CloseBrace = 4;
	public const int Semicolon = 5;
	public const int Arrow = 6;

	/// <summary>
	/// A space that is written only if the next thing printed is not already one.
	/// </summary>
	/// <remarks>
	/// For the gap after an inline <c>/* */</c> comment. The comment cannot know whether the printer
	/// will put a space in front of what follows: it does after <c>=</c> and does not after <c>(</c>.
	/// Writing its own space as well doubled the gap to <c>= /* c */  value</c>.
	/// </remarks>
	public const int MergingSpace = 7;

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
	public const int Glue = 8;

	public static readonly string[] Table =
	[
		"",
		" ",
		",",
		"{",
		"}",
		";",
		"=>",
		" ",
		"",
	];

	public static string Get(int id) => Table[id];
}
