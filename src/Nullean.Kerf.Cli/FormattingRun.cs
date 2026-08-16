using System.Diagnostics;
using System.Globalization;
using System.IO.Abstractions;
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
	public static int Execute(IFileSystem fileSystem, string target, bool write, bool expandUnhandled = false, bool? verify = null, bool forceVerify = false)
	{
		var root = fileSystem.Path.GetFullPath(target);

		// On by default for both commands: the printer tracks whether it actually put a token
		// boundary at risk, so on code that does not, the second parse never happens.
		var verifyRoundTrip = verify ?? true;

		var files = fileSystem.Directory.Exists(root)
			? fileSystem.Directory.EnumerateFiles(root, "*.cs", SearchOption.AllDirectories).Where(IsFormattable).ToArray()
			: [root];

		var editorConfig = new KerfEditorConfig(fileSystem);
		var optionsByDirectory = new Dictionary<string, FormatOptions>(StringComparer.Ordinal);

		// Resolving options is cheap but not free, and it is not thread-safe against the shared
		// parser, so it happens up front on one thread.
		var work = new (string Path, FormatOptions Options)[files.Length];
		for (var i = 0; i < files.Length; i++)
		{
			var directory = fileSystem.Path.GetDirectoryName(files[i]) ?? root;
			if (!optionsByDirectory.TryGetValue(directory, out var options))
			{
				options = EditorConfigOptionsBinder.Bind(editorConfig.For(files[i]));
				optionsByDirectory[directory] = options;
			}
			work[i] = (files[i], options);
		}

		var changed = 0;
		var failed = 0;
		var unparsable = 0;
		long printedTokens = 0;
		long totalTokens = 0;
		var reparsed = 0;
		var messages = new System.Collections.Concurrent.ConcurrentBag<string>();

		var allocatedBefore = GC.GetTotalAllocatedBytes(precise: true);
		var stopwatch = Stopwatch.StartNew();

		Parallel.ForEach(
			work,
			new ParallelOptions { MaxDegreeOfParallelism = Environment.ProcessorCount },
			() => new CSharpFormatter(),
			(item, _, formatter) =>
			{
				var source = fileSystem.File.ReadAllText(item.Path);
				var result = formatter.Format(source, item.Options, produceText: write, expandUnhandled: expandUnhandled, verifyRoundTrip: verifyRoundTrip, forceRoundTrip: forceVerify);

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
						if (result.Changed)
						{
							Interlocked.Increment(ref changed);
							if (write && result.Text is not null)
								fileSystem.File.WriteAllText(item.Path, result.Text);
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
				formatter.Dispose();
			});

		stopwatch.Stop();
		var allocated = GC.GetTotalAllocatedBytes(precise: true) - allocatedBefore;
		var sourceBytes = work.Sum(w => fileSystem.FileInfo.New(w.Path).Length);

		foreach (var message in messages.Take(20))
			Console.Error.WriteLine(message);

		var coverage = totalTokens == 0 ? 1 : printedTokens / (double)totalTokens / 1000;

		Console.WriteLine(string.Create(CultureInfo.InvariantCulture,
			$"{(write ? "Formatted" : "Checked")} {files.Length} file(s) in {stopwatch.ElapsedMilliseconds}ms — "
			+ $"{changed} {(write ? "changed" : "would change")}, {failed} failed, {unparsable} unparsable, "
			+ $"printer coverage {coverage:P1}"));

		var costModel = expandUnhandled ? "  [full-coverage cost model]" : string.Empty;
		Console.WriteLine(string.Create(CultureInfo.InvariantCulture,
			$"  {sourceBytes / 1024.0 / 1024.0:F2} MB source, allocated {allocated / 1024.0 / 1024.0:F1} MB "
			+ $"({allocated / (double)Math.Max(1, sourceBytes):F1}x source){costModel}"));

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
