using System.IO.Abstractions.TestingHelpers;
using System.Text;
using AwesomeAssertions;
using Nullean.Curb.Cli;
using Nullean.Curb.Cli.Languages;

namespace Nullean.Curb.Tests.Cli;

/// <summary>
/// Razor through the whole run: off unless a section naming Razor files turns it on, and then only the
/// C# in its blocks changes.
/// </summary>
public class RazorRunTests
{
	private const string Root = "/repo";
	private const string Unformatted = "<p>  x  </p>\n@code {\nint x=1;\n}\n";
	private const string Formatted = "<p>  x  </p>\n@code {\n    int x = 1;\n}\n";
	private const string OptIn = "root = true\n\n[*.{razor,cshtml}]\ncurb_razor_format_code_blocks = true\n";

	/// <summary>Bytes that are not UTF-8: a file that was read would be reported as unparsable.</summary>
	private static readonly byte[] NotUtf8 = [.. Encoding.UTF8.GetBytes("@code {\nstring s = \"caf"), 0xE9, .. Encoding.UTF8.GetBytes("\";\n}\n")];

	private static MockFileSystem Repo(string editorConfig)
	{
		var fs = new MockFileSystem();
		fs.AddFile($"{Root}/.editorconfig", new MockFileData(editorConfig));
		fs.AddFile($"{Root}/A.cs", new MockFileData("class A { }\n"));
		return fs;
	}

	[Test]
	public async Task Without_the_key_razor_files_are_not_read_or_counted()
	{
		var fs = Repo("root = true\n");
		fs.AddFile($"{Root}/A.razor", new MockFileData(Unformatted));
		fs.AddFile($"{Root}/B.razor", new MockFileData(NotUtf8));
		fs.AddFile($"{Root}/Views/C.cshtml", new MockFileData(Unformatted));

		var summary = FormattingRun.Execute(fs, Root, write: true);

		summary.Files.Should().Be(1, "only the C# file");
		summary.Unparsable.Should().Be(0, "a file that is not UTF-8 would have been reported had it been read");
		fs.File.ReadAllText($"{Root}/A.razor").Should().Be(Unformatted);
		fs.File.ReadAllText($"{Root}/Views/C.cshtml").Should().Be(Unformatted);
		await Task.CompletedTask;
	}

	[Test]
	public async Task The_key_in_a_razor_section_formats_razor_and_cshtml_files()
	{
		var fs = Repo(OptIn);
		fs.AddFile($"{Root}/A.razor", new MockFileData(Unformatted));
		fs.AddFile($"{Root}/Views/C.cshtml", new MockFileData(Unformatted.Replace("@code", "@functions", StringComparison.Ordinal)));

		var summary = FormattingRun.Execute(fs, Root, write: true);

		summary.Files.Should().Be(3);
		summary.Changed.Should().Be(2);
		fs.File.ReadAllText($"{Root}/A.razor").Should().Be(Formatted);
		fs.File.ReadAllText($"{Root}/Views/C.cshtml").Should().Be(Formatted.Replace("@code", "@functions", StringComparison.Ordinal));
		await Task.CompletedTask;
	}

	[Test]
	[Arguments("[*]")]
	[Arguments("[**]")]
	[Arguments("[*.*]")]
	public async Task The_key_in_a_catch_all_section_does_not_turn_razor_on(string section)
	{
		var fs = Repo($"root = true\n\n{section}\ncurb_razor_format_code_blocks = true\n");
		fs.AddFile($"{Root}/A.razor", new MockFileData(Unformatted));

		FormattingRun.Execute(fs, Root, write: true).Files.Should().Be(1);

		fs.File.ReadAllText($"{Root}/A.razor").Should().Be(Unformatted);
		await Task.CompletedTask;
	}

	[Test]
	public async Task The_key_reaches_files_in_subfolders_and_only_the_files_its_section_names()
	{
		var fs = Repo("root = true\n");
		fs.AddFile($"{Root}/src/.editorconfig", new MockFileData("[*.razor]\ncurb_razor_format_code_blocks = true\n"));
		fs.AddFile($"{Root}/src/Pages/A.razor", new MockFileData(Unformatted));
		fs.AddFile($"{Root}/src/Pages/B.cshtml", new MockFileData(Unformatted.Replace("@code", "@functions", StringComparison.Ordinal)));
		fs.AddFile($"{Root}/other/C.razor", new MockFileData(Unformatted));

		FormattingRun.Execute(fs, Root, write: true).Files.Should().Be(2, "the C# file and A.razor");

		fs.File.ReadAllText($"{Root}/src/Pages/A.razor").Should().Be(Formatted);
		fs.File.ReadAllText($"{Root}/src/Pages/B.cshtml").Should().Contain("int x=1;");
		fs.File.ReadAllText($"{Root}/other/C.razor").Should().Be(Unformatted);
		await Task.CompletedTask;
	}

	[Test]
	public async Task A_code_block_in_a_view_is_markup_and_left_alone()
	{
		var fs = Repo(OptIn);
		fs.AddFile($"{Root}/Views/C.cshtml", new MockFileData(Unformatted));

		var summary = FormattingRun.Execute(fs, Root, write: true);

		summary.Changed.Should().Be(0, "@code is a directive in a component only");
		fs.File.ReadAllText($"{Root}/Views/C.cshtml").Should().Be(Unformatted);
		await Task.CompletedTask;
	}

	[Test]
	public async Task A_section_for_razor_alone_can_turn_it_off_again()
	{
		var fs = Repo(OptIn + "\n[Legacy.razor]\ncurb_razor_format_code_blocks = false\n");
		fs.AddFile($"{Root}/A.razor", new MockFileData(Unformatted));
		fs.AddFile($"{Root}/Legacy.razor", new MockFileData(Unformatted));

		FormattingRun.Execute(fs, Root, write: true).Files.Should().Be(2);

		fs.File.ReadAllText($"{Root}/A.razor").Should().Be(Formatted);
		fs.File.ReadAllText($"{Root}/Legacy.razor").Should().Be(Unformatted);
		await Task.CompletedTask;
	}

	[Test]
	public async Task Csharp_style_comes_from_a_section_shared_with_cs_files()
	{
		var fs = Repo(OptIn + "\n[*.{cs,razor}]\nindent_style = tab\n");
		fs.AddFile($"{Root}/A.razor", new MockFileData(Unformatted));

		FormattingRun.Execute(fs, Root, write: true);

		fs.File.ReadAllText($"{Root}/A.razor").Should().Be("<p>  x  </p>\n@code {\n\tint x = 1;\n}\n");
		await Task.CompletedTask;
	}

	[Test]
	public async Task A_refused_block_is_reported_and_the_rest_is_formatted()
	{
		var fs = Repo(OptIn);
		const string source = "@code {\nRenderFragment   F => @<p/>;\n}\n@code {\nint x=1;\n}\n";
		fs.AddFile($"{Root}/A.razor", new MockFileData(source));

		var check = FormattingRun.Execute(fs, Root, write: false);
		check.Declined.Should().Be(1);
		check.ExitCode.Should().Be(1, "the second block would change");

		var summary = FormattingRun.Execute(fs, Root, write: true);
		summary.Declined.Should().Be(1);
		summary.Failed.Should().Be(0);
		summary.ExitCode.Should().Be(0, "a declined block is not a failure");
		fs.File.ReadAllText($"{Root}/A.razor").Should().Be("@code {\nRenderFragment   F => @<p/>;\n}\n@code {\n    int x = 1;\n}\n");
		await Task.CompletedTask;
	}

	[Test]
	public async Task A_file_with_a_declined_block_is_never_cached()
	{
		var fs = Repo(OptIn);
		fs.AddFile($"{Root}/A.razor", new MockFileData("@code {\nRenderFragment   F => @<p/>;\n}\n"));
		fs.AddFile($"{Root}/B.razor", new MockFileData(Formatted));

		FormattingRun.Execute(fs, Root, write: false, cachePath: $"{Root}/cache");
		var second = FormattingRun.Execute(fs, Root, write: false, cachePath: $"{Root}/cache");

		second.Cached.Should().Be(2, "the C# file and B.razor");
		second.Declined.Should().Be(1, "A.razor is checked, and reported, again");
		await Task.CompletedTask;
	}

	[Test]
	public async Task A_byte_order_mark_is_kept_whatever_charset_says()
	{
		var fs = Repo(OptIn + "\n[*]\ncharset = utf-8\n");
		var withBom = new byte[] { 0xEF, 0xBB, 0xBF };
		fs.AddFile($"{Root}/A.razor", new MockFileData([.. withBom, .. Encoding.UTF8.GetBytes(Unformatted)]));
		fs.AddFile($"{Root}/B.razor", new MockFileData([.. withBom, .. Encoding.UTF8.GetBytes(Formatted)]));

		var summary = FormattingRun.Execute(fs, Root, write: true);

		summary.Changed.Should().Be(1, "B.razor's blocks are formatted, and its mark is not Curb's to remove");
		fs.File.ReadAllBytes($"{Root}/A.razor").Should().Equal([.. withBom, .. Encoding.UTF8.GetBytes(Formatted)]);
		fs.File.ReadAllBytes($"{Root}/B.razor").Should().Equal([.. withBom, .. Encoding.UTF8.GetBytes(Formatted)]);
		await Task.CompletedTask;
	}

	[Test]
	public async Task A_razor_file_that_is_not_utf8_is_reported_and_left_alone()
	{
		var fs = Repo(OptIn);
		fs.AddFile($"{Root}/A.razor", new MockFileData(NotUtf8));

		var summary = FormattingRun.Execute(fs, Root, write: true);

		summary.Unparsable.Should().Be(1);
		fs.File.ReadAllBytes($"{Root}/A.razor").Should().Equal(NotUtf8);
		await Task.CompletedTask;
	}

	[Test]
	public async Task Generated_code_is_skipped()
	{
		var fs = Repo(OptIn + "\n[Gen.razor]\ngenerated_code = true\n");
		fs.AddFile($"{Root}/Gen.razor", new MockFileData(Unformatted));

		FormattingRun.Execute(fs, Root, write: true).Skipped.Should().Be(1);

		fs.File.ReadAllText($"{Root}/Gen.razor").Should().Be(Unformatted);
		await Task.CompletedTask;
	}

	[Test]
	public async Task Language_razor_formats_razor_alone_and_still_needs_the_key()
	{
		var fs = Repo(OptIn);
		fs.AddFile($"{Root}/A.razor", new MockFileData(Unformatted));
		fs.AddFile($"{Root}/A.csproj", new MockFileData("<Project>\n<PropertyGroup />\n</Project>\n"));

		var summary = FormattingRun.Execute(fs, Root, write: true, languages: LanguageRegistry.RazorOnly);

		summary.Files.Should().Be(1);
		fs.File.ReadAllText($"{Root}/A.razor").Should().Be(Formatted);
		fs.File.ReadAllText($"{Root}/A.cs").Should().Be("class A { }\n");

		var off = Repo("root = true\n");
		off.AddFile($"{Root}/A.razor", new MockFileData(Unformatted));
		FormattingRun.Execute(off, Root, write: true, languages: LanguageRegistry.RazorOnly).Files.Should().Be(0);
		off.File.ReadAllText($"{Root}/A.razor").Should().Be(Unformatted);
		await Task.CompletedTask;
	}

	[Test]
	public async Task A_razor_file_named_without_the_key_is_skipped_with_a_line_saying_why()
	{
		var fs = Repo("root = true\n");
		fs.AddFile($"{Root}/A.razor", new MockFileData(Unformatted));
		fs.AddFile($"{Root}/B.razor", new MockFileData(Unformatted));

		var named = FormattingRun.Execute(fs, $"{Root}/A.razor", write: false);
		named.Files.Should().Be(0);
		named.NamedDeclined.Should().Be(1);

		FormattingRun.Execute(fs, Root, write: false).NamedDeclined.Should().Be(0, "a walk's files are declined silently");

		var opted = Repo("root = true\n[*]\ncurb_razor_format_code_blocks = true\n");
		opted.AddFile($"{Root}/A.razor", new MockFileData(Unformatted));
		FormattingRun.Execute(opted, $"{Root}/A.razor", write: false).NamedDeclined.Should().Be(1, "the key in [*] passes the cheap check but not the explicit one");
		await Task.CompletedTask;
	}

	[Test]
	public async Task The_msbuild_integration_never_formats_a_razor_file()
	{
		var fs = Repo(OptIn);
		fs.AddFile($"{Root}/A.razor", new MockFileData(Unformatted));

		var summary = FormattingRun.Execute(
			fs, Root, write: true, explicitFiles: [$"{Root}/A.cs", $"{Root}/A.razor"], languages: LanguageRegistry.CSharpOnly);

		summary.Files.Should().Be(1);
		fs.File.ReadAllText($"{Root}/A.razor").Should().Be(Unformatted);
		await Task.CompletedTask;
	}
}
