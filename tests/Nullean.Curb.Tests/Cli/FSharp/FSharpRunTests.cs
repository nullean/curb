using System.IO.Abstractions.TestingHelpers;
using System.Text;
using AwesomeAssertions;
using Nullean.Curb.Cli;
using Nullean.Curb.Cli.Languages;
using Nullean.Curb.Cli.Languages.FSharp;

namespace Nullean.Curb.Tests.Cli.FSharp;

/// <summary>
/// F# through the whole run, against a fake Fantomas: how each of its answers lands on Curb's counters and
/// on disk, the re-check that stands between an answer and a write, the cache, and the fallback to the CLI.
/// </summary>
public class FSharpRunTests
{
	private const string Root = "/repo";
	private const string Home = "/home";
	private const string Dll = "/home/.nuget/packages/fantomas/8.1.0/tools/net10.0/any/fantomas.dll";
	private const string CachePath = "/repo/obj/curb.cache";

	private sealed class Repo
	{
		public MockFileSystem Fs { get; } = new();
		public FakeFantomas Fantomas { get; }
		public Dictionary<string, string> Variables { get; } = [];

		public Repo(bool manifest = true, bool restored = true)
		{
			Fantomas = new FakeFantomas(Fs);
			Fs.AddFile($"{Root}/.editorconfig", new MockFileData("root = true\n"));
			if (manifest)
				Fs.AddFile($"{Root}/.config/dotnet-tools.json", new MockFileData(FantomasToolTests.Manifest(isRoot: true)));
			if (restored)
			{
				Fs.AddFile($"{Home}/.dotnet/toolResolverCache/1/fantomas", new MockFileData(FantomasToolTests.ResolverCache("8.1.0", Dll)));
				Fs.AddFile(Dll, new MockFileData(""));
			}
		}

		public LanguageRegistry Languages() =>
			LanguageRegistry.Default(Fs, Fantomas, new ToolEnvironment(name => Variables.GetValueOrDefault(name), Home));

		public FormattingRunSummary Run(bool write, string? cache = null, string? unformattedList = null, CancellationToken cancellationToken = default) =>
			FormattingRun.Execute(Fs, Root, write, cachePath: cache, unformattedListPath: unformattedList, languages: Languages(), cancellationToken: cancellationToken);

		/// <summary>Runs, and hands back what the F# language said once for the run.</summary>
		public (FormattingRunSummary Summary, IReadOnlyCollection<string> Errors) RunReporting(bool write)
		{
			var languages = Languages();
			var summary = FormattingRun.Execute(Fs, Root, write, languages: languages);
			return (summary, ((IExternalLanguage)languages[2]).Errors);
		}

		public void Add(string relative, string content) => Fs.AddFile($"{Root}/{relative}", new MockFileData(content));

		public string Read(string relative) => Fs.File.ReadAllText($"{Root}/{relative}");
	}

	[Test]
	public async Task A_formatted_file_is_unchanged_and_starts_one_daemon()
	{
		var repo = new Repo();
		repo.Add("src/A.fs", "let x = 1\n");
		repo.Add("src/B.fs", "let y = 2\n");

		var summary = repo.Run(write: false);

		summary.Files.Should().Be(2);
		summary.Changed.Should().Be(0);
		summary.ExitCode.Should().Be(0);
		repo.Fantomas.DaemonsStarted.Should().Be(1, "one daemon per run, however many files");
		repo.Fantomas.CliRuns.Should().Be(0);
		repo.Fantomas.Requests.Count.Should().Be(2, "an unchanged file is not re-checked");
		var command = repo.Fantomas.Started.Single();
		command.Arguments.Should().Equal("exec", Dll, "daemon");
		command.WorkingDirectory.Should().Be(Root);
		await Task.CompletedTask;
	}

	[Test]
	public async Task A_file_needing_formatting_is_rechecked_then_written_by_curb()
	{
		var repo = new Repo();
		repo.Add("A.fs", "let  x =  1\n");

		var summary = repo.Run(write: true);

		summary.Changed.Should().Be(1);
		summary.ExitCode.Should().Be(0);
		repo.Read("A.fs").Should().Be("let x = 1\n");
		repo.Fantomas.Requests.Select(r => r.Source).Should().Equal(["let  x =  1\n", "let x = 1\n"],
			"the formatted text goes back to the daemon before it is believed");
		await Task.CompletedTask;
	}

	[Test]
	public async Task Check_reports_without_writing_and_lists_the_file()
	{
		var repo = new Repo();
		repo.Add("A.fs", "let  x = 1\n");

		var summary = repo.Run(write: false, unformattedList: $"{Root}/list");

		summary.Changed.Should().Be(1);
		summary.ExitCode.Should().Be(1);
		repo.Read("A.fs").Should().Be("let  x = 1\n");
		repo.Fs.File.ReadAllText($"{Root}/list").Trim().Should().Be($"{Root}/A.fs");
		await Task.CompletedTask;
	}

	[Test]
	public async Task An_error_is_unparsable_with_fantomas_reason()
	{
		var repo = new Repo();
		repo.Add("A.fs", "let BROKEN =\n");

		var summary = repo.Run(write: true);

		summary.Unparsable.Should().Be(1);
		summary.Failed.Should().Be(0);
		summary.ExitCode.Should().Be(0, "an unparsable file is reported, as it is for C#, not a failure of the run");
		repo.Read("A.fs").Should().Be("let BROKEN =\n");
		await Task.CompletedTask;
	}

	[Test]
	public async Task Output_that_does_not_parse_on_the_recheck_fails_and_leaves_the_file_alone()
	{
		var repo = new Repo();
		repo.Add("A.fs", "let INVALID = 1\n");

		var summary = repo.Run(write: true);

		summary.Failed.Should().Be(1);
		summary.Changed.Should().Be(0);
		summary.ExitCode.Should().Be(3);
		repo.Read("A.fs").Should().Be("let INVALID = 1\n");
		await Task.CompletedTask;
	}

	[Test]
	public async Task Unstable_output_fails_and_leaves_the_file_alone()
	{
		var repo = new Repo();
		repo.Add("A.fs", "let UNSTABLE = 1\n");

		var summary = repo.Run(write: true);

		summary.Failed.Should().Be(1);
		summary.ExitCode.Should().Be(3);
		repo.Read("A.fs").Should().Be("let UNSTABLE = 1\n");

		// Check fails it too, so the two commands never disagree about a file.
		repo.Run(write: false).Failed.Should().Be(1);
		await Task.CompletedTask;
	}

	[Test]
	public async Task An_ignored_file_is_skipped_and_cached_until_the_ignore_file_changes()
	{
		var repo = new Repo();
		repo.Add(".fantomasignore", "A.fs\n");
		repo.Add("A.fs", "let  x = 1\n");

		var first = repo.Run(write: true, cache: CachePath);
		first.Skipped.Should().Be(1);
		first.Changed.Should().Be(0);
		repo.Read("A.fs").Should().Be("let  x = 1\n");

		var second = repo.Run(write: true, cache: CachePath);
		second.Cached.Should().Be(1, "the ignore file's content is in the key, so the answer can be remembered");
		repo.Fantomas.DaemonsStarted.Should().Be(1, "a cached ignore costs no daemon");

		repo.Fs.File.WriteAllText($"{Root}/.fantomasignore", "Other.fs\n");
		var third = repo.Run(write: true, cache: CachePath);
		third.Cached.Should().Be(0, "a changed ignore file invalidates what it decided");
		third.Changed.Should().Be(1);
		repo.Read("A.fs").Should().Be("let x = 1\n");
		await Task.CompletedTask;
	}

	[Test]
	public async Task A_fully_cached_run_starts_no_process()
	{
		var repo = new Repo();
		repo.Add("A.fs", "let x = 1\n");
		repo.Add("B.fsi", "val y: int\n");
		repo.Add("C.cs", "class C { }\n");

		repo.Run(write: false, cache: CachePath).Cached.Should().Be(0);
		repo.Fantomas.Started.Clear();

		var cached = repo.Run(write: false, cache: CachePath);

		cached.Cached.Should().Be(3);
		repo.Fantomas.Started.Should().BeEmpty();
		await Task.CompletedTask;
	}

	[Test]
	public async Task A_manifest_version_bump_invalidates_only_fsharp()
	{
		var repo = new Repo();
		repo.Add("A.fs", "let x = 1\n");
		repo.Add("C.cs", "class C { }\n");
		repo.Run(write: false, cache: CachePath);

		repo.Fs.File.WriteAllText($"{Root}/.config/dotnet-tools.json", FantomasToolTests.Manifest("8.2.0", isRoot: true));
		repo.Fs.File.WriteAllText($"{Home}/.dotnet/toolResolverCache/1/fantomas", FantomasToolTests.ResolverCache("8.2.0", Dll));
		repo.Fantomas.Version = "8.2.0";

		var summary = repo.Run(write: false, cache: CachePath);

		summary.Cached.Should().Be(1, "the C# entry survives a Fantomas upgrade");
		repo.Fantomas.DaemonsStarted.Should().Be(2);
		await Task.CompletedTask;
	}

	[Test]
	public async Task Without_a_manifest_fsharp_is_declined_unread_and_nothing_starts()
	{
		var repo = new Repo(manifest: false);
		repo.Add("A.fs", "let  x = 1\n");
		repo.Add("C.cs", "class C { }\n");

		var summary = repo.Run(write: true);

		summary.Files.Should().Be(1, "only the C# file");
		summary.Skipped.Should().Be(0, "a declined file is not a skipped one");
		summary.ExitCode.Should().Be(0);
		repo.Read("A.fs").Should().Be("let  x = 1\n");
		repo.Fantomas.Started.Should().BeEmpty();
		await Task.CompletedTask;
	}

	[Test]
	public async Task A_manifest_that_is_not_restored_fails_the_run_without_starting_anything()
	{
		var repo = new Repo(restored: false);
		repo.Add("A.fs", "let x = 1\n");
		repo.Add("C.cs", "class C { }\n");

		var check = repo.Run(write: false);

		check.Failed.Should().Be(1);
		check.ExitCode.Should().Be(3, "a check that could not check is a failure, not a pass");
		repo.Fantomas.Started.Should().BeEmpty();
		await Task.CompletedTask;
	}

	[Test]
	public async Task A_daemon_of_another_version_fails_the_run()
	{
		var repo = new Repo();
		repo.Fantomas.Version = "8.0.0+abc";
		repo.Add("A.fs", "let  x = 1\n");

		var summary = repo.Run(write: true);

		summary.Failed.Should().Be(1);
		summary.ExitCode.Should().Be(3);
		repo.Read("A.fs").Should().Be("let  x = 1\n");
		repo.Fantomas.Requests.Should().BeEmpty("nothing is formatted by a Fantomas the manifest did not pin");
		await Task.CompletedTask;
	}

	[Test]
	public async Task A_daemon_that_cannot_start_falls_back_to_one_cli_check()
	{
		var repo = new Repo();
		repo.Fantomas.DaemonFailsToStart = true;
		repo.Add("A.fs", "let  x = 1\n");
		repo.Add("B.fs", "let y = 1\n");
		repo.Add("C.fs", "let BROKEN\n");
		repo.Add("D.fs", "let INVALID = 1\n");

		var summary = repo.Run(write: false);

		repo.Fantomas.CliRuns.Should().Be(1, "one batch process, never one per file");
		repo.Fantomas.Started.Last().Arguments.Should().Contain(["check", "--json"]);
		summary.Changed.Should().Be(1);
		summary.Unparsable.Should().Be(1);
		summary.Failed.Should().Be(1);
		await Task.CompletedTask;
	}

	[Test]
	public async Task A_daemon_that_exits_before_its_handshake_falls_back_and_curb_still_does_the_writing()
	{
		var repo = new Repo();
		repo.Fantomas.DaemonExitsBeforeHandshake = true;
		repo.Add(".editorconfig", "root = true\n\n[*.fs]\ncharset = utf-8-bom\n");
		repo.Add("A.fs", "let  x = 1\n");
		repo.Add("B.fs", "let y = 1\n");
		repo.Add("U.fs", "let UNSTABLE = 1\n");

		var summary = repo.Run(write: true);

		repo.Fantomas.DaemonsStarted.Should().Be(1);
		repo.Fantomas.CliRuns.Should().Be(2, "a formatting pass, and a check of what it wrote");
		summary.Changed.Should().Be(2, "A.fs is reformatted, and B.fs gains the byte-order mark its charset asks for");
		summary.Failed.Should().Be(1);

		var bytes = repo.Fs.File.ReadAllBytes($"{Root}/A.fs");
		bytes.Take(3).Should().Equal(0xEF, 0xBB, 0xBF);
		Encoding.UTF8.GetString(bytes.AsSpan(3)).Should().Be("let x = 1\n");
		repo.Read("U.fs").Should().Be("let UNSTABLE = 1\n", "the CLI's in-place write of an unstable file is undone");
		await Task.CompletedTask;
	}

	[Test]
	public async Task The_daemon_can_be_switched_off()
	{
		var repo = new Repo();
		repo.Variables[FantomasRunner.DaemonVariable] = "false";
		repo.Add("A.fs", "let x = 1\n");

		repo.Run(write: false).ExitCode.Should().Be(0);

		repo.Fantomas.DaemonsStarted.Should().Be(0);
		repo.Fantomas.CliRuns.Should().Be(1);
		await Task.CompletedTask;
	}

	[Test]
	public async Task Many_files_in_flight_each_get_their_own_answer()
	{
		var repo = new Repo();
		for (var i = 0; i < 200; i++)
			repo.Add($"src/F{i:000}.fs", i % 2 == 0 ? $"let  x{i} = {i}\n" : $"let x{i} = {i}\n");

		var summary = repo.Run(write: true);

		summary.Changed.Should().Be(100);
		summary.Failed.Should().Be(0);
		for (var i = 0; i < 200; i += 2)
			repo.Read($"src/F{i:000}.fs").Should().Be($"let x{i} = {i}\n");
		await Task.CompletedTask;
	}

	[Test]
	public async Task A_file_with_a_generated_header_is_skipped_before_fantomas_sees_it()
	{
		var repo = new Repo();
		repo.Add("AssemblyInfo.fs", "// <auto-generated>\n//     Generated.\n// </auto-generated>\nnamespace  X\n");

		var summary = repo.Run(write: true);

		summary.Skipped.Should().Be(1);
		repo.Fantomas.Started.Should().BeEmpty();
		await Task.CompletedTask;
	}

	[Test]
	public async Task Configuration_warnings_are_collected_once()
	{
		var repo = new Repo();
		repo.Fantomas.UnknownSettings = ["fsharp_made_up"];
		repo.Add("A.fs", "let x = 1\n");
		repo.Add("B.fs", "let y = 1\n");
		var languages = repo.Languages();

		FormattingRun.Execute(repo.Fs, Root, write: false, languages: languages);

		var fsharp = (IExternalLanguage)languages[2];
		fsharp.Warnings.Should().Equal("Fantomas does not know the setting 'fsharp_made_up'");
		await Task.CompletedTask;
	}

	[Test]
	public async Task The_msbuild_fsharp_registry_formats_only_fsharp()
	{
		var repo = new Repo();
		repo.Add("A.fs", "let  x = 1\n");
		repo.Add("C.cs", "class  C { }\n");

		var summary = FormattingRun.Execute(repo.Fs, Root, write: true,
			explicitFiles: [$"{Root}/A.fs", $"{Root}/C.cs"],
			languages: LanguageRegistry.FSharpOnly(repo.Fs, repo.Fantomas, new ToolEnvironment(_ => null, Home)));

		summary.Files.Should().Be(1);
		repo.Read("A.fs").Should().Be("let x = 1\n");
		repo.Read("C.cs").Should().Be("class  C { }\n");
		await Task.CompletedTask;
	}

	[Test]
	public async Task A_daemon_that_stops_answering_is_killed_and_nothing_is_written()
	{
		var repo = new Repo();
		repo.Fantomas.DaemonStalls = true;

		// Long enough that the handshake, which shares the base timeout, cannot run out on a loaded machine;
		// the request still stalls for ever, so it is the one that times out.
		repo.Variables[FantomasRunner.TimeoutVariable] = "2";
		repo.Add("A.fs", "let  x = 1\n");
		repo.Add("B.fs", "let y = 1\n");

		var (summary, errors) = repo.RunReporting(write: true);

		summary.Failed.Should().Be(2, "the whole group fails, including any file answered before the daemon stopped");
		summary.ExitCode.Should().Be(3);
		repo.Read("A.fs").Should().Be("let  x = 1\n");
		repo.Fantomas.DaemonsStarted.Should().Be(1);
		repo.Fantomas.CliRuns.Should().Be(0, "a daemon that stopped answering is not a reason to try the command line");

		// Killed outright, with its input still open: a daemon that stopped answering is not asked to stop.
		repo.Fantomas.Aborts.Should().Be(1);
		errors.Should().ContainSingle().Which.Should().Contain("did not answer").And.Contain(FantomasRunner.TimeoutVariable);
		await Task.CompletedTask;
	}

	[Test]
	public async Task Cancelling_the_run_kills_the_daemon_and_fails_its_files()
	{
		var repo = new Repo();
		repo.Fantomas.DaemonStalls = true;
		repo.Add("A.fs", "let  x = 1\n");
		using var cancellation = new CancellationTokenSource();

		// Cancelled only once the daemon holds the request, so there is a running process to stop.
		_ = repo.Fantomas.FirstRequest.ContinueWith(_ => cancellation.Cancel(), TaskScheduler.Default);
		var languages = repo.Languages();
		var summary = FormattingRun.Execute(repo.Fs, Root, write: true, languages: languages, cancellationToken: cancellation.Token);

		summary.Failed.Should().Be(1);
		summary.ExitCode.Should().Be(3);
		repo.Read("A.fs").Should().Be("let  x = 1\n");
		repo.Fantomas.Aborts.Should().Be(1);

		// Said as a cancellation, not as the request timeout running out.
		((IExternalLanguage)languages[2]).Errors.Should().ContainSingle().Which.Should().Contain("cancelled");
		await Task.CompletedTask;
	}

	[Test]
	public async Task A_run_cancelled_before_fantomas_starts_starts_nothing()
	{
		var repo = new Repo();
		repo.Add("A.fs", "let  x = 1\n");
		using var cancellation = new CancellationTokenSource();
		cancellation.Cancel();

		var summary = repo.Run(write: true, cancellationToken: cancellation.Token);

		summary.Failed.Should().Be(1);
		summary.ExitCode.Should().Be(3);
		repo.Read("A.fs").Should().Be("let  x = 1\n");
		repo.Fantomas.Started.Should().BeEmpty();
		await Task.CompletedTask;
	}

	[Test]
	public async Task A_command_line_that_never_finishes_is_killed_and_every_file_is_put_back()
	{
		var repo = new Repo();
		repo.Variables[FantomasRunner.DaemonVariable] = "false";
		repo.Variables[FantomasRunner.TimeoutVariable] = "0.2";
		repo.Fantomas.CliStalls = true;
		repo.Add("A.fs", "let  x = 1\n");

		var summary = repo.Run(write: true);

		summary.Failed.Should().Be(1);
		summary.ExitCode.Should().Be(3);
		repo.Read("A.fs").Should().Be("let  x = 1\n", "the in-place write the command line made before it hung is undone");
		repo.Fantomas.Kills.Should().Be(1);
		await Task.CompletedTask;
	}

	[Test]
	public async Task A_report_that_cannot_be_read_still_puts_every_file_back()
	{
		var repo = new Repo();
		repo.Variables[FantomasRunner.DaemonVariable] = "false";
		repo.Fantomas.CliPrintsGarbage = true;
		repo.Add(".editorconfig", "root = true\n\n[*.fs]\ncharset = utf-8-bom\n");
		repo.Add("A.fs", "let  x = 1\n");
		repo.Fs.AddFile($"{Root}/B.fs", new MockFileData([0xEF, 0xBB, 0xBF, .. Encoding.UTF8.GetBytes("let  y = 1\n")]));

		var (summary, errors) = repo.RunReporting(write: true);

		summary.Failed.Should().Be(2);
		summary.Changed.Should().Be(0);
		repo.Fs.File.ReadAllBytes($"{Root}/A.fs").Should().Equal(Encoding.UTF8.GetBytes("let  x = 1\n"));
		repo.Fs.File.ReadAllBytes($"{Root}/B.fs").Should().Equal([0xEF, 0xBB, 0xBF, .. Encoding.UTF8.GetBytes("let  y = 1\n")]);
		errors.Should().ContainSingle().Which.Should().Contain("did not report what it did");
		await Task.CompletedTask;
	}

	[Test]
	public async Task A_file_that_cannot_be_put_back_is_failed_and_reported()
	{
		var repo = new Repo();
		repo.Variables[FantomasRunner.DaemonVariable] = "false";
		repo.Fantomas.CliLeavesReadOnly = true;
		repo.Add("A.fs", "let  x = 1\n");

		var (summary, errors) = repo.RunReporting(write: true);

		summary.Changed.Should().Be(0, "a file Curb could not restore is not one Curb wrote");
		summary.Failed.Should().Be(1);
		errors.Should().ContainSingle().Which.Should().Contain("could not be put back");
		await Task.CompletedTask;
	}

	[Test]
	public async Task The_fallback_refuses_a_fantomas_without_check_json()
	{
		var repo = new Repo();
		repo.Fs.File.WriteAllText($"{Root}/.config/dotnet-tools.json", FantomasToolTests.Manifest("7.0.3", isRoot: true));
		repo.Fs.File.WriteAllText($"{Home}/.dotnet/toolResolverCache/1/fantomas", FantomasToolTests.ResolverCache("7.0.3", Dll));
		repo.Fantomas.DaemonFailsToStart = true;
		repo.Add("A.fs", "let  x = 1\n");

		var (summary, errors) = repo.RunReporting(write: true);

		summary.ExitCode.Should().Be(3);
		repo.Fantomas.CliRuns.Should().Be(0);
		repo.Fantomas.Started.Single().Arguments.Should().Contain("--daemon", "7.0.3 predates the daemon command");
		errors.Should().ContainSingle().Which.Should().Contain(FantomasRunner.MinimumCliVersion).And.Contain("7.0.3");
		await Task.CompletedTask;
	}

	[Test]
	public async Task A_malformed_manifest_fails_check_instead_of_passing_it()
	{
		var repo = new Repo();
		repo.Fs.File.WriteAllText($"{Root}/.config/dotnet-tools.json", "{ \"tools\": ");
		repo.Add("A.fs", "let  x = 1\n");
		repo.Add("C.cs", "class C { }\n");

		var (summary, errors) = repo.RunReporting(write: false);

		summary.ExitCode.Should().Be(3);
		summary.Failed.Should().Be(1);
		repo.Fantomas.Started.Should().BeEmpty();
		errors.Should().ContainSingle().Which.Should().Contain($"{Root}/.config/dotnet-tools.json").And.Contain("could not be read");
		await Task.CompletedTask;
	}

	[Test]
	public async Task One_broken_manifest_fails_only_its_own_files()
	{
		var repo = new Repo();
		repo.Add("good/A.fs", "let  x = 1\n");
		repo.Fs.AddFile($"{Root}/other/dotnet-tools.json", new MockFileData(FantomasToolTests.Manifest("9.9.9", isRoot: true)));
		repo.Add("other/B.fs", "let  y = 1\n");

		var (summary, errors) = repo.RunReporting(write: true);

		summary.Changed.Should().Be(1);
		summary.Failed.Should().Be(1);
		repo.Read("good/A.fs").Should().Be("let x = 1\n");
		repo.Read("other/B.fs").Should().Be("let  y = 1\n");
		errors.Should().ContainSingle().Which.Should().Contain("9.9.9").And.Contain("dotnet tool restore");
		await Task.CompletedTask;
	}

	[Test]
	public async Task An_answer_for_another_file_is_never_written()
	{
		var repo = new Repo();
		repo.Fantomas.DaemonAnswersFor = $"{Root}/Other.fs";
		repo.Add("A.fs", "let  x = 1\n");

		var summary = repo.Run(write: true);

		summary.Failed.Should().Be(1);
		repo.Read("A.fs").Should().Be("let  x = 1\n");
		await Task.CompletedTask;
	}

	[Test]
	public async Task An_external_language_that_throws_fails_every_file_it_was_handed()
	{
		var fs = new MockFileSystem();
		fs.AddFile($"{Root}/.editorconfig", new MockFileData("root = true\n"));
		fs.AddFile($"{Root}/A.boom", new MockFileData("a\n"));
		fs.AddFile($"{Root}/B.boom", new MockFileData("b\n"));
		fs.AddFile($"{Root}/C.cs", new MockFileData("class C { }\n"));

		var summary = FormattingRun.Execute(fs, Root, write: true, languages: new LanguageRegistry(
			Nullean.Curb.Cli.Languages.CSharp.CSharpLanguage.Instance, new Boom()));

		summary.Failed.Should().Be(2);
		summary.ExitCode.Should().Be(3);
		fs.File.ReadAllText($"{Root}/A.boom").Should().Be("a\n");
		await Task.CompletedTask;
	}

	/// <summary>An external language whose tool cannot run at all.</summary>
	private sealed class Boom : IExternalLanguage
	{
		public string Id => "boom";

		public string[] Extensions { get; } = [".boom"];

		public LanguageOptions BindOptions(global::EditorConfig.Core.FileConfiguration configuration, global::Nullean.Curb.EditorConfig.CurbEditorConfig editorConfig) => new Options();

		public bool SkipSource(string source) => false;

		public Task<FormatResult[]> FormatBatchAsync(IReadOnlyList<ExternalFile> files, bool produceText, CancellationToken cancellationToken) =>
			throw new InvalidOperationException("no tool");

		public IReadOnlyCollection<string> Warnings => [];

		public IReadOnlyCollection<string> Errors => [];

		private sealed class Options : LanguageOptions
		{
			public override LayoutOptions Layout => new();

			public override bool Excluded => false;
		}
	}
}
