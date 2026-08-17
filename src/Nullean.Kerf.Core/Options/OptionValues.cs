using System.Globalization;

namespace Nullean.Kerf.Options;

/// <summary>
/// Renders a resolved <see cref="FormatOptions"/> back as <c>.editorconfig</c> values.
/// </summary>
/// <remarks>
/// <para>
/// This is what <c>kerf print-config</c> shows. It exists as a lookup by key rather than as a fixed
/// list of lines so that the command reports exactly the set in
/// <see cref="OptionCatalog.ImplementedKeys"/> — a hand-written list drifts, and a config-driven
/// tool whose config-inspection command under-reports is worse than one that has none.
/// </para>
/// <para>
/// A test asserts every implemented key resolves here, so onboarding an option without teaching
/// <c>print-config</c> about it fails the build.
/// </para>
/// </remarks>
public static class OptionValues
{
	/// <summary>The resolved value of <paramref name="key"/>, or null if Kerf does not implement it.</summary>
	public static string? Of(in FormatOptions options, string key) => key switch
	{
		"indent_style" => options.UseTabs ? "tab" : "space",
		"indent_size" => options.IndentSize.ToString(CultureInfo.InvariantCulture),
		"tab_width" => options.TabWidth.ToString(CultureInfo.InvariantCulture),
		"max_line_length" => options.ReflowDisabled
			? "off"
			: options.MaxLineLength.ToString(CultureInfo.InvariantCulture),
		"end_of_line" => options.EndOfLine.ToString().ToLowerInvariant(),
		"insert_final_newline" => Bool(options.InsertFinalNewLine),
		"trim_trailing_whitespace" => Bool(options.TrimTrailingWhitespace),

		// Read and written but never a layout decision, so there is nothing resolved to report.
		"charset" => "utf-8",

		"csharp_trailing_comma_in_multiline_lists" => Bool(options.TrailingCommaInMultilineLists),
		"csharp_trailing_comma_in_singleline_lists" => Bool(options.TrailingCommaInSinglelineLists),
		"csharp_style_expression_bodied_methods" => Expression(options.ExpressionBodiedMethods),
		"csharp_style_expression_bodied_constructors" => Expression(options.ExpressionBodiedConstructors),
		"csharp_style_expression_bodied_operators" => Expression(options.ExpressionBodiedOperators),
		"csharp_style_expression_bodied_local_functions" => Expression(options.ExpressionBodiedLocalFunctions),
		"csharp_style_expression_bodied_accessors" => Expression(options.ExpressionBodiedAccessors),
		"csharp_style_expression_bodied_properties" => Expression(options.ExpressionBodiedProperties),
		"csharp_style_expression_bodied_indexers" => Expression(options.ExpressionBodiedIndexers),
		"file_header_template" => options.FileHeaderTemplate ?? "   # no header is required",
		"csharp_max_formal_parameters_on_line" => Count(options.MaxParametersOnLine),
		"csharp_max_invocation_arguments_on_line" => Count(options.MaxArgumentsOnLine),
		"csharp_wrap_before_declaration_rpar" => Rpar(options.WrapBeforeDeclarationRpar),
		"csharp_wrap_before_invocation_rpar" => Rpar(options.WrapBeforeInvocationRpar),
		"csharp_using_directive_placement" => options.UsingPlacement switch
		{
			UsingPlacement.InsideNamespace => "inside_namespace",
			UsingPlacement.OutsideNamespace => "outside_namespace",
			UsingPlacement.AsWritten => "outside_namespace   # directives are left where they are; the key is the opt-in",
			_ => "outside_namespace",
		},
		"csharp_style_namespace_declarations" => options.NamespaceStyle == NamespaceStyle.FileScoped
			? "file_scoped"
			: "block   # namespaces are left as written; Kerf never adds the braces back",
		"csharp_prefer_braces" => options.PreferBraces switch
		{
			BraceRequirement.Always => "true",
			BraceRequirement.WhenMultiline => "when_multiline",
			BraceRequirement.AsWritten => "false   # bodies are left as written; Kerf never removes braces",
			_ => "false",
		},
		"csharp_preferred_modifier_order" => options.PreferredModifierOrder is { } order
			? string.Join(",", order)
			: "   # modifiers are left as written; the key is the opt-in",
		"generated_code" => Bool(options.Excluded),
		"dotnet_diagnostic.ide0055.severity" => options.Excluded ? "none" : "default",

		"csharp_new_line_before_open_brace" => BraceStyleParser.ToEditorConfigValue(options.NewLineBeforeOpenBrace),
		"csharp_new_line_before_else" => Bool(options.NewLineBeforeElse),
		"csharp_new_line_before_catch" => Bool(options.NewLineBeforeCatch),
		"csharp_new_line_before_finally" => Bool(options.NewLineBeforeFinally),

		"csharp_new_line_before_members_in_object_initializers" => Bool(options.NewLineBeforeMembersInObjectInitializers),
		"csharp_new_line_before_members_in_anonymous_types" => Bool(options.NewLineBeforeMembersInAnonymousTypes),
		"csharp_new_line_between_query_expression_clauses" => Bool(options.NewLineBetweenQueryExpressionClauses),

		"dotnet_sort_system_directives_first" => options.SortUsings switch
		{
			UsingOrder.SystemFirst => "true",
			UsingOrder.Alphabetical => "false",
			UsingOrder.AsWritten => "false   # usings are left as written; see UsingOrder",
			_ => "false",
		},
		"dotnet_separate_import_directive_groups" => Bool(options.SeparateImportDirectiveGroups),

		"csharp_preserve_single_line_blocks" => Bool(options.PreserveSingleLineBlocks),
		"csharp_preserve_single_line_statements" => Bool(options.PreserveSingleLineStatements),

		"csharp_indent_case_contents" => Bool(options.IndentCaseContents),
		"csharp_indent_case_contents_when_block" => Bool(options.IndentCaseContentsWhenBlock),
		"csharp_indent_switch_labels" => Bool(options.IndentSwitchLabels),
		"csharp_indent_block_contents" => Bool(options.IndentBlockContents),
		"csharp_indent_braces" => Bool(options.IndentBraces),
		"csharp_indent_labels" => options.IndentLabels switch
		{
			LabelIndent.NoChange => "no_change",
			LabelIndent.FlushLeft => "flush_left",
			LabelIndent.OneLessThanCurrent => "one_less_than_current",
			_ => "one_less_than_current",
		},

		"csharp_space_after_cast" => Bool(options.SpaceAfterCast),
		"csharp_space_after_keywords_in_control_flow_statements" => Bool(options.SpaceAfterKeywordsInControlFlowStatements),
		"csharp_space_before_colon_in_inheritance_clause" => Bool(options.SpaceBeforeColonInInheritanceClause),
		"csharp_space_after_colon_in_inheritance_clause" => Bool(options.SpaceAfterColonInInheritanceClause),
		"csharp_space_around_binary_operators" => options.SpaceAroundBinaryOperators switch
		{
			BinaryOperatorSpacing.None => "none",
			BinaryOperatorSpacing.Ignore => "ignore",
			BinaryOperatorSpacing.BeforeAndAfter => "before_and_after",
			_ => "before_and_after",
		},
		"csharp_space_around_declaration_statements" =>
			options.SpaceAroundDeclarationStatements == DeclarationSpacing.Ignore ? "ignore" : "false",
		"csharp_space_after_comma" => Bool(options.SpaceAfterComma),
		"csharp_space_before_comma" => Bool(options.SpaceBeforeComma),
		"csharp_space_after_dot" => Bool(options.SpaceAfterDot),
		"csharp_space_before_dot" => Bool(options.SpaceBeforeDot),
		"csharp_space_after_semicolon_in_for_statement" => Bool(options.SpaceAfterSemicolonInForStatement),
		"csharp_space_before_semicolon_in_for_statement" => Bool(options.SpaceBeforeSemicolonInForStatement),

		"csharp_space_between_parentheses" => ParenthesisSpacingParser.ToEditorConfigValue(options.SpaceBetweenParentheses),
		"csharp_space_between_method_declaration_parameter_list_parentheses" => Bool(options.SpaceInDeclarationParameterList),
		"csharp_space_between_method_declaration_empty_parameter_list_parentheses" => Bool(options.SpaceInEmptyDeclarationParameterList),
		"csharp_space_between_method_declaration_name_and_open_parenthesis" => Bool(options.SpaceBeforeDeclarationParameterList),
		"csharp_space_between_method_call_parameter_list_parentheses" => Bool(options.SpaceInCallArgumentList),
		"csharp_space_between_method_call_empty_parameter_list_parentheses" => Bool(options.SpaceInEmptyCallArgumentList),
		"csharp_space_between_method_call_name_and_opening_parenthesis" => Bool(options.SpaceBeforeCallArgumentList),

		"csharp_space_before_open_square_brackets" => Bool(options.SpaceBeforeOpenSquareBrackets),
		"csharp_space_between_empty_square_brackets" => Bool(options.SpaceBetweenEmptySquareBrackets),
		"csharp_space_between_square_brackets" => Bool(options.SpaceBetweenSquareBrackets),

		_ => null,
	};

	private static string Bool(bool value) => value ? "true" : "false";

	private static string Count(int? value) =>
		value?.ToString(CultureInfo.InvariantCulture) ?? "   # no limit; width alone breaks a list";

	private static string Rpar(bool? value) => value switch
	{
		true => "true",
		false => "false",
		null => "true   # when reflow breaks the list; an author's own arrangement is reproduced",
	};

	private static string Expression(ExpressionBodyStyle value) => value switch
	{
		ExpressionBodyStyle.Always => "true",
		ExpressionBodyStyle.WhenOnSingleLine => "when_on_single_line",
		ExpressionBodyStyle.AsWritten => "false   # bodies are left as written; Kerf never adds braces back",
		_ => "false",
	};
}
