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
	private readonly int _width;
	private readonly char _character;

	public Indenter(bool useTabs, int indentSize)
	{
		_character = useTabs ? '\t' : ' ';
		_width = useTabs ? 1 : indentSize;
		_pad = new char[Math.Max(1, _width) * 64];
		Array.Fill(_pad, _character);
	}

	/// <summary>Columns a level occupies, used for width accounting.</summary>
	public int ColumnsFor(int level) => Math.Max(0, level) * _width;

	public ReadOnlySpan<char> For(int level)
	{
		if (level <= 0)
			return [];

		var needed = level * _width;
		if (needed > _pad.Length)
		{
			var grown = new char[Math.Max(needed, _pad.Length * 2)];
			Array.Fill(grown, _character);
			_pad = grown;
		}

		return _pad.AsSpan(0, needed);
	}
}
