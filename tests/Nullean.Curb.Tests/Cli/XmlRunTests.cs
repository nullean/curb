using System.IO.Abstractions.TestingHelpers;
using System.Text;
using AwesomeAssertions;
using Nullean.Curb.Cli;
using Nullean.Curb.Cli.Languages;
using Nullean.Curb.EditorConfig;
using Nullean.Curb.Options;
using Nullean.Curb.Xml;

namespace Nullean.Curb.Tests.Cli;

/// <summary>
/// XML through the whole run: which files it claims, what a repository can switch off, and that the
/// MSBuild integration never reaches a project file.
/// </summary>
public class XmlRunTests
{
	private const string Root = "/repo";
	private const string Unformatted = "<Project>\n<PropertyGroup />\n</Project>\n";
	private const string Formatted = "<Project>\n  <PropertyGroup />\n</Project>\n";

	private static MockFileSystem Repo(string editorConfig = "root = true\n")
	{
		var fs = new MockFileSystem();
		fs.AddFile($"{Root}/.editorconfig", new MockFileData(editorConfig));
		fs.AddFile($"{Root}/A.cs", new MockFileData("class A { }\n"));
		return fs;
	}

	[Test]
	public async Task Project_and_solution_files_are_formatted_by_default()
	{
		var fs = Repo();
		fs.AddFile($"{Root}/A.csproj", new MockFileData(Unformatted));
		fs.AddFile($"{Root}/Directory.Packages.props", new MockFileData(Unformatted));
		fs.AddFile($"{Root}/build/x.targets", new MockFileData(Unformatted));
		fs.AddFile($"{Root}/nuget.config", new MockFileData("<configuration>\n<a/>\n</configuration>\n"));
		fs.AddFile($"{Root}/A.slnx", new MockFileData("<Solution>\n<Project Path=\"A.csproj\"/>\n</Solution>\n"));

		var summary = FormattingRun.Execute(fs, Root, write: true);

		summary.Files.Should().Be(6);
		summary.Changed.Should().Be(5);
		fs.File.ReadAllText($"{Root}/A.csproj").Should().Be(Formatted);
		fs.File.ReadAllText($"{Root}/build/x.targets").Should().Be(Formatted);
		fs.File.ReadAllText($"{Root}/nuget.config").Should().Be("<configuration>\n  <a/>\n</configuration>\n");
		fs.File.ReadAllText($"{Root}/A.slnx").Should().Be("<Solution>\n  <Project Path=\"A.csproj\" />\n</Solution>\n");
		await Task.CompletedTask;
	}

	[Test]
	public async Task Curb_xml_format_false_leaves_files_unread_and_uncounted()
	{
		var fs = Repo("root = true\n\n[*.{csproj,props}]\ncurb_xml_format = false\n");
		fs.AddFile($"{Root}/A.csproj", new MockFileData(Unformatted));
		fs.AddFile($"{Root}/A.props", new MockFileData(Unformatted));
		fs.AddFile($"{Root}/A.targets", new MockFileData(Unformatted));

		var summary = FormattingRun.Execute(fs, Root, write: true);

		summary.Files.Should().Be(2, "the C# file and the one XML file the section did not cover");
		summary.Skipped.Should().Be(0, "a declined file is not a skipped one");
		fs.File.ReadAllText($"{Root}/A.csproj").Should().Be(Unformatted);
		fs.File.ReadAllText($"{Root}/A.targets").Should().Be(Formatted);
		await Task.CompletedTask;
	}

	[Test]
	public async Task Plain_xml_is_formatted_only_when_its_extension_is_named()
	{
		var fs = Repo();
		fs.AddFile($"{Root}/a.xml", new MockFileData(Unformatted));
		fs.AddFile($"{Root}/sub/b.xml", new MockFileData(Unformatted));
		fs.AddFile($"{Root}/sub/.editorconfig", new MockFileData("[*.xml]\ncurb_xml_extensions = xml\n"));
		fs.AddFile($"{Root}/app.config", new MockFileData(Unformatted));

		FormattingRun.Execute(fs, Root, write: true).Files.Should().Be(2);

		fs.File.ReadAllText($"{Root}/a.xml").Should().Be(Unformatted);
		fs.File.ReadAllText($"{Root}/app.config").Should().Be(Unformatted, "only nuget.config is claimed by name");
		fs.File.ReadAllText($"{Root}/sub/b.xml").Should().Be(Formatted);
		await Task.CompletedTask;
	}

	[Test]
	public async Task The_msbuild_integration_never_formats_a_project_file()
	{
		var fs = Repo();
		fs.AddFile($"{Root}/A.csproj", new MockFileData(Unformatted));
		fs.AddFile($"{Root}/list.txt", new MockFileData($"{Root}/A.cs\n{Root}/A.csproj\n"));

		var summary = FormattingRun.Execute(fs, Root, write: true, explicitFiles: [$"{Root}/A.cs", $"{Root}/A.csproj"], languages: LanguageRegistry.CSharpOnly);

		summary.Files.Should().Be(1);
		fs.File.ReadAllText($"{Root}/A.csproj").Should().Be(Unformatted);
		await Task.CompletedTask;
	}

	[Test]
	public async Task Malformed_xml_is_reported_and_left_alone()
	{
		var fs = Repo();
		fs.AddFile($"{Root}/A.csproj", new MockFileData("<Project>\n<A></B>\n</Project>\n"));

		var summary = FormattingRun.Execute(fs, Root, write: true);

		summary.Unparsable.Should().Be(1);
		summary.ExitCode.Should().Be(0);
		fs.File.ReadAllText($"{Root}/A.csproj").Should().Be("<Project>\n<A></B>\n</Project>\n");
		await Task.CompletedTask;
	}

	[Test]
	public async Task A_byte_order_mark_is_kept_and_build_output_folders_are_not_walked()
	{
		var fs = Repo();
		fs.AddFile($"{Root}/A.csproj", new MockFileData([0xEF, 0xBB, 0xBF, .. Encoding.UTF8.GetBytes(Unformatted)]));
		fs.AddFile($"{Root}/obj/A.csproj.nuget.g.props", new MockFileData(Unformatted));
		fs.AddFile($"{Root}/bin/x.props", new MockFileData(Unformatted));
		fs.AddFile($"{Root}/.git/x.props", new MockFileData(Unformatted));

		FormattingRun.Execute(fs, Root, write: true).Files.Should().Be(2);

		fs.File.ReadAllBytes($"{Root}/A.csproj").Should().Equal([0xEF, 0xBB, 0xBF, .. Encoding.UTF8.GetBytes(Formatted)]);
		fs.File.ReadAllText($"{Root}/obj/A.csproj.nuget.g.props").Should().Be(Unformatted);
		await Task.CompletedTask;
	}

	[Test]
	public async Task Bad_values_are_diagnosed_and_keep_their_defaults()
	{
		var diagnostics = new List<CurbDiagnostic>();
		var properties = new Dictionary<string, string>
		{
			["curb_xml_format"] = "yes",
			["curb_xml_extensions"] = "xml, .md",
			["xml_max_blank_lines_between_tags"] = "-1",
			["xml_space_before_self_closing"] = "sometimes",
			["xml_attribute_indent"] = "tab",
		};
		var options = XmlOptionsBinder.Bind(properties, new Dictionary<string, string>(), XmlProfile.MsBuild, diagnostics);

		options.Enabled.Should().BeTrue();
		options.Extensions.Should().Equal(".xml");
		options.MaxBlankLines.Should().Be(1);
		options.SpaceBeforeSelfClosingOption.Should().Be(SelfClosingSpace.Preserve);
		options.AttributeIndent.Should().Be(AttributeIndent.AsWritten);
		options.Layout.IndentSize.Should().Be(2, "XML indents by two unless told otherwise");
		diagnostics.Select(d => d.Id).Should().Equal("CURB1001", "CURB1001", "CURB1001", "CURB1001", "CURB1001");
		diagnostics[1].Message.Should().Contain("'.md'");
		await Task.CompletedTask;
	}

	[Test]
	[Arguments("*", false)]
	[Arguments("**", false)]
	[Arguments("**/*", false)]
	[Arguments("*.*", false)]
	[Arguments("**.*", false)]
	[Arguments("src/**", false)]
	[Arguments("*.csproj", true)]
	[Arguments("*.{csproj,props}", true)]
	[Arguments("*.xml", true)]
	[Arguments("Directory.Packages.props", true)]
	[Arguments("src/**/*.props", true)]
	public async Task A_section_is_explicit_when_its_glob_names_an_extension_or_a_file(string glob, bool isExplicit)
	{
		CurbEditorConfig.IsExplicit(glob).Should().Be(isExplicit);
		await Task.CompletedTask;
	}

	/// <summary>
	/// A repository's [*] section is written for its code. XML reads the core keys only from sections
	/// that name its files, and the curb_xml_* and xml_* keys from anywhere.
	/// </summary>
	[Test]
	public async Task Core_keys_from_star_sections_do_not_reach_xml()
	{
		var fs = new MockFileSystem();
		fs.AddFile($"{Root}/.editorconfig", new MockFileData(
			"root = true\n[*]\nindent_size = 4\nindent_style = tab\nmax_line_length = 20\nend_of_line = crlf\ninsert_final_newline = true\n"
			+ "trim_trailing_whitespace = true\ncharset = utf-8-bom\nxml_max_blank_lines_between_tags = 0\n[*.*]\nindent_size = 8\n[**]\ntab_width = 3\n"));
		fs.AddFile($"{Root}/A.csproj", new MockFileData(""));

		var options = XmlOptionsBinder.Bind(new CurbEditorConfig(fs), $"{Root}/A.csproj");

		options.Layout.Should().Be(XmlOptions.DefaultLayout);
		options.FinalNewLineSet.Should().BeFalse();
		options.TrimTrailingWhitespaceSet.Should().BeFalse();
		options.MaxBlankLines.Should().Be(0, "an xml_* key says whose it is, so [*] is fine for it");
		await Task.CompletedTask;
	}

	[Test]
	public async Task Explicit_sections_override_in_the_parsers_own_order()
	{
		var fs = new MockFileSystem();
		fs.AddFile($"{Root}/.editorconfig", new MockFileData("root = true\n[*.csproj]\nindent_size = 4\nmax_line_length = 100\n[*]\nindent_size = 3\n"));
		fs.AddFile($"{Root}/sub/.editorconfig", new MockFileData("[*]\nindent_size = 5\n[{A,B}.csproj]\nmax_line_length = 80\n"));
		fs.AddFile($"{Root}/sub/deeper/.editorconfig", new MockFileData("[*.csproj]\nindent_size = 8\n"));
		fs.AddFile($"{Root}/sub/deeper/A.csproj", new MockFileData(""));
		fs.AddFile($"{Root}/sub/A.csproj", new MockFileData(""));
		var editorConfig = new CurbEditorConfig(fs);

		var deeper = XmlOptionsBinder.Bind(editorConfig, $"{Root}/sub/deeper/A.csproj").Layout;
		var sub = XmlOptionsBinder.Bind(editorConfig, $"{Root}/sub/A.csproj").Layout;

		deeper.IndentSize.Should().Be(8);
		deeper.MaxLineLength.Should().Be(80, "a glob without a slash matches at any depth below its file");
		sub.IndentSize.Should().Be(4, "[*] in sub/ is not explicit, so the root's [*.csproj] still applies");
		sub.MaxLineLength.Should().Be(80);
		await Task.CompletedTask;
	}

	[Test]
	public async Task A_star_section_cannot_reindent_or_reflow_a_project_file()
	{
		var fs = Repo("root = true\n[*]\nindent_size = 4\nmax_line_length = 20\n");
		fs.AddFile($"{Root}/A.csproj", new MockFileData("<Project>\n<PropertyGroup Condition=\"'$(A)' == 'b'\" Label=\"x\" />\n</Project>\n"));

		FormattingRun.Execute(fs, Root, write: true);

		fs.File.ReadAllText($"{Root}/A.csproj").Should().Be("<Project>\n  <PropertyGroup Condition=\"'$(A)' == 'b'\" Label=\"x\" />\n</Project>\n");
		await Task.CompletedTask;
	}

	[Test]
	public async Task Moving_a_key_into_an_explicit_section_invalidates_the_cache()
	{
		var fs = Repo("root = true\n[*]\nindent_size = 4\n");
		fs.AddFile($"{Root}/A.csproj", new MockFileData(Formatted));
		var cache = $"{Root}/curb.cache";

		FormattingRun.Execute(fs, Root, write: false, cachePath: cache).Cached.Should().Be(0);
		FormattingRun.Execute(fs, Root, write: false, cachePath: cache).Cached.Should().Be(2, "nothing changed");

		// The same merged settings for both files; only the XML options move.
		fs.File.WriteAllText($"{Root}/.editorconfig", "root = true\n[*]\nindent_size = 4\n[*.csproj]\nindent_size = 4\n");
		var summary = FormattingRun.Execute(fs, Root, write: false, cachePath: cache);

		summary.Cached.Should().Be(1, "the merged settings are the same, but the XML options are not");
		summary.Changed.Should().Be(1);
		await Task.CompletedTask;
	}

	[Test]
	public async Task A_project_file_that_is_not_utf8_is_left_byte_for_byte()
	{
		var fs = Repo();
		byte[] latin1 = [.. Encoding.ASCII.GetBytes("<?xml version=\"1.0\" encoding=\"windows-1252\"?>\n<Project>\n<PropertyGroup>\n<Copyright>"), 0xA9, .. Encoding.ASCII.GetBytes(" Ma"), 0xEF, .. Encoding.ASCII.GetBytes("s</Copyright>\n</PropertyGroup>\n</Project>\n")];
		fs.AddFile($"{Root}/A.csproj", new MockFileData(latin1));
		byte[] ascii = Encoding.ASCII.GetBytes("<?xml version=\"1.0\" encoding=\"windows-1252\"?>\n<Project>\n<PropertyGroup />\n</Project>\n");
		fs.AddFile($"{Root}/B.csproj", new MockFileData(ascii));

		var summary = FormattingRun.Execute(fs, Root, write: true);

		summary.Unparsable.Should().Be(2);
		fs.File.ReadAllBytes($"{Root}/A.csproj").Should().Equal(latin1);
		fs.File.ReadAllBytes($"{Root}/B.csproj").Should().Equal(ascii, "valid UTF-8 bytes do not make a windows-1252 declaration true");
		await Task.CompletedTask;
	}

	/// <summary>
	/// The explicit-section resolver matches globs itself, so it is held to the parser: over every
	/// section, it has to reproduce what the parser resolves, apart from the tab width the parser
	/// derives from indent_size.
	/// </summary>
	[Test]
	public async Task Matching_every_section_reproduces_the_parser()
	{
		var fs = new MockFileSystem();
		fs.AddFile($"{Root}/.editorconfig", new MockFileData(
			"root = true\n[*]\nindent_size = 4\n[*.{csproj,props}]\nindent_size = 2\nx = root\n[src/**]\ny = src\n[**/Directory.*.props]\nz = dir\n[*.cs]\nw = cs\n"));
		fs.AddFile($"{Root}/src/.editorconfig", new MockFileData("[*.csproj]\nx = src\n[{A,B}.props]\nv = ab\n[*]\ny = star\n"));
		var editorConfig = new CurbEditorConfig(fs);

		foreach (var file in new[] { "A.csproj", "src/A.props", "src/deep/B.props", "src/Directory.Build.props", "src/x/A.cs", "nuget.config" })
		{
			var path = $"{Root}/{file}";
			var expected = editorConfig.For(path).Properties.Where(p => p.Key != "tab_width").OrderBy(p => p.Key, StringComparer.Ordinal);
			editorConfig.Matching(path, explicitOnly: false).OrderBy(p => p.Key, StringComparer.Ordinal).Should().Equal(expected, file);
		}

		await Task.CompletedTask;
	}
}
