using System.IO.Abstractions.TestingHelpers;
using System.Text;
using AwesomeAssertions;
using Nullean.Curb.Cli;

namespace Nullean.Curb.Tests.Cli;

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
	public async Task An_explicit_file_list_skips_files_that_are_not_cs()
	{
		// The MSBuild integration hands over @(Compile) unfiltered. A shared Directory.Build.props
		// reaches every project underneath it, so this list can contain an .fsproj's own .fs files.
		var fs = Repo();
		fs.AddFile($"{Root}/Mine.cs", new MockFileData("class Mine {    }"));
		fs.AddFile($"{Root}/Theirs.fs", new MockFileData("module Theirs"));

		var summary = FormattingRun.Execute(fs, Root, write: true, explicitFiles: [$"{Root}/Mine.cs", $"{Root}/Theirs.fs"]);

		summary.Files.Should().Be(1);
		summary.Unparsable.Should().Be(0, "the .fs file should never reach the parser");
		fs.File.ReadAllText($"{Root}/Theirs.fs").Should().Be("module Theirs", "it is not C# and must be left alone");
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

	[Test]
	public async Task A_write_failure_in_one_file_is_reported_rather_than_aborting_the_run()
	{
		// A read-only file's write throws inside the worker — standing in for any per-file fault,
		// printer bug included, that has nothing to do with the rest of the tree. Chosen because
		// MockFileSystem genuinely throws UnauthorizedAccessException for it, without a fake seam.
		var fs = Repo();
		fs.AddFile($"{Root}/A.cs", new MockFileData("class A {    }"));
		fs.AddFile($"{Root}/Broken.cs", new MockFileData("class Broken {    }"));
		fs.File.SetAttributes($"{Root}/Broken.cs", FileAttributes.ReadOnly);

		var summary = FormattingRun.Execute(fs, Root, write: true);

		summary.Failed.Should().Be(1, "the broken file is reported, not silently dropped");
		summary.Changed.Should().Be(1, "the other file in the same run still gets formatted");
		summary.ExitCode.Should().Be(3);
		fs.File.ReadAllText($"{Root}/A.cs").Should().Be("class A { }\n", "the rest of the tree was not aborted");
		fs.File.ReadAllText($"{Root}/Broken.cs").Should().Be(
			"class Broken {    }", "a file the write failed for is left exactly as it was found");
		await Task.CompletedTask;
	}
}
