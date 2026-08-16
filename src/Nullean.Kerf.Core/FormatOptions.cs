using Nullean.Kerf.Options;

namespace Nullean.Kerf;

/// <summary>What a <c>csharp_style_expression_bodied_*</c> key asks for.</summary>
public enum ExpressionBodyStyle
{
	/// <summary>Leave bodies as written. The default, and where <c>false</c> lands.</summary>
	AsWritten,

	/// <summary>Give every eligible block body an expression body.</summary>
	Always,

	/// <summary>Only where the block was written on one line already.</summary>
	WhenOnSingleLine,
}

/// <summary>What <c>csharp_style_namespace_declarations</c> asks for.</summary>
public enum NamespaceStyle
{
	/// <summary>Leave the declaration in whichever form it was written. The default.</summary>
	AsWritten,

	/// <summary>Turn an eligible block namespace into a file-scoped one.</summary>
	FileScoped,
}

/// <summary>What <c>csharp_prefer_braces</c> asks for.</summary>
public enum BraceRequirement
{
	/// <summary>Leave every body as the author wrote it. The default, and where <c>false</c> lands.</summary>
	AsWritten,

	/// <summary>Give every unbraced body braces.</summary>
	Always,

	/// <summary>Give a body braces only when it does not fit on the header's line.</summary>
	WhenMultiline,
}

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

	// ---- Opinions ---------------------------------------------------------------------------------
	//
	// Layout choices dotnet format declines to make, and which Kerf will make when asked. Each is off
	// by default, each is a fixed point of dotnet format when on — that is the admission test, and it
	// is the number ./build.sh conformance already reports — and each is asked for by a key somebody
	// else already defined: ReSharper's for syntax style, Microsoft's IDE2000 series for blank lines.
	//
	// There is deliberately no master switch. An earlier design had `kerf_opinionated` on the premise
	// that .NET offered nothing to borrow, which turned out to be wrong: Rider has held these opinions
	// for years and reads these keys today, so a repository that has configured Rider has already said
	// what it wants. A bundle switch on top would also mean a Kerf upgrade could silently reformat a
	// repository as the bundle grew, which is the opposite of what the defaults promise. Kerf invents
	// no formatting keys.

	/// <summary>
	/// Put a comma after the last element of a list the printer breaks across lines.
	/// </summary>
	/// <remarks>
	/// <para>
	/// ReSharper's <c>trailing_comma_in_multiline_lists</c>, and deliberately not something Kerf
	/// invented: Rider already offers this and already reads this key, so a repository that sets it
	/// gets the same answer from Rider's cleanup and from Kerf. Also accepted under the
	/// <c>csharp_</c>, <c>resharper_</c> and <c>resharper_csharp_</c> prefixes, all four of which
	/// ReSharper documents.
	/// </para>
	/// <para>
	/// Off by default. It is the one opinion here that adds and removes a token rather than moving
	/// whitespace, and it is divisive enough that it has always been a setting wherever it is offered,
	/// so it stays an explicit ask.
	/// </para>
	/// <para>
	/// The decision is taken at print time, not from the source: whether the list breaks is reflow's
	/// answer, so the comma is emitted as an <c>IfBreak</c> against the list's own group.
	/// </para>
	/// </remarks>
	public bool TrailingCommaInMultilineLists { get; init; }

	/// <summary>
	/// Put a comma after the last element even when the whole list sits on one line.
	/// </summary>
	/// <remarks>
	/// ReSharper's <c>trailing_comma_in_singleline_lists</c>, under the same four prefixes as
	/// <see cref="TrailingCommaInMultilineLists"/>, and independent of it: the two combine into the
	/// four states ReSharper allows, including "flat only", which normalises a list to carrying a
	/// trailing comma however it is laid out.
	/// </remarks>
	public bool TrailingCommaInSinglelineLists { get; init; }

	/// <summary>
	/// The order to write modifiers in, or null to leave them as the author wrote them.
	/// </summary>
	/// <remarks>
	/// <para>
	/// <c>csharp_preferred_modifier_order</c>, code style rule IDE0036 — a real .NET key, not a Kerf
	/// invention, and one <c>dotnet format style</c> already implements. What Kerf adds is doing it
	/// without a compilation: the rule is decidable from the token list alone, so it runs on a bare
	/// folder in the same pass as everything else rather than costing the 40-odd seconds a workspace
	/// load does.
	/// </para>
	/// <para>
	/// Null unless the key is present. The key carries a documented default value, so a repository
	/// that has never mentioned it would otherwise be reordered on first contact — the same reason
	/// using sorting treats the presence of the key as the ask. Writing it, even as the default value,
	/// opts in.
	/// </para>
	/// <para>
	/// Modifiers not named in the value keep their position relative to each other. Stored as the
	/// keyword texts in preference order; ranking compares against a token's interned text, so it
	/// allocates nothing.
	/// </para>
	/// </remarks>
	public string[]? PreferredModifierOrder { get; init; }

	/// <summary>
	/// Whether a control-flow body written without braces should be given them.
	/// </summary>
	/// <remarks>
	/// <para>
	/// <c>csharp_prefer_braces</c>, code style rule IDE0011 — another real key that
	/// <c>dotnet format style</c> implements only with a workspace behind it. Kerf reaches the same
	/// answer from syntax alone, which is the point of the MSBuild integration: what Kerf fixes before
	/// the compiler runs is what <c>EnforceCodeStyleInBuild</c> no longer has to report.
	/// </para>
	/// <para>
	/// <see cref="BraceRequirement.AsWritten"/> unless the key says otherwise, so a repository that
	/// never mentioned it is untouched. <c>false</c> also maps to that: Roslyn reads it as "prefer no
	/// braces" and will take them off, but removing a brace pair can change what a name means — a
	/// declaration inside the block stops being scoped to it — and adding braces cannot. Kerf only
	/// adds.
	/// </para>
	/// <para>
	/// When braces are added the body is expanded onto its own lines even if the author had it on the
	/// header's line, which is what Roslyn does. Leaving <c>if (a) Call();</c> inline would satisfy
	/// nobody: IDE0011 would still fire on the next build, and the whole point is that it does not.
	/// </para>
	/// </remarks>
	public BraceRequirement PreferBraces { get; init; } = BraceRequirement.AsWritten;

	/// <summary>
	/// Whether a block namespace should be rewritten as a file-scoped one.
	/// </summary>
	/// <remarks>
	/// <para>
	/// <c>csharp_style_namespace_declarations</c>, code style rule IDE0161. Another real key that
	/// <c>dotnet format style</c> can only apply with a workspace behind it, and the one with the
	/// largest visible effect: the whole file comes back an indent level.
	/// </para>
	/// <para>
	/// One direction only. <c>file_scoped</c> converts; <c>block</c> is accepted and does nothing,
	/// because a file-scoped namespace is already the form the compiler and every modern template
	/// prefer, and putting the braces back would indent an entire file to no end.
	/// </para>
	/// <para>
	/// Conversion is refused unless the file is eligible: the namespace has to be the compilation
	/// unit's only member and contain no namespace of its own. A file-scoped namespace takes
	/// everything after it, so a type declared beside the namespace, or a second namespace, changes
	/// what the code means rather than how it looks.
	/// </para>
	/// </remarks>
	public NamespaceStyle NamespaceStyle { get; init; }

	/// <summary>
	/// <c>csharp_style_expression_bodied_methods</c>, code style rule IDE0022.
	/// </summary>
	/// <remarks>
	/// <para>
	/// One direction only, like <see cref="PreferBraces"/>: a block body that qualifies gains an
	/// expression body, and <c>false</c> does nothing rather than turning expression bodies back into
	/// blocks. Adding one cannot change what a name means; the reverse rewrite is a larger change for
	/// no gain.
	/// </para>
	/// <para>
	/// <c>when_on_single_line</c> asks whether the author already had the block on one line, which is
	/// a fact about the source that reflow cannot move. Asking whether the *result* fits would make
	/// one run's width decide the next run's tokens, which is what stopped initializer joining from
	/// ever settling.
	/// </para>
	/// </remarks>
	public ExpressionBodyStyle ExpressionBodiedMethods { get; init; }

	/// <summary><c>csharp_style_expression_bodied_constructors</c>, IDE0021.</summary>
	public ExpressionBodyStyle ExpressionBodiedConstructors { get; init; }

	/// <summary><c>csharp_style_expression_bodied_operators</c>, IDE0023 and IDE0024.</summary>
	public ExpressionBodyStyle ExpressionBodiedOperators { get; init; }

	/// <summary><c>csharp_style_expression_bodied_local_functions</c>, IDE0061.</summary>
	public ExpressionBodyStyle ExpressionBodiedLocalFunctions { get; init; }

	/// <summary><c>csharp_style_expression_bodied_accessors</c>, IDE0027.</summary>
	public ExpressionBodyStyle ExpressionBodiedAccessors { get; init; }

	/// <summary><c>csharp_style_expression_bodied_properties</c>, IDE0025.</summary>
	/// <remarks>
	/// A wider rewrite than the accessor one: the whole accessor list goes, not just the accessor's
	/// braces, so it applies only to a property with a single getter and nothing else to keep.
	/// </remarks>
	public ExpressionBodyStyle ExpressionBodiedProperties { get; init; }

	/// <summary><c>csharp_style_expression_bodied_indexers</c>, IDE0026.</summary>
	public ExpressionBodyStyle ExpressionBodiedIndexers { get; init; }

	/// <summary>True when any expression-body key asked for a rewrite.</summary>
	/// <remarks>Gates the printer work and the verifier's allowance, so a repository that has not
	/// opted in pays for neither.</remarks>
	public bool RewritesExpressionBodies =>
		ExpressionBodiedMethods != ExpressionBodyStyle.AsWritten
		|| ExpressionBodiedConstructors != ExpressionBodyStyle.AsWritten
		|| ExpressionBodiedOperators != ExpressionBodyStyle.AsWritten
		|| ExpressionBodiedLocalFunctions != ExpressionBodyStyle.AsWritten
		|| ExpressionBodiedAccessors != ExpressionBodyStyle.AsWritten
		|| ExpressionBodiedProperties != ExpressionBodyStyle.AsWritten
		|| ExpressionBodiedIndexers != ExpressionBodyStyle.AsWritten;

	/// <summary>True when either trailing-comma option asked for anything.</summary>
	/// <remarks>
	/// Gates both the printer work and the verifier's allowance for a comma that appears or vanishes,
	/// so a repository that has not opted in cannot pay for either.
	/// </remarks>
	public bool RewritesTrailingCommas => TrailingCommaInMultilineLists || TrailingCommaInSinglelineLists;

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
