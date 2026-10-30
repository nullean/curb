namespace Nullean.Curb.Xml;

/// <summary>Which kind of XML a file is, chosen by its name. Each kind brings its own defaults.</summary>
public enum XmlProfile : byte
{
	/// <summary>Any other XML: <c>nuget.config</c>, or an extension named in <c>curb_xml_extensions</c>.</summary>
	Generic,

	/// <summary>An MSBuild project, props or targets file.</summary>
	/// <remarks>
	/// MSBuild reads a property's value, and an item's metadata, from the element's inner XML,
	/// whitespace included. So their content is left exactly as written, even where it is only
	/// elements.
	/// </remarks>
	MsBuild,

	/// <summary>A <c>.slnx</c> solution file, in the shape Visual Studio writes it: two spaces and <c> /&gt;</c>.</summary>
	Slnx,
}

/// <summary><c>xml_space_before_self_closing</c>.</summary>
public enum SelfClosingSpace : byte
{
	/// <summary>Not set: keep a space before <c>/&gt;</c> where the author wrote one, and none where they did not.</summary>
	Preserve,

	/// <summary><c>true</c>: always <c>&lt;a /&gt;</c>.</summary>
	Always,

	/// <summary><c>false</c>: always <c>&lt;a/&gt;</c>.</summary>
	Never,
}

/// <summary><c>xml_attribute_indent</c>: where an attribute on a line of its own starts.</summary>
public enum AttributeIndent : byte
{
	/// <summary>
	/// Not set: an attribute keeps the offset from its element's line the author gave it, moving only
	/// as far as the element itself moved.
	/// </summary>
	/// <remarks>
	/// Most hand-written MSBuild aligns attributes under the first one, and a value that spans lines
	/// (a <c>Condition</c>, typically) has its continuation lines aligned to match. Its continuation
	/// lines are content and cannot move, so re-indenting the attributes around them by a level would
	/// leave them stranded.
	/// </remarks>
	AsWritten,

	/// <summary>One indent level in from the element.</summary>
	SingleIndent,

	/// <summary>Two indent levels in from the element.</summary>
	DoubleIndent,

	/// <summary>Under the element's first attribute, when that one is on the element's line.</summary>
	AlignByFirstAttribute,
}

/// <summary>The settings an XML file is formatted with.</summary>
/// <remarks>
/// <see cref="Layout"/> carries the EditorConfig core keys. Two of them mean something slightly
/// different here than for C#, because XML keeps the author's layout wherever it can: an unset
/// <c>end_of_line</c> keeps the line ending the file already uses, and an unset
/// <c>insert_final_newline</c> keeps whether it ends with one (<see cref="FinalNewLineSet"/>).
/// </remarks>
public readonly record struct XmlOptions
{
	/// <summary>The defaults XML starts from before <c>.editorconfig</c> is read: two-space indents.</summary>
	public static LayoutOptions DefaultLayout { get; } = new() { IndentSize = 2, TabWidth = 2 };

	public XmlOptions() { }

	/// <summary>The core keys.</summary>
	public LayoutOptions Layout { get; init; } = DefaultLayout;

	/// <summary>Which kind of XML the file is.</summary>
	public XmlProfile Profile { get; init; }

	/// <summary>
	/// <c>curb_xml_format</c>. On by default; <c>false</c> stops a file being read at all.
	/// </summary>
	public bool Enabled { get; init; } = true;

	/// <summary>
	/// <c>curb_xml_extensions</c>: further extensions, each with its dot, formatted as generic XML.
	/// </summary>
	public string[] Extensions { get; init; } = [];

	/// <summary><c>xml_max_blank_lines_between_tags</c>: the most blank lines kept between two nodes.</summary>
	public int MaxBlankLines { get; init; } = 1;

	/// <summary><c>xml_space_before_self_closing</c> as written, before the profile's default applies.</summary>
	public SelfClosingSpace SpaceBeforeSelfClosingOption { get; init; } = SelfClosingSpace.Preserve;

	/// <summary>What the printer does before <c>/&gt;</c>: the key when set, else the profile's default.</summary>
	/// <remarks>
	/// Only solution files default to a space, because Visual Studio writes them that way and would put
	/// it back. A project file keeps whatever its author wrote: XML formatting is on by default, and a
	/// space added to every <c>&lt;PackageReference … /&gt;</c> in a repository is churn nobody asked for.
	/// </remarks>
	public SelfClosingSpace SpaceBeforeSelfClosing =>
		SpaceBeforeSelfClosingOption != SelfClosingSpace.Preserve || Profile != XmlProfile.Slnx
			? SpaceBeforeSelfClosingOption
			: SelfClosingSpace.Always;

	/// <summary><c>xml_attribute_indent</c>.</summary>
	public AttributeIndent AttributeIndent { get; init; }

	/// <summary>True when <c>insert_final_newline</c> was written down; otherwise the file keeps what it had.</summary>
	public bool FinalNewLineSet { get; init; }

	/// <summary>The configuration asked for the file not to be formatted at all: <c>generated_code = true</c>.</summary>
	public bool Excluded { get; init; }

	/// <summary>The profile a file name selects.</summary>
	/// <remarks>Case-insensitive, like the extension match that brought the file to XML in the first place.</remarks>
	public static XmlProfile ProfileFor(ReadOnlySpan<char> fileName)
	{
		if (fileName.EndsWith(".slnx", StringComparison.OrdinalIgnoreCase))
			return XmlProfile.Slnx;

		foreach (var extension in MsBuildExtensions)
		{
			if (fileName.EndsWith(extension, StringComparison.OrdinalIgnoreCase))
				return XmlProfile.MsBuild;
		}

		return XmlProfile.Generic;
	}

	/// <summary>
	/// The extensions <c>curb_xml_extensions</c> may name. Files with them are found by the walk but
	/// only formatted when a repository opts them in.
	/// </summary>
	/// <remarks>
	/// A closed list because the walk has to know a file might be XML before it resolves the file's
	/// configuration, and resolving it for every file in a repository to find out would cost more than
	/// formatting the XML does.
	/// </remarks>
	public static readonly string[] OptInExtensions =
		[".xml", ".config", ".nuspec", ".resx", ".runsettings", ".ruleset", ".xsd", ".xsl", ".xslt", ".manifest", ".vsixmanifest", ".xaml", ".axaml", ".wxs", ".wxi"];

	/// <summary>The extensions that are MSBuild files.</summary>
	public static readonly string[] MsBuildExtensions = [".csproj", ".fsproj", ".vbproj", ".proj", ".props", ".targets"];
}
