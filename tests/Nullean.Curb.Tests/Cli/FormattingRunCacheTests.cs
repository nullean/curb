using System.IO.Abstractions;
using System.IO.Abstractions.TestingHelpers;
using AwesomeAssertions;
using Nullean.Curb.Cli;

namespace Nullean.Curb.Tests.Cli;

/// <summary>A whole run against the cache: what it skips, and — the part that matters — what it will not.</summary>
public class FormattingRunCacheTests
{
	private const string Root = "/repo";
	private const string CachePath = "/repo/obj/curb.cache";

	private const string EditorConfig = """
		root = true

		[*.cs]
		indent_style = space
		indent_size = 4
		""";

	private static MockFileSystem Repo(params (string Name, string Source)[] files)
	{
		var fs = new MockFileSystem();
		fs.AddFile($"{Root}/.editorconfig", new MockFileData(EditorConfig));
		foreach (var (name, source) in files)
			fs.AddFile($"{Root}/{name}", new MockFileData(source));
		return fs;
	}

	private static FormattingRunSummary Check(IFileSystem fs, string? cache = CachePath) =>
		FormattingRun.Execute(fs, Root, write: false, cachePath: cache);

	private static FormattingRunSummary Format(IFileSystem fs, string? cache = CachePath) =>
		FormattingRun.Execute(fs, Root, write: true, cachePath: cache);

	/// <summary>Formats until nothing moves, so a test can start from files Curb agrees with.</summary>
	private static MockFileSystem Settled(params (string Name, string Source)[] files)
	{
		var fs = Repo(files);
		FormattingRun.Execute(fs, Root, write: true);
		return fs;
	}

	[Test]
	public async Task A_second_run_over_an_untouched_tree_does_no_work()
	{
		var fs = Settled(("A.cs", "class A { }"), ("B.cs", "class B { }"));

		Check(fs).Cached.Should().Be(0, "the first run has nothing to go on");
		var second = Check(fs);

		second.Cached.Should().Be(2);
		second.Changed.Should().Be(0);
		second.ExitCode.Should().Be(0);
		await Task.CompletedTask;
	}

	[Test]
	public async Task Editing_one_file_costs_one_file()
	{
		// The whole point of the feature: MSBuild's incrementality re-runs the target over the entire
		// compile set when a single file moves, and this is what makes that re-run cheap.
		var fs = Settled(("A.cs", "class A { }"), ("B.cs", "class B { }"), ("C.cs", "class C { }"));
		Check(fs);

		fs.File.WriteAllText($"{Root}/B.cs", "class B {    }");
		var summary = Check(fs);

		summary.Cached.Should().Be(2);
		summary.Files.Should().Be(3);
		await Task.CompletedTask;
	}

	[Test]
	public async Task Touching_a_file_without_changing_it_is_still_a_hit()
	{
		// Keyed on content, not on a timestamp. Checking out a branch rewrites every mtime in the tree
		// while leaving most bytes alone, and a cache that believed mtimes would throw itself away.
		var fs = Settled(("A.cs", "class A { }"));
		Check(fs);

		fs.File.SetLastWriteTimeUtc($"{Root}/A.cs", DateTime.UtcNow.AddHours(1));

		Check(fs).Cached.Should().Be(1);
		await Task.CompletedTask;
	}

	[Test]
	public async Task Changing_the_editorconfig_invalidates_every_file_it_governs()
	{
		var fs = Settled(("A.cs", "class A { }"), ("B.cs", "class B { }"));
		Check(fs);

		fs.File.WriteAllText($"{Root}/.editorconfig", EditorConfig + "\nindent_style = tab\n");

		Check(fs).Cached.Should().Be(0);
		await Task.CompletedTask;
	}

	[Test]
	public async Task Format_does_not_take_its_own_output_on_trust()
	{
		// The rule the whole design turns on. Curb is idempotent, so recording what it just wrote would
		// hit almost always — and would make a printer that stopped being idempotent invisible, because
		// the second run that today reports the file changing again would report it cached.
		var fs = Repo(("A.cs", "class A {    }"));

		var formatted = Format(fs);
		formatted.Changed.Should().Be(1);
		formatted.Cached.Should().Be(0);

		var after = Check(fs);
		after.Changed.Should().Be(0, "the file is formatted now");
		after.Cached.Should().Be(0, "but nothing has yet watched the formatter leave it alone");

		Check(fs).Cached.Should().Be(1, "now something has");
		await Task.CompletedTask;
	}

	[Test]
	public async Task A_file_that_would_change_is_never_recorded()
	{
		var fs = Repo(("A.cs", "class A {    }"));

		Check(fs).Changed.Should().Be(1);
		var second = Check(fs);

		second.Changed.Should().Be(1, "it is still unformatted and must still be reported");
		second.Cached.Should().Be(0);
		await Task.CompletedTask;
	}

	[Test]
	public async Task A_cache_written_by_check_is_honoured_by_format_and_the_other_way_round()
	{
		// An entry says a fixed point was observed, which is not a statement about which verb observed it.
		var fs = Settled(("A.cs", "class A { }"));

		Check(fs);
		Format(fs).Cached.Should().Be(1);

		var other = Settled(("B.cs", "class B { }"));
		Format(other);
		Check(other).Cached.Should().Be(1);
		await Task.CompletedTask;
	}

	[Test]
	public async Task A_file_that_does_not_parse_is_never_recorded()
	{
		var fs = Repo(("Broken.cs", "class Broken { void M( }"));

		Check(fs).Unparsable.Should().Be(1);
		var second = Check(fs);

		second.Unparsable.Should().Be(1, "a broken file must be reported on every run, not once");
		second.Cached.Should().Be(0);
		await Task.CompletedTask;
	}

	[Test]
	public async Task Files_curb_was_told_to_leave_alone_stay_skipped_rather_than_becoming_cached()
	{
		// The skipped count exists so a file that quietly stopped being formatted is visible. Folding it
		// into the cache count would hide exactly that.
		var fs = Settled(("A.cs", "class A { }"), ("Generated.cs", "// <auto-generated/>\nclass G {    }"));

		Check(fs);
		var second = Check(fs);

		second.Skipped.Should().Be(1);
		second.Cached.Should().Be(1, "only the file that was actually formatted");
		await Task.CompletedTask;
	}

	[Test]
	public async Task A_run_that_fails_its_check_still_writes_the_cache()
	{
		// The case the feature earns the most in. Curb_Check=true does not stamp on failure, so the
		// target repeats on every build until someone formats the file; without this those repeats would
		// re-parse the whole project each time.
		var fs = Settled(("A.cs", "class A { }"));
		Check(fs);
		fs.File.WriteAllText($"{Root}/B.cs", "class B {    }");

		var summary = Check(fs);

		summary.ExitCode.Should().Be(1);
		summary.Cached.Should().Be(1);
		Check(fs).Cached.Should().Be(1, "A.cs is still cached even though the run before it failed");
		await Task.CompletedTask;
	}

	[Test]
	public async Task Without_the_flag_there_is_no_cache_and_no_file()
	{
		var fs = Settled(("A.cs", "class A { }"));

		Check(fs, cache: null);
		Check(fs, cache: null).Cached.Should().Be(0);

		fs.File.Exists(CachePath).Should().BeFalse("nothing may be written to a path nobody named");
		await Task.CompletedTask;
	}

	[Test]
	public async Task Coverage_reporting_turns_the_cache_off()
	{
		// A histogram of syntax kinds assembled from whichever files happened to miss is a wrong answer
		// presented as a right one, so the two do not combine.
		var fs = Settled(("A.cs", "class A { }"));

		FormattingRun.Execute(fs, Root, write: false, coverageReport: true, cachePath: CachePath);

		fs.File.Exists(CachePath).Should().BeFalse();
		await Task.CompletedTask;
	}

	[Test]
	public async Task An_unwritable_cache_path_does_not_change_the_verdict()
	{
		// A cache can only ever skip work that was provably unnecessary. Failing a build over one would
		// trade the single thing it must not cost for the one thing it was never worth.
		var fs = Settled(("A.cs", "class A { }"));
		fs.AddDirectory(CachePath);

		var summary = Check(fs);

		summary.ExitCode.Should().Be(0);
		summary.Cached.Should().Be(0);
		await Task.CompletedTask;
	}
}
