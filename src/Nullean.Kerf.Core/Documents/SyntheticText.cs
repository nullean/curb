namespace Nullean.Kerf.Documents;

/// <summary>
/// The fixed set of strings Kerf may insert that are not present in the source.
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

	public static readonly string[] Table =
	[
		"",
		" ",
		",",
		"{",
		"}",
		";",
	];

	public static string Get(int id) => Table[id];
}
