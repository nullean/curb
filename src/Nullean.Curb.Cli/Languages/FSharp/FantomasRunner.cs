using System.Globalization;
using System.IO.Abstractions;
using System.Text;
using System.Text.Json;

namespace Nullean.Curb.Cli.Languages.FSharp;

/// <summary>Fantomas could not be run at all, so none of the files it was handed were formatted.</summary>
internal sealed class ExternalToolException(string message, Exception? inner = null) : Exception(message, inner);

/// <summary>Hands one run's F# files to Fantomas and maps what comes back onto Curb's outcomes.</summary>
/// <remarks>
/// <para>
/// One daemon per manifest per run, started here and stopped here, with about one request per processor
/// in flight. Measured against the batch CLI doing the same job (formatting, then checking what it wrote):
/// 0.61 s against 1.01 s for one file and 17.6 s against 28.2 s for 1,000. Checking files that are already
/// formatted, where the CLI needs one pass, the daemon costs about 0.1 s more up to 100 files and about
/// the same at 1,000; the cache makes that the uncommon case.
/// </para>
/// <para>
/// Curb never writes what Fantomas hands back on the strength of one answer. The daemon does not validate
/// its own output (the CLI does), and on dotnet/fsharp's <c>src/</c> (518 files) it returned 6 results that
/// do not parse and 4 that change again as <c>Formatted</c>; the CLI catches the 6 and writes the 4. So
/// every <c>Formatted</c> result is sent back, and only an <c>Unchanged</c> answer to that lets it through:
/// an error means the output does not parse, another <c>Formatted</c> means it is not a fixed point, and
/// either way the file is failed and left as it was. That costs one more request for each file that needed
/// formatting, and nothing for one that did not.
/// </para>
/// <para>
/// When the daemon cannot be started or does not answer its handshake, the batch CLI is the fallback: one
/// process per chunk of files sized to the command line, never one per file. It needs
/// <c>fantomas check --json</c>, so Fantomas 8.0.0-alpha-016 or later.
/// </para>
/// <para>
/// Each manifest's files succeed or fail on their own: a manifest that is broken, not restored, or whose
/// Fantomas stops answering fails its own files, reported once through <c>fail</c>, and no other.
/// Nothing is ever waited on without a limit: every request and every command line run has a timeout, scaled
/// to the size of what it was handed, after which the process is killed and the group fails.
/// </para>
/// </remarks>
/// <param name="fileSystem">What files are read, restored and resolved through.</param>
/// <param name="launcher">Starts Fantomas.</param>
/// <param name="environment">Where the SDK keeps its caches, and the timeout and daemon switches.</param>
/// <param name="warn">Advice worth saying once, such as a setting Fantomas does not know.</param>
/// <param name="fail">A group's failure, said once instead of once per file.</param>
internal sealed class FantomasRunner(IFileSystem fileSystem, IToolLauncher launcher, ToolEnvironment environment, Action<string> warn, Action<string> fail)
{
	/// <summary>Set to <c>false</c> to skip the daemon and use the batch CLI. For measurement and for a daemon that misbehaves.</summary>
	internal const string DaemonVariable = "CURB_FANTOMAS_DAEMON";

	/// <summary>Seconds Fantomas gets per request, and per command line run, before Curb gives up on it. Scaled up for large input.</summary>
	internal const string TimeoutVariable = "CURB_FANTOMAS_TIMEOUT";

	/// <summary>The first Fantomas with <c>fantomas check --json</c>, and with the daemon's ignore-file rules on the command line.</summary>
	internal const string MinimumCliVersion = "8.0.0-alpha-016";

	private static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(60);

	/// <summary>How much source one timeout unit covers: a request this size is given the base timeout, twice it twice that.</summary>
	private const long BytesPerRequestUnit = 256 * 1024;

	/// <summary>The same for a command line run, which formats its files in parallel.</summary>
	private const long BytesPerCliUnit = 1024 * 1024;

	private TimeSpan? _timeout;

	/// <summary>A result for a file whose group failed; the reason is said once, through <c>fail</c>.</summary>
	internal static FormatResult GroupFailed => new(FormatStatus.VerificationFailed, false, null, 1, null);

	public async Task<FormatResult[]> RunAsync(IReadOnlyList<ExternalFile> files, bool produceText, CancellationToken cancellationToken)
	{
		var results = new FormatResult[files.Count];
		var resolver = new FantomasResolver(fileSystem, environment);

		// Grouped by manifest: two subtrees may pin two versions, and each file must meet the one its own
		// manifest pins.
		var groups = new Dictionary<FantomasManifest, List<int>>();
		for (var i = 0; i < files.Count; i++)
		{
			var manifest = ((FSharpOptions)files[i].Options).Manifest!;
			if (!groups.TryGetValue(manifest, out var indices))
				groups[manifest] = indices = [];
			indices.Add(i);
		}

		foreach (var (manifest, indices) in groups.OrderBy(group => group.Key.ManifestPath, StringComparer.Ordinal))
		{
			try
			{
				cancellationToken.ThrowIfCancellationRequested();
				await RunGroupAsync(resolver, manifest, files, indices, results, produceText, cancellationToken).ConfigureAwait(false);
			}
			catch (Exception exception) when (exception is ExternalToolException or OperationCanceledException)
			{
				var reason = exception is OperationCanceledException ? "the run was cancelled" : exception.Message;
				fail($"{indices.Count} file(s) under {manifest.ManifestPath} not formatted — {reason}");
				foreach (var index in indices)
					results[index] = GroupFailed;
			}
		}

		return results;
	}

	private async Task RunGroupAsync(
		FantomasResolver resolver, FantomasManifest manifest, IReadOnlyList<ExternalFile> files, List<int> indices, FormatResult[] results,
		bool produceText, CancellationToken cancellationToken)
	{
		// The SDK refuses a manifest it cannot read, so `dotnet tool run` would fail here too.
		if (manifest.Problem is { } problem)
			throw new ExternalToolException(problem);

		var resolution = resolver.Resolve(manifest);
		if (resolution.Tool is not { } tool)
			throw new ExternalToolException(resolution.Problem!);

		var useDaemon = !string.Equals(environment.GetVariable(DaemonVariable), "false", StringComparison.OrdinalIgnoreCase);
		if (useDaemon && await TryDaemonAsync(tool, files, indices, results, produceText, cancellationToken).ConfigureAwait(false))
			return;

		if (!FantomasVersion.AtLeast(manifest.Version, MinimumCliVersion))
		{
			throw new ExternalToolException(
				$"{(useDaemon ? "the Fantomas daemon could not be used, and " : "")}the Fantomas command line fallback needs Fantomas {MinimumCliVersion} or later "
				+ $"(for `check --json`), but {manifest.ManifestPath} pins {manifest.Version}");
		}

		await BatchAsync(tool, files, indices, results, produceText, cancellationToken).ConfigureAwait(false);
	}

	/// <summary>The base timeout: <see cref="TimeoutVariable"/> in seconds when it is set and positive, else 60 s.</summary>
	internal TimeSpan BaseTimeout
	{
		get
		{
			if (_timeout is { } known)
				return known;

			var configured = environment.GetVariable(TimeoutVariable);
			var timeout = DefaultTimeout;
			if (configured is { Length: > 0 })
			{
				if (double.TryParse(configured, NumberStyles.Float, CultureInfo.InvariantCulture, out var seconds) && seconds > 0 && seconds < 24 * 3600)
					timeout = TimeSpan.FromSeconds(seconds);
				else
					warn($"{TimeoutVariable}='{configured}' is not a number of seconds; using {DefaultTimeout.TotalSeconds:0} s");
			}

			_timeout = timeout;
			return timeout;
		}
	}

	/// <summary>The base timeout once per <paramref name="unit"/> bytes of input, and at least once.</summary>
	internal static TimeSpan Scaled(TimeSpan baseTimeout, long bytes, long unit) =>
		baseTimeout * Math.Max(1, (bytes + unit - 1) / unit);

	/// <summary>Formats a group through one daemon. False when the daemon could not be started or did not answer, and nothing was done.</summary>
	private async Task<bool> TryDaemonAsync(
		FantomasTool tool, IReadOnlyList<ExternalFile> files, List<int> indices, FormatResult[] results, bool produceText, CancellationToken cancellationToken)
	{
		var command = tool.Command(environment.Dotnet, [DaemonArgument(tool.Manifest.Version)]);
		IToolProcess process;
		try
		{
			process = launcher.Start(command);
		}
		catch (ToolStartException exception)
		{
			warn($"could not start the Fantomas daemon, using the Fantomas command line instead — {exception.Message}");
			return false;
		}

		var client = new FantomasDaemonClient(process, warn);
		await using (client.ConfigureAwait(false))
		{
			string version;
			try
			{
				version = await client.VersionAsync(cancellationToken).WaitAsync(BaseTimeout, cancellationToken).ConfigureAwait(false);
			}
			catch (Exception exception) when (exception is FantomasDaemonException or TimeoutException)
			{
				client.Abort();
				warn($"the Fantomas daemon did not answer, using the Fantomas command line instead — {exception.Message}");
				return false;
			}
			catch (OperationCanceledException)
			{
				client.Abort();
				throw;
			}

			if (!VersionMatches(version, tool.Manifest.Version))
			{
				throw new ExternalToolException(
					$"{tool.Manifest.ManifestPath} pins {tool.Manifest.ToolId} {tool.Manifest.Version}, but the Fantomas that started is {version}. Run `dotnet tool restore`.");
			}

			try
			{
				await Parallel.ForEachAsync(
					indices,
					new ParallelOptions { MaxDegreeOfParallelism = Environment.ProcessorCount, CancellationToken = cancellationToken },
					async (index, token) => results[index] = await FormatOneAsync(client, files[index], produceText, token).ConfigureAwait(false)
				).ConfigureAwait(false);
			}
			catch (DaemonTimeoutException exception)
			{
				// Nothing from this daemon is written: what it answered before it stopped answering came from a
				// process now known to misbehave, and the group fails as a whole.
				client.Abort();
				throw new ExternalToolException(
					$"Fantomas did not answer for {exception.Path} within {exception.Timeout.TotalSeconds:0.#} s, so the daemon was stopped. Set {TimeoutVariable} to allow longer.");
			}
			catch (OperationCanceledException)
			{
				client.Abort();
				throw;
			}
		}

		return true;
	}

	/// <summary>A request that ran out of time. Escapes the parallel loop, which then cancels the rest.</summary>
	private sealed class DaemonTimeoutException(string path, TimeSpan timeout) : Exception($"no answer for {path}")
	{
		public string Path { get; } = path;
		public TimeSpan Timeout { get; } = timeout;
	}

	private async Task<FantomasReply> RequestAsync(FantomasDaemonClient client, string path, string source, CancellationToken cancellationToken)
	{
		// Characters stand in for bytes: close enough for F# source, and a timeout only has to be generous.
		var timeout = Scaled(BaseTimeout, source.Length, BytesPerRequestUnit);
		try
		{
			return await client.FormatAsync(path, source, cancellationToken).WaitAsync(timeout, cancellationToken).ConfigureAwait(false);
		}
		catch (TimeoutException)
		{
			throw new DaemonTimeoutException(path, timeout);
		}
	}

	private async Task<FormatResult> FormatOneAsync(FantomasDaemonClient client, ExternalFile file, bool produceText, CancellationToken cancellationToken)
	{
		var path = fileSystem.Path.GetFullPath(file.Path);
		try
		{
			var reply = await RequestAsync(client, path, file.Source, cancellationToken).ConfigureAwait(false);
			if (!SamePath(reply.Path, path))
				return WrongFile(reply.Path);

			switch (reply.Outcome)
			{
				case FantomasOutcome.Unchanged:
					return Unchanged;
				case FantomasOutcome.Ignored:
					return Ignored;
				case FantomasOutcome.Error:
					return new FormatResult(FormatStatus.SyntaxError, false, null, 1, DescribeError(reply.Message, path));
			}

			// Formatted. Sent back before anything is believed: only a fixed point that parses gets written.
			var formatted = reply.Content!;
			var recheck = await RequestAsync(client, path, formatted, cancellationToken).ConfigureAwait(false);
			if (!SamePath(recheck.Path, path))
				return WrongFile(recheck.Path);

			return recheck.Outcome switch
			{
				FantomasOutcome.Unchanged => new FormatResult(FormatStatus.Formatted, true, produceText ? formatted : null, 1, null),
				FantomasOutcome.Formatted => Unstable,
				FantomasOutcome.Error => new FormatResult(FormatStatus.VerificationFailed, false, null, 1,
					$"Fantomas produced output that does not parse, so it was not written — {DescribeError(recheck.Message, path)}"),
				_ => new FormatResult(FormatStatus.VerificationFailed, false, null, 1,
					"Fantomas ignored its own output on the second pass, so it was not written"),
			};
		}
		catch (FantomasDaemonException exception)
		{
			return new FormatResult(FormatStatus.VerificationFailed, false, null, 1, exception.Message);
		}
	}

	/// <summary>
	/// Whether the daemon answered for the file it was asked about. It echoes the request's path verbatim, so
	/// an answer for any other path is a protocol fault, and its content belongs to some other file.
	/// </summary>
	private static bool SamePath(string? answered, string asked) => answered is null || string.Equals(answered, asked, StringComparison.Ordinal);

	private static FormatResult WrongFile(string? answered) => new(FormatStatus.VerificationFailed, false, null, 1,
		$"Fantomas answered for another file ({answered}), so nothing was written");

	private static FormatResult Unchanged => new(FormatStatus.Formatted, false, null, 1, null);

	private static FormatResult Ignored => new(FormatStatus.Ignored, false, null, 1, null);

	private static FormatResult Unstable => new(FormatStatus.VerificationFailed, false, null, 1,
		"Fantomas's output is not stable (formatting it again changes it again), so it was not written");

	/// <summary>
	/// The batch CLI: <c>check --json</c> to check, and to format, a formatting pass, a <c>check</c> of what
	/// it wrote, and the original bytes put back so Curb does the writing.
	/// </summary>
	/// <remarks>
	/// <para>
	/// The CLI can only format in place. Formatting copies elsewhere would change which <c>.editorconfig</c>
	/// chain and which <c>.fantomasignore</c> govern each file, and that cannot be shown to stay the same. So
	/// the CLI is let write, what it wrote is read back and checked again for stability, and every file handed
	/// to it is put back to the bytes it was read with, whatever happened in between: a report that cannot be
	/// read, a run that times out, a cancelled run. Each file is restored on its own, and one that cannot be
	/// is failed and reported rather than counted as written.
	/// </para>
	/// <para>
	/// The run then writes the stable results itself, with its own byte-order mark, exactly as it does for the
	/// daemon. A crash between the two leaves Fantomas's validated output in place, which is a formatted file
	/// rather than a damaged one.
	/// </para>
	/// </remarks>
	private async Task BatchAsync(
		FantomasTool tool, IReadOnlyList<ExternalFile> files, List<int> indices, FormatResult[] results, bool produceText, CancellationToken cancellationToken)
	{
		var paths = indices.Select(i => fileSystem.Path.GetFullPath(files[i].Path)).ToArray();
		var sizes = indices.Select(i => (long)files[i].Bytes.Length).ToArray();

		if (!produceText)
		{
			var report = await RunCliAsync(tool, check: true, paths, sizes, cancellationToken).ConfigureAwait(false);
			for (var k = 0; k < indices.Count; k++)
				results[indices[k]] = FromReport(report, paths[k], formatted: false);
			return;
		}

		var outcome = new FormatResult[indices.Count];
		try
		{
			var formatReport = await RunCliAsync(tool, check: false, paths, sizes, cancellationToken).ConfigureAwait(false);
			var written = new List<int>();
			var contents = new Dictionary<int, string>();
			for (var k = 0; k < indices.Count; k++)
			{
				outcome[k] = FromReport(formatReport, paths[k], formatted: true);
				if (!outcome[k].Changed)
					continue;

				written.Add(k);
				try
				{
					var bytes = fileSystem.File.ReadAllBytes(paths[k]);
					var hasBom = bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF;
					contents[k] = FormattingRun.StrictUtf8.GetString(hasBom ? bytes.AsSpan(3) : bytes);
				}
				catch (Exception exception) when (exception is DecoderFallbackException or IOException or UnauthorizedAccessException)
				{
					outcome[k] = new FormatResult(FormatStatus.VerificationFailed, false, null, 1, $"could not read back what Fantomas wrote — {exception.Message}");
				}
			}

			// What Fantomas wrote is confirmed before Curb writes it, as the daemon's answers are: only a file
			// the check then leaves alone goes through.
			var readable = written.Where(contents.ContainsKey).ToArray();
			if (readable.Length > 0)
			{
				var recheck = await RunCliAsync(tool, check: true, [.. readable.Select(k => paths[k])], [.. readable.Select(k => sizes[k])], cancellationToken)
					.ConfigureAwait(false);
				foreach (var k in readable)
				{
					var second = FromReport(recheck, paths[k], formatted: false);
					outcome[k] = second is { Status: FormatStatus.Formatted, Changed: false }
						? new FormatResult(FormatStatus.Formatted, true, contents[k], 1, null)
						: second.Status == FormatStatus.Formatted ? Unstable
						: new FormatResult(FormatStatus.VerificationFailed, false, null, 1, $"Fantomas could not check its own output — {second.Message}");
				}
			}
		}
		finally
		{
			// Every file handed to the format pass, not only the ones a report said were written: when the report
			// could not be read, nothing says which were.
			var restoreFailures = Restore(files, indices, paths);
			foreach (var (k, message) in restoreFailures)
				outcome[k] = new FormatResult(FormatStatus.VerificationFailed, false, null, 1, message);
			for (var k = 0; k < indices.Count; k++)
				results[indices[k]] = outcome[k];
		}
	}

	/// <summary>Puts each file back to the bytes it was read with, if it no longer has them. Best effort per file.</summary>
	/// <returns>The files that could not be put back, with why.</returns>
	private List<(int Index, string Message)> Restore(IReadOnlyList<ExternalFile> files, List<int> indices, string[] paths)
	{
		var failures = new List<(int, string)>();
		for (var k = 0; k < indices.Count; k++)
		{
			var original = files[indices[k]].Bytes;
			try
			{
				byte[]? current = null;
				try
				{
					current = fileSystem.File.ReadAllBytes(paths[k]);
				}
				catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
				{
					// Unreadable, so it cannot be shown to be intact: write it back regardless.
				}

				// Compared first, so a file Fantomas left alone keeps its timestamp and does not trigger a rebuild.
				if (current is null || !original.Span.SequenceEqual(current))
					fileSystem.File.WriteAllBytes(paths[k], original.ToArray());
			}
			catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or NotSupportedException)
			{
				var message = $"Fantomas rewrote this file and it could not be put back — {exception.Message}";
				failures.Add((k, message));
				fail($"{paths[k]}: {message}");
			}
		}

		return failures;
	}

	/// <summary>What the CLI reported for each file, by full path. A file it left out was ignored.</summary>
	private async Task<Dictionary<string, (string Status, string? Message)>> RunCliAsync(
		FantomasTool tool, bool check, string[] paths, long[] sizes, CancellationToken cancellationToken)
	{
		var report = new Dictionary<string, (string, string?)>(OperatingSystem.IsLinux() ? StringComparer.Ordinal : StringComparer.OrdinalIgnoreCase);
		var sizeOf = new Dictionary<string, long>(StringComparer.Ordinal);
		for (var i = 0; i < paths.Length; i++)
			sizeOf[paths[i]] = sizes[i];

		foreach (var chunk in Chunks(paths))
		{
			string[] arguments = check ? ["check", "--json", .. chunk] : ["--json", .. chunk];
			var command = tool.Command(environment.Dotnet, arguments);
			IToolProcess process;
			try
			{
				process = launcher.Start(command);
			}
			catch (ToolStartException exception)
			{
				throw new ExternalToolException($"could not run Fantomas — {exception.Message}", exception);
			}

			var timeout = Scaled(BaseTimeout, chunk.Sum(path => sizeOf.GetValueOrDefault(path)), BytesPerCliUnit);
			string output;
			using (process)
			{
				try
				{
					process.StandardInput.Dispose();
					using var reader = new StreamReader(process.StandardOutput, Encoding.UTF8);
					output = await reader.ReadToEndAsync(cancellationToken).WaitAsync(timeout, cancellationToken).ConfigureAwait(false);
					await process.Exited.WaitAsync(timeout, cancellationToken).ConfigureAwait(false);
				}
				catch (TimeoutException)
				{
					process.Kill();
					throw new ExternalToolException(
						$"the Fantomas command line did not finish within {timeout.TotalSeconds:0.#} s, so it was stopped. Set {TimeoutVariable} to allow longer.");
				}
				catch (OperationCanceledException)
				{
					process.Kill();
					throw;
				}

				try
				{
					ReadReport(output, command.WorkingDirectory, report);
				}
				catch (JsonException)
				{
					var error = process.StandardError.Trim();
					throw new ExternalToolException($"Fantomas did not report what it did{(error.Length > 0 ? $" — {error}" : "")}");
				}
			}
		}

		return report;
	}

	private void ReadReport(string output, string workingDirectory, Dictionary<string, (string, string?)> report)
	{
		using var document = JsonDocument.Parse(output);
		var root = document.RootElement;
		if (root.ValueKind != JsonValueKind.Object)
			throw new JsonException("not an object");

		if (root.TryGetProperty("error", out var error) && error.ValueKind == JsonValueKind.String)
			throw new ExternalToolException($"Fantomas could not run — {error.GetString()}");

		if (!root.TryGetProperty("files", out var entries) || entries.ValueKind != JsonValueKind.Array)
			throw new JsonException("no files");

		foreach (var entry in entries.EnumerateArray())
		{
			if (entry.ValueKind != JsonValueKind.Object
				|| !entry.TryGetProperty("path", out var path) || path.ValueKind != JsonValueKind.String || path.GetString() is not { } relative
				|| !entry.TryGetProperty("status", out var status) || status.ValueKind != JsonValueKind.String || status.GetString() is not { } outcome)
			{
				continue;
			}

			var message = entry.TryGetProperty("message", out var m) && m.ValueKind == JsonValueKind.String ? m.GetString() : null;
			report[fileSystem.Path.GetFullPath(fileSystem.Path.Combine(workingDirectory, relative))] = (outcome, message);
		}
	}

	private static FormatResult FromReport(Dictionary<string, (string Status, string? Message)> report, string path, bool formatted)
	{
		if (!report.TryGetValue(path, out var entry))
			return Ignored;

		return entry.Status switch
		{
			"unchanged" => Unchanged,
			"needs-formatting" or "formatted" => new FormatResult(FormatStatus.Formatted, true, null, 1, null),
			"error" when entry.Message?.Contains("could not be parsed", StringComparison.Ordinal) == true =>
				new FormatResult(FormatStatus.SyntaxError, false, null, 1, DescribeError(entry.Message, path)),
			"error" => new FormatResult(FormatStatus.VerificationFailed, false, null, 1,
				$"Fantomas refused its own output, so it was not written — {DescribeError(entry.Message, path)}"),
			_ => new FormatResult(FormatStatus.VerificationFailed, false, null, 1, $"Fantomas reported '{entry.Status}'{(formatted ? " while formatting" : "")}"),
		};
	}

	/// <summary>Splits the paths so no one command line runs past what the platform allows.</summary>
	internal static IEnumerable<string[]> Chunks(string[] paths, int? budget = null)
	{
		// Windows caps a command line at 32,767 characters; elsewhere the limit is the argument and
		// environment space together, which is far larger but not unbounded.
		var limit = budget ?? (OperatingSystem.IsWindows() ? 30_000 : 200_000);
		var chunk = new List<string>();
		var length = 0;
		foreach (var path in paths)
		{
			if (chunk.Count > 0 && length + path.Length + 3 > limit)
			{
				yield return [.. chunk];
				chunk.Clear();
				length = 0;
			}

			chunk.Add(path);
			length += path.Length + 3;
		}

		if (chunk.Count > 0)
			yield return [.. chunk];
	}

	/// <summary>
	/// The first error line of Fantomas's message, without the path it repeats: Curb already prefixes every
	/// message with the file.
	/// </summary>
	internal static string DescribeError(string? message, string path)
	{
		if (string.IsNullOrWhiteSpace(message))
			return "Fantomas gave no reason";

		string? first = null;
		foreach (var raw in message.Split('\n'))
		{
			var line = raw.TrimEnd('\r').Trim();
			if (line.Length == 0)
				continue;

			first ??= line;
			if (line.Contains(": error ", StringComparison.Ordinal))
			{
				first = line;
				break;
			}
		}

		first ??= message.Trim();
		return first.StartsWith(path, StringComparison.Ordinal) ? first[path.Length..].TrimStart(':', ' ') : first;
	}

	/// <summary>
	/// <c>daemon</c>, or <c>--daemon</c> for a Fantomas from before 8.0.0-alpha-016, which only knows the flag.
	/// </summary>
	internal static string DaemonArgument(string version) =>
		FantomasVersion.AtLeast(version, "8.0.0-alpha-016") ? "daemon" : "--daemon";

	/// <summary>Whether the daemon is the version the manifest pins, ignoring build metadata and a <c>v</c> prefix.</summary>
	internal static bool VersionMatches(string reported, string pinned)
	{
		var version = reported.Trim();
		if (version.StartsWith("Fantomas", StringComparison.OrdinalIgnoreCase))
			version = version[8..].Trim();
		if (version.StartsWith('v') || version.StartsWith('V'))
			version = version[1..];
		var plus = version.IndexOf('+');
		if (plus >= 0)
			version = version[..plus];

		var pinnedPlus = pinned.IndexOf('+');
		return string.Equals(version, pinnedPlus < 0 ? pinned : pinned[..pinnedPlus], StringComparison.OrdinalIgnoreCase);
	}
}

/// <summary>Orders NuGet versions as far as Fantomas's releases need: release numbers, then prerelease labels.</summary>
internal static class FantomasVersion
{
	/// <summary>Whether <paramref name="version"/> is <paramref name="minimum"/> or later. An unreadable version is treated as new.</summary>
	public static bool AtLeast(string version, string minimum) => Compare(version, minimum) >= 0;

	/// <summary>SemVer precedence, without build metadata: a release sorts after its prereleases, and prerelease parts compare numerically where both are numbers.</summary>
	public static int Compare(string left, string right)
	{
		var (leftRelease, leftLabel) = Split(left);
		var (rightRelease, rightLabel) = Split(right);
		if (leftRelease is null || rightRelease is null)
			return leftRelease is null ? (rightRelease is null ? 0 : 1) : -1;

		var byRelease = leftRelease.CompareTo(rightRelease);
		if (byRelease != 0)
			return byRelease;
		if (leftLabel is null || rightLabel is null)
			return leftLabel is null ? (rightLabel is null ? 0 : 1) : -1;

		var leftParts = leftLabel.Split('-', '.');
		var rightParts = rightLabel.Split('-', '.');
		for (var i = 0; i < Math.Min(leftParts.Length, rightParts.Length); i++)
		{
			var leftNumber = int.TryParse(leftParts[i], NumberStyles.None, CultureInfo.InvariantCulture, out var l);
			var rightNumber = int.TryParse(rightParts[i], NumberStyles.None, CultureInfo.InvariantCulture, out var r);
			var compared = leftNumber && rightNumber ? l.CompareTo(r)
				: leftNumber ? -1
				: rightNumber ? 1
				: string.Compare(leftParts[i], rightParts[i], StringComparison.OrdinalIgnoreCase);
			if (compared != 0)
				return compared;
		}

		return leftParts.Length.CompareTo(rightParts.Length);
	}

	private static (Version? Release, string? Label) Split(string version)
	{
		var plus = version.IndexOf('+');
		var core = (plus < 0 ? version : version[..plus]).Trim();
		var dash = core.IndexOf('-');
		var release = dash < 0 ? core : core[..dash];
		if (!Version.TryParse(release, out var parsed))
			return (null, null);
		return (new Version(parsed.Major, parsed.Minor, Math.Max(0, parsed.Build)), dash < 0 ? null : core[(dash + 1)..]);
	}
}
