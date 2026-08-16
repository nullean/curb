using System.Globalization;
using EditorConfig.Core;
using Nullean.Kerf.Options;

namespace Nullean.Kerf.EditorConfig;

/// <summary>
/// Turns resolved <c>.editorconfig</c> settings into <see cref="FormatOptions"/>, reporting anything
/// it could not honour.
/// </summary>
/// <remarks>
/// Binding never fails. A bad value produces a diagnostic and keeps the default, because refusing to
/// format a file over a typo in a config key three directories up is a worse outcome than formatting
/// it with a documented default.
/// </remarks>
public static class EditorConfigOptionsBinder
{
	public static FormatOptions Bind(FileConfiguration configuration, ICollection<KerfDiagnostic>? diagnostics = null)
	{
		var options = new FormatOptions();
		var properties = configuration.Properties;

		if (properties.TryGetValue("indent_style", out var indentStyle))
		{
			switch (indentStyle)
			{
				case "tab":
					options = options with { UseTabs = true };
					break;
				case "space":
					options = options with { UseTabs = false };
					break;
				default:
					diagnostics?.Add(KerfDiagnostic.UnrecognisedValue("indent_style", indentStyle, "tab or space"));
					break;
			}
		}

		if (TryIndentWidth(properties, "indent_size", diagnostics, out var indentSize))
			options = options with { IndentSize = indentSize };

		if (TryIndentWidth(properties, "tab_width", diagnostics, out var tabWidth))
			options = options with { TabWidth = tabWidth };
		else if (properties.ContainsKey("indent_size"))
			options = options with { TabWidth = options.IndentSize };

		if (properties.TryGetValue("max_line_length", out var maxLineLength))
		{
			if (string.Equals(maxLineLength, "off", StringComparison.OrdinalIgnoreCase))
				options = options with { MaxLineLength = FormatOptions.Off };
			else if (int.TryParse(maxLineLength, NumberStyles.Integer, CultureInfo.InvariantCulture, out var width) && width > 0)
				options = options with { MaxLineLength = width };
			else
				diagnostics?.Add(KerfDiagnostic.UnrecognisedValue("max_line_length", maxLineLength, "a positive integer or 'off'"));
		}

		if (properties.TryGetValue("end_of_line", out var endOfLine))
		{
			switch (endOfLine.ToLowerInvariant())
			{
				case "lf":
					options = options with { EndOfLine = EndOfLine.Lf };
					break;
				case "crlf":
					options = options with { EndOfLine = EndOfLine.CrLf };
					break;
				case "cr":
					// EditorConfig allows it; nothing has emitted lone CR line endings for decades and
					// supporting it would mean a third code path through every break.
					diagnostics?.Add(KerfDiagnostic.UnrecognisedValue("end_of_line", endOfLine, "lf or crlf; cr is not supported"));
					break;
				default:
					diagnostics?.Add(KerfDiagnostic.UnrecognisedValue("end_of_line", endOfLine, "lf or crlf"));
					break;
			}
		}

		if (TryBool(properties, "insert_final_newline", diagnostics, out var insertFinalNewLine))
			options = options with { InsertFinalNewLine = insertFinalNewLine };

		if (TryBool(properties, "trim_trailing_whitespace", diagnostics, out var trimTrailing))
			options = options with { TrimTrailingWhitespace = trimTrailing };

		if (TryBool(properties, "csharp_new_line_before_else", diagnostics, out var newLineBeforeElse))
			options = options with { NewLineBeforeElse = newLineBeforeElse };

		if (TryBool(properties, "csharp_new_line_before_catch", diagnostics, out var newLineBeforeCatch))
			options = options with { NewLineBeforeCatch = newLineBeforeCatch };

		if (TryBool(properties, "csharp_new_line_before_finally", diagnostics, out var newLineBeforeFinally))
			options = options with { NewLineBeforeFinally = newLineBeforeFinally };

		if (TryBool(properties, "csharp_space_after_cast", diagnostics, out var spaceAfterCast))
			options = options with { SpaceAfterCast = spaceAfterCast };

		if (TryBool(properties, "csharp_space_after_keywords_in_control_flow_statements", diagnostics, out var spaceAfterKeyword))
			options = options with { SpaceAfterKeywordsInControlFlowStatements = spaceAfterKeyword };

		if (TryBool(properties, "csharp_space_before_colon_in_inheritance_clause", diagnostics, out var spaceBeforeColon))
			options = options with { SpaceBeforeColonInInheritanceClause = spaceBeforeColon };

		if (TryBool(properties, "csharp_space_after_colon_in_inheritance_clause", diagnostics, out var spaceAfterColon))
			options = options with { SpaceAfterColonInInheritanceClause = spaceAfterColon };

		if (TryBool(properties, "csharp_space_after_comma", diagnostics, out var spaceAfterComma))
			options = options with { SpaceAfterComma = spaceAfterComma };

		if (TryBool(properties, "csharp_space_before_comma", diagnostics, out var spaceBeforeComma))
			options = options with { SpaceBeforeComma = spaceBeforeComma };

		if (TryBool(properties, "csharp_space_between_method_declaration_parameter_list_parentheses", diagnostics, out var inDeclarationParens))
			options = options with { SpaceInDeclarationParameterList = inDeclarationParens };

		if (TryBool(properties, "csharp_space_between_method_declaration_empty_parameter_list_parentheses", diagnostics, out var inEmptyDeclarationParens))
			options = options with { SpaceInEmptyDeclarationParameterList = inEmptyDeclarationParens };

		if (TryBool(properties, "csharp_space_between_method_declaration_name_and_open_parenthesis", diagnostics, out var beforeDeclarationParens))
			options = options with { SpaceBeforeDeclarationParameterList = beforeDeclarationParens };

		if (TryBool(properties, "csharp_space_between_method_call_parameter_list_parentheses", diagnostics, out var inCallParens))
			options = options with { SpaceInCallArgumentList = inCallParens };

		if (TryBool(properties, "csharp_space_between_method_call_empty_parameter_list_parentheses", diagnostics, out var inEmptyCallParens))
			options = options with { SpaceInEmptyCallArgumentList = inEmptyCallParens };

		if (TryBool(properties, "csharp_space_between_method_call_name_and_opening_parenthesis", diagnostics, out var beforeCallParens))
			options = options with { SpaceBeforeCallArgumentList = beforeCallParens };

		if (properties.TryGetValue("csharp_space_between_parentheses", out var betweenParentheses))
		{
			if (ParenthesisSpacingParser.TryParse(betweenParentheses, out var parenSpacing))
				options = options with { SpaceBetweenParentheses = parenSpacing };
			else
			{
				diagnostics?.Add(KerfDiagnostic.UnrecognisedValue(
					"csharp_space_between_parentheses",
					betweenParentheses,
					"false, or a comma-separated list of " + string.Join(", ", ParenthesisSpacingParser.Names)));
			}
		}

		if (TryBool(properties, "csharp_space_before_open_square_brackets", diagnostics, out var spaceBeforeBracket))
			options = options with { SpaceBeforeOpenSquareBrackets = spaceBeforeBracket };

		if (TryBool(properties, "csharp_space_between_empty_square_brackets", diagnostics, out var spaceInEmptyBrackets))
			options = options with { SpaceBetweenEmptySquareBrackets = spaceInEmptyBrackets };

		if (TryBool(properties, "csharp_space_between_square_brackets", diagnostics, out var spaceInBrackets))
			options = options with { SpaceBetweenSquareBrackets = spaceInBrackets };

		if (TryBool(properties, "csharp_space_before_dot", diagnostics, out var spaceBeforeDot))
			options = options with { SpaceBeforeDot = spaceBeforeDot };

		if (TryBool(properties, "csharp_space_after_dot", diagnostics, out var spaceAfterDot))
			options = options with { SpaceAfterDot = spaceAfterDot };

		if (TryBool(properties, "csharp_space_before_semicolon_in_for_statement", diagnostics, out var spaceBeforeSemicolon))
			options = options with { SpaceBeforeSemicolonInForStatement = spaceBeforeSemicolon };

		if (TryBool(properties, "csharp_space_after_semicolon_in_for_statement", diagnostics, out var spaceAfterSemicolon))
			options = options with { SpaceAfterSemicolonInForStatement = spaceAfterSemicolon };

		if (properties.TryGetValue("csharp_space_around_binary_operators", out var binaryOperators))
		{
			switch (binaryOperators.ToLowerInvariant())
			{
				case "before_and_after":
					options = options with { SpaceAroundBinaryOperators = BinaryOperatorSpacing.BeforeAndAfter };
					break;
				case "none":
					options = options with { SpaceAroundBinaryOperators = BinaryOperatorSpacing.None };
					break;
				case "ignore":
					options = options with { SpaceAroundBinaryOperators = BinaryOperatorSpacing.Ignore };
					break;
				default:
					diagnostics?.Add(KerfDiagnostic.UnrecognisedValue(
						"csharp_space_around_binary_operators",
						binaryOperators,
						"before_and_after, none or ignore"));
					break;
			}
		}

		if (properties.TryGetValue("csharp_space_around_declaration_statements", out var declarationSpacing))
		{
			switch (declarationSpacing.ToLowerInvariant())
			{
				case "false":
					options = options with { SpaceAroundDeclarationStatements = DeclarationSpacing.Normalise };
					break;
				case "ignore":
					options = options with { SpaceAroundDeclarationStatements = DeclarationSpacing.Ignore };
					break;
				default:
					// `true` is not a value this option takes: the choice is between normalising the
					// spacing and reproducing it, not between adding and removing spaces.
					diagnostics?.Add(KerfDiagnostic.UnrecognisedValue(
						"csharp_space_around_declaration_statements",
						declarationSpacing,
						"false or ignore"));
					break;
			}
		}

		if (TryBool(properties, "csharp_new_line_before_members_in_object_initializers", diagnostics, out var newLineInInitializers))
			options = options with { NewLineBeforeMembersInObjectInitializers = newLineInInitializers };

		if (TryBool(properties, "csharp_new_line_before_members_in_anonymous_types", diagnostics, out var newLineInAnonymous))
			options = options with { NewLineBeforeMembersInAnonymousTypes = newLineInAnonymous };

		if (TryBool(properties, "csharp_new_line_between_query_expression_clauses", diagnostics, out var newLineBetweenClauses))
			options = options with { NewLineBetweenQueryExpressionClauses = newLineBetweenClauses };

		// Presence is the opt-in: neither key says whether to sort, so writing either one is taken
		// as asking for it. See UsingOrder for the reasoning.
		if (TryBool(properties, "dotnet_sort_system_directives_first", diagnostics, out var systemFirst))
			options = options with { SortUsings = systemFirst ? UsingOrder.SystemFirst : UsingOrder.Alphabetical };

		if (TryBool(properties, "dotnet_separate_import_directive_groups", diagnostics, out var separateGroups))
		{
			options = options with { SeparateImportDirectiveGroups = separateGroups };
			if (options.SortUsings == UsingOrder.AsWritten)
				options = options with { SortUsings = UsingOrder.SystemFirst };
		}

		if (TryBool(properties, "kerf_opinionated", diagnostics, out var opinionated))
			options = options with { Opinionated = opinionated };

		// The two native ways of saying "do not format this file".
		if (TryBool(properties, "generated_code", diagnostics, out var generatedCode) && generatedCode)
			options = options with { Excluded = true };

		// EditorConfig lower-cases keys, so the rule id arrives as `ide0055` however it was written.
		if (properties.TryGetValue("dotnet_diagnostic.ide0055.severity", out var formattingSeverity)
			&& string.Equals(formattingSeverity, "none", StringComparison.OrdinalIgnoreCase))
			options = options with { Excluded = true };

		if (TryBool(properties, "csharp_preserve_single_line_statements", diagnostics, out var preserveStatements))
			options = options with { PreserveSingleLineStatements = preserveStatements };

		if (TryBool(properties, "csharp_preserve_single_line_blocks", diagnostics, out var preserveBlocks))
			options = options with { PreserveSingleLineBlocks = preserveBlocks };

		if (TryBool(properties, "csharp_indent_case_contents", diagnostics, out var indentCaseContents))
			options = options with { IndentCaseContents = indentCaseContents };

		if (TryBool(properties, "csharp_indent_case_contents_when_block", diagnostics, out var indentCaseBlock))
			options = options with { IndentCaseContentsWhenBlock = indentCaseBlock };

		if (TryBool(properties, "csharp_indent_switch_labels", diagnostics, out var indentSwitchLabels))
			options = options with { IndentSwitchLabels = indentSwitchLabels };

		if (TryBool(properties, "csharp_indent_braces", diagnostics, out var indentBraces))
			options = options with { IndentBraces = indentBraces };

		if (TryBool(properties, "csharp_indent_block_contents", diagnostics, out var indentBlockContents))
			options = options with { IndentBlockContents = indentBlockContents };

		if (properties.TryGetValue("csharp_indent_labels", out var indentLabels))
		{
			switch (indentLabels.ToLowerInvariant())
			{
				case "one_less_than_current":
					options = options with { IndentLabels = LabelIndent.OneLessThanCurrent };
					break;
				case "no_indent":
					options = options with { IndentLabels = LabelIndent.NoIndent };
					break;
				case "flip_when_block":
					options = options with { IndentLabels = LabelIndent.FlipWhenBlock };
					break;
				default:
					diagnostics?.Add(KerfDiagnostic.UnrecognisedValue(
						"csharp_indent_labels",
						indentLabels,
						"one_less_than_current, no_indent or flip_when_block"));
					break;
			}
		}

		if (properties.TryGetValue("csharp_new_line_before_open_brace", out var braceStyle))
		{
			if (BraceStyleParser.TryParse(braceStyle, out var style))
				options = options with { NewLineBeforeOpenBrace = style };
			else
			{
				diagnostics?.Add(KerfDiagnostic.UnrecognisedValue(
					"csharp_new_line_before_open_brace",
					braceStyle,
					"all, none, or a comma-separated list of " + string.Join(", ", BraceStyleParser.Names[2..])));
			}
		}

		if (diagnostics is not null)
			ReportUnhandledKeys(properties, diagnostics);

		return options;
	}

	/// <summary>Reports keys Kerf knows about but has not implemented, and keys it does not recognise at all.</summary>
	private static void ReportUnhandledKeys(IReadOnlyDictionary<string, string> properties, ICollection<KerfDiagnostic> diagnostics)
	{
		foreach (var key in properties.Keys)
		{
			if (OptionCatalog.IsImplemented(key))
				continue;

			if (OptionCatalog.IsKnown(key))
			{
				diagnostics.Add(KerfDiagnostic.NotImplemented(key));
				continue;
			}

			// Code style, naming and analyzer settings are legitimately none of Kerf's business.
			if (OptionCatalog.IsOtherCodeStyleKey(key))
				continue;

			if (!key.StartsWith("csharp_", StringComparison.Ordinal) && !key.StartsWith("dotnet_", StringComparison.Ordinal))
				continue;

			// The csharp_* namespace is shared: ReSharper and Rider put their own formatting keys
			// there too (csharp_align_multiline_*, csharp_wrap_*, csharp_preferred_modifier_order,
			// and many more). Warning about those would fire on most real repositories and would be
			// wrong — they are another tool's settings, not typos.
			//
			// So only report a key we can plausibly read as a misspelling of one of ours. No near
			// match means it almost certainly was not meant for us, and silence is the right answer.
			var suggestion = OptionCatalog.Suggest(key);
			if (suggestion is not null)
				diagnostics.Add(KerfDiagnostic.UnknownKey(key, suggestion));
		}
	}

	private static bool TryIndentWidth(
		IReadOnlyDictionary<string, string> properties,
		string key,
		ICollection<KerfDiagnostic>? diagnostics,
		out int value)
	{
		value = 0;
		if (!properties.TryGetValue(key, out var raw))
			return false;

		// `indent_size = tab` defers to tab_width; the caller's default already covers it.
		if (string.Equals(raw, "tab", StringComparison.OrdinalIgnoreCase))
			return false;

		if (int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out value) && value is > 0 and <= 64)
			return true;

		diagnostics?.Add(KerfDiagnostic.UnrecognisedValue(key, raw, "an integer between 1 and 64"));
		return false;
	}

	private static bool TryBool(
		IReadOnlyDictionary<string, string> properties,
		string key,
		ICollection<KerfDiagnostic>? diagnostics,
		out bool value)
	{
		value = false;
		if (!properties.TryGetValue(key, out var raw))
			return false;

		switch (raw.ToLowerInvariant())
		{
			case "true":
				value = true;
				return true;
			case "false":
				value = false;
				return true;
			default:
				diagnostics?.Add(KerfDiagnostic.UnrecognisedValue(key, raw, "true or false"));
				return false;
		}
	}
}
