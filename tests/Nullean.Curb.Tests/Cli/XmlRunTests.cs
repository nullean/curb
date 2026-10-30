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
		var options = XmlOptionsBinder.Bind(
			new Dictionary<string, string>
			{
				["curb_xml_format"] = "yes",
				["curb_xml_extensions"] = "xml, .md",
				["xml_max_blank_lines_between_tags"] = "-1",
				["xml_space_before_self_closing"] = "sometimes",
				["xml_attribute_indent"] = "tab",
			},
			XmlProfile.MsBuild,
			diagnostics);

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
}
