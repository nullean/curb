using System.Diagnostics;
using System.Globalization;
using System.IO.Abstractions;
using System.Text;
using Nullean.Kerf.EditorConfig;

namespace Nullean.Kerf.Cli;

/// <summary>Formats or checks a directory tree.</summary>
/// <remarks>
/// Files are processed in parallel with one <see cref="CSharpFormatter"/> per worker, since a
/// formatter owns pooled buffers and is not thread-safe. Configuration is resolved through a single
/// shared <see cref="KerfEditorConfig"/>, whose per-directory caching means a repository resolves
/// its <c>.editorconfig</c> chain once per folder rather than once per file.
/// </remarks>
internal static class FormattingRun
{
	/// <summary>UTF-8 without a byte-order mark, and without the exception-throwing default.</summary>
	private static readonly UTF8Encoding Utf8 = new(encoderShouldEmitUTF8Identifier: false);

	/// <summary>UTF-8 with a byte-order mark, for <c>charset = utf-8-bom</c>.</summary>
	private static readonly UTF8Encoding Utf8WithBom = new(encoderShouldEmitUTF8Identifier: true);

	/// <summary>Renders the skipped count only when there is one, to keep the usual line unchanged.</summary>
	private static string SkippedText(int skipped) =>
		skipped == 0 ? "" : string.Create(CultureInfo.InvariantCulture, $"{skipped} skipped, ");

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
	public static int Execute(
		IFileSystem fileSystem,
		string target,
		bool write,
		bool expandUnhandled = false,
		bool? verify = null,
		bool coverageReport = false,
		string[]? explicitFiles = null)
	{
		// On by default for both commands: the printer tracks whether it actually put a token
		// boundary at risk, so on code that does not, the second parse never happens.
		var verifyRoundTrip = verify ?? true;

		string[] files;
		if (explicitFiles is not null)
			files = explicitFiles;
		else
		{
			var root = fileSystem.Path.GetFullPath(target);
			files = fileSystem.Directory.Exists(root)
				? [.. fileSystem.Directory.EnumerateFiles(root, "*.cs", SearchOption.AllDirectories).Where(IsFormattable)]
				: [root];
		}

		var editorConfig = new KerfEditorConfig(fileSystem);

		// Resolved per file, not per directory. A section can discriminate on the file name —
		// `[*Tests.cs]`, `[Program.cs]`, and `generated_code = true` in particular — so reusing one
		// file's options across its directory applies or misses them by alphabetical accident.
		// KerfEditorConfig already caches the chain per directory, which is the part that costs;
		// what is left is a glob match per file, about 1 ms across the whole corpus.
		//
		// It is not thread-safe against the shared parser, so it happens up front on one thread.
		var work = new (string Path, FormatOptions Options)[files.Length];
		for (var i = 0; i < files.Length; i++)
			work[i] = (files[i], EditorConfigOptionsBinder.Bind(editorConfig.For(files[i])));

		var changed = 0;
		var skipped = 0;
		var failed = 0;
		var unparsable = 0;
		long printedTokens = 0;
		long totalTokens = 0;
		var reparsed = 0;
		var unhandled = new Dictionary<int, int>();
		var messages = new System.Collections.Concurrent.ConcurrentBag<string>();

		var allocatedBefore = GC.GetTotalAllocatedBytes(precise: true);
		var stopwatch = Stopwatch.StartNew();

		Parallel.ForEach(
			work,
			new ParallelOptions { MaxDegreeOfParallelism = Environment.ProcessorCount },
			() => new CSharpFormatter { UnhandledByKind = coverageReport ? [] : null },
			(item, _, formatter) =>
			{
				// Read bytes rather than text: ReadAllText silently swallows a byte-order mark and
				// WriteAllText silently writes none, so `charset` was unobservable at both ends and
				// every file Kerf touched lost its mark.
				var bytes = fileSystem.File.ReadAllBytes(item.Path);
				var hadBom = bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF;
				var source = Utf8.GetString(hadBom ? bytes.AsSpan(3) : bytes);

				// The file said not to format it: `generated_code = true`, an IDE0055 severity of
				// none, or an `<auto-generated>` header. Counted rather than passed over silently,
				// so a file that quietly stopped being formatted is visible in the summary.
				if (item.Options.Excluded || CSharpSource.HasGeneratedHeader(source))
				{
					Interlocked.Increment(ref skipped);
					return formatter;
				}

				var result = formatter.Format(source, item.Options, produceText: write, expandUnhandled: expandUnhandled, verifyRoundTrip: verifyRoundTrip);

				switch (result.Status)
				{
					case FormatStatus.SyntaxError:
						Interlocked.Increment(ref unparsable);
						messages.Add($"{item.Path}: does not parse — {result.Message}");
						break;

					case FormatStatus.VerificationFailed:
					case FormatStatus.TooDeep:
						Interlocked.Increment(ref failed);
						messages.Add($"{item.Path}: {result.Message}");
						break;

					default:
						// A mark that has to be added or removed is a change to the file even when
						// every byte after it is identical, and dotnet format treats it as one.
						var wantsBom = item.Options.Charset switch
						{
							Charset.Utf8Bom => true,
							Charset.Utf8 => false,
							Charset.Preserve => hadBom,
							_ => hadBom,
						};

						if (result.Changed || wantsBom != hadBom)
						{
							Interlocked.Increment(ref changed);
							if (write)
								fileSystem.File.WriteAllText(
									item.Path,
									result.Text ?? source,
									wantsBom ? Utf8WithBom : Utf8);
						}
						break;
				}

				// Coverage is reported per run so that printer progress is visible as a number.
				var tokens = result.Coverage;
				Interlocked.Add(ref printedTokens, (long)(tokens * 1000));
				Interlocked.Increment(ref totalTokens);
				return formatter;
			},
			formatter =>
			{
				Interlocked.Add(ref reparsed, formatter.RoundTripsChecked);
				if (formatter.UnhandledByKind is { } byKind)
				{
					lock (unhandled)
					{
						foreach (var (kind, count) in byKind)
						{
							unhandled.TryGetValue(kind, out var existing);
							unhandled[kind] = existing + count;
						}
					}
				}
				formatter.Dispose();
			});

		stopwatch.Stop();
		var allocated = GC.GetTotalAllocatedBytes(precise: true) - allocatedBefore;
		var sourceBytes = work.Sum(w => fileSystem.FileInfo.New(w.Path).Length);

		// Sorted, because a ConcurrentBag hands them back in whatever order the threads finished, so
		// two runs over the same tree reported a different arbitrary twenty. Diagnosing a large
		// repository against a moving sample is worse than useless.
		var reported = messages.OrderBy(m => m, StringComparer.Ordinal).ToArray();
		foreach (var message in reported.Take(20))
			Console.Error.WriteLine(message);

		if (reported.Length > 20)
			Console.Error.WriteLine($"… and {reported.Length - 20} more not shown");

		var coverage = totalTokens == 0 ? 1 : printedTokens / (double)totalTokens / 1000;

		Console.WriteLine(string.Create(CultureInfo.InvariantCulture,
			$"{(write ? "Formatted" : "Checked")} {files.Length} file(s) in {stopwatch.ElapsedMilliseconds}ms — "
			+ $"{changed} {(write ? "changed" : "would change")}, {SkippedText(skipped)}{failed} failed, {unparsable} unparsable, "
			+ $"printer coverage {coverage:P1}"));

		var costModel = expandUnhandled ? "  [full-coverage cost model]" : string.Empty;
		Console.WriteLine(string.Create(CultureInfo.InvariantCulture,
			$"  {sourceBytes / 1024.0 / 1024.0:F2} MB source, allocated {allocated / 1024.0 / 1024.0:F1} MB "
			+ $"({allocated / (double)Math.Max(1, sourceBytes):F1}x source){costModel}"));

		if (coverageReport && unhandled.Count > 0)
		{
			Console.WriteLine();
			Console.WriteLine("# tokens still emitted verbatim, by syntax kind");
			foreach (var (kind, count) in unhandled.OrderByDescending(p => p.Value).Take(25))
			{
				Console.WriteLine(string.Create(CultureInfo.InvariantCulture,
					$"  {count,8}  {(Microsoft.CodeAnalysis.CSharp.SyntaxKind)kind}"));
			}
			Console.WriteLine();
		}

		if (verifyRoundTrip)
		{
			Console.WriteLine(string.Create(CultureInfo.InvariantCulture,
				$"  round-trip verified {reparsed} of {files.Length} file(s) "
				+ $"({reparsed / (double)Math.Max(1, files.Length):P1} needed a second parse)"));
		}

		if (failed > 0)
			return 3;
		return !write && changed > 0 ? 1 : 0;
	}

	private static bool IsFormattable(string path) =>
		!path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
		&& !path.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal);
}
