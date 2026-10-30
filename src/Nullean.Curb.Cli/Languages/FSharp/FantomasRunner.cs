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
/// in flight. Measured against one <c>fantomas</c> batch process: equal from 100 files up and faster at
/// 1000; about 90 ms dearer for a handful of files, which the cache makes the rare case.
/// </para>
/// <para>
/// Curb never writes what Fantomas hands back on the strength of one answer. The daemon does not validate
/// its own output (the CLI does), and on a 1000-file corpus it returned 15 invalid and 9 unstable results as
/// <c>Formatted</c>. So every <c>Formatted</c> result is sent back, and only an <c>Unchanged</c> answer to
/// that lets it through: an error means the output does not parse, another <c>Formatted</c> means it is not
/// a fixed point, and either way the file is failed and left as it was. That costs one more request for
/// each file that needed formatting, and nothing for one that did not.
/// </para>
/// <para>
/// When the daemon cannot be started or does not answer its handshake, the batch CLI is the fallback: one
/// process per chunk of files sized to the command line, never one per file.
/// </para>
/// </remarks>
internal sealed class FantomasRunner(IFileSystem fileSystem, IToolLauncher launcher, ToolEnvironment environment, Action<string> warn)
{
	/// <summary>How long a daemon gets to answer its first request. Generous, because the first start of a tool pays for the JIT and a cold disk.</summary>
	private static readonly TimeSpan HandshakeTimeout = TimeSpan.FromSeconds(60);

	/// <summary>Set to <c>false</c> to skip the daemon and use the batch CLI. For measurement and for a daemon that misbehaves.</summary>
	internal const string DaemonVariable = "CURB_FANTOMAS_DAEMON";

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

		foreach (var (manifest, indices) in groups)
		{
			var resolution = resolver.Resolve(manifest);
			if (resolution.Tool is not { } tool)
				throw new ExternalToolException(resolution.Problem!);

			var useDaemon = !string.Equals(environment.GetVariable(DaemonVariable), "false", StringComparison.OrdinalIgnoreCase);
			if (!useDaemon || !await TryDaemonAsync(tool, files, indices, results, produceText, cancellationToken).ConfigureAwait(false))
				await BatchAsync(tool, files, indices, results, produceText, cancellationToken).ConfigureAwait(false);
		}

		return results;
	}

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
				version = await client.VersionAsync(cancellationToken).WaitAsync(HandshakeTimeout, cancellationToken).ConfigureAwait(false);
			}
			catch (Exception exception) when (exception is FantomasDaemonException or TimeoutException)
			{
				warn($"the Fantomas daemon did not answer, using the Fantomas command line instead — {exception.Message}");
				return false;
			}

			if (!VersionMatches(version, tool.Manifest.Version))
			{
				throw new ExternalToolException(
					$"{tool.Manifest.ManifestPath} pins {tool.Manifest.ToolId} {tool.Manifest.Version}, but the Fantomas that started is {version}. Run `dotnet tool restore`.");
			}

			await Parallel.ForEachAsync(
				indices,
				new ParallelOptions { MaxDegreeOfParallelism = Environment.ProcessorCount, CancellationToken = cancellationToken },
				async (index, token) => results[index] = await FormatOneAsync(client, files[index], produceText, token).ConfigureAwait(false)
			).ConfigureAwait(false);
		}

		return true;
	}

	private async Task<FormatResult> FormatOneAsync(FantomasDaemonClient client, ExternalFile file, bool produceText, CancellationToken cancellationToken)
	{
		var path = fileSystem.Path.GetFullPath(file.Path);
		try
		{
			var reply = await client.FormatAsync(path, file.Source, cancellationToken).ConfigureAwait(false);
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
			var recheck = await client.FormatAsync(path, formatted, cancellationToken).ConfigureAwait(false);
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

	private static FormatResult Unchanged => new(FormatStatus.Formatted, false, null, 1, null);

	private static FormatResult Ignored => new(FormatStatus.Ignored, false, null, 1, null);

	private static FormatResult Unstable => new(FormatStatus.VerificationFailed, false, null, 1,
		"Fantomas's output is not stable (formatting it again changes it again), so it was not written");

	/// <summary>
	/// The batch CLI: <c>check --json</c> to check, and to format, a formatting pass, a <c>check</c> of what
	/// it wrote, and the original bytes put back so Curb does the writing.
	/// </summary>
	/// <remarks>
	/// The CLI can only format in place. So it is let, the files it changed are read back and checked again
	/// for stability, and every one of them is restored before this returns; the run then writes the stable
	/// ones itself, with its own byte-order mark, exactly as it does for the daemon. A crash between the two
	/// leaves Fantomas's validated output in place, which is a formatted file rather than a damaged one.
	/// </remarks>
	private async Task BatchAsync(
		FantomasTool tool, IReadOnlyList<ExternalFile> files, List<int> indices, FormatResult[] results, bool produceText, CancellationToken cancellationToken)
	{
		var paths = indices.Select(i => fileSystem.Path.GetFullPath(files[i].Path)).ToArray();

		if (!produceText)
		{
			var report = await RunCliAsync(tool, check: true, paths, cancellationToken).ConfigureAwait(false);
			for (var k = 0; k < indices.Count; k++)
				results[indices[k]] = FromReport(report, paths[k], formatted: false);
			return;
		}

		var formatReport = await RunCliAsync(tool, check: false, paths, cancellationToken).ConfigureAwait(false);
		var written = new List<int>();
		var contents = new Dictionary<int, string>();
		try
		{
			for (var k = 0; k < indices.Count; k++)
			{
				var result = FromReport(formatReport, paths[k], formatted: true);
				results[indices[k]] = result;
				if (result.Changed)
				{
					written.Add(k);
					var bytes = fileSystem.File.ReadAllBytes(paths[k]);
					var hasBom = bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF;
					contents[k] = FormattingRun.StrictUtf8.GetString(hasBom ? bytes.AsSpan(3) : bytes);
				}
			}

			if (written.Count > 0)
			{
				var recheck = await RunCliAsync(tool, check: true, [.. written.Select(k => paths[k])], cancellationToken).ConfigureAwait(false);
				foreach (var k in written)
				{
					var second = FromReport(recheck, paths[k], formatted: false);
					results[indices[k]] = second is { Status: FormatStatus.Formatted, Changed: false }
						? new FormatResult(FormatStatus.Formatted, true, contents[k], 1, null)
						: second.Status == FormatStatus.Formatted ? Unstable
						: new FormatResult(FormatStatus.VerificationFailed, false, null, 1, $"Fantomas could not check its own output — {second.Message}");
				}
			}
		}
		catch (Exception exception) when (exception is DecoderFallbackException or IOException or UnauthorizedAccessException)
		{
			foreach (var k in written)
				results[indices[k]] = new FormatResult(FormatStatus.VerificationFailed, false, null, 1, $"could not read back what Fantomas wrote — {exception.Message}");
		}
		finally
		{
			// Every file the CLI touched goes back to the bytes it had, stable or not: what reaches disk is the
			// run's decision, made with the run's byte-order mark.
			foreach (var k in written)
				fileSystem.File.WriteAllBytes(paths[k], files[indices[k]].Bytes.ToArray());
		}
	}

	/// <summary>What the CLI reported for each file, by full path. A file it left out was ignored.</summary>
	private async Task<Dictionary<string, (string Status, string? Message)>> RunCliAsync(
		FantomasTool tool, bool check, string[] paths, CancellationToken cancellationToken)
	{
		var report = new Dictionary<string, (string, string?)>(OperatingSystem.IsLinux() ? StringComparer.Ordinal : StringComparer.OrdinalIgnoreCase);
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

			string output;
			using (process)
			{
				process.StandardInput.Dispose();
				using var reader = new StreamReader(process.StandardOutput, Encoding.UTF8);
				output = await reader.ReadToEndAsync(cancellationToken).ConfigureAwait(false);
				await process.Exited.ConfigureAwait(false);

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
		if (root.TryGetProperty("error", out var error) && error.ValueKind == JsonValueKind.String)
			throw new ExternalToolException($"Fantomas could not run — {error.GetString()}");

		if (!root.TryGetProperty("files", out var entries) || entries.ValueKind != JsonValueKind.Array)
			throw new JsonException("no files");

		foreach (var entry in entries.EnumerateArray())
		{
			if (!entry.TryGetProperty("path", out var path) || path.GetString() is not { } relative
				|| !entry.TryGetProperty("status", out var status) || status.GetString() is not { } outcome)
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
	internal static string DaemonArgument(string version)
	{
		var plus = version.IndexOf('+');
		var core = plus < 0 ? version : version[..plus];
		var dash = core.IndexOf('-');
		var release = dash < 0 ? core : core[..dash];
		var prerelease = dash < 0 ? null : core[(dash + 1)..];

		if (!Version.TryParse(release, out var parsed))
			return "daemon";
		if (parsed.Major != 8)
			return parsed.Major > 8 ? "daemon" : "--daemon";
		if (parsed.Minor > 0 || parsed.Build > 0 || prerelease is null)
			return "daemon";

		// 8.0.0 prereleases: alpha-016 is the first with the command.
		if (prerelease.StartsWith("alpha-", StringComparison.OrdinalIgnoreCase)
			&& int.TryParse(prerelease.AsSpan(6), NumberStyles.None, CultureInfo.InvariantCulture, out var alpha))
		{
			return alpha >= 16 ? "daemon" : "--daemon";
		}

		return "daemon";
	}

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
