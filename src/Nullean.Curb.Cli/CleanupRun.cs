using System.Diagnostics;
using System.Globalization;
using System.IO.Abstractions;
using System.Text;
using Nullean.Curb.Cleanup;
using Nullean.Curb.EditorConfig;
using Nullean.Curb.Options;

namespace Nullean.Curb.Cli;

/// <summary>Applies the code style fixes a build reported.</summary>
/// <remarks>
/// <para>
/// Shaped like <see cref="FormattingRun"/> deliberately: the same <c>IFileSystem</c>, the same
/// per-worker instance in a <c>Parallel.ForEach</c>, the same summary line. What differs is where the
/// work comes from — a log rather than a directory walk — because the compiler decided what needs doing
/// and this only carries it out.
/// </para>
/// <para>
/// Nothing here starts a build. The log has to already exist, which is what keeps a code clean tool from
/// owning compilation.
/// </para>
/// </remarks>
internal static class CleanupRun
{
	/// <summary>The name the MSBuild integration gives the compiler's error log.</summary>
	private const string LogName = "curb.sarif";

	/// <summary>UTF-8 without a byte-order mark, and without the exception-throwing default.</summary>
	private static readonly UTF8Encoding Utf8 = new(encoderShouldEmitUTF8Identifier: false);

	/// <summary>UTF-8 with a byte-order mark, for <c>charset = utf-8-bom</c>.</summary>
	private static readonly UTF8Encoding Utf8WithBom = new(encoderShouldEmitUTF8Identifier: true);

	/// <param name="fileSystem">Abstracted so a run can be driven entirely in memory by a test.</param>
	/// <param name="target">Where to look for logs when <paramref name="logs"/> is empty.</param>
	/// <param name="write">Apply the fixes, rather than only reporting what would change.</param>
	/// <param name="logs">Explicit log paths, from <c>--sarif-log</c>.</param>
	/// <param name="explicitFiles">Restrict to these files, from <c>--files</c>/<c>--msbuild-list-file</c>. Empty means every file the logs mention.</param>
	/// <param name="forward">Hand what Curb did not fix to <c>dotnet format</c>, from <c>--forward</c>.</param>
	public static int Execute(
		IFileSystem fileSystem,
		string target,
		bool write,
		string[]? logs = null,
		string[]? explicitFiles = null,
		bool forward = false)
	{
		logs = logs is { Length: > 0 } ? logs : Discover(fileSystem, target);

		if (logs.Length == 0)
		{
			Console.Error.WriteLine(
				$"No diagnostics log found. Build first — the {LogName} files the compiler writes are what "
				+ "cleanup reads — or point at one with --sarif-log <path>.");
			return 3;
		}

		var stopwatch = Stopwatch.StartNew();

		var byFile = new Dictionary<string, List<CleanupDiagnostic>>(StringComparer.Ordinal);
		var notOurs = new List<CleanupDiagnostic>();
		var newest = DateTime.MinValue;

		foreach (var log in logs)
		{
			if (!fileSystem.File.Exists(log))
			{
				Console.Error.WriteLine($"{log}: no such file");
				return 3;
			}

			if (!DiagnosticLog.TryRead(fileSystem.File.ReadAllBytes(log), out var all, out var failure))
			{
				Console.Error.WriteLine($"{log}: {failure}");
				return 3;
			}

			// The log's own timestamp is the freshness reference: a source file touched after the compiler
			// looked at it is described by nothing here.
			var written = fileSystem.FileInfo.New(log).LastWriteTimeUtc;
			if (written > newest)
				newest = written;

			foreach (var diagnostic in DiagnosticLog.Fixable(all))
			{
				if (!byFile.TryGetValue(diagnostic.FilePath, out var forFile))
					byFile[diagnostic.FilePath] = forFile = [];

				forFile.Add(diagnostic);
			}

			// Everything the log reported that is not Curb's to fix. Kept separately so `--forward` can
			// name the remainder exactly rather than making someone run the whole command over everything.
			foreach (var diagnostic in all)
			{
				if (!RuleCatalog.IsCleanupRule(diagnostic.RuleId))
					notOurs.Add(diagnostic);
			}
		}

		var wanted = explicitFiles is { Length: > 0 }
			? new HashSet<string>(explicitFiles.Select(fileSystem.Path.GetFullPath), StringComparer.Ordinal)
			: null;

		var work = byFile
			.Where(pair => wanted is null || wanted.Contains(pair.Key))
			.Select(pair => (Path: pair.Key, Diagnostics: (IReadOnlyList<CleanupDiagnostic>)pair.Value))
			.ToArray();

		var editorConfig = new CurbEditorConfig(fileSystem);

		// Resolved on one thread up front, for the reason FormattingRun records: the parser is shared and
		// is not thread-safe.
		var options = new FormatOptions[work.Length];
		for (var i = 0; i < work.Length; i++)
			options[i] = EditorConfigOptionsBinder.Bind(editorConfig.For(work[i].Path));

		var changed = 0;
		var applied = 0;
		var skipped = 0;
		var stale = 0;
		var failed = 0;
		var refused = 0;
		var messages = new System.Collections.Concurrent.ConcurrentBag<string>();
		var unfixed = new System.Collections.Concurrent.ConcurrentBag<CleanupDiagnostic>();

		Parallel.ForEach(
			Enumerable.Range(0, work.Length),
			new ParallelOptions { MaxDegreeOfParallelism = Environment.ProcessorCount },
			() => (Cleaner: new CSharpCleaner(), Formatter: new CSharpFormatter()),
			(index, _, worker) =>
			{
				var (path, diagnostics) = work[index];

				if (!fileSystem.File.Exists(path))
				{
					Interlocked.Increment(ref stale);
					return worker;
				}

				// Read bytes rather than text: ReadAllText silently swallows a byte-order mark and
				// WriteAllText silently writes none, so `charset` was unobservable at both ends —
				// the same bug FormattingRun.cs fixes for `curb format`.
				var bytes = fileSystem.File.ReadAllBytes(path);
				var hadBom = bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF;
				var source = Utf8.GetString(hadBom ? bytes.AsSpan(3) : bytes);

				// The file's own opt-outs, honoured exactly as the formatter honours them.
				if (options[index].Excluded || CSharpSource.HasGeneratedHeader(source))
				{
					Interlocked.Increment(ref skipped);
					return worker;
				}

				// The freshness gate. A span is an offset into the bytes the compiler read; applying it to
				// different bytes is how a tool corrupts source. Edited since the build, so it waits for
				// the next one — and the count is reported rather than passed over.
				if (fileSystem.FileInfo.New(path).LastWriteTimeUtc > newest)
				{
					Interlocked.Increment(ref stale);
					return worker;
				}

				var result = worker.Cleaner.Clean(source, diagnostics);
				Interlocked.Add(ref refused, result.Refusals.Count);

				foreach (var left in result.Unfixed)
					unfixed.Add(left);

				foreach (var refusal in result.Refusals)
					messages.Add($"{path}: {refusal}");

				switch (result.Status)
				{
					case CleanupStatus.SyntaxError:
					case CleanupStatus.VerificationFailed:
						Interlocked.Increment(ref failed);
						messages.Add($"{path}: {result.Message}");
						return worker;
				}

				if (!result.Changed || result.Text is null)
					return worker;

				Interlocked.Increment(ref changed);
				Interlocked.Add(ref applied, result.Applied);

				if (!write)
					return worker;

				// Formatted before it is written. A removed directive leaves the blank-line rules with an
				// opinion, and leaving that to the next build would mean IDE0055 reported against Curb's
				// own output — which is the one thing the formatter's placement exists to prevent.
				var formatted = worker.Formatter.Format(result.Text, options[index], produceText: true, verifyRoundTrip: true);
				var output = formatted.Success && formatted.Text is not null ? formatted.Text : result.Text;

				var wantsBom = options[index].Charset switch
				{
					Charset.Utf8Bom => true,
					Charset.Utf8 => false,
					Charset.Preserve => hadBom,
					_ => hadBom,
				};

				fileSystem.File.WriteAllText(path, output, wantsBom ? Utf8WithBom : Utf8);
				return worker;
			},
			worker => worker.Formatter.Dispose());

		stopwatch.Stop();

		foreach (var message in messages.Take(20))
			Console.Error.WriteLine(message);

		Console.WriteLine(string.Create(CultureInfo.InvariantCulture,
			$"{(write ? "Cleaned" : "Checked")} {work.Length} file(s) from {logs.Length} log(s) in {stopwatch.ElapsedMilliseconds}ms — "
			+ $"{applied} fix(es) in {changed} file(s){(write ? "" : " would change")}, "
			+ $"{refused} refused, {stale} stale, {skipped} skipped, {failed} failed"));

		if (stale > 0)
		{
			Console.WriteLine(
				$"  {stale} file(s) changed after the build, so nothing was applied to them. Build again to pick them up.");
		}

		var forwardFailed = false;
		if (forward)
		{
			forwardFailed = Forward(
				fileSystem.Path.GetFullPath(target), [.. notOurs, .. unfixed], stopwatch.ElapsedMilliseconds, write);
		}

		if (failed > 0 || forwardFailed)
			return 3;

		return !write && changed > 0 ? 1 : 0;
	}

	/// <summary>
	/// Hands the remainder to <c>dotnet format</c> and reports both timings.
	/// </summary>
	/// <remarks>
	/// <para>
	/// The timings are printed side by side because the difference is the point. Curb's own pass is a parse
	/// and a rewrite; <c>dotnet format</c> loads an MSBuild workspace, and that is seconds per solution
	/// whatever it is asked to fix. Measured on a 61-file project: 1.97 s narrowed to one file and one rule
	/// against 1.98 s over everything — so scoping buys no time at all, and printing the two numbers is the
	/// only way someone learns which half of their wait is which.
	/// </para>
	/// <para>
	/// One invocation for the whole target rather than one per project, since the workspace load is the
	/// cost and doing it repeatedly would multiply the only expensive part.
	/// </para>
	/// </remarks>
	/// <returns>True when a forwarded invocation failed.</returns>
	private static bool Forward(string target, IReadOnlyList<CleanupDiagnostic> remainder, long ourMilliseconds, bool write)
	{
		var plan = ForwardPlan.For(remainder, target);

		if (plan.Withheld.Count > 0)
		{
			Console.WriteLine($"  {plan.Withheld.Count} rule(s) not forwarded, because no tool should apply them unattended:");
			foreach (var rule in plan.Withheld)
				Console.WriteLine($"    {rule.Id}  {rule.Title} — {rule.Reason}");
		}

		if (plan.Quiet > 0)
		{
			Console.WriteLine($"  {plan.Quiet} diagnostic(s) not forwarded, reported below warning — "
				+ "your build did not show them, so nothing rewrites source on their account");
		}

		if (plan.Invocations.Count == 0)
			return false;

		var failed = false;
		var forwardedMilliseconds = 0L;

		foreach (var invocation in plan.Invocations)
		{
			// `--verify-no-changes` under --check, so the two halves agree about whether they are allowed to
			// write. A check that quietly rewrote the tree would be worse than no check.
			var arguments = write
				? invocation.Arguments
				: [.. invocation.Arguments, "--verify-no-changes"];

			var scope = invocation.Files.Count > 0
				? $"{invocation.Files.Count} file(s)"
				: "the whole project, since a reported file sits outside the working directory";

			Console.WriteLine($"  forwarding {invocation.RuleIds.Count} rule(s) in {scope} "
				+ $"to `dotnet format {invocation.Subcommand}`: {string.Join(' ', invocation.RuleIds)}");

			var start = Stopwatch.StartNew();
			var exitCode = Run(target, arguments);
			start.Stop();
			forwardedMilliseconds += start.ElapsedMilliseconds;

			// Exit 2 from `--verify-no-changes` is "there is something to fix", which is the answer, not a
			// failure. Anything else non-zero means the tool could not run.
			if (exitCode != 0 && !(!write && exitCode == 2))
			{
				Console.Error.WriteLine($"  `dotnet format {invocation.Subcommand}` exited {exitCode}");
				failed = true;
			}
		}

		var ratio = ourMilliseconds > 0
			? string.Create(CultureInfo.InvariantCulture, $" ({forwardedMilliseconds / (double)ourMilliseconds:F1}x)")
			: "";

		Console.WriteLine(string.Create(CultureInfo.InvariantCulture,
			$"  curb {ourMilliseconds}ms · dotnet format {forwardedMilliseconds}ms{ratio}"
			+ $" — the difference is the workspace load, which no amount of scoping avoids"));

		return failed;
	}

	/// <summary>
	/// Starts <c>dotnet format</c> and waits.
	/// </summary>
	/// <remarks>
	/// The one place Curb starts a process, and it only happens when <c>--forward</c> asked for it. Not
	/// routed through <c>IFileSystem</c> because it is not filesystem access; the part worth testing is
	/// which arguments get built, and <see cref="ForwardPlan"/> is pure so that it can be.
	/// </remarks>
	private static int Run(string target, IReadOnlyList<string> arguments)
	{
		var info = new ProcessStartInfo("dotnet") { UseShellExecute = false };
		foreach (var argument in arguments)
			info.ArgumentList.Add(argument);

		// `dotnet format` searches for a project or solution from its working directory, so pointing it at
		// the same place cleanup was pointed keeps the two looking at one thing.
		if (Directory.Exists(target))
			info.WorkingDirectory = target;

		using var process = Process.Start(info);
		if (process is null)
			return -1;

		process.WaitForExit();
		return process.ExitCode;
	}

	/// <summary>Finds the compiler's error logs under a directory. They live in <c>obj</c>, so nothing is excluded.</summary>
	private static string[] Discover(IFileSystem fileSystem, string target)
	{
		var root = fileSystem.Path.GetFullPath(target);

		if (fileSystem.File.Exists(root))
			return [root];

		return fileSystem.Directory.Exists(root)
			? [.. fileSystem.Directory.EnumerateFiles(root, LogName, SearchOption.AllDirectories)]
			: [];
	}
}
