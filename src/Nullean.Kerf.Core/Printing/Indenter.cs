namespace Nullean.Kerf.Printing;

/// <summary>
/// Turns an indent level into the characters that represent it.
/// </summary>
/// <remarks>
/// Indent is a plain <c>int</c> level, not a string, so shifting it — including <i>negative</i>
/// shifts — is arithmetic. That matters: <c>csharp_indent_braces</c> and <c>csharp_indent_labels</c>
/// both need to dedent relative to the enclosing context, and <c>csharp_indent_labels =
/// flush_left</c> needs column 0 outright. Emitting is one span copy out of a preallocated pad.
/// </remarks>
internal sealed class Indenter
{
	private char[] _pad;
	private readonly int _charactersPerLevel;
	private readonly int _columnsPerLevel;
	private readonly char _character;

	public Indenter(bool useTabs, int indentSize, int tabWidth)
	{
		_character = useTabs ? '\t' : ' ';

		// How many characters a level costs and how wide it prints are the same number for spaces
		// and different for tabs: one character, tab_width columns. Conflating them made every
		// indent level count as one column against max_line_length, so a tab-indented file came out
		// with lines far wider than it asked for — 231 of them on the corpus, up to 814 columns.
		_charactersPerLevel = useTabs ? 1 : indentSize;
		_columnsPerLevel = useTabs ? Math.Max(1, tabWidth) : indentSize;

		_pad = new char[Math.Max(1, _charactersPerLevel) * 64];
		Array.Fill(_pad, _character);
	}

	/// <summary>Columns a level occupies, used for width accounting.</summary>
	public int ColumnsFor(int level) => Math.Max(0, level) * _columnsPerLevel;

	public ReadOnlySpan<char> For(int level)
	{
		if (level <= 0)
			return [];

		var needed = level * _charactersPerLevel;
		if (needed > _pad.Length)
		{
			var grown = new char[Math.Max(needed, _pad.Length * 2)];
			Array.Fill(grown, _character);
			_pad = grown;
		}

		return _pad.AsSpan(0, needed);
	}
}
