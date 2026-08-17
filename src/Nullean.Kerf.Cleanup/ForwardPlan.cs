using Nullean.Kerf.Options;

namespace Nullean.Kerf.Cleanup;

/// <summary>One <c>dotnet format</c> invocation, covering the diagnostics Kerf left behind.</summary>
/// <param name="Subcommand"><c>style</c> for the IDE series, <c>analyzers</c> for third-party rules.</param>
/// <param name="RuleIds">Exactly the ids to fix, sorted. Never a rule the log did not report.</param>
/// <param name="Files">
/// Exactly the files the log named, sorted, <b>relative to the working directory</b>. Empty means the
/// whole project, which is what happens when a file cannot be expressed relatively.
/// </param>
public readonly record struct ForwardInvocation(
	string Subcommand,
	IReadOnlyList<string> RuleIds,
	IReadOnlyList<string> Files)
{
	/// <summary>
	/// The arguments to pass to <c>dotnet format</c>.
	/// </summary>
	/// <remarks>
	/// <para>
	/// <c>--include</c> is here for blast radius, not for speed. Measured on a 61-file project: 1.97 s with
	/// it and 1.98 s without, because the cost is the workspace load rather than applying fixes. What it
	/// does buy is that a file the build never complained about is not rewritten — checked, and a second
	/// file carrying the identical offence was left untouched. Fixing exactly what was reported is the
	/// whole premise of cleanup, so forwarding keeps it.
	/// </para>
	/// <para>
	/// The paths have to be <b>relative</b>. Measured: an absolute path matches nothing and
	/// <c>dotnet format</c> exits zero having done nothing at all — a silent no-op, which is the worst
	/// shape a bug can take here because it looks exactly like success.
	/// </para>
	/// <para>
	/// <c>--severity info</c> because a diagnostic only reaches the log at info or above, so this is the
	/// matching superset rather than a widening. <c>--no-restore</c> because the build that produced the
	/// log already restored. There is no <c>--no-build</c> to pass: <c>dotnet format</c> does not build
	/// output, it loads an MSBuild workspace, and that is the cost.
	/// </para>
	/// </remarks>
	public IReadOnlyList<string> Arguments =>
		Files.Count > 0
			? ["format", Subcommand, "--diagnostics", .. RuleIds, "--severity", "info", "--no-restore", "--include", .. Files]
			: ["format", Subcommand, "--diagnostics", .. RuleIds, "--severity", "info", "--no-restore"];

	/// <summary>The invocation as someone would type it, for reporting.</summary>
	public string CommandLine => "dotnet " + string.Join(' ', Arguments.Select(Quote));

	private static string Quote(string argument) =>
		argument.Contains(' ', StringComparison.Ordinal) ? $"\"{argument}\"" : argument;
}

/// <summary>A rule Kerf will not forward, and why.</summary>
public readonly record struct WithheldRule(string Id, string Title, string Reason);

/// <summary>What forwarding would run, and what it deliberately left out.</summary>
/// <param name="Invocations">At most one per <c>dotnet format</c> subcommand.</param>
/// <param name="Withheld">Rules Kerf will not hand to another tool either, with the reason.</param>
/// <param name="Quiet">How many diagnostics were skipped for being reported below warning.</param>
public readonly record struct ForwardResult(
	IReadOnlyList<ForwardInvocation> Invocations,
	IReadOnlyList<WithheldRule> Withheld,
	int Quiet);

/// <summary>
/// What to hand to <c>dotnet format</c> for the diagnostics Kerf did not fix.
/// </summary>
/// <remarks>
/// <para>
/// Kerf is not a replacement for <c>dotnet format style</c> and says so. What it can do is be precise
/// about the remainder: it knows every diagnostic the build reported and which of them it dealt with, so
/// it can name the rest exactly rather than leaving someone to run the whole command over the whole
/// solution.
/// </para>
/// <para>
/// Pure, so the interesting part — which ids, which files, which subcommand — is unit-testable without
/// starting a process.
/// </para>
/// </remarks>
public static class ForwardPlan
{
	/// <summary>
	/// Splits the diagnostics Kerf did not fix into invocations and refusals.
	/// </summary>
	/// <remarks>
	/// A rule Kerf refuses is still forwarded, unless the refusal reaches every tool. Most of Kerf's
	/// refusals are its own constraints — it will not delete a declaration, and it cannot derive a fix the
	/// diagnostic does not describe — and <c>dotnet format style</c> is entitled to do both. Only
	/// <see cref="RefusalScope.AnyTool"/> is withheld, which today is the two renames, where a fix compiles
	/// while changing which overload binds and breaks reflection strings no compiler check sees.
	/// </remarks>
	/// <param name="unfixed">Diagnostics from the log that Kerf did not apply a fix for.</param>
	/// <param name="workingDirectory">
	/// Where <c>dotnet format</c> will run, so file paths can be made relative to it. A file outside it
	/// cannot be named in <c>--include</c>, and rather than let it match nothing the invocation drops
	/// <c>--include</c> altogether and widens to the project.
	/// </param>
	public static ForwardResult For(IEnumerable<CleanupDiagnostic> unfixed, string workingDirectory)
	{
		var style = new SortedSet<string>(StringComparer.Ordinal);
		var analyzers = new SortedSet<string>(StringComparer.Ordinal);
		var styleFiles = new SortedSet<string>(StringComparer.Ordinal);
		var analyzerFiles = new SortedSet<string>(StringComparer.Ordinal);
		var withheld = new Dictionary<string, WithheldRule>(StringComparer.OrdinalIgnoreCase);
		var unscoped = false;
		var quiet = 0;

		foreach (var diagnostic in unfixed)
		{
			// Only a refusal that reaches every tool. "Kerf will not delete a declaration" is Kerf's own
			// constraint, and `dotnet format style` does it properly — withholding it from someone who
			// escalated the rule *and* asked to forward would be Kerf imposing its limits on another tool.
			if (RuleCatalog.Find(diagnostic.RuleId) is { Owner: RuleOwner.Never, Scope: RefusalScope.AnyTool } refused)
			{
				withheld.TryAdd(refused.Id, new WithheldRule(refused.Id, refused.Title, refused.Refusal ?? ""));
				continue;
			}

			// Reported, but not as a problem. Forwarding it would have `dotnet format` fix every occurrence
			// of that rule in the file on the strength of something invisible at normal build verbosity —
			// a wider blast radius than Kerf's own span-local fixes, taken for a weaker reason. The .NET
			// analysers are on by default at this level, so without the gate `--forward` would quietly start
			// applying CA suggestions nobody asked about.
			if (diagnostic.Level == DiagnosticLevel.Note)
			{
				quiet++;
				continue;
			}

			// A compiler warning is not a code style rule, and `dotnet format` cannot fix one. Passing it
			// would be asking for a no-op that looks like a failure.
			if (diagnostic.RuleId.StartsWith("CS", StringComparison.Ordinal))
				continue;

			var relative = Relativise(workingDirectory, diagnostic.FilePath);
			if (relative is null)
				unscoped = true;

			if (diagnostic.RuleId.StartsWith("IDE", StringComparison.Ordinal))
			{
				style.Add(diagnostic.RuleId);
				if (relative is not null)
					styleFiles.Add(relative);
			}
			else
			{
				analyzers.Add(diagnostic.RuleId);
				if (relative is not null)
					analyzerFiles.Add(relative);
			}
		}

		var invocations = new List<ForwardInvocation>();

		// One un-includable file widens the whole invocation, because a partial `--include` would silently
		// skip it while looking like a complete run.
		if (style.Count > 0)
			invocations.Add(new ForwardInvocation("style", [.. style], unscoped ? [] : [.. styleFiles]));

		if (analyzers.Count > 0)
			invocations.Add(new ForwardInvocation("analyzers", [.. analyzers], unscoped ? [] : [.. analyzerFiles]));

		return new ForwardResult([.. invocations], [.. withheld.Values], quiet);
	}

	/// <summary>The path relative to <paramref name="workingDirectory"/>, or null when it cannot be one.</summary>
	private static string? Relativise(string workingDirectory, string path)
	{
		var relative = Path.GetRelativePath(workingDirectory, path);

		// GetRelativePath hands back the absolute path when the two share no root, and a `..` prefix when
		// the file sits outside. Neither is something `--include` matches.
		return Path.IsPathRooted(relative) || relative.StartsWith("..", StringComparison.Ordinal) ? null : relative;
	}
}
