using System.Globalization;
using Nullean.Curb.Options;

namespace Nullean.Curb.EditorConfig;

/// <summary>
/// Binds the EditorConfig core keys, which every language Curb formats shares, into
/// <see cref="LayoutOptions"/>.
/// </summary>
/// <remarks>
/// <para>
/// The keys are the same for every language; their defaults are not. XML indents by two where C#
/// indents by four, so the caller passes the defaults a key that is absent leaves in place.
/// </para>
/// <para>
/// Binding comes in two halves, <see cref="BindIndentationAndWidth"/> and <see cref="BindLineEndings"/>,
/// so a language whose layout mode resolves from the width can bind its mode key between them, while
/// the width is known and before anything else asks. <see cref="Bind"/> runs both for a language that
/// has no such key. Like <see cref="EditorConfigOptionsBinder"/>, it never fails: a bad value is a
/// diagnostic and keeps the default.
/// </para>
/// </remarks>
public static class LayoutOptionsBinder
{
	/// <summary>Binds every core key over <paramref name="defaults"/>.</summary>
	public static LayoutOptions Bind(
		IReadOnlyDictionary<string, string> properties,
		ICollection<CurbDiagnostic>? diagnostics,
		LayoutOptions defaults) =>
		BindLineEndings(properties, diagnostics, BindIndentationAndWidth(properties, diagnostics, defaults));

	/// <summary>
	/// Binds <c>indent_style</c>, <c>indent_size</c>, <c>tab_width</c>, <c>max_line_length</c> and
	/// <c>charset</c>, in that order.
	/// </summary>
	public static LayoutOptions BindIndentationAndWidth(
		IReadOnlyDictionary<string, string> properties,
		ICollection<CurbDiagnostic>? diagnostics,
		LayoutOptions defaults)
	{
		var options = defaults;

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
				options = options with { MaxLineLength = LayoutOptions.Off };
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

		return options;
	}

	/// <summary>
	/// Binds <c>end_of_line</c>, <c>insert_final_newline</c> and <c>trim_trailing_whitespace</c>, in that
	/// order.
	/// </summary>
	public static LayoutOptions BindLineEndings(
		IReadOnlyDictionary<string, string> properties,
		ICollection<CurbDiagnostic>? diagnostics,
		LayoutOptions defaults)
	{
		var options = defaults;

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

		return options;
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

	/// <summary>Reads a <c>true</c>/<c>false</c> key, case-insensitively. Shared with every language's binder.</summary>
	internal static bool TryBool(
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
