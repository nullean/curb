namespace Nullean.Curb.Options;

/// <summary>How implemented options are grouped in the option reference.</summary>
public enum OptionGroup
{
	Core,
	NewLines,
	Indentation,
	Spacing,
	Wrapping,
	BlankLines,
	ExpressionBodies,
	Usings,
	ModifiersAndBraces,
	TrailingCommas,
	Suppression,
}

/// <summary>
/// Metadata for one implemented .editorconfig key.
/// </summary>
/// <param name="Key">The .editorconfig key name.</param>
/// <param name="Group">Grouping for the option reference.</param>
/// <param name="Values">
/// Accepted literal values. Placeholders like <c>&lt;integer&gt;</c> and <c>&lt;template&gt;</c>
/// are not iterable and are skipped during example generation.
/// </param>
/// <param name="Default">The value the default <see cref="FormatOptions"/> produces.</param>
/// <param name="Summary">One-line description.</param>
/// <param name="SnippetKey">Key into <c>Snippets.All</c>.</param>
public sealed record OptionDescriptor(
	string Key,
	OptionGroup Group,
	string[] Values,
	string Default,
	string Summary,
	string SnippetKey)
{
	/// <summary>All implemented option descriptors, one per key.</summary>
	public static IReadOnlyList<OptionDescriptor> All { get; } = Build();

	private static OptionDescriptor[] Build() =>
	[
		// ---- Core (8) -----------------------------------------------------------------------
		new("indent_style", OptionGroup.Core, ["space", "tab"], "space", "Indent with spaces or tabs.", "basic_method"),
		new("indent_size", OptionGroup.Core, ["<integer>"], "4", "Columns per indent level.", "basic_method"),
		new("tab_width", OptionGroup.Core, ["<integer>"], "4", "Columns a tab occupies when measuring line width.", "basic_method"),
		new("end_of_line", OptionGroup.Core, ["lf", "crlf", "auto"], "auto", "Line ending to write. auto matches whatever the source uses.", "basic_method"),
		new("insert_final_newline", OptionGroup.Core, ["true", "false"], "true", "Ensure the file ends with a newline.", "basic_method"),
		new("trim_trailing_whitespace", OptionGroup.Core, ["true", "false"], "true", "Remove trailing whitespace from each line.", "basic_method"),
		new("charset", OptionGroup.Core, ["utf-8", "utf-8-bom", "preserve"], "preserve", "Byte-order mark handling. preserve keeps whatever the file arrived with.", "basic_method"),
		new("max_line_length", OptionGroup.Core, ["off", "<integer>"], "off", "Column target for reflow. off disables reflow entirely and selects preservation mode.", "long_chain"),

		// ---- NewLines (7) -------------------------------------------------------------------
		new(
			"csharp_new_line_before_open_brace",
			OptionGroup.NewLines,
			["all", "none", "types", "methods", "accessors", "anonymous_types", "anonymous_methods",
			 "lambdas", "control_blocks", "object_collection_array_initializers", "properties", "events"],
			"all",
			"Which constructs put their opening brace on a new line.",
			"class_with_method"),
		new("csharp_new_line_before_else", OptionGroup.NewLines, ["true", "false"], "true", "Put else on its own line rather than joining it to the preceding brace.", "if_else"),
		new("csharp_new_line_before_catch", OptionGroup.NewLines, ["true", "false"], "true", "Put catch on its own line.", "try_catch"),
		new("csharp_new_line_before_finally", OptionGroup.NewLines, ["true", "false"], "true", "Put finally on its own line.", "try_catch"),
		new("csharp_new_line_before_members_in_object_initializers", OptionGroup.NewLines, ["true", "false"], "false", "Put each member of an object initializer on its own line.", "object_init"),
		new("csharp_new_line_before_members_in_anonymous_types", OptionGroup.NewLines, ["true", "false"], "false", "Put each member of an anonymous type on its own line.", "anonymous_type"),
		new("csharp_new_line_between_query_expression_clauses", OptionGroup.NewLines, ["true", "false"], "false", "Put each clause of a query expression on its own line.", "query"),

		// ---- Indentation (6) ----------------------------------------------------------------
		new("csharp_indent_case_contents", OptionGroup.Indentation, ["true", "false"], "true", "Indent statements inside a case label.", "switch_stmt"),
		new("csharp_indent_switch_labels", OptionGroup.Indentation, ["true", "false"], "true", "Indent case labels inside their switch.", "switch_stmt"),
		new("csharp_indent_labels", OptionGroup.Indentation, ["one_less_than_current", "flush_left", "no_change"], "one_less_than_current", "How goto labels are indented.", "goto_label"),
		new("csharp_indent_block_contents", OptionGroup.Indentation, ["true", "false"], "true", "Indent statements inside a block.", "basic_method"),
		new("csharp_indent_braces", OptionGroup.Indentation, ["true", "false"], "false", "Indent the brace tokens themselves an extra level.", "basic_method"),
		new("csharp_indent_case_contents_when_block", OptionGroup.Indentation, ["true", "false"], "true", "Indent a braced case body. csharp_indent_case_contents governs other statements.", "switch_stmt"),

		// ---- Spacing (22) -------------------------------------------------------------------
		new("csharp_space_after_cast", OptionGroup.Spacing, ["true", "false"], "false", "Space between a cast and its operand.", "cast"),
		new("csharp_space_after_keywords_in_control_flow_statements", OptionGroup.Spacing, ["true", "false"], "true", "Space between a control-flow keyword and its opening parenthesis.", "if_else"),
		new("csharp_space_before_colon_in_inheritance_clause", OptionGroup.Spacing, ["true", "false"], "true", "Space before the colon in a base list.", "class_with_method"),
		new("csharp_space_after_colon_in_inheritance_clause", OptionGroup.Spacing, ["true", "false"], "true", "Space after the colon in a base list.", "class_with_method"),
		new("csharp_space_around_binary_operators", OptionGroup.Spacing, ["before_and_after", "none", "ignore"], "before_and_after", "Spaces around binary and assignment operators.", "binary_ops"),
		new("csharp_space_around_declaration_statements", OptionGroup.Spacing, ["false", "ignore"], "false", "Extra spaces in declaration statements. false normalises; ignore leaves them alone.", "declaration"),
		new("csharp_space_after_comma", OptionGroup.Spacing, ["true", "false"], "true", "Space after a comma.", "call_args"),
		new("csharp_space_before_comma", OptionGroup.Spacing, ["true", "false"], "false", "Space before a comma.", "call_args"),
		new("csharp_space_between_parentheses", OptionGroup.Spacing, ["false", "control_flow_statements", "type_casts", "expressions"], "false", "Space just inside parentheses for specific constructs.", "if_else"),
		new("csharp_space_between_method_declaration_parameter_list_parentheses", OptionGroup.Spacing, ["true", "false"], "false", "Space just inside the parentheses of a parameter list.", "method_decl"),
		new("csharp_space_between_method_declaration_empty_parameter_list_parentheses", OptionGroup.Spacing, ["true", "false"], "false", "Space inside an empty parameter list ().", "method_decl"),
		new("csharp_space_between_method_declaration_name_and_open_parenthesis", OptionGroup.Spacing, ["true", "false"], "false", "Space between the method name and its opening parenthesis.", "method_decl"),
		new("csharp_space_between_method_call_parameter_list_parentheses", OptionGroup.Spacing, ["true", "false"], "false", "Space just inside the parentheses of an argument list.", "call_args"),
		new("csharp_space_between_method_call_empty_parameter_list_parentheses", OptionGroup.Spacing, ["true", "false"], "false", "Space inside an empty argument list ().", "call_args"),
		new("csharp_space_between_method_call_name_and_opening_parenthesis", OptionGroup.Spacing, ["true", "false"], "false", "Space between the method name and its opening parenthesis at a call site.", "call_args"),
		new("csharp_space_before_open_square_brackets", OptionGroup.Spacing, ["true", "false"], "false", "Space before an opening square bracket.", "indexer"),
		new("csharp_space_between_empty_square_brackets", OptionGroup.Spacing, ["true", "false"], "false", "Space inside empty square brackets [].", "indexer"),
		new("csharp_space_between_square_brackets", OptionGroup.Spacing, ["true", "false"], "false", "Space inside non-empty square brackets.", "indexer"),
		new("csharp_space_before_dot", OptionGroup.Spacing, ["true", "false"], "false", "Space before a dot.", "long_chain"),
		new("csharp_space_after_dot", OptionGroup.Spacing, ["true", "false"], "false", "Space after a dot.", "long_chain"),
		new("csharp_space_before_semicolon_in_for_statement", OptionGroup.Spacing, ["true", "false"], "false", "Space before a semicolon in a for statement.", "for_loop"),
		new("csharp_space_after_semicolon_in_for_statement", OptionGroup.Spacing, ["true", "false"], "true", "Space after a semicolon in a for statement.", "for_loop"),

		// ---- Wrapping (26) ------------------------------------------------------------------
		new("csharp_preserve_single_line_blocks", OptionGroup.Wrapping, ["true", "false"], "true", "Allow a block the author wrote on one line to stay on one line.", "single_line_block"),
		new("csharp_empty_block_style", OptionGroup.Wrapping, ["multiline", "together", "together_same_line"], "(not set)", "How an empty method, constructor, accessor or control-flow body is laid out. together_same_line keeps { } on the signature's line.", "single_line_block"),
		new("csharp_preserve_single_line_statements", OptionGroup.Wrapping, ["true", "false"], "true", "Allow a control-flow body the author left on its header's line to stay there.", "if_else"),
		new("csharp_keep_existing_linebreaks", OptionGroup.Wrapping, ["true", "false"], "(derived from max_line_length)", "Preservation mode. Defaults to true when max_line_length is off.", "long_chain"),
		new("csharp_wrap_before_first_method_call", OptionGroup.Wrapping, ["true", "false"], "(not set)", "When a chain breaks, break before the first call too, leaving the receiver on its own line.", "long_chain"),
		new("csharp_wrap_before_first_type_parameter_constraint", OptionGroup.Wrapping, ["true", "false"], "false", "Put a where clause on its own line.", "generic_constraint"),
		new("csharp_wrap_before_extends_colon", OptionGroup.Wrapping, ["true", "false"], "false", "Put a base list on its own line, colon first.", "class_with_method"),
		new("csharp_place_constructor_initializer_on_same_line", OptionGroup.Wrapping, ["true", "false"], "true", "Keep : base(...) on the constructor's line rather than giving it its own.", "constructor_init"),
		new("csharp_wrap_before_arrow_with_expressions", OptionGroup.Wrapping, ["true", "false"], "false", "When an expression body wraps, put => at the start of the new line.", "expression_body"),
		new("csharp_wrap_after_dot_in_method_calls", OptionGroup.Wrapping, ["true", "false"], "false", "When a chain breaks, leave the dot at the end of the preceding line.", "long_chain"),
		new("csharp_wrap_chained_method_calls", OptionGroup.Wrapping, ["chop_if_long"], "chop_if_long", "Break a chain of three or more calls onto its own line per call once it does not fit. Curb's default behaviour either way; setting the key only makes it explicit.", "long_chain"),
		new("csharp_place_simple_enum_on_single_line", OptionGroup.Wrapping, ["true", "false"], "true", "Allow a short enum body to stay on one line.", "simple_enum"),
		new("csharp_place_simple_accessorholder_on_single_line", OptionGroup.Wrapping, ["true", "false"], "true", "Allow { get; set; } to stay on the property's line.", "property"),
		new("csharp_wrap_chained_binary_expressions", OptionGroup.Wrapping, ["chop_always", "chop_if_long", "wrap_if_long"], "(not set)", "Break each operand of a long && or || chain onto its own line. chop_always breaks regardless of width; wrap_if_long packs operands instead of breaking every one. Only honoured in deterministic mode (max_line_length set).", "binary_ops"),
		new("csharp_wrap_before_binary_opsign", OptionGroup.Wrapping, ["true", "false"], "true", "When a binary chain breaks, start the new line with the operator.", "binary_ops"),
		new("csharp_wrap_parameters_style", OptionGroup.Wrapping, ["chop_always", "chop_if_long", "wrap_if_long"], "chop_if_long", "chop_always gives every parameter its own line regardless of width; wrap_if_long packs parameters instead. wrap_if_long is only honoured in deterministic mode (max_line_length set) — chop_always and chop_if_long work in either.", "method_decl"),
		new("csharp_wrap_arguments_style", OptionGroup.Wrapping, ["chop_always", "chop_if_long", "wrap_if_long"], "chop_if_long", "chop_always gives every argument its own line; wrap_if_long packs arguments instead. Only honoured in deterministic mode (max_line_length set).", "call_args"),
		new("csharp_wrap_object_and_collection_initializer_style", OptionGroup.Wrapping, ["chop_always", "chop_if_long"], "chop_if_long", "chop_always gives every initializer element its own line. Only honoured in deterministic mode. wrap_if_long is not offered: dotnet format forces one member per line once an initializer opens out, so a packed layout could never stay put.", "object_init"),
		new("csharp_max_formal_parameters_on_line", OptionGroup.Wrapping, ["<integer>"], "(not set)", "Chop a parameter list carrying more than this many parameters, regardless of width.", "method_decl"),
		new("csharp_max_invocation_arguments_on_line", OptionGroup.Wrapping, ["<integer>"], "(not set)", "Chop an argument list carrying more than this many arguments.", "call_args"),
		new("csharp_max_initializer_elements_on_line", OptionGroup.Wrapping, ["<integer>"], "(not set)", "Chop an initializer carrying more than this many elements.", "object_init"),
		new("csharp_max_chained_method_calls_on_line", OptionGroup.Wrapping, ["<integer>"], "(not set)", "Chop a chain carrying more than this many calls, regardless of width. Deterministic mode only.", "long_chain"),
		new("csharp_max_array_initializer_elements_on_line", OptionGroup.Wrapping, ["<integer>"], "(not set)", "Chop an array initializer carrying more than this many elements.", "array_init"),
		new("csharp_wrap_before_declaration_rpar", OptionGroup.Wrapping, ["true", "false"], "(not set)", "Put a broken parameter list's ) on its own line.", "method_decl"),
		new("csharp_wrap_before_invocation_rpar", OptionGroup.Wrapping, ["true", "false"], "(not set)", "Put a broken argument list's ) on its own line.", "call_args"),
		new("csharp_place_method_attribute_on_same_line", OptionGroup.Wrapping, ["own_line", "if_owner_is_single_line"], "own_line", "Where a method's attribute section goes. if_owner_is_single_line joins it when the method fits on one line. Deterministic mode only.", "attributed_method"),
		new("csharp_place_field_attribute_on_same_line", OptionGroup.Wrapping, ["own_line", "if_owner_is_single_line"], "own_line", "Where a field's attribute section goes.", "attributed_method"),
		new("csharp_place_property_attribute_on_same_line", OptionGroup.Wrapping, ["own_line", "if_owner_is_single_line"], "own_line", "Where a property's attribute section goes.", "attributed_method"),
		new("csharp_place_event_attribute_on_same_line", OptionGroup.Wrapping, ["own_line", "if_owner_is_single_line"], "own_line", "Where an event's attribute section goes.", "attributed_method"),

		// ---- BlankLines (10) ----------------------------------------------------------------
		new("csharp_keep_blank_lines_in_declarations", OptionGroup.BlankLines, ["<integer>"], "1", "Maximum consecutive blank lines between members.", "members"),
		new("csharp_keep_blank_lines_in_code", OptionGroup.BlankLines, ["<integer>"], "1", "Maximum consecutive blank lines between statements.", "members"),
		new("csharp_blank_lines_around_invocable", OptionGroup.BlankLines, ["<integer>"], "0", "Blank lines before and after each method or constructor. 0 means leave the author's.", "members"),
		new("csharp_blank_lines_around_type", OptionGroup.BlankLines, ["<integer>"], "0", "Blank lines before and after each type declaration.", "members"),
		new("csharp_blank_lines_around_property", OptionGroup.BlankLines, ["<integer>"], "0", "Blank lines before and after each property, indexer or event.", "members"),
		new("csharp_blank_lines_around_field", OptionGroup.BlankLines, ["<integer>"], "0", "Blank lines before and after each field.", "members"),
		new("csharp_blank_lines_inside_type", OptionGroup.BlankLines, ["<integer>"], "0", "Blank lines below the opening brace and above the closing brace of a type.", "members"),
		new("csharp_blank_lines_around_namespace", OptionGroup.BlankLines, ["<integer>"], "0", "Blank lines before and after a namespace declaration.", "members"),
		new("csharp_blank_lines_after_using_list", OptionGroup.BlankLines, ["<integer>"], "0", "Blank lines below the last using directive.", "usings"),
		new("csharp_blank_lines_after_file_scoped_namespace_directive", OptionGroup.BlankLines, ["<integer>"], "1", "Blank lines after a file-scoped namespace declaration.", "usings"),

		// ---- ExpressionBodies (7) -----------------------------------------------------------
		new("csharp_style_expression_bodied_methods", OptionGroup.ExpressionBodies, ["false", "true", "when_on_single_line"], "false", "Convert eligible block-bodied methods to expression bodies. IDE0022.", "expression_body"),
		new("csharp_style_expression_bodied_constructors", OptionGroup.ExpressionBodies, ["false", "true", "when_on_single_line"], "false", "Convert eligible block-bodied constructors to expression bodies. IDE0021.", "constructor_expr"),
		new("csharp_style_expression_bodied_operators", OptionGroup.ExpressionBodies, ["false", "true", "when_on_single_line"], "false", "Convert eligible block-bodied operators. IDE0023/IDE0024.", "expression_body"),
		new("csharp_style_expression_bodied_local_functions", OptionGroup.ExpressionBodies, ["false", "true", "when_on_single_line"], "false", "Convert eligible block-bodied local functions. IDE0061.", "expression_body"),
		new("csharp_style_expression_bodied_accessors", OptionGroup.ExpressionBodies, ["false", "true", "when_on_single_line"], "false", "Convert eligible block-bodied accessors. IDE0027.", "property"),
		new("csharp_style_expression_bodied_properties", OptionGroup.ExpressionBodies, ["false", "true", "when_on_single_line"], "false", "Convert an eligible single-getter property to an expression body. IDE0025.", "property"),
		new("csharp_style_expression_bodied_indexers", OptionGroup.ExpressionBodies, ["false", "true", "when_on_single_line"], "false", "Convert eligible block-bodied indexers. IDE0026.", "indexer"),

		// ---- Usings (3) ---------------------------------------------------------------------
		new("dotnet_sort_system_directives_first", OptionGroup.Usings, ["true", "false"], "false", "Put System.* usings before all others.", "usings"),
		new("dotnet_separate_import_directive_groups", OptionGroup.Usings, ["true", "false"], "false", "Separate groups of usings by first namespace segment with a blank line.", "usings"),
		new("csharp_using_directive_placement", OptionGroup.Usings, ["inside_namespace", "outside_namespace"], "(as written)", "Move using directives inside or outside the namespace. IDE0065.", "usings"),

		// ---- ModifiersAndBraces (3) ---------------------------------------------------------
		new("csharp_preferred_modifier_order", OptionGroup.ModifiersAndBraces, ["<modifier-list>"], "(not set)", "Reorder modifiers to the specified preference. IDE0036. Applied without a compilation.", "modifiers"),
		new("csharp_prefer_braces", OptionGroup.ModifiersAndBraces, ["false", "true", "when_multiline"], "false", "Add braces to unbraced control-flow bodies. IDE0011. Applied without a compilation.", "if_else"),
		new("csharp_style_namespace_declarations", OptionGroup.ModifiersAndBraces, ["block_scoped", "file_scoped"], "(as written)", "Convert block namespaces to file-scoped. IDE0161. Applied without a compilation.", "namespace_block"),

		// ---- TrailingCommas (2) -------------------------------------------------------------
		new("csharp_trailing_comma_in_multiline_lists", OptionGroup.TrailingCommas, ["true", "false"], "false", "Add a trailing comma after the last element of a list that breaks across lines.", "object_init"),
		new("csharp_trailing_comma_in_singleline_lists", OptionGroup.TrailingCommas, ["true", "false"], "false", "Add a trailing comma after the last element even when the list is on one line.", "call_args"),

		// ---- Suppression (3) ----------------------------------------------------------------
		new("generated_code", OptionGroup.Suppression, ["true", "false"], "false", "Skip this file entirely. Curb never formats files marked as generated.", "basic_method"),
		new("dotnet_diagnostic.ide0055.severity", OptionGroup.Suppression, ["none", "silent", "suggestion", "warning", "error"], "warning", "Set this to none to opt a file out of formatting.", "basic_method"),
		new("file_header_template", OptionGroup.Suppression, ["<template>", "unset"], "(not set)", "Insert or correct a // comment header at the top of the file. Supports {fileName}. IDE0073.", "basic_method"),
	];
}
