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
	/// default <c>before_and_after</c>.
	/// </summary>
	public BinaryOperatorSpacing SpaceAroundBinaryOperators { get; init; } = BinaryOperatorSpacing.BeforeAndAfter;

	/// <summary>
	/// Spacing within a declaration statement. <c>csharp_space_around_declaration_statements</c>,
	/// default <c>false</c>, which means "normalise" rather than "no spaces".
	/// </summary>
	public DeclarationSpacing SpaceAroundDeclarationStatements { get; init; } = DeclarationSpacing.Normalise;

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

	/// <summary>
	/// Put every member of an object, collection or array initializer on its own line.
	/// <c>csharp_new_line_before_members_in_object_initializers</c>.
	/// </summary>
	/// <remarks>
	/// See <see cref="NewLineBetweenQueryExpressionClauses"/> for why this defaults to false rather
	/// than to Roslyn's documented true.
	/// </remarks>
	public bool NewLineBeforeMembersInObjectInitializers { get; init; }

	/// <summary>
	/// Put every member of an anonymous type on its own line.
	/// <c>csharp_new_line_before_members_in_anonymous_types</c>.
	/// </summary>
	public bool NewLineBeforeMembersInAnonymousTypes { get; init; }

	/// <summary>
	/// Put every clause of a query expression on its own line.
	/// <c>csharp_new_line_between_query_expression_clauses</c>.
	/// </summary>
	/// <remarks>
	/// <para>
	/// These three options are documented with a default of true, and Kerf defaults all three to
	/// false anyway. The reason is evidence rather than preference: <c>dotnet format whitespace</c>
	/// never acts on any of them. Setting all three to true and handing it
	/// <c>new Point{X=1,Y=2}</c>, <c>new{First=1}</c> and a one-line query — so that it has to
	/// rewrite the whitespace regardless — it normalises the spacing and inserts no line break at
	/// all. Roslyn applies these only where its formatter is already placing a newline, which the
	/// whitespace formatter never is.
	/// </para>
	/// <para>
	/// So there is no observable behaviour to match, and the choice falls to Kerf. Defaulting to
	/// true would explode every one-line initializer in a repository the first time Kerf ran, on
	/// the strength of an option no other tool honours. Defaulting to false keeps Kerf a fixed
	/// point of dotnet format and leaves the expanded layout one line of config away.
	/// </para>
	/// </remarks>
	public bool NewLineBetweenQueryExpressionClauses { get; init; }

	/// <summary>
	/// Keep a brace pair the author wrote on one line on one line.
	/// <c>csharp_preserve_single_line_blocks</c>, default true.
	/// </summary>
	/// <remarks>
	/// <para>
	/// This is the option that makes Kerf's output depend on its input, and it is on by default
	/// because that is what dotnet format does. <c>void M() { }</c> stays as written rather than
	/// being expanded to three lines, which is the difference between a formatter you can adopt on
	/// an existing repository and one that rewrites all of it.
	/// </para>
	/// <para>
	/// Preserving wins over both <see cref="NewLineBeforeOpenBrace"/> and
	/// <see cref="MaxLineLength"/>: a preserved block keeps its brace on the header line and is not
	/// reflowed. Kerf stays idempotent — running it twice changes nothing — but it is deliberately
	/// not canonicalising, which is correct IDE0055 behaviour.
	/// </para>
	/// </remarks>
	public bool PreserveSingleLineBlocks { get; init; } = true;

	/// <summary>
	/// Keep a statement the author left on one line on one line.
	/// <c>csharp_preserve_single_line_statements</c>, default true.
	/// </summary>
	/// <remarks>
	/// Distinct from <see cref="PreserveSingleLineBlocks"/>, and the split is dotnet format's rather
	/// than one Kerf would have chosen. This one covers a control-flow body sharing its header's
	/// line — braced or not — a <c>catch</c> or <c>finally</c> following a brace, two statements
	/// separated by a semicolon, and a statement on its <c>case</c> label's line. The other covers
	/// member, type, namespace, enum and switch bodies. A one-line <c>if (a) { return; }</c> is
	/// therefore kept by this option even with the block option off.
	/// </remarks>
	public bool PreserveSingleLineStatements { get; init; } = true;

	/// <summary>
	/// Whether and how <c>using</c> directives are reordered.
	/// <c>dotnet_sort_system_directives_first</c>, defaulting to <see cref="UsingOrder.AsWritten"/>
	/// — see <see cref="UsingOrder"/> for why the default is not to sort at all.
	/// </summary>
	public UsingOrder SortUsings { get; init; } = UsingOrder.AsWritten;

	/// <summary>
	/// Separate groups of <c>using</c> directives with a blank line.
	/// <c>dotnet_separate_import_directive_groups</c>, default false. Only reachable when
	/// <see cref="SortUsings"/> is on, since a group is a run of directives sharing a first
	/// namespace segment and that only holds once they are sorted.
	/// </summary>
	public bool SeparateImportDirectiveGroups { get; init; }

	/// <summary>Indent the statements under a <c>case</c> label. <c>csharp_indent_case_contents</c>, default true.</summary>
	public bool IndentCaseContents { get; init; } = true;

	/// <summary>
	/// Indent a braced <c>case</c> body. <c>csharp_indent_case_contents_when_block</c>, default true.
	/// Governs a block where <see cref="IndentCaseContents"/> governs every other statement.
	/// </summary>
	public bool IndentCaseContentsWhenBlock { get; init; } = true;

	/// <summary>Indent <c>case</c> labels inside their switch. <c>csharp_indent_switch_labels</c>, default true.</summary>
	public bool IndentSwitchLabels { get; init; } = true;

	/// <summary>Indent a block's statements. <c>csharp_indent_block_contents</c>, default true.</summary>
	public bool IndentBlockContents { get; init; } = true;

	/// <summary>
	/// Indent the brace tokens themselves one level further than the construct that owns them.
	/// <c>csharp_indent_braces</c>, default false.
	/// </summary>
	/// <remarks>
	/// Unlike the other indentation options this moves the braces rather than their contents, so the
	/// contents stay exactly where they were and the braces come to meet them.
	/// </remarks>
	public bool IndentBraces { get; init; }

	/// <summary><c>csharp_indent_labels</c>, default <see cref="LabelIndent.OneLessThanCurrent"/>.</summary>
	public LabelIndent IndentLabels { get; init; } = LabelIndent.OneLessThanCurrent;

	/// <summary>
	/// Apply the formatting opinions <c>dotnet format</c> declines to hold.
	/// </summary>
	/// <remarks>
	/// <para>
	/// <c>kerf_opinionated</c>, default false. The one key Kerf invents, because .NET has no concept
	/// of "be more opinionated than <c>dotnet format</c>" and so offers nothing to borrow.
	/// </para>
	/// <para>
	/// Everything it enables is still a fixed point of <c>dotnet format</c> — that is the admission
	/// test, and it is the same number <c>./build.sh conformance</c> already reports. A repository
	/// formatted this way stays put when anyone runs <c>dotnet format</c>, hits Format Document, or
	/// builds with <c>EnforceCodeStyleInBuild</c>. What it costs is one diff at the point of opting
	/// in, which is why the default is off: onboarding should be undramatic.
	/// </para>
	/// <para>
	/// Rules that have a real EditorConfig key of their own — the IDE2000 series — are honoured
	/// whether or not this is set, and an explicit setting of one of those wins over this switch in
	/// both directions.
	/// </para>
	/// </remarks>
	public bool Opinionated { get; init; }

	/// <summary>
	/// The file asked not to be formatted at all.
	/// </summary>
	/// <remarks>
	/// Set by <c>generated_code = true</c> or by <c>dotnet_diagnostic.IDE0055.severity = none</c>.
	/// Neither is a layout choice — they are the two ways .NET already has of saying "leave this
	/// alone", which is why Kerf needs no ignore file of its own. Honoured by the CLI rather than by
	/// the printer: a caller who hands the library a string has asked for it to be formatted.
	/// </remarks>
	public bool Excluded { get; init; }

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
