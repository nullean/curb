using System.Diagnostics;
using System.Globalization;
using System.IO.Abstractions;
using Nullean.Kerf.Cleanup;
using Nullean.Kerf.EditorConfig;

namespace Nullean.Kerf.Cli;

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
	private const string LogName = "kerf.sarif";

	/// <param name="fileSystem">Abstracted so a run can be driven entirely in memory by a test.</param>
	/// <param name="target">Where to look for logs when <paramref name="logs"/> is empty.</param>
	/// <param name="write">Apply the fixes, rather than only reporting what would change.</param>
	/// <param name="logs">Explicit log paths, from <c>--diagnostics</c>.</param>
	/// <param name="explicitFiles">Restrict to these files, from <c>--files</c>. Empty means every file the logs mention.</param>
	public static int Execute(
		IFileSystem fileSystem,
		string target,
		bool write,
		string[]? logs = null,
		string[]? explicitFiles = null)
	{
		logs = logs is { Length: > 0 } ? logs : Discover(fileSystem, target);

		if (logs.Length == 0)
		{
			Console.Error.WriteLine(
				$"No diagnostics log found. Build first — the {LogName} files the compiler writes are what "
				+ "cleanup reads — or point at one with --diagnostics <path>.");
			return 3;
		}

		var stopwatch = Stopwatch.StartNew();

		var byFile = new Dictionary<string, List<CleanupDiagnostic>>(StringComparer.Ordinal);
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
		}

		var wanted = explicitFiles is { Length: > 0 }
			? new HashSet<string>(explicitFiles.Select(fileSystem.Path.GetFullPath), StringComparer.Ordinal)
			: null;

		var work = byFile
			.Where(pair => wanted is null || wanted.Contains(pair.Key))
			.Select(pair => (Path: pair.Key, Diagnostics: (IReadOnlyList<CleanupDiagnostic>)pair.Value))
			.ToArray();

		var editorConfig = new KerfEditorConfig(fileSystem);

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

				var source = fileSystem.File.ReadAllText(path);

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
				// opinion, and leaving that to the next build would mean IDE0055 reported against Kerf's
				// own output — which is the one thing the formatter's placement exists to prevent.
				var formatted = worker.Formatter.Format(result.Text, options[index], produceText: true, verifyRoundTrip: true);
				var output = formatted.Success && formatted.Text is not null ? formatted.Text : result.Text;

				fileSystem.File.WriteAllText(path, output);
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

		if (failed > 0)
			return 3;

		return !write && changed > 0 ? 1 : 0;
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
