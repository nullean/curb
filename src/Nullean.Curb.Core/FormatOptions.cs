using Nullean.Curb.Options;

namespace Nullean.Curb;

/// <summary>How a list or chain is laid out.</summary>
public enum WrapStyle
{
	/// <summary>Every element on its own line, but only when it does not fit. Curb's own behaviour.</summary>
	ChopIfLong,

	/// <summary>Every element on its own line, fit or not.</summary>
	ChopAlways,

	/// <summary>Pack elements onto a line until the width runs out. Not implemented.</summary>
	WrapIfLong,
}

/// <summary>Where <c>csharp_using_directive_placement</c> wants the using directives.</summary>
public enum UsingPlacement
{
	/// <summary>Leave them where they are. The default.</summary>
	AsWritten,

	/// <summary>Above the namespace.</summary>
	OutsideNamespace,

	/// <summary>Inside it.</summary>
	InsideNamespace,
}

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

/// <summary>What <c>[resharper_]csharp_empty_block_style</c> asks for.</summary>
/// <remarks>
/// ReSharper's own key rather than one of dotnet format's — see <see cref="FormatOptions.EmptyBlockStyle"/>
/// for how it relates to <see cref="FormatOptions.PreserveSingleLineBlocks"/>, which already collapses an
/// empty pair without moving it.
/// </remarks>
public enum EmptyBlockStyle
{
	/// <summary>Force an empty pair apart onto two lines, even one the author or preservation joined.</summary>
	Multiline,

	/// <summary>Collapse an empty pair to <c>{ }</c>, wherever <c>csharp_new_line_before_open_brace</c> puts it.</summary>
	Together,

	/// <summary>Collapse an empty pair to <c>{ }</c> and keep it on the owner's line, whatever the brace option says.</summary>
	TogetherSameLine,
}

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

/// <summary>Where a member's attribute section goes, for the <c>csharp_place_*_attribute_on_same_line</c> family.</summary>
public enum AttributePlacement
{
	/// <summary>Its own line, always. The default, and what <c>dotnet format</c> leaves alone.</summary>
	OwnLine,

	// ReSharper's `true` — the member's line unconditionally — is deliberately absent, and this is the
	// clearest measured case of the "decides the other way" classification in docs/contribute/layout-decisions.md.
	// Joining every attribute is idempotent in Curb and still inadmissible: dotnet format *moves* an
	// attribute back off a member that spans lines, so 347 of 1,196 corpus files came back changed by the
	// next Format Document. Its real rule turns out to be IfOwnerIsSingleLine, which is why that value is
	// clean at 100% where this one sits at 849/1,196. The binder refuses `true` and says so.

	/// <summary>
	/// The member's line when the member itself comes out on one line.
	/// </summary>
	/// <remarks>
	/// ReSharper's <c>if_owner_is_single_line</c>, and the reason this family took three attempts. Asked
	/// of the source it is the textbook non-idempotent rule: run 1 sees a member spanning two lines and
	/// leaves the attribute above it, reflow then joins the member, and run 2 answers the same question
	/// differently — 68 corpus files never settled and 16 lost their fixed point. Asked of the group that
	/// encloses the attribute *and* the member, it is one question answered once, and the width feedback
	/// that made it circular is inside the measurement rather than spread across two runs. Needs
	/// <see cref="FormatOptions.KeepExistingLinebreaks"/> off for that reason.
	/// </remarks>
	IfOwnerIsSingleLine,
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
/// Defaults deliberately mirror Roslyn's own, so Curb agrees with Visual Studio and Rider out of the
/// box rather than reformatting a repository on first run. The one deliberate exception is
/// <see cref="MaxLineLength"/>, which defaults to <see cref="Off"/>: reflow is opt-in, because a
/// formatter that rewrites every file the moment it is installed does not get installed twice.
/// </para>
/// <para>
/// <see cref="MaxLineLength"/> carries a second job: it selects the layout mode. See
/// <see cref="KeepExistingLinebreaks"/>. Asking for a width is asking Curb to decide layout, so it is the
/// same opt-in rather than a second one.
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

	// There is no implied width. Deterministic layout used to supply 160 when the configuration named
	// none, which was the weakest part of that design — a magic number nobody typed, reflowing a
	// repository that never asked. Gating the mode on the width instead makes it unreachable: no width
	// means preservation, so there is nothing left to imply.

	public FormatOptions() { }

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
	/// These three options are documented with a default of true, and Curb defaults all three to
	/// false anyway. The reason is evidence rather than preference: <c>dotnet format whitespace</c>
	/// never acts on any of them. Setting all three to true and handing it
	/// <c>new Point{X=1,Y=2}</c>, <c>new{First=1}</c> and a one-line query — so that it has to
	/// rewrite the whitespace regardless — it normalises the spacing and inserts no line break at
	/// all. Roslyn applies these only where its formatter is already placing a newline, which the
	/// whitespace formatter never is.
	/// </para>
	/// <para>
	/// So there is no observable behaviour to match, and the choice falls to Curb. Defaulting to
	/// true would explode every one-line initializer in a repository the first time Curb ran, on
	/// the strength of an option no other tool honours. Defaulting to false keeps Curb a fixed
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
	/// This is the option that makes Curb's output depend on its input, and it is on by default
	/// because that is what dotnet format does. <c>void M() { }</c> stays as written rather than
	/// being expanded to three lines, which is the difference between a formatter you can adopt on
	/// an existing repository and one that rewrites all of it.
	/// </para>
	/// <para>
	/// Preserving wins over both <see cref="NewLineBeforeOpenBrace"/> and
	/// <see cref="MaxLineLength"/>: a preserved block keeps its brace on the header line and is not
	/// reflowed. Curb stays idempotent — running it twice changes nothing — but it is deliberately
	/// not canonicalising, which is correct IDE0055 behaviour.
	/// </para>
	/// </remarks>
	public bool PreserveSingleLineBlocks { get; init; } = true;

	/// <summary>
	/// How an empty brace pair on a method, constructor, accessor or control-flow body is laid out.
	/// <c>[resharper_]csharp_empty_block_style</c> / <c>[resharper_]empty_block_style</c>, unset by default.
	/// </summary>
	/// <remarks>
	/// <para>
	/// ReSharper's own key, and independent of <see cref="PreserveSingleLineBlocks"/>: that option already
	/// collapses an empty pair to <c>{ }</c> once <c>csharp_keep_existing_linebreaks</c> is false, but never
	/// moves it — the brace still goes wherever <c>csharp_new_line_before_open_brace</c> puts it. This key
	/// is the opt-in for actually moving it, which is what <c>together_same_line</c> is for: an empty
	/// constructor or method stays <c>private C() { }</c> on one line even when every other brace in the
	/// file breaks onto its own.
	/// </para>
	/// <para>
	/// Unset leaves every existing rule exactly as it was — <c>Together</c> and <c>TogetherSameLine</c> are
	/// both opt-in collapsing, unconditional on <see cref="PreserveSingleLineBlocks"/> and on layout mode,
	/// and <c>Multiline</c> is the opposite opt-in: it forces an empty pair the author or preservation would
	/// have kept joined apart onto two lines instead.
	/// </para>
	/// </remarks>
	public EmptyBlockStyle? EmptyBlockStyle { get; init; }

	/// <summary>
	/// Keep a statement the author left on one line on one line.
	/// <c>csharp_preserve_single_line_statements</c>, default true.
	/// </summary>
	/// <remarks>
	/// Distinct from <see cref="PreserveSingleLineBlocks"/>, and the split is dotnet format's rather
	/// than one Curb would have chosen. This one covers a control-flow body sharing its header's
	/// line — braced or not — a <c>catch</c> or <c>finally</c> following a brace, two statements
	/// separated by a semicolon, and a statement on its <c>case</c> label's line. The other covers
	/// member, type, namespace, enum and switch bodies. A one-line <c>if (a) { return; }</c> is
	/// therefore kept by this option even with the block option off.
	/// </remarks>
	public bool PreserveSingleLineStatements { get; init; } = true;

	/// <summary>
	/// <c>csharp_keep_existing_linebreaks</c> exactly as written, or null when it was not mentioned.
	/// </summary>
	/// <remarks>
	/// Kept separately from <see cref="KeepExistingLinebreaks"/> so the resolved value can depend on
	/// <see cref="MaxLineLength"/> while an explicit setting still overrides it in both directions.
	/// </remarks>
	public bool? KeepExistingLinebreaksOption { get; init; }

	/// <summary>
	/// Whether the author's own line breaks are an input to layout. Resolved from
	/// <c>csharp_keep_existing_linebreaks</c>, and from <c>max_line_length</c> when that is absent.
	/// </summary>
	/// <remarks>
	/// <para>
	/// True is preservation: a construct the author opened out stays opened out at their breaks, and only
	/// one they left on a single line is handed to width. False is deterministic layout, where
	/// <c>layout = f(tokens, width)</c>. Tokens are invariant under formatting, so
	/// <c>format(format(x)) == format(x)</c> holds by construction rather than by measurement, and the
	/// whole class of bug documented in <c>docs/layout-decisions.md</c> — a rule whose answer its own
	/// output changes — cannot be written, because there is no layout to ask about. Deterministic mode is
	/// also the better fixed point of <c>dotnet format</c>: 100% of the corpus against preservation's
	/// 99.92% with reflow on.
	/// </para>
	/// <para>
	/// <b>The default is the width.</b> <c>max_line_length</c> already means "Curb may decide layout", so
	/// asking for reflow is asking for deterministic reflow — one switch rather than two, and no third
	/// blended behaviour in between. No width and Curb is an IDE0055 whitespace formatter that never
	/// touches a line's length; a width and Curb owns the layout. Measured on the 1,196-file corpus, that
	/// is the difference between a 17,035-line diff and a 46,907-line one, which is why it must not happen
	/// to a repository that never named a width — the 894-versus-742 file count hides a 2.7× cost.
	/// </para>
	/// <para>
	/// This is the one option that changes what every other layout rule is allowed to read, which is why
	/// it is a mode rather than a preference. It makes <see cref="PreserveSingleLineStatements"/> inert and
	/// <see cref="PreserveSingleLineBlocks"/> partly so; both are reported rather than silently dropped,
	/// and <c>Printers.KeepsOneLine</c> records why neither has a deterministic counterpart.
	/// </para>
	/// </remarks>
	public bool KeepExistingLinebreaks => KeepExistingLinebreaksOption ?? (MaxLineLength == Off);

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
	// Layout choices dotnet format declines to make, and which Curb will make when asked. Each is off
	// by default, each is a fixed point of dotnet format when on — that is the admission test, and it
	// is the number ./build.sh conformance already reports — and each is asked for by a key somebody
	// else already defined: ReSharper's for syntax style, Microsoft's IDE2000 series for blank lines.
	//
	// There is deliberately no master switch. An earlier design had `curb_opinionated` on the premise
	// that .NET offered nothing to borrow, which turned out to be wrong: Rider has held these opinions
	// for years and reads these keys today, so a repository that has configured Rider has already said
	// what it wants. A bundle switch on top would also mean a Curb upgrade could silently reformat a
	// repository as the bundle grew, which is the opposite of what the defaults promise. Curb invents
	// no formatting keys.

	/// <summary>
	/// Put a comma after the last element of a list the printer breaks across lines.
	/// </summary>
	/// <remarks>
	/// <para>
	/// ReSharper's <c>trailing_comma_in_multiline_lists</c>, and deliberately not something Curb
	/// invented: Rider already offers this and already reads this key, so a repository that sets it
	/// gets the same answer from Rider's cleanup and from Curb. Also accepted under the
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
	/// <c>csharp_preferred_modifier_order</c>, code style rule IDE0036 — a real .NET key, not a Curb
	/// invention, and one <c>dotnet format style</c> already implements. What Curb adds is doing it
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
	/// <c>dotnet format style</c> implements only with a workspace behind it. Curb reaches the same
	/// answer from syntax alone, which is the point of the MSBuild integration: what Curb fixes before
	/// the compiler runs is what <c>EnforceCodeStyleInBuild</c> no longer has to report.
	/// </para>
	/// <para>
	/// <see cref="BraceRequirement.AsWritten"/> unless the key says otherwise, so a repository that
	/// never mentioned it is untouched. <c>false</c> also maps to that: Roslyn reads it as "prefer no
	/// braces" and will take them off, but removing a brace pair can change what a name means — a
	/// declaration inside the block stops being scoped to it — and adding braces cannot. Curb only
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

	/// <summary>
	/// <c>csharp_using_directive_placement</c>, code style rule IDE0065.
	/// </summary>
	/// <remarks>
	/// <para>
	/// Moves the directives across the namespace boundary, in whichever direction the key names.
	/// Nothing is added or removed, so it is the same kind of change as sorting them: the region from
	/// the first directive to the namespace's opening is a permutation of itself, which is what the
	/// content check is told.
	/// </para>
	/// <para>
	/// Only where the file has exactly one namespace and nothing beside it. A file with two would
	/// need to know which one a directive belonged to, and moving it into the wrong one changes what
	/// the names in that namespace resolve to.
	/// </para>
	/// </remarks>
	public UsingPlacement UsingPlacement { get; init; }

	/// <summary>
	/// <c>file_header_template</c>, code style rule IDE0073, or null when no header is required.
	/// </summary>
	/// <remarks>
	/// <para>
	/// The template's own lines are separated by a literal <c>\n</c> in the value, since an
	/// EditorConfig value cannot span lines. Each becomes a <c>//</c> comment, and a blank line
	/// follows the block — which is what Roslyn's fixer writes. A <c>{fileName}</c> placeholder is
	/// substituted with the file's own name, matching Roslyn.
	/// </para>
	/// <para>
	/// Curb adds a missing header, and corrects a leading <c>//</c> block whose text does not match
	/// the template — the same two verdicts Roslyn's own analyzer reports, compared the same way:
	/// line count and, per line, trimmed content. A file that opens with a <c>/* */</c> block or a
	/// <c>///</c> doc comment is left alone regardless of what it says: telling "the wrong header"
	/// from "a comment that happens to lead the file" needs more structure than a syntax-only check
	/// is willing to guess at for those forms, so only the shape Curb itself writes is ever rewritten.
	/// </para>
	/// <para>
	/// Also left alone whenever the file's using directives will be sorted: the sorter treats a
	/// leading comment-then-blank-line block as the file's own banner and reprints it verbatim ahead
	/// of the reordered block, and a second, independent rewrite of the same trivia is not safe to
	/// combine with that. See <c>UsingOrganiser.BannerEnd</c>.
	/// </para>
	/// <para>
	/// An empty value means "no header", which Roslyn documents as the way to turn the rule off. It
	/// is not the same as the key being absent, but it means the same thing here.
	/// </para>
	/// </remarks>
	public string? FileHeaderTemplate { get; init; }

	/// <summary>
	/// <c>csharp_wrap_before_declaration_rpar</c>: put a broken parameter list's <c>)</c> on a line
	/// of its own. Null leaves the decision where it has always been.
	/// </summary>
	/// <remarks>
	/// <para>
	/// ReSharper's key. Curb has always had the behaviour without the control: when reflow decides a
	/// parameter list must break, the closing parenthesis goes on its own line, and when the author
	/// broke the list themselves, whatever they did is reproduced. Setting this takes the decision
	/// away from both — <c>true</c> gives it a line whenever the list breaks, <c>false</c> keeps it
	/// beside the last parameter.
	/// </para>
	/// <para>
	/// Worth knowing what it moves with it: an expression body's arrow follows the closing
	/// parenthesis, so a hugged <c>)</c> puts a <c>switch</c> body a level deeper. That is
	/// <c>dotnet format</c>'s rule and it holds either way this is set.
	/// </para>
	/// </remarks>
	public bool? WrapBeforeDeclarationRpar { get; init; }

	/// <summary><c>csharp_wrap_before_invocation_rpar</c>, the same for an argument list.</summary>
	public bool? WrapBeforeInvocationRpar { get; init; }

	/// <summary>
	/// <c>csharp_max_formal_parameters_on_line</c>: chop a parameter list carrying more than this
	/// many, whatever the width says. Null leaves width the only thing that breaks a list.
	/// </summary>
	/// <remarks>
	/// A count rather than a column, which is the point of it: <c>max_line_length</c> asks whether
	/// the line is too long, and this asks whether there are too many things on it. A four-parameter
	/// list that fits in eighty columns is still four parameters, and some repositories would rather
	/// see them stacked. Composes with
	/// <see cref="WrapBeforeDeclarationRpar"/>, which then decides where the <c>)</c> lands.
	/// </remarks>
	public int? MaxParametersOnLine { get; init; }

	/// <summary><c>csharp_max_invocation_arguments_on_line</c>, the same for an argument list.</summary>
	public int? MaxArgumentsOnLine { get; init; }

	/// <summary>
	/// <c>csharp_max_initializer_elements_on_line</c>: chop an object or collection initializer
	/// carrying more than this many elements.
	/// </summary>
	/// <remarks>
	/// The safe half of the initializer wrapping family. This only ever *breaks* a list — the joining
	/// direction, closing up an initializer that fits, is the one that cannot be made to settle while
	/// every other construct still reads its layout from the source.
	/// </remarks>
	public int? MaxInitializerElementsOnLine { get; init; }

	/// <summary><c>csharp_max_array_initializer_elements_on_line</c>, the same for an array.</summary>
	public int? MaxArrayInitializerElementsOnLine { get; init; }

	/// <summary>
	/// <c>csharp_wrap_parameters_style</c>: <c>chop_always</c> gives every parameter its own line
	/// whether or not the list fits. Null leaves width to decide, which is <c>chop_if_long</c>.
	/// </summary>
	/// <remarks>
	/// The breaking direction again, and safe for the same reason the counts are: it asks nothing
	/// about the layout being decided. <c>wrap_if_long</c> — the fill layout — is reported as
	/// unimplemented rather than quietly treated as one of the others.
	/// </remarks>
	public WrapStyle? WrapParametersStyle { get; init; }

	/// <summary>
	/// <c>csharp_wrap_arguments_style</c>: the same for an argument list.
	/// </summary>
	/// <remarks>
	/// <para>
	/// Honoured only under <see cref="KeepExistingLinebreaks"/> <c>= false</c>, and the asymmetry with
	/// the parameter key is measured rather than chosen. Forcing every argument list to break moves
	/// constructs whose indentation other rules read from the source, and the file then formats
	/// differently on its second pass — 140 corpus files for arguments against none for parameters.
	/// Deterministic layout has no such rules, so the key becomes admissible there and nowhere else.
	/// </para>
	/// <para>
	/// Set in preservation mode it is reported, not silently dropped.
	/// </para>
	/// </remarks>
	public WrapStyle? WrapArgumentsStyle { get; init; }

	/// <summary>
	/// <c>csharp_wrap_object_and_collection_initializer_style</c>: one element per line whether or not
	/// the initializer fits.
	/// </summary>
	/// <remarks>
	/// Deterministic mode only, for the same measured reason as <see cref="WrapArgumentsStyle"/>. The
	/// count-based half of this family — <see cref="MaxInitializerElementsOnLine"/> — is admissible in
	/// both modes, because a count is not a layout question.
	/// </remarks>
	public WrapStyle? WrapObjectAndCollectionInitializerStyle { get; init; }

	// wrap_chained_method_calls stays absent: 156 corpus files stopped settling in preservation mode,
	// and unlike the two above it has not been measured under deterministic layout.

	/// <summary>
	/// <c>csharp_place_method_attribute_on_same_line</c>. Covers constructors, operators, destructors and
	/// local functions too, the same grouping <c>csharp_style_expression_bodied_methods</c> uses.
	/// </summary>
	/// <remarks>
	/// Deterministic mode only — see <see cref="AttributePlacement.IfOwnerIsSingleLine"/> for why, and
	/// note that <c>same_line</c> is held to the same rule as the conditional value: it changes what the
	/// enclosing group is measuring, so it is not admissible while other rules read the source.
	/// </remarks>
	public AttributePlacement PlaceMethodAttributeOnSameLine { get; init; }

	/// <summary><c>csharp_place_field_attribute_on_same_line</c>.</summary>
	public AttributePlacement PlaceFieldAttributeOnSameLine { get; init; }

	/// <summary><c>csharp_place_property_attribute_on_same_line</c>. Indexers answer to it too.</summary>
	public AttributePlacement PlacePropertyAttributeOnSameLine { get; init; }

	/// <summary><c>csharp_place_event_attribute_on_same_line</c>.</summary>
	public AttributePlacement PlaceEventAttributeOnSameLine { get; init; }

	// place_type_attribute_on_same_line and place_accessorholder_attribute_on_same_line are absent for a
	// reason no mode changes: dotnet format *moves* a type's attribute and an accessor's onto their own
	// line, so neither `same_line` nor the conditional value could ever be a fixed point. This is the
	// "decides the other way" half of the classification in docs/layout-decisions.md, not the
	// "declines to decide" half the four keys above sit in.

	/// <summary>
	/// <c>csharp_wrap_before_first_method_call</c>: when a chain breaks at its dots, break before the
	/// first call too, leaving the receiver on a line of its own.
	/// </summary>
	/// <remarks>
	/// <para>
	/// Null keeps what Curb does: a plain identifier receiver keeps its first call, because
	/// <c>builder.AddProject(…)</c> reads as one thing, while anything more involved is left to
	/// stand alone. <c>true</c> strands every receiver; <c>false</c> attaches every first call.
	/// </para>
	/// <para>
	/// An author's own break at that dot is theirs either way — this decides what Curb does when it
	/// is the one introducing the breaks.
	/// </para>
	/// </remarks>
	public bool? WrapBeforeFirstMethodCall { get; init; }

	/// <summary>
	/// <c>csharp_wrap_before_first_type_parameter_constraint</c>: put a <c>where</c> clause on its own
	/// line, indented under the declaration it constrains.
	/// </summary>
	/// <remarks>
	/// <para>
	/// Off by default, which keeps the clause on the signature line. On, it is Rider's own default
	/// rendering, and one of the few places a real repository's layout differs from Curb's by a whole
	/// line rather than by a column.
	/// </para>
	/// <para>
	/// A forced placement, not a width-driven one: the break is always there or never, so nothing here
	/// reads the source and there is no layout for a second run to read back differently. Where a
	/// declaration carries several <c>where</c> clauses they each take their own line, since a break
	/// before the first and none before the rest is a rendering nobody asks for.
	/// </para>
	/// </remarks>
	public bool WrapBeforeFirstTypeParameterConstraint { get; init; }

	/// <summary>
	/// <c>csharp_wrap_before_extends_colon</c>: put a base list on its own line, colon first.
	/// </summary>
	/// <remarks>
	/// Off by default, keeping <c>class C : B</c> on one line. Forced rather than width-driven, for
	/// the same reason as <see cref="WrapBeforeFirstTypeParameterConstraint"/>.
	/// </remarks>
	public bool WrapBeforeExtendsColon { get; init; }

	/// <summary>
	/// <c>csharp_place_constructor_initializer_on_same_line</c>: keep <c>: base(…)</c> on the
	/// constructor's own line.
	/// </summary>
	/// <remarks>
	/// On by default, which is what Curb has always written. Off gives the initializer a line of its
	/// own, indented under the signature — the same forced placement as
	/// <see cref="WrapBeforeExtendsColon"/>, and free ground on the same measurement: dotnet format
	/// neither joins an initializer the author broke nor breaks one they joined.
	/// </remarks>
	public bool PlaceConstructorInitializerOnSameLine { get; init; } = true;

	/// <summary>
	/// <c>csharp_wrap_before_arrow_with_expressions</c>: when an expression body wraps, put the
	/// <c>=&gt;</c> at the head of the new line rather than the tail of the old one.
	/// </summary>
	/// <remarks>
	/// <para>
	/// Off by default. Unlike the three clause options this one is width-driven, but it is still not
	/// reading any layout: it moves the arrow across a break the printer was already going to make on
	/// this run, so a body that fits prints identically either way and the option can never be what
	/// causes a wrap.
	/// </para>
	/// <para>
	/// A switch or query body is exempt, because that path does not break at the arrow at all — it
	/// hugs, and where it anchors is the one rule holding the last two points of reflow conformance.
	/// </para>
	/// </remarks>
	public bool WrapBeforeArrowWithExpressions { get; init; }

	/// <summary>
	/// <c>csharp_wrap_after_dot_in_method_calls</c>: when a chain breaks, leave the dot at the end of
	/// the line it follows rather than the start of the line it introduces.
	/// </summary>
	/// <remarks>
	/// Off by default. Only the dot moves — where a chain breaks, and whether it breaks at all, are
	/// decided exactly as they were. Free ground: dotnet format leaves a chain alone whichever side of
	/// the break its dots sit on.
	/// </remarks>
	public bool WrapAfterDotInMethodCalls { get; init; }

	/// <summary>
	/// <c>csharp_place_simple_enum_on_single_line</c>: allow an enum body the author wrote on one line
	/// to stay there.
	/// </summary>
	/// <remarks>
	/// On by default, which is what Curb has always done. Off expands every enum body, unconditionally
	/// — so it never has to ask whether one would fit, and a second run has nothing left to decide.
	/// Finer-grained than <see cref="PreserveSingleLineBlocks"/>, which says the same thing about every
	/// block at once.
	/// </remarks>
	public bool PlaceSimpleEnumOnSingleLine { get; init; } = true;

	/// <summary>
	/// <c>csharp_place_simple_accessorholder_on_single_line</c>: allow <c>{ get; set; }</c> to stay on
	/// the property's line.
	/// </summary>
	/// <remarks>
	/// On by default. Off expands every accessor list the author kept on one line, the same expansion
	/// <see cref="PreserveSingleLineBlocks"/> performs, but without also expanding every other block in
	/// the file.
	/// </remarks>
	public bool PlaceSimpleAccessorholderOnSingleLine { get; init; } = true;

	/// <summary>
	/// <c>csharp_wrap_chained_binary_expressions</c>: give each operand of a long <c>&amp;&amp;</c>
	/// or <c>||</c> chain a line of its own. Null leaves such a chain unbroken.
	/// </summary>
	/// <remarks>
	/// <para>
	/// Curb has no break opportunity inside a binary chain at all, so a condition too long for the
	/// line simply overflows it — the parentheses around it move, the operands do not. This is the
	/// key that gives them somewhere to go.
	/// </para>
	/// <para>
	/// <c>chop_if_long</c> breaks once the chain does not fit; <c>chop_always</c> breaks every
	/// operand onto its own line regardless of width, the same distinction
	/// <see cref="WrapArgumentsStyle"/> draws. <c>wrap_if_long</c> is the fill layout, packing
	/// operands until the width runs out, and the document printer has no primitive for it.
	/// </para>
	/// <para>
	/// Deterministic mode only, for the same reason as <see cref="WrapArgumentsStyle"/>: forcing a
	/// break moves operand indentation that preservation mode's rules read from the source.
	/// </para>
	/// </remarks>
	public WrapStyle? WrapChainedBinaryExpressions { get; init; }

	/// <summary>
	/// <c>csharp_wrap_before_binary_opsign</c>: start the new line with the operator rather than
	/// ending the old one with it. True by default, which is where ReSharper and CSharpier both put
	/// it, and only consulted once a chain is being broken at all.
	/// </summary>
	public bool WrapBeforeBinaryOpsign { get; init; } = true;

	// ---- blank lines ------------------------------------------------------------------------------
	//
	// ReSharper's family, and free ground in a way almost nothing else is: `dotnet format` adds no
	// blank line, removes none and collapses none, measured in both directions. So whatever Curb does
	// here it may keep doing, and every one of these is a fixed point whatever it is set to.
	//
	// The defaults reproduce what Curb did before they existed, so nobody's repository moves.

	/// <summary>
	/// <c>csharp_keep_blank_lines_in_declarations</c>: how many consecutive blank lines survive
	/// between members. One by default, which is the collapsing Curb has always done.
	/// </summary>
	public int KeepBlankLinesInDeclarations { get; init; } = 1;

	/// <summary><c>csharp_keep_blank_lines_in_code</c>, the same between statements.</summary>
	public int KeepBlankLinesInCode { get; init; } = 1;

	/// <summary>
	/// <c>csharp_blank_lines_around_invocable</c>: how many blank lines a method or constructor is
	/// given, whether or not the author left any. Zero asks for none, which leaves the author's.
	/// </summary>
	public int BlankLinesAroundInvocable { get; init; }

	/// <summary><c>csharp_blank_lines_around_type</c>, the same for a type declaration.</summary>
	public int BlankLinesAroundType { get; init; }

	/// <summary><c>csharp_blank_lines_around_property</c>, the same for a property, indexer or event.</summary>
	public int BlankLinesAroundProperty { get; init; }

	/// <summary><c>csharp_blank_lines_around_field</c>, the same for a field.</summary>
	public int BlankLinesAroundField { get; init; }

	/// <summary><c>csharp_blank_lines_after_using_list</c>: below the last using directive.</summary>
	public int BlankLinesAfterUsingList { get; init; }

	/// <summary>
	/// <c>csharp_blank_lines_inside_type</c>: below a type's opening brace and above its closing one.
	/// </summary>
	/// <remarks>
	/// The space the <c>around</c> settings deliberately do not touch. Asking for air around fields
	/// should not open a gap under every <c>{</c>; this is the setting that does that on purpose.
	/// </remarks>
	public int BlankLinesInsideType { get; init; }

	/// <summary><c>csharp_blank_lines_around_namespace</c>, around a namespace declaration.</summary>
	public int BlankLinesAroundNamespace { get; init; }

	// The align_multiline_* family is absent, and this is the reason rather than a note that nobody
	// got to it. csharp_align_multiline_argument was built and measured.
	//
	// It is free ground — dotnet format leaves an aligned continuation exactly where it finds it —
	// and the primitives are already here: Anchor captures an output column, AlignedLine breaks to
	// one, and a register file indexed by nesting depth handles lists inside lists. Alignment itself
	// came out correct, nested calls included.
	//
	// What it does not survive is anything nested inside an aligned list. dotnet format anchors a
	// construct that brings its own braces to the indentation of the line it starts on; under
	// alignment that line's indentation *is* the anchor column, so the construct wants a column-valued
	// indent. Curb's indent stack holds levels, and the two do not compose: an aligned list one level
	// deep and an aligned list forty columns deep are the same to it.
	//
	// Measured on the corpus, each step recovering some of the gap and none closing it: 76 files
	// losing their fixed point and 22 not settling; 39 and 16 once the list stopped compensating for
	// an indent scope it no longer opens; 24 and 15 once lists holding a brace-bringing argument stood
	// down entirely. The tail continues because it is not about arguments — every nested construct
	// that anchors meets the same mismatch.
	//
	// So the cost is not a printer change, it is making indentation column-valued throughout, which is
	// the one thing the hot path cannot absorb quietly. Standing down at 24 rather than shipping a
	// key that is a fixed point most of the time.

	// The place_*_attribute_on_same_line family is absent too, and it fails the same way.
	//
	// Two of its six keys are inadmissible outright: dotnet format lifts a *type's* attribute and an
	// *accessor's* onto their own line, so `always` there could never be a fixed point. For the other
	// four it declines to decide — which is what made them look like free ground.
	//
	// They are not. dotnet format keeps an attribute beside its member exactly when the whole member
	// is on one line, so plain `always` was measured at 347 corpus files it then moved. Implementing
	// the narrower `if_owner_is_single_line` instead restored conformance to baseline on a corpus
	// without reflow — but with reflow on, 68 files stopped settling and 16 lost their fixed point,
	// because Curb's own wrapping decides whether a member is one line. A property whose body Curb
	// joins becomes single-line on the run after the one that read it as multi-line, and the attribute
	// follows it down a run late.
	//
	// That is the same trap as the single-line blank-line family above, reached from a different
	// direction, and it is worth noting the sequence: a small hand-written probe said free ground, the
	// corpus without reflow agreed, and only the corpus with reflow disagreed.

	// The blank_lines_around_single_line_* family is deliberately absent, and this is the one place
	// in the whole blank-line category where the free-ground argument does not hold.
	//
	// Whether a member is "single line" would have to be read from the source, and reflow moves it: a
	// member written on one line and wrapped by the printer is one line on the first pass and two on
	// the second, so it takes the single-line setting once and the ordinary one after. Two corpus
	// files grew a blank line per run. Everything else here is safe because a blank-line count cannot
	// change what a blank-line count is; this one asks a question whose answer the formatter itself
	// changes.

	/// <summary>
	/// <c>csharp_blank_lines_after_file_scoped_namespace_directive</c>. One by default, because that
	/// is what <c>dotnet format</c> writes there and Curb follows it.
	/// </summary>
	public int BlankLinesAfterFileScopedNamespace { get; init; } = 1;

	// csharp_remove_blank_lines_near_braces_in_declarations and _in_code are deliberately absent.
	// Curb already drops a bare blank line above a closing brace, and keeps the one above a *comment*
	// that sits there, because that blank belongs to the comment rather than to the brace. ReSharper's
	// key does not draw that distinction, so honouring it either way would mean changing behaviour
	// that is already right in one of the two cases. Left alone rather than half-mapped.

	/// <summary>
	/// True when some option needs a whole member measured as one unit, so the printer has to group it.
	/// </summary>
	/// <remarks>
	/// Only <see cref="AttributePlacement.IfOwnerIsSingleLine"/> does. Gating on it keeps the group — and
	/// the change in what the lines inside a member resolve against — off every file that has not asked
	/// for the family, the same way <see cref="RewritesTrailingCommas"/> gates its own cost.
	/// </remarks>
	public bool MeasuresWholeMembers =>
		PlaceMethodAttributeOnSameLine == AttributePlacement.IfOwnerIsSingleLine
		|| PlaceFieldAttributeOnSameLine == AttributePlacement.IfOwnerIsSingleLine
		|| PlacePropertyAttributeOnSameLine == AttributePlacement.IfOwnerIsSingleLine
		|| PlaceEventAttributeOnSameLine == AttributePlacement.IfOwnerIsSingleLine;

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
	/// alone", which is why Curb needs no ignore file of its own. Honoured by the CLI rather than by
	/// the printer: a caller who hands the library a string has asked for it to be formatted.
	/// </remarks>
	public bool Excluded { get; init; }

	/// <summary>True when reflow is disabled, which lets the printer skip fit measurement entirely.</summary>
	/// <remarks>
	/// Also implies preservation, because <see cref="KeepExistingLinebreaks"/> resolves from the same width.
	/// The two can only disagree if the configuration asked for deterministic layout with no width, which
	/// the binder refuses — see CURB1007.
	/// </remarks>
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
