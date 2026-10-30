namespace Nullean.Curb;

/// <summary>What <c>charset</c> asks for, to the extent Curb writes it.</summary>
public enum Charset : byte
{
	/// <summary>
	/// No <c>charset</c> key. The file keeps whatever byte-order mark it arrived with, which is what
	/// dotnet format does when nothing asks otherwise and is the least churn.
	/// </summary>
	Preserve = 0,

	/// <summary>UTF-8 with no byte-order mark.</summary>
	Utf8 = 1,

	/// <summary>UTF-8 with a byte-order mark.</summary>
	Utf8Bom = 2,
}

/// <summary>Line ending to emit.</summary>
public enum EndOfLine : byte
{
	/// <summary>
	/// The platform's own line ending (<see cref="System.Environment.NewLine"/>) — Roslyn's own default
	/// when <c>end_of_line</c> is unset, so this is what keeps Curb agreeing with the IDE out of the box.
	/// </summary>
	Auto = 0,
	Lf = 1,
	CrLf = 2,

	/// <summary>
	/// A lone <c>\r</c>. Never bound from <c>end_of_line</c>, which refuses <c>cr</c>; XML resolves to it
	/// when the file it is keeping the line endings of uses them.
	/// </summary>
	Cr = 3,
}


/// <summary>
/// The settings the layout printer reads, whatever the language: indentation, width, line endings and
/// the byte-order mark.
/// </summary>
/// <remarks>
/// <para>
/// These are the EditorConfig core keys, so every language Curb formats binds them the same way. Only
/// their defaults differ by language, which is why the binder takes a default <see cref="LayoutOptions"/>
/// rather than assuming one. The defaults here are C#'s, which are Roslyn's own.
/// </para>
/// <para>
/// <see cref="MaxLineLength"/> defaults to <see cref="Off"/>. A language may give the width a second job
/// (C# selects its layout mode from it); the printer only ever treats it as a width.
/// </para>
/// </remarks>
public readonly record struct LayoutOptions
{
	/// <summary>Value of <see cref="MaxLineLength"/> meaning "never reflow".</summary>
	public const int Off = int.MaxValue;

	public LayoutOptions() { }

	/// <summary>Columns per indent level. <c>indent_size</c>.</summary>
	public int IndentSize { get; init; } = 4;

	/// <summary>Columns a tab occupies when measuring width. <c>tab_width</c>.</summary>
	public int TabWidth { get; init; } = 4;

	/// <summary>Indent with tabs rather than spaces. <c>indent_style</c>.</summary>
	public bool UseTabs { get; init; }

	/// <summary>Reflow target. <c>max_line_length</c>. <see cref="Off"/> disables reflow entirely.</summary>
	public int MaxLineLength { get; init; } = Off;

	/// <summary>
	/// <c>charset</c>. Governs the byte-order mark only; Curb reads and writes UTF-8 either way.
	/// </summary>
	/// <remarks>
	/// Defaults to <see cref="Charset.Preserve"/>, so a file keeps the mark it had. Curb used to write
	/// every file without one, which silently stripped the mark from any repository asking for
	/// <c>utf-8-bom</c> — 17,000 files on roslyn, which sets it on every source file.
	/// </remarks>
	public Charset Charset { get; init; } = Charset.Preserve;

	/// <summary><c>end_of_line</c>.</summary>
	public EndOfLine EndOfLine { get; init; } = EndOfLine.Auto;

	/// <summary><c>insert_final_newline</c>.</summary>
	public bool InsertFinalNewLine { get; init; } = true;

	/// <summary><c>trim_trailing_whitespace</c>.</summary>
	public bool TrimTrailingWhitespace { get; init; } = true;

	/// <summary>
	/// Resolves <see cref="EndOfLine.Auto"/> to <see cref="System.Environment.NewLine"/> — the same
	/// fallback Roslyn's own <c>FormattingOptions2.NewLine</c> uses when <c>end_of_line</c> is unset.
	/// </summary>
	internal string ResolveEndOfLine() =>
		EndOfLine switch
		{
			EndOfLine.Lf => "\n",
			EndOfLine.CrLf => "\r\n",
			EndOfLine.Cr => "\r",
			_ => Environment.NewLine,
		};
}
