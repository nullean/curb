using System.Collections.Frozen;

namespace Nullean.Kerf.Options;

/// <summary>
/// Every <c>.editorconfig</c> key Kerf recognises, and whether it is wired up yet.
/// </summary>
/// <remarks>
/// <para>
/// The point of separating "known" from "implemented" is honesty. Kerf onboards formatting options
/// one at a time, and a user whose <c>.editorconfig</c> sets an option Kerf has not reached yet
/// deserves to be told so, rather than watching it be silently ignored — which is the failure mode
/// that makes people distrust a formatter.
/// </para>
/// <para>
/// This list is hand-maintained while the surface is small. It becomes generated, along with the
/// parsers, validators and docs, once the option catalog proper lands.
/// </para>
/// </remarks>
public static class OptionCatalog
{
	/// <summary>Core EditorConfig keys, all of which Kerf honours.</summary>
	public static readonly FrozenSet<string> CoreKeys = new[]
	{
		"indent_style",
		"indent_size",
		"tab_width",
		"end_of_line",
		"insert_final_newline",
		"trim_trailing_whitespace",
		"charset",
		"max_line_length",
	}.ToFrozenSet(StringComparer.Ordinal);

	/// <summary>The 39 formatting options that make up code style rule IDE0055.</summary>
	public static readonly FrozenSet<string> FormattingKeys = new[]
	{
		// New-line options (7)
		"csharp_new_line_before_open_brace",
		"csharp_new_line_before_else",
		"csharp_new_line_before_catch",
		"csharp_new_line_before_finally",
		"csharp_new_line_before_members_in_object_initializers",
		"csharp_new_line_before_members_in_anonymous_types",
		"csharp_new_line_between_query_expression_clauses",

		// Indentation options (6)
		"csharp_indent_case_contents",
		"csharp_indent_switch_labels",
		"csharp_indent_labels",
		"csharp_indent_block_contents",
		"csharp_indent_braces",
		"csharp_indent_case_contents_when_block",

		// Spacing options (22)
		"csharp_space_after_cast",
		"csharp_space_after_keywords_in_control_flow_statements",
		"csharp_space_between_parentheses",
		"csharp_space_before_colon_in_inheritance_clause",
		"csharp_space_after_colon_in_inheritance_clause",
		"csharp_space_around_binary_operators",
		"csharp_space_between_method_declaration_parameter_list_parentheses",
		"csharp_space_between_method_declaration_empty_parameter_list_parentheses",
		"csharp_space_between_method_declaration_name_and_open_parenthesis",
		"csharp_space_between_method_call_parameter_list_parentheses",
		"csharp_space_between_method_call_empty_parameter_list_parentheses",
		"csharp_space_between_method_call_name_and_opening_parenthesis",
		"csharp_space_after_comma",
		"csharp_space_before_comma",
		"csharp_space_after_dot",
		"csharp_space_before_dot",
		"csharp_space_after_semicolon_in_for_statement",
		"csharp_space_before_semicolon_in_for_statement",
		"csharp_space_around_declaration_statements",
		"csharp_space_before_open_square_brackets",
		"csharp_space_between_empty_square_brackets",
		"csharp_space_between_square_brackets",

		// Wrap options (2)
		"csharp_preserve_single_line_statements",
		"csharp_preserve_single_line_blocks",

		// .NET formatting options (2)
		"dotnet_sort_system_directives_first",
		"dotnet_separate_import_directive_groups",
	}.ToFrozenSet(StringComparer.Ordinal);

	/// <summary>Keys Kerf actually acts on today. Everything else in the catalog reports KERF1003.</summary>
	public static readonly FrozenSet<string> ImplementedKeys =
		CoreKeys.Concat([
			"csharp_new_line_before_open_brace",
			"csharp_new_line_before_else",
			"csharp_new_line_before_catch",
			"csharp_new_line_before_finally",
			"csharp_space_after_cast",
			"csharp_space_after_keywords_in_control_flow_statements",
			"csharp_space_before_colon_in_inheritance_clause",
			"csharp_space_after_colon_in_inheritance_clause",
			"csharp_space_around_binary_operators",
			"csharp_space_after_comma",
			"csharp_space_before_comma",
			"csharp_space_before_dot",
			"csharp_space_after_dot",
			"csharp_space_before_semicolon_in_for_statement",
			"csharp_space_after_semicolon_in_for_statement",
		]).ToFrozenSet(StringComparer.Ordinal);

	/// <summary>True for a key Kerf knows about, whether or not it is implemented yet.</summary>
	public static bool IsKnown(string key) => CoreKeys.Contains(key) || FormattingKeys.Contains(key);

	public static bool IsImplemented(string key) => ImplementedKeys.Contains(key);

	/// <summary>
	/// True for keys that belong to .NET code style but are not formatting, so Kerf should ignore
	/// them rather than warn about them. These are <c>dotnet format style</c>'s territory.
	/// </summary>
	public static bool IsOtherCodeStyleKey(string key) =>
		key.StartsWith("dotnet_style_", StringComparison.Ordinal)
		|| key.StartsWith("csharp_style_", StringComparison.Ordinal)
		|| key.StartsWith("csharp_prefer_", StringComparison.Ordinal)
		|| key.StartsWith("dotnet_naming_", StringComparison.Ordinal)
		|| key.StartsWith("dotnet_diagnostic.", StringComparison.Ordinal)
		|| key.StartsWith("dotnet_analyzer_diagnostic", StringComparison.Ordinal)
		|| key.StartsWith("dotnet_code_quality", StringComparison.Ordinal)
		|| key.StartsWith("dotnet_remove_unnecessary_suppression_exclusions", StringComparison.Ordinal);

	/// <summary>Finds the closest known key to <paramref name="key"/>, for "did you mean" suggestions.</summary>
	public static string? Suggest(string key)
	{
		string? best = null;
		var bestDistance = int.MaxValue;

		foreach (var candidate in FormattingKeys.Concat(CoreKeys))
		{
			var distance = Levenshtein(key, candidate, bestDistance);
			if (distance >= bestDistance)
				continue;
			bestDistance = distance;
			best = candidate;
		}

		// Beyond a quarter of the key's length the "suggestion" is noise, not help.
		return bestDistance <= Math.Max(2, key.Length / 4) ? best : null;
	}

	private static int Levenshtein(string a, string b, int ceiling)
	{
		if (Math.Abs(a.Length - b.Length) >= ceiling)
			return int.MaxValue;

		var previous = new int[b.Length + 1];
		var current = new int[b.Length + 1];

		for (var j = 0; j <= b.Length; j++)
			previous[j] = j;

		for (var i = 1; i <= a.Length; i++)
		{
			current[0] = i;
			var rowBest = current[0];

			for (var j = 1; j <= b.Length; j++)
			{
				var cost = a[i - 1] == b[j - 1] ? 0 : 1;
				current[j] = Math.Min(Math.Min(current[j - 1] + 1, previous[j] + 1), previous[j - 1] + cost);
				rowBest = Math.Min(rowBest, current[j]);
			}

			if (rowBest >= ceiling)
				return int.MaxValue;

			(previous, current) = (current, previous);
		}

		return previous[b.Length];
	}
}
