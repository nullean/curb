using System.Diagnostics;
using System.Globalization;
using System.IO.Abstractions;
using System.Text;
using System.Threading.Channels;
using Nullean.Curb.Cli.Languages;
using Nullean.Curb.EditorConfig;

namespace Nullean.Curb.Cli;

/// <summary>Formats or checks a directory tree.</summary>
/// <remarks>
/// Files are processed in parallel with one <see cref="ILanguageWorker"/> per language per thread, since
/// a worker owns pooled buffers and is not thread-safe. Which language a file belongs to is the
/// <see cref="LanguageRegistry"/>'s call; everything else here is shared by every language.
/// Configuration is resolved through a single shared <see cref="CurbEditorConfig"/>, whose
/// per-directory caching means a repository resolves its <c>.editorconfig</c> chain once per folder
/// rather than once per file.
/// </remarks>
internal static class FormattingRun
{
	/// <summary>UTF-8 without a byte-order mark, and without the exception-throwing default.</summary>
	private static readonly UTF8Encoding Utf8 = new(encoderShouldEmitUTF8Identifier: false);

	/// <summary>
	/// The decoder for what is read: invalid bytes throw rather than become U+FFFD.
	/// </summary>
	/// <remarks>
	/// The lenient default turned a Latin-1 <c>"café"</c> into <c>"caf�"</c>, and the verifier, which
	/// compares decoded text, could not see it. A file that is not UTF-8 is reported and left as it was.
	/// </remarks>
	internal static readonly UTF8Encoding StrictUtf8 = new(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);

	/// <summary>UTF-8 with a byte-order mark, for <c>charset = utf-8-bom</c>.</summary>
	private static readonly UTF8Encoding Utf8WithBom = new(encoderShouldEmitUTF8Identifier: true);

	/// <summary>Renders the skipped count only when there is one, to keep the usual line unchanged.</summary>
	private static string SkippedText(int skipped) =>
		skipped == 0 ? "" : string.Create(CultureInfo.InvariantCulture, $"{skipped} skipped, ");

	/// <summary>
	/// Renders the cache hits beside the file count rather than among the verdicts, because every figure
	/// after it — coverage, megabytes of source, the allocation ratio — covers only the files that were
	/// actually printed. Absent when nothing was served from cache, so the usual line is unchanged.
	/// </summary>
	private static string CachedText(int cached) =>
		cached == 0 ? "" : string.Create(CultureInfo.InvariantCulture, $" ({cached} from cache)");

	/// <param name="fileSystem">Abstracted so a run can be driven entirely in memory by a test.</param>
	/// <param name="target">A file or directory to walk. Ignored when <paramref name="explicitFiles"/> is given.</param>
	/// <param name="write">Format in place, rather than only reporting what would change.</param>
	/// <param name="expandUnhandled">Benchmark-only cost model; see <c>PrintContext.ExpandUnhandled</c>.</param>
	/// <param name="verify">Re-parse output to prove the token stream is unchanged. On by default.</param>
	/// <param name="coverageReport">Report which syntax kinds are still emitted verbatim.</param>
	/// <param name="explicitFiles">
	/// The exact files to work on, instead of walking <paramref name="target"/>. What the MSBuild
	/// integration passes: a project's compile set is not the same thing as the C# files under its
	/// folder — it can exclude some, and link others in from outside — so formatting the directory
	/// would reach files belonging to another project, or to none.
	/// </param>
	/// <param name="cachePath">
	/// Where to remember which files were already formatted, so a run triggered by one changed file does
	/// not re-parse the rest. Absent means no cache: the caller names the path or there is not one.
	/// </param>
	/// <param name="unformattedListPath">
	/// Where to write the paths of files that would change, one per line, sorted. What the MSBuild
	/// integration reads back after a failed <c>check</c>, so it can attach the CURB0001 diagnostic to
	/// each file that actually needs reformatting instead of to the project. Ignored when <paramref
	/// name="write"/> is <see langword="true"/>: a format run leaves nothing unformatted to list.
	/// </param>
	/// <param name="languages">Which languages the run covers. <see cref="LanguageRegistry.Default"/> when absent.</param>
	public static FormattingRunSummary Execute(
		IFileSystem fileSystem,
		string target,
		bool write,
		bool expandUnhandled = false,
		bool? verify = null,
		bool coverageReport = false,
		string[]? explicitFiles = null,
		string? cachePath = null,
		string? unformattedListPath = null,
		LanguageRegistry? languages = null)
	{
		languages ??= LanguageRegistry.Default(fileSystem);

		// On by default for both commands: the printer tracks whether it actually put a token
		// boundary at risk, so on code that does not, the second parse never happens.
		var verifyRoundTrip = verify ?? true;

		// Both of these are diagnostic modes that would make a cache lie. --expand-unhandled changes what
		// the printer emits, so its verdicts are not the ones a real run would reach; --coverage counts
		// syntax kinds as they are printed, and a run that skipped nine hundred files would report a
		// histogram of the ninety that missed as though it were the repository's.
		if (cachePath is not null && (coverageReport || expandUnhandled))
		{
			Console.Error.WriteLine("--cache is ignored together with --coverage or --expand-unhandled");
			cachePath = null;
		}

		var cache = cachePath is null
			? null
			: FormattingCache.Load(fileSystem, fileSystem.Path.GetFullPath(cachePath), DateTimeOffset.UtcNow);

		string[] files;
		if (explicitFiles is not null)
			// The MSBuild integration hands over a project's whole compile set unfiltered — see this
			// parameter's own doc comment — and a shared Directory.Build.props reaches every project
			// that imports it, C# or not. @(Compile) for an .fsproj is real .fs paths, not an empty
			// group, so nothing upstream of here already excludes them.
			files = [.. explicitFiles.Where(path => IsFormattable(languages, path))];
		else
		{
			var root = fileSystem.Path.GetFullPath(target);
			files = fileSystem.Directory.Exists(root)
				? [.. Walk(fileSystem, root, languages).Where(path => IsFormattable(languages, path))]
				: [root];
		}

		var editorConfig = new CurbEditorConfig(fileSystem);

		// Resolved per file, not per directory. A section can discriminate on the file name —
		// `[*Tests.cs]`, `[Program.cs]`, and `generated_code = true` in particular — so reusing one
		// file's options across its directory applies or misses them by alphabetical accident.
		// CurbEditorConfig already caches the chain per directory, which is the part that costs;
		// what is left is a glob match per file, about 1 ms across the whole corpus.
		//
		// It is not thread-safe against the shared parser, so it happens up front on one thread.
		//
		// The options fingerprint has to be taken here for the same reason, and it is only taken at all
		// when there is a cache to key. The path is absolutised for the cache alone, so the messages and
		// the writes below still say whatever the caller asked for.
		var claimed = new List<(string Path, int Language, LanguageOptions Options, string CacheKey, UInt128 OptionsHash)>(files.Length);
		var declined = new Dictionary<(int Language, string Reason), int>();
		var fingerprinter = cache is null ? null : new OptionsFingerprinter();
		for (var i = 0; i < files.Length; i++)
		{
			var configuration = editorConfig.For(files[i]);

			// A file given as the target itself never went through the extension filter, and has always
			// been formatted as C# regardless; the first language keeps it that way.
			var language = Math.Max(0, languages.IndexOf(files[i]));
			var options = languages[language].BindOptions(configuration, editorConfig);

			// A file its language declines (XML the repository has not opted into, F# with no Fantomas) leaves
			// the run here, before it is read, hashed or counted. One the repository would want to hear about
			// is tallied, for a single line at the end.
			if (!options.Claimed)
			{
				if (options.DeclinedReason is { } reason)
				{
					declined.TryGetValue((language, reason), out var count);
					declined[(language, reason)] = count + 1;
				}
				continue;
			}

			claimed.Add((
				files[i],
				language,
				options,
				cache is null ? "" : fileSystem.Path.GetFullPath(files[i]),
				fingerprinter is null ? default : OptionsHash(fingerprinter, configuration, options)));
		}

		var work = claimed.ToArray();
		if (work.Length != files.Length)
			files = [.. work.Select(item => item.Path)];

		var changed = 0;
		var cached = 0;
		var skipped = 0;
		var failed = 0;
		var unparsable = 0;
		long printedTokens = 0;
		long totalTokens = 0;
		var reparsed = 0;

		// Files printed by a language with no printer coverage and no second parse (XML), kept out of
		// both figures.
		var uncovered = 0;
		var unhandled = new Dictionary<(int Language, int Kind), int>();

		// Per language, for the breakdown printed once more than one language ran.
		var tallies = new LanguageTally[languages.Count];
		var messages = new System.Collections.Concurrent.ConcurrentBag<string>();

		// Files for a language another tool formats, gathered from the workers and handed over in one batch
		// per language once the in-process files are done.
		var external = new System.Collections.Concurrent.ConcurrentBag<PendingFile>[languages.Count];
		for (var i = 0; i < external.Length; i++)
			external[i] = [];
		var toolFailures = new List<string>();
		var unformatted = write || unformattedListPath is null ? null : new System.Collections.Concurrent.ConcurrentBag<string>();

		var allocatedBefore = GC.GetTotalAllocatedBytes(precise: true);
		var stopwatch = Stopwatch.StartNew();

		// A bounded channel lets 4 reader tasks overlap I/O with CPU: formatter workers pull
		// (path, options, bytes) tuples that are already in memory, so they never block on the
		// file system. Capacity 32 keeps formatters fed without holding the whole corpus at once.
		//
		// The two hashes ride along because the reader has already computed the content one to ask the
		// cache; recomputing it in the worker to record the answer would hash every file twice.
		var channel = Channel.CreateBounded<(string Path, int Language, LanguageOptions Options, byte[] Bytes, string CacheKey, UInt128 ContentHash, UInt128 OptionsHash)>(
			new BoundedChannelOptions(32) { SingleWriter = false, SingleReader = false });

		// 4 reader tasks mirror the old gate width. Each races for the next index via an atomic
		// counter so readers don't need coordination beyond the channel itself.
		var nextIndex = -1;
		var readerTasks = Enumerable.Range(0, 4).Select(_ => Task.Run(async () =>
		{
			int i;
			while ((i = Interlocked.Increment(ref nextIndex)) < work.Length)
			{
				var item = work[i];
				// Read bytes rather than text: ReadAllText silently swallows a byte-order mark and
				// WriteAllText silently writes none, so `charset` was unobservable at both ends and
				// every file Curb touched lost its mark.
				var bytes = fileSystem.File.ReadAllBytes(item.Path);

				// Asked here rather than in the formatter worker, so a hit costs the read and the hash
				// and then stops: the file never enters the channel and is never decoded. The decode is
				// the larger half of what a file needing no work would otherwise cost.
				var contentHash = cache is null ? default : Fingerprint.OfContent(bytes);
				if (cache is not null && cache.IsFixedPoint(item.CacheKey, contentHash, item.OptionsHash))
				{
					Interlocked.Increment(ref cached);
					continue;
				}

				// await WriteAsync so the thread is released (rather than blocked) when the channel
				// is full; the reader backs off without pinning a thread-pool thread.
				await channel.Writer.WriteAsync((item.Path, item.Language, item.Options, bytes, item.CacheKey, contentHash, item.OptionsHash));
			}
		})).ToArray();

		// Complete the channel — with any reader exception — once all readers exit.
		Task.WhenAll(readerTasks).ContinueWith(t =>
			channel.Writer.TryComplete(t.IsFaulted ? t.Exception?.InnerException : null));

		// One worker per language per OS thread: a worker owns pooled buffers and is not thread-safe.
		// ThreadLocal initialises each thread's slots on first access, and a slot is filled the first time
		// that thread meets the language; the bag collects every worker for teardown.
		var workerBag = new System.Collections.Concurrent.ConcurrentBag<(int Language, ILanguageWorker Worker)>();
		var threadWorkers = new ThreadLocal<ILanguageWorker?[]>(() => new ILanguageWorker?[languages.Count]);

		// Parallel.ForEachAsync consumes the IAsyncEnumerable produced by ReadAllAsync; it
		// completes only after the channel is drained and the Writer has been marked complete.
		Parallel.ForEachAsync(
			channel.Reader.ReadAllAsync(),
			new ParallelOptions { MaxDegreeOfParallelism = Environment.ProcessorCount },
			(item, _) =>
			{
				var workers = threadWorkers.Value!;

				// A bug in the printer, or a failed write — a read-only file, a permission error —
				// is one file's problem, not the run's. Isolated per item rather than left to escape
				// Parallel.ForEachAsync, which would abort every file still queued behind it on the
				// strength of one the printer was never meant to survive. The file is reported and,
				// because the catch sits before any write, left exactly as it was found.
				if (!item.Options.ReportsCoverage)
					Interlocked.Increment(ref uncovered);

				try
				{
					var hadBom = item.Bytes.Length >= 3 && item.Bytes[0] == 0xEF && item.Bytes[1] == 0xBB && item.Bytes[2] == 0xBF;
					string source;
					try
					{
						source = StrictUtf8.GetString(hadBom ? item.Bytes.AsSpan(3) : item.Bytes);
					}
					catch (DecoderFallbackException)
					{
						Interlocked.Increment(ref unparsable);
						Interlocked.Increment(ref tallies[item.Language].Unparsable);
						messages.Add($"{item.Path}: is not valid UTF-8 — left unchanged");
						return ValueTask.CompletedTask;
					}

					var language = languages[item.Language];

					// The file said not to format it: `generated_code = true`, an IDE0055 severity of
					// none, or an `<auto-generated>` header. Counted rather than passed over silently,
					// so a file that quietly stopped being formatted is visible in the summary.
					if (item.Options.Excluded || language.SkipSource(source))
					{
						Interlocked.Increment(ref skipped);
						Interlocked.Increment(ref tallies[item.Language].Skipped);
						return ValueTask.CompletedTask;
					}

					var pending = new PendingFile(item.Path, item.Language, item.Options, item.Bytes, hadBom, source, item.CacheKey, item.ContentHash, item.OptionsHash);

					// Another tool's file goes into its batch, already read, decoded and past the skip rules.
					if (language is IExternalLanguage)
					{
						external[item.Language].Add(pending);
						return ValueTask.CompletedTask;
					}

					var worker = workers[item.Language];
					if (worker is null)
					{
						worker = workers[item.Language] = ((IInProcessLanguage)language).CreateWorker(collectUnhandled: coverageReport);
						workerBag.Add((item.Language, worker));
					}

					var result = worker.Format(
						source, item.Options, fileSystem.Path.GetFileName(item.Path),
						produceText: write, expandUnhandled: expandUnhandled, verifyRoundTrip: verifyRoundTrip);

					Apply(pending, result);
				}
				catch (Exception exception)
				{
					Interlocked.Increment(ref failed);
					Interlocked.Increment(ref tallies[item.Language].Failed);
					messages.Add($"{item.Path}: internal error — {exception.Message}");
				}

				return ValueTask.CompletedTask;
			}).GetAwaiter().GetResult();

		// The other tools' batches, one call per language, after the in-process work. A tool is started only
		// here, so a run whose files all came from the cache starts nothing.
		for (var language = 0; language < languages.Count; language++)
		{
			if (external[language].IsEmpty || languages[language] is not IExternalLanguage tool)
				continue;

			// Sorted, so the tool sees the same order on every run and its output does too.
			var batch = external[language].OrderBy(file => file.Path, StringComparer.Ordinal).ToArray();
			FormatResult[] results;
			try
			{
				results = tool.FormatBatchAsync(
					[.. batch.Select(file => new ExternalFile(file.Path, file.Source, file.Options, file.Bytes))],
					produceText: write,
					CancellationToken.None).GetAwaiter().GetResult();
				if (results.Length != batch.Length)
					throw new InvalidOperationException($"answered for {results.Length} of {batch.Length} file(s)");
			}
			catch (Exception exception)
			{
				// The tool could not run at all. Every file it was handed failed for the one reason, said once
				// rather than once per file.
				Interlocked.Add(ref failed, batch.Length);
				Interlocked.Add(ref tallies[language].Failed, batch.Length);
				toolFailures.Add($"{tool.Id}: {batch.Length} file(s) not formatted — {exception.Message}");
				continue;
			}

			for (var i = 0; i < batch.Length; i++)
			{
				try
				{
					Apply(batch[i], results[i]);
				}
				catch (Exception exception)
				{
					Interlocked.Increment(ref failed);
					Interlocked.Increment(ref tallies[language].Failed);
					messages.Add($"{batch[i].Path}: internal error — {exception.Message}");
				}
			}
		}

		// One result, from whichever language produced it, onto the counters and the disk. Throws only for a
		// failed write, which the caller counts as failed: the catch sits before anything else is recorded.
		void Apply(PendingFile item, FormatResult result)
		{
			switch (result.Status)
			{
				case FormatStatus.SyntaxError:
					Interlocked.Increment(ref unparsable);
					Interlocked.Increment(ref tallies[item.Language].Unparsable);
					messages.Add($"{item.Path}: does not parse — {result.Message}");
					break;

				case FormatStatus.VerificationFailed:
				case FormatStatus.TooDeep:
					Interlocked.Increment(ref failed);
					Interlocked.Increment(ref tallies[item.Language].Failed);
					messages.Add($"{item.Path}: {result.Message}");
					break;

				case FormatStatus.Ignored:
					// The tool's own ignore rules, such as .fantomasignore. Not recorded: the options hash does
					// not cover those rules, so a remembered answer would outlive a change to them.
					Interlocked.Increment(ref skipped);
					Interlocked.Increment(ref tallies[item.Language].Skipped);
					break;

				default:
					// A mark that has to be added or removed is a change to the file even when
					// every byte after it is identical, and dotnet format treats it as one.
					var wantsBom = item.Options.Layout.Charset switch
					{
						Charset.Utf8Bom => true,
						Charset.Utf8 => false,
						Charset.Preserve => item.HadBom,
						_ => item.HadBom,
					};

					if (result.Changed || wantsBom != item.HadBom)
					{
						// After the write, not before: a write that throws is caught below and
						// counted as failed, and a file only counts as changed once it actually is.
						if (write)
							fileSystem.File.WriteAllText(
								item.Path,
								result.Text ?? item.Source,
								wantsBom ? Utf8WithBom : Utf8);
						else
							unformatted?.Add(item.Path);
						Interlocked.Increment(ref changed);
						Interlocked.Increment(ref tallies[item.Language].Changed);
					}
					else
					{
						// The only place anything is recorded, and deliberately not the branch above:
						// what is remembered is that the formatter was run over these bytes and handed
						// them back, which is something this run watched happen. A file that was just
						// rewritten would have to be taken on trust instead, and a formatter that
						// trusts its own output cannot notice when it stops being idempotent.
						cache?.Record(item.CacheKey, item.ContentHash, item.OptionsHash);
					}
					break;
			}

			// Coverage is reported per run so that printer progress is visible as a number. Only a
			// language that has printers to cover counts towards it.
			if (item.Options.ReportsCoverage)
			{
				var tokens = result.Coverage;
				Interlocked.Add(ref printedTokens, (long)(tokens * 1000));
				Interlocked.Increment(ref totalTokens);
			}
		}

		threadWorkers.Dispose();
		foreach (var (language, worker) in workerBag)
		{
			Interlocked.Add(ref reparsed, worker.RoundTripsChecked);
			if (worker.UnhandledByKind is { } byKind)
			{
				lock (unhandled)
				{
					foreach (var (kind, count) in byKind)
					{
						unhandled.TryGetValue((language, kind), out var existing);
						unhandled[(language, kind)] = existing + count;
					}
				}
			}
			worker.Dispose();
		}

		stopwatch.Stop();
		var allocated = GC.GetTotalAllocatedBytes(precise: true) - allocatedBefore;
		var sourceBytes = work.Sum(w => fileSystem.FileInfo.New(w.Path).Length);

		// After the measurements, so neither the timing nor the allocation ratio counts bookkeeping the
		// formatter did not do. Before the exit code, because the runs worth caching hardest are the ones
		// that fail: a check that finds an unformatted file never stamps, so the build repeats it until
		// somebody formats the file, and the cache is what makes those repeats cost one file.
		cache?.Save();

		// Sorted for the same reason the messages below are: a ConcurrentBag's order is whichever
		// worker finished last, and the MSBuild target that reads this back turns each line into a
		// diagnostic — a build log that reorders itself between runs is worse than an unsorted one.
		if (unformattedListPath is not null)
			fileSystem.File.WriteAllLines(unformattedListPath, (unformatted ?? []).OrderBy(p => p, StringComparer.Ordinal));

		// Sorted, because a ConcurrentBag hands them back in whatever order the threads finished, so
		// two runs over the same tree reported a different arbitrary twenty. Diagnosing a large
		// repository against a moving sample is worse than useless.
		var reported = messages.OrderBy(m => m, StringComparer.Ordinal).ToArray();
		foreach (var message in reported.Take(20))
			Console.Error.WriteLine(message);

		if (reported.Length > 20)
			Console.Error.WriteLine($"… and {reported.Length - 20} more not shown");

		foreach (var failure in toolFailures)
			Console.Error.WriteLine(failure);

		for (var i = 0; i < languages.Count; i++)
		{
			if (languages[i] is IExternalLanguage { Warnings: { Count: > 0 } warnings })
			{
				foreach (var warning in warnings)
					Console.Error.WriteLine($"{languages[i].Id}: warning: {warning}");
			}
		}

		var coverage = totalTokens == 0 ? 1 : printedTokens / (double)totalTokens / 1000;

		// Coverage is a C# figure. A run that printed only XML has none to report, rather than a
		// vacuous 100 %; a run that printed nothing at all keeps the line it always had.
		var coverageText = totalTokens == 0 && uncovered > 0 ? "" : string.Create(CultureInfo.InvariantCulture, $", printer coverage {coverage:P1}");
		Console.WriteLine(string.Create(CultureInfo.InvariantCulture,
			$"{(write ? "Formatted" : "Checked")} {files.Length} file(s){CachedText(cached)} in {stopwatch.ElapsedMilliseconds}ms — "
			+ $"{changed} {(write ? "changed" : "would change")}, {SkippedText(skipped)}{failed} failed, {unparsable} unparsable{coverageText}"));

		// Only once a second language ran, so a C#-only run prints exactly what it always has.
		foreach (var item in work)
			tallies[item.Language].Files++;
		if (tallies.Count(tally => tally.Files > 0) > 1)
		{
			for (var i = 0; i < tallies.Length; i++)
			{
				if (tallies[i].Files == 0)
					continue;
				Console.WriteLine(string.Create(CultureInfo.InvariantCulture,
					$"  {languages[i].Id}: {tallies[i].Files} file(s) — {tallies[i].Changed} {(write ? "changed" : "would change")}, "
					+ $"{SkippedText(tallies[i].Skipped)}{tallies[i].Failed} failed, {tallies[i].Unparsable} unparsable"));
			}
		}

		// Once per reason, with a count, rather than a line per file nobody asked to have formatted.
		foreach (var ((language, reason), count) in declined.OrderBy(pair => pair.Key.Language))
		{
			Console.WriteLine(string.Create(CultureInfo.InvariantCulture,
				$"  {languages[language].Id}: {count} file(s) not formatted — {reason}"));
		}

		var costModel = expandUnhandled ? "  [full-coverage cost model]" : string.Empty;
		Console.WriteLine(string.Create(CultureInfo.InvariantCulture,
			$"  {sourceBytes / 1024.0 / 1024.0:F2} MB source, allocated {allocated / 1024.0 / 1024.0:F1} MB "
			+ $"({allocated / (double)Math.Max(1, sourceBytes):F1}x source){costModel}"));

		if (coverageReport && unhandled.Count > 0)
		{
			Console.WriteLine();
			Console.WriteLine("# tokens still emitted verbatim, by syntax kind");
			foreach (var (key, count) in unhandled.OrderByDescending(p => p.Value).Take(25))
			{
				Console.WriteLine(string.Create(CultureInfo.InvariantCulture,
					$"  {count,8}  {((IInProcessLanguage)languages[key.Language]).DescribeUnhandledKind(key.Kind)}"));
			}
			Console.WriteLine();
		}

		if (verifyRoundTrip)
		{
			// Out of the files that were printed, not out of every file named. A cache hit never reaches
			// the printer, so counting it in the denominator would report a proportion of a population
			// that was never at risk.
			//
			// XML is left out too: every XML output is re-lexed and verified, which is not a second parse.
			var printed = files.Length - cached - uncovered;
			if (printed > 0 || uncovered == 0)
			{
				Console.WriteLine(string.Create(CultureInfo.InvariantCulture,
					$"  round-trip verified {reparsed} of {printed} file(s) "
					+ $"({reparsed / (double)Math.Max(1, printed):P1} needed a second parse)"));
			}
		}

		var exitCode = failed > 0 ? 3 : !write && changed > 0 ? 1 : 0;
		return new FormattingRunSummary(files.Length, changed, cached, skipped, failed, unparsable, reparsed, coverage, exitCode);
	}

	/// <summary>
	/// The cache key's options half: the file's merged settings, and whatever else its language bound
	/// from.
	/// </summary>
	private static UInt128 OptionsHash(OptionsFingerprinter fingerprinter, global::EditorConfig.Core.FileConfiguration configuration, LanguageOptions options)
	{
		var hash = fingerprinter.Of(configuration);
		if (options.AdditionalProperties is { } additional)
			hash = (hash * 1_000_003) ^ Fingerprint.OfProperties(additional);
		return hash;
	}

	/// <summary>
	/// Every file under <paramref name="root"/> a registered language might own, without descending into
	/// <c>.git</c>, <c>bin</c> or <c>obj</c>.
	/// </summary>
	/// <remarks>
	/// With one language the file system is asked for its one extension across the whole tree, as it
	/// always was. With more there is no single pattern to ask for, and enumerating everything only to
	/// filter it would hand back a path string for every object in <c>.git</c> and every build output.
	/// Those folders are pruned before they are entered instead. The bin/obj half of
	/// <see cref="IsFormattable"/> still applies afterwards, so a root that is itself inside one behaves
	/// as before.
	/// </remarks>
	private static IEnumerable<string> Walk(IFileSystem fileSystem, string root, LanguageRegistry languages)
	{
		if (languages.SearchPattern != "*")
		{
			foreach (var file in fileSystem.Directory.EnumerateFiles(root, languages.SearchPattern, SearchOption.AllDirectories))
				yield return file;
			yield break;
		}

		var pending = new Stack<string>();
		pending.Push(root);
		while (pending.Count > 0)
		{
			var directory = pending.Pop();
			foreach (var file in fileSystem.Directory.EnumerateFiles(directory))
			{
				if (languages.IndexOf(file) >= 0)
					yield return file;
			}

			foreach (var child in fileSystem.Directory.EnumerateDirectories(directory))
			{
				var name = fileSystem.Path.GetFileName(child.AsSpan());
				if (name is ".git" or "bin" or "obj")
					continue;
				pending.Push(child);
			}
		}
	}

	/// <summary>
	/// A file some registered language owns, outside a build-output folder. The directory walk already
	/// narrows to this with the registry's own glob before the bin/obj half of this ever runs; the
	/// extension half exists for <paramref name="path"/>s an explicit file list handed over unfiltered,
	/// which is what a project in a language Curb does not cover, reaching Curb through a shared build
	/// import, looks like from here.
	/// </summary>
	private static bool IsFormattable(LanguageRegistry languages, string path) =>
		languages.IndexOf(path) >= 0
		&& !path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
		&& !path.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal);
}

/// <summary>What a formatting run did, for a caller that needs more than an exit code.</summary>
/// <remarks>
/// The run prints its own summary; this exists so tests can assert on the counts without capturing
/// console output, which TUnit's parallelism makes unreliable.
/// </remarks>
internal readonly record struct FormattingRunSummary(
	int Files,
	int Changed,
	int Cached,
	int Skipped,
	int Failed,
	int Unparsable,
	int Reparsed,
	double Coverage,
	int ExitCode);

/// <summary>One language's share of a run's outcomes.</summary>
internal struct LanguageTally
{
	public int Files;
	public int Changed;
	public int Skipped;
	public int Failed;
	public int Unparsable;
}

/// <summary>A file read, decoded and past the skip rules, on its way to a formatter and then to <c>Apply</c>.</summary>
internal readonly record struct PendingFile(
	string Path,
	int Language,
	LanguageOptions Options,
	byte[] Bytes,
	bool HadBom,
	string Source,
	string CacheKey,
	UInt128 ContentHash,
	UInt128 OptionsHash);
