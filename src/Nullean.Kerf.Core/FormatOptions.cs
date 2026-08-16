using Nullean.Kerf.Options;

namespace Nullean.Kerf;

/// <summary>Line ending to emit.</summary>
public enum EndOfLine : byte
{
	/// <summary>Match whatever the source uses, defaulting to LF.</summary>
	Auto = 0,
	Lf = 1,
	CrLf = 2,
}

/// <summary>
/// Every setting that influences layout, resolved from <c>.editorconfig</c>.
/// </summary>
/// <remarks>
/// <para>
/// Defaults deliberately mirror Roslyn's own, so Kerf agrees with Visual Studio and Rider out of the
/// box rather than reformatting a repository on first run. The one deliberate exception is
/// <see cref="MaxLineLength"/>, which defaults to <see cref="Off"/>: reflow is opt-in, because a
/// formatter that rewrites every file the moment it is installed does not get installed twice.
/// </para>
/// <para>
/// Storage becomes generated bit-packed fields when the option catalog lands and this grows to the
/// full 39-key surface; the shape is a struct now so that change stays behind these accessors.
/// </para>
/// </remarks>
public readonly record struct FormatOptions
{
	/// <summary>Value of <see cref="MaxLineLength"/> meaning "never reflow".</summary>
	public const int Off = int.MaxValue;

	public FormatOptions() { }

	/// <summary>Columns per indent level. <c>indent_size</c>.</summary>
	public int IndentSize { get; init; } = 4;

	/// <summary>Columns a tab occupies when measuring width. <c>tab_width</c>.</summary>
	public int TabWidth { get; init; } = 4;

	/// <summary>Indent with tabs rather than spaces. <c>indent_style</c>.</summary>
	public bool UseTabs { get; init; }

	/// <summary>Reflow target. <c>max_line_length</c>. <see cref="Off"/> disables reflow entirely.</summary>
	public int MaxLineLength { get; init; } = Off;

	/// <summary><c>end_of_line</c>.</summary>
	public EndOfLine EndOfLine { get; init; } = EndOfLine.Auto;

	/// <summary><c>insert_final_newline</c>.</summary>
	public bool InsertFinalNewLine { get; init; } = true;

	/// <summary><c>trim_trailing_whitespace</c>.</summary>
	public bool TrimTrailingWhitespace { get; init; } = true;

	/// <summary>
	/// Which constructs put their opening brace on a new line.
	/// <c>csharp_new_line_before_open_brace</c>, default <see cref="BraceStyle.All"/> (Allman).
	/// </summary>
	public BraceStyle NewLineBeforeOpenBrace { get; init; } = BraceStyle.All;

	/// <summary>
	/// Put <c>else</c> on a line of its own rather than joining it to the preceding brace.
	/// <c>csharp_new_line_before_else</c>, default true.
	/// </summary>
	public bool NewLineBeforeElse { get; init; } = true;

	/// <summary>
	/// Put <c>catch</c> on a line of its own rather than joining it to the preceding brace.
	/// <c>csharp_new_line_before_catch</c>, default true.
	/// </summary>
	public bool NewLineBeforeCatch { get; init; } = true;

	/// <summary>
	/// Put <c>finally</c> on a line of its own rather than joining it to the preceding brace.
	/// <c>csharp_new_line_before_finally</c>, default true.
	/// </summary>
	public bool NewLineBeforeFinally { get; init; } = true;

	/// <summary>Space between a cast and its operand. <c>csharp_space_after_cast</c>, default false.</summary>
	public bool SpaceAfterCast { get; init; }

	/// <summary>
	/// Space between a control-flow keyword and its parenthesis.
	/// <c>csharp_space_after_keywords_in_control_flow_statements</c>, default true.
	/// </summary>
	public bool SpaceAfterKeywordsInControlFlowStatements { get; init; } = true;

	/// <summary><c>csharp_space_before_colon_in_inheritance_clause</c>, default true.</summary>
	public bool SpaceBeforeColonInInheritanceClause { get; init; } = true;

	/// <summary><c>csharp_space_after_colon_in_inheritance_clause</c>, default true.</summary>
	public bool SpaceAfterColonInInheritanceClause { get; init; } = true;

	/// <summary>
	/// Spaces around binary and assignment operators. <c>csharp_space_around_binary_operators</c>,
	/// default <c>before_and_after</c>; <c>none</c> sets this false. The third value, <c>ignore</c>,
	/// is not implemented.
	/// </summary>
	public bool SpaceAroundBinaryOperators { get; init; } = true;

	/// <summary><c>csharp_space_after_comma</c>, default true.</summary>
	public bool SpaceAfterComma { get; init; } = true;

	/// <summary><c>csharp_space_before_comma</c>, default false.</summary>
	public bool SpaceBeforeComma { get; init; }

	/// <summary>
	/// Which parenthesised constructs get a space just inside their parentheses.
	/// <c>csharp_space_between_parentheses</c>, default none.
	/// </summary>
	public ParenthesisSpacing SpaceBetweenParentheses { get; init; } = ParenthesisSpacing.None;

	/// <summary><c>csharp_space_between_method_declaration_parameter_list_parentheses</c>, default false.</summary>
	public bool SpaceInDeclarationParameterList { get; init; }

	/// <summary><c>csharp_space_between_method_declaration_empty_parameter_list_parentheses</c>, default false.</summary>
	public bool SpaceInEmptyDeclarationParameterList { get; init; }

	/// <summary><c>csharp_space_between_method_declaration_name_and_open_parenthesis</c>, default false.</summary>
	public bool SpaceBeforeDeclarationParameterList { get; init; }

	/// <summary><c>csharp_space_between_method_call_parameter_list_parentheses</c>, default false.</summary>
	public bool SpaceInCallArgumentList { get; init; }

	/// <summary><c>csharp_space_between_method_call_empty_parameter_list_parentheses</c>, default false.</summary>
	public bool SpaceInEmptyCallArgumentList { get; init; }

	/// <summary><c>csharp_space_between_method_call_name_and_opening_parenthesis</c>, default false.</summary>
	public bool SpaceBeforeCallArgumentList { get; init; }

	/// <summary><c>csharp_space_before_open_square_brackets</c>, default false.</summary>
	public bool SpaceBeforeOpenSquareBrackets { get; init; }

	/// <summary><c>csharp_space_between_empty_square_brackets</c>, default false.</summary>
	public bool SpaceBetweenEmptySquareBrackets { get; init; }

	/// <summary><c>csharp_space_between_square_brackets</c>, default false.</summary>
	public bool SpaceBetweenSquareBrackets { get; init; }

	/// <summary><c>csharp_space_before_dot</c>, default false.</summary>
	public bool SpaceBeforeDot { get; init; }

	/// <summary><c>csharp_space_after_dot</c>, default false.</summary>
	public bool SpaceAfterDot { get; init; }

	/// <summary><c>csharp_space_before_semicolon_in_for_statement</c>, default false.</summary>
	public bool SpaceBeforeSemicolonInForStatement { get; init; }

	/// <summary><c>csharp_space_after_semicolon_in_for_statement</c>, default true.</summary>
	public bool SpaceAfterSemicolonInForStatement { get; init; } = true;

	/// <summary>True when reflow is disabled, which lets the printer skip fit measurement entirely.</summary>
	public bool ReflowDisabled => MaxLineLength == Off;

	/// <summary>Resolves <see cref="EndOfLine.Auto"/> by sniffing the source.</summary>
	internal string ResolveEndOfLine(ReadOnlySpan<char> source)
	{
		switch (EndOfLine)
		{
			case EndOfLine.Lf:
				return "\n";
			case EndOfLine.CrLf:
				return "\r\n";
			default:
				var newLine = source.IndexOf('\n');
				return newLine > 0 && source[newLine - 1] == '\r' ? "\r\n" : "\n";
		}
	}
}
