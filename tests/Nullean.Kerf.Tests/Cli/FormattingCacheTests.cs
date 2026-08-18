using System.IO.Abstractions.TestingHelpers;
using AwesomeAssertions;
using Nullean.Kerf.Cli;

namespace Nullean.Kerf.Tests.Cli;

/// <summary>The store on its own: what it keeps, what it throws away, and what it refuses to throw.</summary>
public class FormattingCacheTests
{
	private const string CachePath = "/repo/obj/kerf.cache";
	private static readonly DateTimeOffset Now = new(2026, 8, 18, 12, 0, 0, TimeSpan.Zero);
	private static readonly UInt128 Content = 12345;
	private static readonly UInt128 Options = 67890;

	private static MockFileSystem Repo()
	{
		var fs = new MockFileSystem();
		fs.AddDirectory("/repo/obj");
		return fs;
	}

	private static MockFileSystem Saved(DateTimeOffset at)
	{
		var fs = Repo();
		var cache = FormattingCache.Load(fs, CachePath, at);
		cache.Record("/repo/A.cs", Content, Options);
		cache.Save();
		return fs;
	}

	[Test]
	public async Task Round_trips_what_a_run_recorded()
	{
		var fs = Saved(Now);

		var reloaded = FormattingCache.Load(fs, CachePath, Now);

		reloaded.Count.Should().Be(1);
		reloaded.IsFixedPoint("/repo/A.cs", Content, Options).Should().BeTrue();
		await Task.CompletedTask;
	}

	[Test]
	public async Task Different_content_or_options_is_a_miss()
	{
		var cache = FormattingCache.Load(Saved(Now), CachePath, Now);

		cache.IsFixedPoint("/repo/A.cs", Content + 1, Options).Should().BeFalse("the file's bytes moved");
		cache.IsFixedPoint("/repo/A.cs", Content, Options + 1).Should().BeFalse("its resolved options moved");
		cache.IsFixedPoint("/repo/B.cs", Content, Options).Should().BeFalse("nothing was recorded for that path");
		await Task.CompletedTask;
	}

	[Test]
	public async Task A_cache_written_by_another_version_of_kerf_is_discarded()
	{
		// Not migrated and not partially honoured. An entry records a verdict only that formatter could
		// have reached, so a formatter that prints differently must not inherit any of them.
		var fs = Saved(Now);
		var lines = fs.File.ReadAllLines(CachePath);
		lines[0] = "kerf-cache 1 0.0.1-someone-elses-build";
		fs.File.WriteAllLines(CachePath, lines);

		FormattingCache.Load(fs, CachePath, Now).Count.Should().Be(0);
		await Task.CompletedTask;
	}

	[Test]
	public async Task A_cache_written_in_another_line_format_is_discarded()
	{
		var fs = Saved(Now);
		var lines = fs.File.ReadAllLines(CachePath);
		lines[0] = lines[0].Replace("kerf-cache 1 ", "kerf-cache 2 ", StringComparison.Ordinal);
		fs.File.WriteAllLines(CachePath, lines);

		FormattingCache.Load(fs, CachePath, Now).Count.Should().Be(0);
		await Task.CompletedTask;
	}

	[Test]
	public async Task Rubbish_in_the_file_is_not_an_error()
	{
		// A build artefact gets truncated by a killed process, half-written by a full disk, and opened in
		// an editor by someone curious. None of that may fail a build, because the run can always just do
		// the work.
		foreach (var contents in new[] { "", "\0\0\0\0", "not a header at all", "kerf-cache" })
		{
			var fs = Repo();
			fs.AddFile(CachePath, new MockFileData(contents));

			var load = () => FormattingCache.Load(fs, CachePath, Now);

			load.Should().NotThrow($"'{contents}' must degrade to no cache");
			load().Count.Should().Be(0);
		}

		await Task.CompletedTask;
	}

	[Test]
	public async Task A_line_that_does_not_parse_costs_only_that_line()
	{
		var fs = Saved(Now);
		var lines = fs.File.ReadAllLines(CachePath);
		fs.File.WriteAllLines(CachePath, [lines[0], "half a line", "\tnot\thex\t/repo/B.cs", lines[1]]);

		var cache = FormattingCache.Load(fs, CachePath, Now);

		cache.Count.Should().Be(1, "the two unreadable lines are dropped, the good one is not");
		cache.IsFixedPoint("/repo/A.cs", Content, Options).Should().BeTrue();
		await Task.CompletedTask;
	}

	[Test]
	public async Task A_missing_cache_and_a_directory_in_its_place_are_both_fine()
	{
		FormattingCache.Load(Repo(), CachePath, Now).Count.Should().Be(0);

		var fs = Repo();
		fs.AddDirectory(CachePath);
		FormattingCache.Load(fs, CachePath, Now).Count.Should().Be(0);
		await Task.CompletedTask;
	}

	[Test]
	public async Task Entries_stop_being_honoured_after_a_week()
	{
		var fs = Saved(Now);

		FormattingCache.Load(fs, CachePath, Now.AddDays(6)).Count.Should().Be(1);
		FormattingCache.Load(fs, CachePath, Now.AddDays(8)).Count.Should().Be(0, "nothing has seen it in a week");
		await Task.CompletedTask;
	}

	[Test]
	public async Task Being_used_keeps_an_entry_alive()
	{
		// Otherwise a file nobody ever edits would be re-parsed once a week purely for having been stable.
		var fs = Saved(Now);

		var cache = FormattingCache.Load(fs, CachePath, Now.AddDays(6));
		cache.IsFixedPoint("/repo/A.cs", Content, Options).Should().BeTrue();
		cache.Save();

		FormattingCache.Load(fs, CachePath, Now.AddDays(12)).Count
			.Should().Be(1, "it was last seen on day six, not day zero");
		await Task.CompletedTask;
	}

	[Test]
	public async Task Entries_this_run_never_looked_at_are_carried_forward()
	{
		// The pre-commit case: a hook that only ever hands over the staged files must not throw away
		// everything it did not mention.
		var fs = Saved(Now);

		var cache = FormattingCache.Load(fs, CachePath, Now);
		cache.Record("/repo/B.cs", Content, Options);
		cache.Save();

		FormattingCache.Load(fs, CachePath, Now).Count.Should().Be(2);
		await Task.CompletedTask;
	}

	[Test]
	public async Task Saving_creates_the_directory_and_leaves_no_temporary_behind()
	{
		var fs = new MockFileSystem();

		var cache = FormattingCache.Load(fs, "/repo/nested/deeper/kerf.cache", Now);
		cache.Record("/repo/A.cs", Content, Options);
		cache.Save();

		fs.File.Exists("/repo/nested/deeper/kerf.cache").Should().BeTrue();
		fs.File.Exists("/repo/nested/deeper/kerf.cache.tmp").Should().BeFalse();
		await Task.CompletedTask;
	}

	[Test]
	public async Task The_same_entries_always_render_the_same_bytes()
	{
		// Sorted rather than in whatever order the dictionary held them, so the file diffs like anything
		// else and two runs over an unchanged tree do not look like they did different work.
		var first = Repo();
		var second = Repo();

		foreach (var fs in new[] { first, second })
		{
			var cache = FormattingCache.Load(fs, CachePath, Now);
			foreach (var path in new[] { "/repo/C.cs", "/repo/A.cs", "/repo/B.cs" })
				cache.Record(path, Content, Options);
			cache.Save();
		}

		first.File.ReadAllText(CachePath).Should().Be(second.File.ReadAllText(CachePath));
		await Task.CompletedTask;
	}
}
