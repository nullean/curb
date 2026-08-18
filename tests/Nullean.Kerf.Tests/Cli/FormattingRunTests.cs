using System.IO.Abstractions.TestingHelpers;
using System.Text;
using AwesomeAssertions;
using Nullean.Kerf.Cli;

namespace Nullean.Kerf.Tests.Cli;

/// <summary>
/// The run's own contract, which nothing covered before: what it walks, what it writes, and what it
/// exits with. Everything CLI-shaped used to be asserted only by the F# build targets, out of process.
/// </summary>
public class FormattingRunTests
{
	private const string Root = "/repo";

	private static MockFileSystem Repo(string editorConfig = "root = true\n\n[*.cs]\nindent_style = space\nindent_size = 4\n")
	{
		var fs = new MockFileSystem();
		fs.AddFile($"{Root}/.editorconfig", new MockFileData(editorConfig));
		return fs;
	}

	[Test]
	public async Task A_tree_that_needs_nothing_exits_zero()
	{
		var fs = Repo();
		fs.AddFile($"{Root}/A.cs", new MockFileData("class A {    }"));
		FormattingRun.Execute(fs, Root, write: true);

		FormattingRun.Execute(fs, Root, write: false).ExitCode.Should().Be(0);
		await Task.CompletedTask;
	}

	[Test]
	public async Task Check_exits_one_when_something_would_change_and_writes_nothing()
	{
		var fs = Repo();
		fs.AddFile($"{Root}/A.cs", new MockFileData("class A {    }"));

		var summary = FormattingRun.Execute(fs, Root, write: false);

		summary.ExitCode.Should().Be(1);
		summary.Changed.Should().Be(1);
		fs.File.ReadAllText($"{Root}/A.cs").Should().Be("class A {    }", "check never writes");
		await Task.CompletedTask;
	}

	[Test]
	public async Task A_file_that_does_not_parse_is_reported_rather_than_rewritten()
	{
		var fs = Repo();
		fs.AddFile($"{Root}/Broken.cs", new MockFileData("class Broken { void M( }"));

		var summary = FormattingRun.Execute(fs, Root, write: true);

		summary.Unparsable.Should().Be(1);
		summary.Changed.Should().Be(0);
		fs.File.ReadAllText($"{Root}/Broken.cs").Should().Be("class Broken { void M( }");
		await Task.CompletedTask;
	}

	[Test]
	public async Task Obj_and_bin_are_never_walked()
	{
		var fs = Repo();
		fs.AddFile($"{Root}/A.cs", new MockFileData("class A { }"));
		fs.AddFile($"{Root}/obj/Generated.cs", new MockFileData("class G {    }"));
		fs.AddFile($"{Root}/bin/Copied.cs", new MockFileData("class C {    }"));

		FormattingRun.Execute(fs, Root, write: true).Files.Should().Be(1);

		fs.File.ReadAllText($"{Root}/obj/Generated.cs").Should().Be("class G {    }");
		await Task.CompletedTask;
	}

	[Test]
	public async Task An_explicit_file_list_is_taken_over_the_directory()
	{
		// What the build integration passes: a project's compile set is not the C# files under its folder.
		var fs = Repo();
		fs.AddFile($"{Root}/Mine.cs", new MockFileData("class Mine {    }"));
		fs.AddFile($"{Root}/Theirs.cs", new MockFileData("class Theirs {    }"));

		var summary = FormattingRun.Execute(fs, Root, write: true, explicitFiles: [$"{Root}/Mine.cs"]);

		summary.Files.Should().Be(1);
		fs.File.ReadAllText($"{Root}/Theirs.cs").Should().Be("class Theirs {    }", "it was not in the list");
		await Task.CompletedTask;
	}

	[Test]
	public async Task A_byte_order_mark_survives_a_format_that_was_told_to_keep_it()
	{
		var fs = Repo("root = true\n\n[*.cs]\nindent_style = space\ncharset = utf-8-bom\n");
		byte[] withMark = [0xEF, 0xBB, 0xBF, .. Encoding.UTF8.GetBytes("class A {    }")];
		fs.AddFile($"{Root}/A.cs", new MockFileData(withMark));

		FormattingRun.Execute(fs, Root, write: true);

		var bytes = fs.File.ReadAllBytes($"{Root}/A.cs");
		bytes.Take(3).Should().Equal(0xEF, 0xBB, 0xBF);
		await Task.CompletedTask;
	}
}
