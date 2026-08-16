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
					options = options with { SpaceAroundBinaryOperators = true };
					break;
				case "none":
					options = options with { SpaceAroundBinaryOperators = false };
					break;
				default:
					// `ignore` means reproduce the author's own whitespace verbatim, which is a
					// different mechanism from a spacing flag and is not built yet.
					diagnostics?.Add(KerfDiagnostic.UnrecognisedValue(
						"csharp_space_around_binary_operators",
						binaryOperators,
						"before_and_after or none; ignore is not implemented yet"));
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
