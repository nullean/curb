using System.Globalization;
using EditorConfig.Core;
using Nullean.Curb.Options;
using Nullean.Curb.Xml;

namespace Nullean.Curb.EditorConfig;

/// <summary>Turns resolved <c>.editorconfig</c> settings for an XML file into <see cref="XmlOptions"/>.</summary>
/// <remarks>
/// <para>
/// Like <see cref="EditorConfigOptionsBinder"/>, it never fails: a bad value is a diagnostic and keeps
/// the default.
/// </para>
/// <para>
/// The core keys (indent, width, line endings, final newline, trailing whitespace, charset) are read
/// only from explicit sections, ones whose glob names an extension or a file name: see
/// <see cref="CurbEditorConfig.ExplicitFor"/>. Without one XML keeps its own defaults: two-space
/// indents, no width, and the file's own line endings, final newline and trailing whitespace. The
/// <c>curb_xml_*</c> and <c>xml_*</c> keys say whose they are by name, so they apply from any section.
/// </para>
/// </remarks>
public static class XmlOptionsBinder
{
	/// <summary>Binds the options for <paramref name="filePath"/>, reading the core keys from explicit sections only.</summary>
	public static XmlOptions Bind(CurbEditorConfig editorConfig, string filePath, ICollection<CurbDiagnostic>? diagnostics = null) =>
		Bind(editorConfig.For(filePath), editorConfig.ExplicitFor(filePath), diagnostics);

	public static XmlOptions Bind(FileConfiguration configuration, IReadOnlyDictionary<string, string> explicitProperties, ICollection<CurbDiagnostic>? diagnostics = null) =>
		Bind(configuration.Properties, explicitProperties, XmlOptions.ProfileFor(configuration.FileName), diagnostics);

	/// <param name="properties">Every key that applies to the file, from any section.</param>
	/// <param name="explicitProperties">The keys that apply from explicit sections alone.</param>
	/// <param name="profile">The kind of XML the file is.</param>
	/// <param name="diagnostics">Collects bad values.</param>
	public static XmlOptions Bind(
		IReadOnlyDictionary<string, string> properties,
		IReadOnlyDictionary<string, string> explicitProperties,
		XmlProfile profile,
		ICollection<CurbDiagnostic>? diagnostics = null)
	{
		var options = new XmlOptions
		{
			Profile = profile,
			Layout = LayoutOptionsBinder.Bind(explicitProperties, diagnostics, XmlOptions.DefaultLayout),
			FinalNewLineSet = explicitProperties.ContainsKey("insert_final_newline"),
			TrimTrailingWhitespaceSet = explicitProperties.ContainsKey("trim_trailing_whitespace"),
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
