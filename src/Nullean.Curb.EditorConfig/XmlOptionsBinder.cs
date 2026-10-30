using System.Globalization;
using EditorConfig.Core;
using Nullean.Curb.Options;
using Nullean.Curb.Xml;

namespace Nullean.Curb.EditorConfig;

/// <summary>Turns resolved <c>.editorconfig</c> settings for an XML file into <see cref="XmlOptions"/>.</summary>
/// <remarks>
/// Like <see cref="EditorConfigOptionsBinder"/>, it never fails: a bad value is a diagnostic and keeps
/// the default. The core keys bind over XML's own defaults, two-space indents.
/// </remarks>
public static class XmlOptionsBinder
{
	public static XmlOptions Bind(FileConfiguration configuration, ICollection<CurbDiagnostic>? diagnostics = null) =>
		Bind(configuration.Properties, XmlOptions.ProfileFor(configuration.FileName), diagnostics);

	public static XmlOptions Bind(IReadOnlyDictionary<string, string> properties, XmlProfile profile, ICollection<CurbDiagnostic>? diagnostics = null)
	{
		var options = new XmlOptions
		{
			Profile = profile,
			Layout = LayoutOptionsBinder.Bind(properties, diagnostics, XmlOptions.DefaultLayout),
			FinalNewLineSet = properties.ContainsKey("insert_final_newline"),
		};

		if (LayoutOptionsBinder.TryBool(properties, "curb_xml_format", diagnostics, out var enabled))
			options = options with { Enabled = enabled };

		if (properties.TryGetValue("curb_xml_extensions", out var extensions))
			options = options with { Extensions = BindExtensions(extensions, diagnostics) };

		if (properties.TryGetValue("xml_max_blank_lines_between_tags", out var blankLines))
		{
			if (int.TryParse(blankLines, NumberStyles.Integer, CultureInfo.InvariantCulture, out var count) && count >= 0)
				options = options with { MaxBlankLines = count };
			else
				diagnostics?.Add(CurbDiagnostic.UnrecognisedValue("xml_max_blank_lines_between_tags", blankLines, "a non-negative integer"));
		}

		if (LayoutOptionsBinder.TryBool(properties, "xml_space_before_self_closing", diagnostics, out var spaceBeforeSelfClosing))
			options = options with { SpaceBeforeSelfClosingOption = spaceBeforeSelfClosing ? SelfClosingSpace.Always : SelfClosingSpace.Never };

		if (properties.TryGetValue("xml_attribute_indent", out var attributeIndent))
		{
			switch (attributeIndent.ToLowerInvariant())
			{
				case "single_indent":
					options = options with { AttributeIndent = AttributeIndent.SingleIndent };
					break;
				case "double_indent":
					options = options with { AttributeIndent = AttributeIndent.DoubleIndent };
					break;
				case "align_by_first_attribute":
					options = options with { AttributeIndent = AttributeIndent.AlignByFirstAttribute };
					break;
				default:
					diagnostics?.Add(CurbDiagnostic.UnrecognisedValue("xml_attribute_indent", attributeIndent, "single_indent, double_indent or align_by_first_attribute"));
					break;
			}
		}

		if (LayoutOptionsBinder.TryBool(properties, "generated_code", diagnostics, out var generated) && generated)
			options = options with { Excluded = true };

		return options;
	}

	/// <summary>
	/// Reads a comma-separated list of extensions, with or without their dots, keeping the ones a walk
	/// can find.
	/// </summary>
	private static string[] BindExtensions(string value, ICollection<CurbDiagnostic>? diagnostics)
	{
		var bound = new List<string>();
		foreach (var item in value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
		{
			var extension = (item.StartsWith('.') ? item : "." + item).ToLowerInvariant();
			if (Array.IndexOf(XmlOptions.OptInExtensions, extension) >= 0)
			{
				bound.Add(extension);
				continue;
			}

			diagnostics?.Add(CurbDiagnostic.UnrecognisedValue(
				"curb_xml_extensions", item, "a comma-separated list drawn from " + string.Join(", ", XmlOptions.OptInExtensions)));
		}

		return [.. bound];
	}
}
