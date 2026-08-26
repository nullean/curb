using System.Globalization;
using EditorConfig.Core;
using Nullean.Curb.Options;

namespace Nullean.Curb.EditorConfig;

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
	public static FormatOptions Bind(FileConfiguration configuration, ICollection<CurbDiagnostic>? diagnostics = null)
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
					diagnostics?.Add(CurbDiagnostic.UnrecognisedValue("indent_style", indentStyle, "tab or space"));
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
				diagnostics?.Add(CurbDiagnostic.UnrecognisedValue("max_line_length", maxLineLength, "a positive integer or 'off'"));
		}

		if (properties.TryGetValue("charset", out var charset))
		{
			switch (charset.Trim().ToLowerInvariant())
			{
				case "utf-8":
					options = options with { Charset = Charset.Utf8 };
					break;
				case "utf-8-bom":
					options = options with { Charset = Charset.Utf8Bom };
					break;
				default:
					// latin1, utf-16le and utf-16be are documented by EditorConfig and not supported
					// here: Curb reads and writes UTF-8, and re-encoding a file is not a formatting
					// change. Said out loud rather than silently ignored.
					diagnostics?.Add(CurbDiagnostic.UnrecognisedValue("charset", charset, "utf-8 or utf-8-bom"));
					break;
			}
		}

		// After max_line_length and before anything that asks which mode it is in, and that order is
		// load-bearing rather than tidy: FormatOptions.KeepExistingLinebreaks resolves from the width when
		// the key is absent, and the diagnostic helpers at the bottom of this method read that resolved
		// value while binding is still in progress. Bind this before the width and every one of them sees
		// the default `off` and mis-fires; bind it after the keys that ask and they see the wrong mode.
		if (TryBool(properties, "csharp_keep_existing_linebreaks", diagnostics, out var keepLinebreaks))
		{
			options = options with { KeepExistingLinebreaksOption = keepLinebreaks };

			// Deterministic layout needs a width. The printer treats `off` as infinite, so every group
			// that fits would be flattened — the largest diff Curb can produce rather than the smallest.
			// Refused rather than given an implied width: a repository that never named a width must not
			// be reflowed by a key that reads as being about something else.
			if (!keepLinebreaks && options.MaxLineLength == FormatOptions.Off)
			{
				diagnostics?.Add(CurbDiagnostic.DeterministicLayoutNeedsAWidth());
				options = options with { KeepExistingLinebreaksOption = null };
			}
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
				case "auto":
					// The struct default. Named explicitly here so writing it out is not an error —
					// it is what print-config and the catalog already advertise as a legal value.
					break;
				case "cr":
					// EditorConfig allows it; nothing has emitted lone CR line endings for decades and
					// supporting it would mean a third code path through every break.
					diagnostics?.Add(CurbDiagnostic.UnrecognisedValue("end_of_line", endOfLine, "lf or crlf; cr is not supported"));
					break;
				default:
					diagnostics?.Add(CurbDiagnostic.UnrecognisedValue("end_of_line", endOfLine, "lf or crlf"));
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
				diagnostics?.Add(CurbDiagnostic.UnrecognisedValue(
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

		if (TryBool(properties, "csharp_space_after_unary_operator", diagnostics, out var spaceAfterUnary))
			options = options with { SpaceAfterUnaryOperator = spaceAfterUnary };

		if (TryBool(properties, "csharp_space_around_ternary_operator", diagnostics, out var spaceAroundTernary))
			options = options with { SpaceAroundTernaryOperator = spaceAroundTernary };

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
					diagnostics?.Add(CurbDiagnostic.UnrecognisedValue(
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
					diagnostics?.Add(CurbDiagnostic.UnrecognisedValue(
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

		// ReSharper's keys rather than invented ones, so a repository that has already told Rider what
		// it wants does not have to tell Curb as well.
		if (TryPrefixedBool(properties, "trailing_comma_in_multiline_lists", diagnostics, out var multiline))
			options = options with { TrailingCommaInMultilineLists = multiline };

		if (TryPrefixedBool(properties, "trailing_comma_in_singleline_lists", diagnostics, out var singleline))
			options = options with { TrailingCommaInSinglelineLists = singleline };

		// IDE0036. Presence is the ask: the key has a documented default value, so binding it whether
		// or not it was written would reorder a repository that never mentioned it.
		var deterministic = !options.KeepExistingLinebreaks;
		options = options with
		{
			ExpressionBodiedMethods = ExpressionBody(properties, "methods", diagnostics, deterministic),
			ExpressionBodiedConstructors = ExpressionBody(properties, "constructors", diagnostics, deterministic),
			ExpressionBodiedOperators = ExpressionBody(properties, "operators", diagnostics, deterministic),
			ExpressionBodiedLocalFunctions = ExpressionBody(properties, "local_functions", diagnostics, deterministic),
			ExpressionBodiedAccessors = ExpressionBody(properties, "accessors", diagnostics, deterministic),
			ExpressionBodiedProperties = ExpressionBody(properties, "properties", diagnostics, deterministic),
			ExpressionBodiedIndexers = ExpressionBody(properties, "indexers", diagnostics, deterministic),
		};

		if (properties.TryGetValue("file_header_template", out var header)
			&& !string.IsNullOrWhiteSpace(header)
			&& !string.Equals(header, "unset", StringComparison.OrdinalIgnoreCase))
			options = options with { FileHeaderTemplate = header };

		if (TryPrefixedBool(properties, "wrap_before_binary_opsign", diagnostics, out var beforeOpsign))
			options = options with { WrapBeforeBinaryOpsign = beforeOpsign };

		foreach (var prefix in ReSharperPrefixes)
		{
			if (!properties.TryGetValue(prefix + "wrap_chained_method_calls", out var methodChainWrap))
				continue;

			var colon = methodChainWrap.LastIndexOf(':');
			switch ((colon >= 0 ? methodChainWrap[..colon] : methodChainWrap).Trim().ToLowerInvariant())
			{
				case "chop_if_long":
					// Confirms behaviour the chain printer already has unconditionally; see
					// FormatOptions.WrapChainedMethodCalls.
					options = options with { WrapChainedMethodCalls = WrapStyle.ChopIfLong };
					break;
				case "chop_always":
					diagnostics?.Add(CurbDiagnostic.UnrecognisedValue(
						prefix + "wrap_chained_method_calls", methodChainWrap,
						"chop_if_long; chop_always is not supported, see csharp_max_chained_method_calls_on_line"));
					break;
				case "wrap_if_long":
					diagnostics?.Add(CurbDiagnostic.UnrecognisedValue(
						prefix + "wrap_chained_method_calls", methodChainWrap,
						"chop_if_long; wrap_if_long is not implemented"));
					break;
				default:
					diagnostics?.Add(CurbDiagnostic.UnrecognisedValue(
						prefix + "wrap_chained_method_calls", methodChainWrap, "chop_if_long"));
					break;
			}

			break;
		}

		if (TryPrefixedCount(properties, "max_chained_method_calls_on_line", diagnostics, out var maxChainCalls))
		{
			if (options.KeepExistingLinebreaks)
				diagnostics?.Add(CurbDiagnostic.RequiresDeterministicLayout("csharp_max_chained_method_calls_on_line"));
			else
				options = options with { MaxChainedMethodCallsOnLine = maxChainCalls };
		}
		if (TryPrefixedBool(properties, "wrap_before_first_method_call", diagnostics, out var wrapFirstCall))
			options = options with { WrapBeforeFirstMethodCall = wrapFirstCall };

		if (TryPrefixedBool(properties, "wrap_before_first_type_parameter_constraint", diagnostics, out var wrapConstraint))
			options = options with { WrapBeforeFirstTypeParameterConstraint = wrapConstraint };

		if (TryPrefixedBool(properties, "wrap_before_extends_colon", diagnostics, out var wrapExtends))
			options = options with { WrapBeforeExtendsColon = wrapExtends };

		if (TryPrefixedBool(properties, "place_constructor_initializer_on_same_line", diagnostics, out var placeInitializer))
			options = options with { PlaceConstructorInitializerOnSameLine = placeInitializer };

		if (TryPrefixedBool(properties, "wrap_before_arrow_with_expressions", diagnostics, out var wrapArrow))
			options = options with { WrapBeforeArrowWithExpressions = wrapArrow };

		if (TryPrefixedBool(properties, "wrap_after_dot_in_method_calls", diagnostics, out var wrapAfterDot))
			options = options with { WrapAfterDotInMethodCalls = wrapAfterDot };

		if (TryPrefixedBool(properties, "place_simple_enum_on_single_line", diagnostics, out var placeEnum))
			options = options with { PlaceSimpleEnumOnSingleLine = placeEnum };

		if (TryPrefixedBool(properties, "place_simple_accessorholder_on_single_line", diagnostics, out var placeAccessors))
			options = options with { PlaceSimpleAccessorholderOnSingleLine = placeAccessors };

		if (TryPrefixedBool(properties, "place_simple_declaration_blocks_on_single_line", diagnostics, out var placeDeclBlocks))
			options = options with { PlaceSimpleDeclarationBlocksOnSingleLine = placeDeclBlocks };

		if (TryPrefixedBool(properties, "place_simple_blocks_on_single_line", diagnostics, out var placeBlocks))
			options = options with { PlaceSimpleBlocksOnSingleLine = placeBlocks };

		// The parameter list's is admissible in either mode. The argument, initializer and chained-binary
		// styles are not: forcing every one of those to break moves constructs whose indentation other
		// rules read from the source, and the file then formats differently on its second pass — 140
		// corpus files for arguments against none for parameters. Deterministic layout has no rule that
		// reads indentation from the source, which is exactly why they become available there.
		//
		// wrap_chained_method_calls is bound above, restricted to chop_if_long — its chop_always stays
		// out for the same reason as the two below, and it cost 156 files besides.
		options = options with
		{
			WrapParametersStyle = ParametersWrapStyle(),
			WrapArgumentsStyle = DeterministicOnlyWrapStyle("wrap_arguments_style"),
			WrapObjectAndCollectionInitializerStyle = InitializerWrapStyle(),
			WrapChainedBinaryExpressions = DeterministicOnlyWrapStyle("wrap_chained_binary_expressions"),
		};

		// Four of ReSharper's six. place_type_attribute_on_same_line and the accessorholder one are not
		// here because dotnet format *moves* those onto their own line, so no value but the default could
		// survive a Format Document — that is a fact about the reference implementation rather than about
		// which mode Curb is in, so they are left to the same silence as any other ReSharper key.
		options = options with
		{
			PlaceMethodAttributeOnSameLine = DeterministicOnlyPlacement("place_method_attribute_on_same_line"),
			PlaceFieldAttributeOnSameLine = DeterministicOnlyPlacement("place_field_attribute_on_same_line"),
			PlacePropertyAttributeOnSameLine = DeterministicOnlyPlacement("place_property_attribute_on_same_line"),
			PlaceEventAttributeOnSameLine = DeterministicOnlyPlacement("place_event_attribute_on_same_line"),
		};

		if (TryPrefixedLines(properties, "keep_blank_lines_in_declarations", diagnostics, out var keepDeclarations))
			options = options with { KeepBlankLinesInDeclarations = keepDeclarations };

		if (TryPrefixedLines(properties, "keep_blank_lines_in_code", diagnostics, out var keepCode))
			options = options with { KeepBlankLinesInCode = keepCode };

		if (TryPrefixedLines(properties, "blank_lines_around_invocable", diagnostics, out var aroundInvocable))
			options = options with { BlankLinesAroundInvocable = aroundInvocable };

		if (TryPrefixedLines(properties, "blank_lines_around_local_method", diagnostics, out var aroundLocalMethod))
			options = options with { BlankLinesAroundLocalMethod = aroundLocalMethod };

		if (TryPrefixedLines(properties, "blank_lines_around_type", diagnostics, out var aroundType))
			options = options with { BlankLinesAroundType = aroundType };

		if (TryPrefixedLines(properties, "blank_lines_around_property", diagnostics, out var aroundProperty))
			options = options with { BlankLinesAroundProperty = aroundProperty };

		if (TryPrefixedLines(properties, "blank_lines_around_accessor", diagnostics, out var aroundAccessor))
			options = options with { BlankLinesAroundAccessor = aroundAccessor };

		if (TryPrefixedLines(properties, "blank_lines_around_field", diagnostics, out var aroundField))
			options = options with { BlankLinesAroundField = aroundField };

		if (TryPrefixedLines(properties, "blank_lines_inside_type", diagnostics, out var insideType))
			options = options with { BlankLinesInsideType = insideType };

		if (TryPrefixedLines(properties, "blank_lines_around_namespace", diagnostics, out var aroundNamespace))
			options = options with { BlankLinesAroundNamespace = aroundNamespace };

		if (TryPrefixedLines(properties, "blank_lines_inside_namespace", diagnostics, out var insideNamespace))
			options = options with { BlankLinesInsideNamespace = insideNamespace };

		if (TryPrefixedLines(properties, "blank_lines_around_region", diagnostics, out var aroundRegion))
			options = options with { BlankLinesAroundRegion = aroundRegion };

		if (TryPrefixedLines(properties, "blank_lines_inside_region", diagnostics, out var insideRegion))
			options = options with { BlankLinesInsideRegion = insideRegion };

		if (TryPrefixedLines(properties, "blank_lines_after_using_list", diagnostics, out var afterUsings))
			options = options with { BlankLinesAfterUsingList = afterUsings };

		if (TryPrefixedLines(properties, "blank_lines_after_file_scoped_namespace_directive", diagnostics, out var afterFileScoped))
			options = options with { BlankLinesAfterFileScopedNamespace = afterFileScoped };

		if (TryPrefixedLines(properties, "blank_lines_before_block_statements", diagnostics, out var beforeBlockStatements))
			options = options with { BlankLinesBeforeBlockStatements = beforeBlockStatements };

		if (TryPrefixedLines(properties, "blank_lines_after_block_statements", diagnostics, out var afterBlockStatements))
			options = options with { BlankLinesAfterBlockStatements = afterBlockStatements };

		if (TryPrefixedLines(properties, "blank_lines_before_control_transfer_statements", diagnostics, out var beforeControlTransfer))
			options = options with { BlankLinesBeforeControlTransferStatements = beforeControlTransfer };

		if (TryPrefixedLines(properties, "blank_lines_after_control_transfer_statements", diagnostics, out var afterControlTransfer))
			options = options with { BlankLinesAfterControlTransferStatements = afterControlTransfer };

		if (TryPrefixedLines(properties, "blank_lines_before_case", diagnostics, out var beforeCase))
			options = options with { BlankLinesBeforeCase = beforeCase };

		if (TryPrefixedLines(properties, "blank_lines_after_case", diagnostics, out var afterCase))
			options = options with { BlankLinesAfterCase = afterCase };

		if (TryPrefixedLines(properties, "blank_lines_before_single_line_comment", diagnostics, out var beforeSingleLineComment))
			options = options with { BlankLinesBeforeSingleLineComment = beforeSingleLineComment };

		if (TryBool(properties, "csharp_remove_blank_lines_near_braces_in_declarations", diagnostics, out var removeNearBracesDeclarations))
			options = options with { RemoveBlankLinesNearBracesInDeclarations = removeNearBracesDeclarations };

		if (TryBool(properties, "csharp_remove_blank_lines_near_braces_in_code", diagnostics, out var removeNearBracesCode))
			options = options with { RemoveBlankLinesNearBracesInCode = removeNearBracesCode };

		if (TryPrefixedCount(properties, "max_initializer_elements_on_line", diagnostics, out var maxElements))
			options = options with { MaxInitializerElementsOnLine = maxElements };

		if (TryPrefixedCount(properties, "max_array_initializer_elements_on_line", diagnostics, out var maxArrayElements))
			options = options with { MaxArrayInitializerElementsOnLine = maxArrayElements };

		if (TryPrefixedCount(properties, "max_formal_parameters_on_line", diagnostics, out var maxParameters))
			options = options with { MaxParametersOnLine = maxParameters };

		if (TryPrefixedCount(properties, "max_invocation_arguments_on_line", diagnostics, out var maxArguments))
			options = options with { MaxArgumentsOnLine = maxArguments };

		if (TryPrefixedBool(properties, "wrap_before_declaration_rpar", diagnostics, out var declarationRpar))
			options = options with { WrapBeforeDeclarationRpar = declarationRpar };

		if (TryPrefixedBool(properties, "wrap_before_invocation_rpar", diagnostics, out var invocationRpar))
			options = options with { WrapBeforeInvocationRpar = invocationRpar };

		if (properties.TryGetValue("csharp_using_directive_placement", out var usingPlacement))
		{
			var colon = usingPlacement.LastIndexOf(':');
			switch ((colon >= 0 ? usingPlacement[..colon] : usingPlacement).Trim().ToLowerInvariant())
			{
				case "inside_namespace":
					options = options with { UsingPlacement = UsingPlacement.InsideNamespace };
					break;
				case "outside_namespace":
					options = options with { UsingPlacement = UsingPlacement.OutsideNamespace };
					break;
				default:
					diagnostics?.Add(CurbDiagnostic.UnrecognisedValue(
						"csharp_using_directive_placement", usingPlacement, "inside_namespace or outside_namespace"));
					break;
			}
		}

		if (properties.TryGetValue("csharp_style_namespace_declarations", out var namespaceStyle))
		{
			var colon = namespaceStyle.LastIndexOf(':');
			switch ((colon >= 0 ? namespaceStyle[..colon] : namespaceStyle).Trim().ToLowerInvariant())
			{
				case "file_scoped":
					options = options with { NamespaceStyle = NamespaceStyle.FileScoped };
					break;
				case "block_scoped":
					// Accepted, and deliberately inert: see FormatOptions.NamespaceStyle.
					break;
				default:
					// `block_scoped`, not `block`. Roslyn throws on the short spelling rather than
					// ignoring it, so accepting it here would have let Curb bless a file that stops
					// `dotnet format` dead — which is what verifyexpectations caught on its first run.
					diagnostics?.Add(CurbDiagnostic.UnrecognisedValue(
						"csharp_style_namespace_declarations", namespaceStyle, "file_scoped or block_scoped"));
					break;
			}
		}

		if (properties.TryGetValue("csharp_prefer_braces", out var preferBraces))
		{
			var colon = preferBraces.LastIndexOf(':');
			switch ((colon >= 0 ? preferBraces[..colon] : preferBraces).Trim().ToLowerInvariant())
			{
				case "true":
					options = options with { PreferBraces = BraceRequirement.Always };
					break;
				case "when_multiline":
					options = options with { PreferBraces = BraceRequirement.WhenMultiline };
					ReportIfDeterministic("csharp_prefer_braces", "every unbraced body is braced, as 'true' would");
					break;
				case "false":
					// Roslyn reads this as "take the braces off". Curb only ever adds them.
					break;
				default:
					diagnostics?.Add(CurbDiagnostic.UnrecognisedValue(
						"csharp_prefer_braces", preferBraces, "true, false or when_multiline"));
					break;
			}
		}

		if (properties.TryGetValue("csharp_preferred_modifier_order", out var modifierOrder))
		{
			// Code style options carry an optional `:severity` suffix — `public,...,async:error` — and
			// it belongs to the rule, not to the last modifier in the list. Left on, it makes that
			// modifier match nothing and sort as though it were unnamed, which is silent and wrong.
			// Real repositories write it: the corpus does.
			var colon = modifierOrder.LastIndexOf(':');
			var value = colon >= 0 ? modifierOrder[..colon] : modifierOrder;

			var order = value
				.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

			if (order.Length > 0)
				options = options with { PreferredModifierOrder = order };
			else
				diagnostics?.Add(CurbDiagnostic.UnrecognisedValue(
					"csharp_preferred_modifier_order", modifierOrder, "a comma-separated list of modifiers"));
		}

		// The two native ways of saying "do not format this file".
		if (TryBool(properties, "generated_code", diagnostics, out var generatedCode) && generatedCode)
			options = options with { Excluded = true };

		// EditorConfig lower-cases keys, so the rule id arrives as `ide0055` however it was written.
		if (properties.TryGetValue("dotnet_diagnostic.ide0055.severity", out var formattingSeverity)
			&& string.Equals(formattingSeverity, "none", StringComparison.OrdinalIgnoreCase))
			options = options with { Excluded = true };

		// The two effects below are narrower than "no effect", and getting them wrong once already shipped:
		// width does *not* decide these, because there is no conditional-flatten for a block body — Curb
		// expands every one. And csharp_preserve_single_line_blocks is only partly inert, because the
		// accessor-list printer reads it as a plain option rather than through the source.
		if (TryBool(properties, "csharp_preserve_single_line_statements", diagnostics, out var preserveStatements))
		{
			options = options with { PreserveSingleLineStatements = preserveStatements };
			ReportIfDeterministic(
				"csharp_preserve_single_line_statements",
				"every body, label and statement takes its own line");
		}

		if (TryBool(properties, "csharp_preserve_single_line_blocks", diagnostics, out var preserveBlocks))
		{
			options = options with { PreserveSingleLineBlocks = preserveBlocks };
			ReportIfDeterministic(
				"csharp_preserve_single_line_blocks",
				"block, type, namespace, enum and switch bodies each take their own lines. Accessor lists "
				+ "still answer to it, alongside csharp_place_simple_accessorholder_on_single_line");
		}

		// ReSharper's key, not dotnet format's — see FormatOptions.EmptyBlockStyle for how it differs
		// from csharp_preserve_single_line_blocks, which this leaves untouched when absent.
		options = options with { EmptyBlockStyle = EmptyBlockStyleOf(properties, diagnostics) };

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
				case "no_change":
					options = options with { IndentLabels = LabelIndent.NoChange };
					break;
				case "flush_left":
					options = options with { IndentLabels = LabelIndent.FlushLeft };
					break;
				default:
					// Roslyn's three, and only those. Curb used to accept `no_indent` and
					// `flip_when_block`, which are the names of Roslyn's *internal* enum rather than
					// of its EditorConfig values — so a repository writing the documented `flush_left`
					// was answered with a diagnostic and the default, and `flush_left` itself was
					// never implemented. verifyexpectations is what surfaced it.
					diagnostics?.Add(CurbDiagnostic.UnrecognisedValue(
						"csharp_indent_labels",
						indentLabels,
						"one_less_than_current, no_change or flush_left"));
					break;
			}
		}

		if (properties.TryGetValue("csharp_new_line_before_open_brace", out var braceStyle))
		{
			if (BraceStyleParser.TryParse(braceStyle, out var style))
				options = options with { NewLineBeforeOpenBrace = style };
			else
			{
				diagnostics?.Add(CurbDiagnostic.UnrecognisedValue(
					"csharp_new_line_before_open_brace",
					braceStyle,
					"all, none, or a comma-separated list of " + string.Join(", ", BraceStyleParser.Names[2..])));
			}
		}

		if (diagnostics is not null)
			ReportUnhandledKeys(properties, diagnostics);

		return options;

		// Reads the mode off the enclosing local, which is why csharp_keep_existing_linebreaks is bound
		// before anything else in this method.
		void ReportIfDeterministic(string key, string effect)
		{
			if (!options.KeepExistingLinebreaks)
				diagnostics?.Add(CurbDiagnostic.InertUnderDeterministicLayout(key, effect));
		}

		// The mirror of the above, for the keys that only exist in the other mode.
		WrapStyle? DeterministicOnlyWrapStyle(string suffix)
		{
			var style = WrapStyleOf(properties, suffix, diagnostics);
			if (style is null || !options.KeepExistingLinebreaks)
				return style;

			diagnostics?.Add(CurbDiagnostic.RequiresDeterministicLayout("csharp_" + suffix));
			return null;
		}

		// wrap_parameters_style is admissible in either mode for chop_always — see the remark above
		// this method's caller — but wrap_if_long packs elements by measuring width the same way
		// chop_always forces a break, and that is exactly the risk preservation mode cannot take: a
		// break introduced by packing moves indentation another rule would otherwise read from the
		// source. Only this one value needs the check the other three keys apply to themselves whole.
		WrapStyle? ParametersWrapStyle()
		{
			var style = WrapStyleOf(properties, "wrap_parameters_style", diagnostics);
			if (style != WrapStyle.WrapIfLong || !options.KeepExistingLinebreaks)
				return style;

			diagnostics?.Add(CurbDiagnostic.RequiresDeterministicLayout("csharp_wrap_parameters_style"));
			return null;
		}

		// wrap_if_long is not offered here at all, the one value this key cannot take even under
		// DeterministicOnlyWrapStyle's ordinary gate. Measured directly: dotnet format forces every
		// member of an object or collection initializer onto its own line once it has opened out,
		// unconditionally — even with csharp_new_line_before_members_in_object_initializers = false
		// — so a packed layout is never a fixed point of it. Parameters, arguments and chained binary
		// expressions carry no such rule from dotnet format, which is why they can offer it.
		WrapStyle? InitializerWrapStyle()
		{
			var style = DeterministicOnlyWrapStyle("wrap_object_and_collection_initializer_style");
			if (style != WrapStyle.WrapIfLong)
				return style;

			diagnostics?.Add(CurbDiagnostic.UnrecognisedValue(
				"csharp_wrap_object_and_collection_initializer_style", "wrap_if_long",
				"chop_always or chop_if_long; wrap_if_long is not supported here — dotnet format forces "
				+ "one member per line once an initializer opens out, so a packed layout could never stay put"));
			return null;
		}

		AttributePlacement DeterministicOnlyPlacement(string suffix)
		{
			var placement = PlacementOf(properties, suffix, diagnostics);
			if (placement is null or AttributePlacement.OwnLine)
				return AttributePlacement.OwnLine;

			if (!options.KeepExistingLinebreaks)
				return placement.Value;

			diagnostics?.Add(CurbDiagnostic.RequiresDeterministicLayout("csharp_" + suffix));
			return AttributePlacement.OwnLine;
		}
	}

	/// <summary>Reports keys Curb knows about but has not implemented, and keys it does not recognise at all.</summary>
	private static void ReportUnhandledKeys(IReadOnlyDictionary<string, string> properties, ICollection<CurbDiagnostic> diagnostics)
	{
		foreach (var key in properties.Keys)
		{
			if (OptionCatalog.IsImplemented(key))
				continue;

			if (OptionCatalog.IsKnown(key))
			{
				diagnostics.Add(CurbDiagnostic.NotImplemented(key));
				continue;
			}

			// Code style, naming and analyzer settings are legitimately none of Curb's business.
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
				diagnostics.Add(CurbDiagnostic.UnknownKey(key, suggestion));
		}
	}

	private static bool TryIndentWidth(
		IReadOnlyDictionary<string, string> properties,
		string key,
		ICollection<CurbDiagnostic>? diagnostics,
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

		diagnostics?.Add(CurbDiagnostic.UnrecognisedValue(key, raw, "an integer between 1 and 64"));
		return false;
	}

	/// <summary>Reads one <c>csharp_style_expression_bodied_*</c> key.</summary>
	/// <remarks>
	/// <c>false</c> binds to <see cref="ExpressionBodyStyle.AsWritten"/> rather than to a mode of its
	/// own: Roslyn reads it as "turn expression bodies back into blocks", and Curb only ever adds.
	/// </remarks>
	private static ExpressionBodyStyle ExpressionBody(
		IReadOnlyDictionary<string, string> properties,
		string member,
		ICollection<CurbDiagnostic>? diagnostics,
		bool deterministic = false)
	{
		var key = "csharp_style_expression_bodied_" + member;
		if (!properties.TryGetValue(key, out var raw))
			return ExpressionBodyStyle.AsWritten;

		var colon = raw.LastIndexOf(':');
		switch ((colon >= 0 ? raw[..colon] : raw).Trim().ToLowerInvariant())
		{
			case "true":
				return ExpressionBodyStyle.Always;
			case "when_on_single_line":
				if (deterministic)
					diagnostics?.Add(CurbDiagnostic.InertUnderDeterministicLayout(
						key, "no body is rewritten, as 'false' would"));
				return ExpressionBodyStyle.WhenOnSingleLine;
			case "false":
				return ExpressionBodyStyle.AsWritten;
			default:
				diagnostics?.Add(CurbDiagnostic.UnrecognisedValue(
					key, raw, "true, false or when_on_single_line"));
				return ExpressionBodyStyle.AsWritten;
		}
	}

	/// <summary>Reads one <c>place_*_attribute_on_same_line</c> key, under all four ReSharper spellings.</summary>
	/// <remarks>
	/// ReSharper writes the conditional value as <c>if_owner_is_single_line</c> and the other two as plain
	/// booleans, so all three spellings are accepted here rather than normalised in the caller.
	/// </remarks>
	private static AttributePlacement? PlacementOf(
		IReadOnlyDictionary<string, string> properties,
		string key,
		ICollection<CurbDiagnostic>? diagnostics)
	{
		foreach (var prefix in ReSharperPrefixes)
		{
			if (!properties.TryGetValue(prefix + key, out var raw))
				continue;

			switch (raw.Trim().ToLowerInvariant())
			{
				case "true":
					// Measured, not assumed: joining every attribute is idempotent in Curb and still came
					// back changed on 347 of 1,196 corpus files, because dotnet format moves an attribute
					// off a member that spans lines. Its rule is if_owner_is_single_line, so that is the
					// value to point at.
					diagnostics?.Add(CurbDiagnostic.OverruledByDotnetFormat(
						prefix + key, "true", "Use 'if_owner_is_single_line', which is the rule dotnet format itself applies."));
					return AttributePlacement.OwnLine;
				case "false":
					return AttributePlacement.OwnLine;
				case "if_owner_is_single_line":
					return AttributePlacement.IfOwnerIsSingleLine;
				default:
					diagnostics?.Add(CurbDiagnostic.UnrecognisedValue(
						prefix + key, raw, "true, false or if_owner_is_single_line"));
					return null;
			}
		}

		return null;
	}

	/// <summary>Reads one of ReSharper's wrapping styles, under its four spellings.</summary>
	/// <remarks>
	/// <c>wrap_if_long</c> is the fill layout: pack elements onto a line until the next one would not
	/// fit, rather than <c>chop_if_long</c>'s all-flat-or-all-broken choice or <c>chop_always</c>'s
	/// unconditional one. Some callers restrict it to deterministic layout the same way they restrict
	/// <c>chop_always</c> — see <c>DeterministicOnlyWrapStyle</c> and <c>ParametersWrapStyle</c> — since
	/// forcing a break by measuring width carries the same risk in preservation mode either value does.
	/// </remarks>
	private static WrapStyle? WrapStyleOf(
		IReadOnlyDictionary<string, string> properties,
		string key,
		ICollection<CurbDiagnostic>? diagnostics)
	{
		foreach (var prefix in ReSharperPrefixes)
		{
			if (!properties.TryGetValue(prefix + key, out var raw))
				continue;

			var colon = raw.LastIndexOf(':');
			switch ((colon >= 0 ? raw[..colon] : raw).Trim().ToLowerInvariant())
			{
				case "chop_always":
					return WrapStyle.ChopAlways;
				case "chop_if_long":
					return WrapStyle.ChopIfLong;
				case "wrap_if_long":
					return WrapStyle.WrapIfLong;
				default:
					diagnostics?.Add(CurbDiagnostic.UnrecognisedValue(
						prefix + key, raw, "chop_always, chop_if_long or wrap_if_long"));
					return null;
			}
		}

		return null;
	}

	/// <summary>
	/// Reads <c>[resharper_]csharp_empty_block_style</c> / <c>[resharper_]empty_block_style</c>, under all
	/// four spellings.
	/// </summary>
	private static EmptyBlockStyle? EmptyBlockStyleOf(
		IReadOnlyDictionary<string, string> properties,
		ICollection<CurbDiagnostic>? diagnostics)
	{
		foreach (var prefix in ReSharperPrefixes)
		{
			var key = prefix + "empty_block_style";
			if (!properties.TryGetValue(key, out var raw))
				continue;

			switch (raw.Trim().ToLowerInvariant())
			{
				case "multiline":
					return EmptyBlockStyle.Multiline;
				case "together":
					return EmptyBlockStyle.Together;
				case "together_same_line":
					return EmptyBlockStyle.TogetherSameLine;
				default:
					diagnostics?.Add(CurbDiagnostic.UnrecognisedValue(
						key, raw, "multiline, together or together_same_line"));
					return null;
			}
		}

		return null;
	}

	/// <summary>Reads a blank-line count, which unlike the wrapping limits may legitimately be zero.</summary>
	private static bool TryPrefixedLines(
		IReadOnlyDictionary<string, string> properties,
		string key,
		ICollection<CurbDiagnostic>? diagnostics,
		out int value)
	{
		foreach (var prefix in ReSharperPrefixes)
		{
			if (!properties.TryGetValue(prefix + key, out var raw))
				continue;

			// Capped rather than trusted. A file is not improved by two hundred blank lines, and a
			// typo in a config should not be able to ask for them.
			if (int.TryParse(raw.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out value)
				&& value is >= 0 and <= 8)
				return true;

			diagnostics?.Add(CurbDiagnostic.UnrecognisedValue(prefix + key, raw, "a number from 0 to 8"));
			value = 0;
			return false;
		}

		value = 0;
		return false;
	}

	/// <summary>Reads a positive count under ReSharper's four spellings.</summary>
	private static bool TryPrefixedCount(
		IReadOnlyDictionary<string, string> properties,
		string key,
		ICollection<CurbDiagnostic>? diagnostics,
		out int value)
	{
		foreach (var prefix in ReSharperPrefixes)
		{
			if (!properties.TryGetValue(prefix + key, out var raw))
				continue;

			if (int.TryParse(raw.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out value) && value > 0)
				return true;

			diagnostics?.Add(CurbDiagnostic.UnrecognisedValue(prefix + key, raw, "a positive integer"));
			value = 0;
			return false;
		}

		value = 0;
		return false;
	}

	/// <summary>
	/// Reads a key ReSharper documents under four spellings, most specific first.
	/// </summary>
	/// <remarks>
	/// ReSharper accepts <c>resharper_csharp_x</c>, <c>csharp_x</c>, <c>resharper_x</c> and bare
	/// <c>x</c> for the same setting, and real repositories use all four. Whichever is present wins,
	/// and the more specific one wins when several are.
	/// </remarks>
	private static bool TryPrefixedBool(
		IReadOnlyDictionary<string, string> properties,
		string key,
		ICollection<CurbDiagnostic>? diagnostics,
		out bool value)
	{
		foreach (var prefix in ReSharperPrefixes)
		{
			if (TryBool(properties, prefix + key, diagnostics, out value))
				return true;
		}

		value = false;
		return false;
	}

	private static readonly string[] ReSharperPrefixes = ["resharper_csharp_", "csharp_", "resharper_", ""];

	private static bool TryBool(
		IReadOnlyDictionary<string, string> properties,
		string key,
		ICollection<CurbDiagnostic>? diagnostics,
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
				diagnostics?.Add(CurbDiagnostic.UnrecognisedValue(key, raw, "true or false"));
				return false;
		}
	}
}
