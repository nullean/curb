using Microsoft.CodeAnalysis.Text;

namespace Nullean.Curb.Cleanup;

/// <summary>One compilation a log describes: a project built for one target framework.</summary>
/// <param name="Id">Unique per compilation: the log's path for SARIF, the log and MSBuild's suffix for a console log.</param>
/// <param name="Group">
/// Shared by the compilations that are the same project built for different target frameworks. Two
/// compilations in one group are assumed to compile the same files unless a compile list says otherwise.
/// </param>
/// <param name="Label">How a reason names it: the target framework where one is known.</param>
public readonly record struct Compilation(string Id, string Group, string Label)
{
	/// <summary>The compilation a SARIF log describes, placed in a group by its path.</summary>
	/// <remarks>
	/// <para>
	/// A SARIF log does not say which project or target framework wrote it; only its path does. The MSBuild
	/// integration writes <c>$(IntermediateOutputPath)curb.sarif</c>, which puts the target framework in a
	/// directory of its own: <c>obj/Debug/net10.0/</c> by default, <c>artifacts/obj/Proj/debug_net10.0/</c> under
	/// the artifacts layout, with a runtime identifier possibly below it. The nearest directory that reads as a
	/// target framework is replaced with a wildcard, and logs whose paths then match are one project's
	/// frameworks.
	/// </para>
	/// <para>
	/// A log with no such directory is a group of its own, which is the behaviour before frameworks were
	/// compared at all. Two unrelated logs are never grouped by accident: a group only ever adds frameworks
	/// that must agree.
	/// </para>
	/// </remarks>
	public static Compilation ForLog(string logPath)
	{
		var segments = logPath.Split('/', '\\');

		// The file name is the last segment; a target framework is a directory above it.
		for (var i = segments.Length - 2; i >= 0; i--)
		{
			if (!TryReadFramework(segments[i], out var prefix, out var framework))
				continue;

			var group = (string[])segments.Clone();
			group[i] = prefix + "*";
			return new Compilation(logPath, string.Join('/', group), framework);
		}

		return new Compilation(logPath, logPath, logPath);
	}

	/// <summary>The compilation one line of a console log names, from MSBuild's suffix.</summary>
	/// <param name="logPath">The log the line came from, so two logs' suffixes stay apart.</param>
	/// <param name="context"><c>/repo/e1.csproj::TargetFramework=net10.0</c>, or just the project.</param>
	public static Compilation ForConsole(string logPath, string context)
	{
		const string marker = "::TargetFramework=";
		var at = context.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
		var project = at < 0 ? context : context[..at];
		var label = at < 0 ? project : context[(at + marker.Length)..];

		return new Compilation(logPath + "|" + context, logPath + "|" + project, label);
	}

	/// <summary>
	/// Whether a directory name is a target framework moniker, optionally after a configuration prefix
	/// (<c>debug_net10.0</c>).
	/// </summary>
	internal static bool TryReadFramework(string segment, out string prefix, out string framework)
	{
		prefix = "";
		framework = segment;

		var underscore = segment.LastIndexOf('_');
		if (underscore >= 0)
		{
			prefix = segment[..(underscore + 1)];
			framework = segment[(underscore + 1)..];
		}

		return IsFramework(framework);
	}

	/// <summary>
	/// <c>net10.0</c>, <c>net48</c>, <c>netstandard2.0</c>, <c>netcoreapp3.1</c>, <c>net8.0-windows</c>: a
	/// known prefix, then a version that starts with a digit.
	/// </summary>
	private static bool IsFramework(string name)
	{
		foreach (var known in (ReadOnlySpan<string>)["netstandard", "netcoreapp", "net"])
		{
			if (name.Length > known.Length
				&& name.StartsWith(known, StringComparison.OrdinalIgnoreCase)
				&& char.IsAsciiDigit(name[known.Length]))
			{
				return true;
			}
		}

		return false;
	}
}

/// <summary>
/// Applies a site only when every target framework that compiled its file reported it.
/// </summary>
/// <remarks>
/// <para>
/// A multi-targeted project compiles each file once per target framework, and each compilation reaches its
/// own verdict. Usually they agree. When they do not, the fix one framework asks for can break another:
/// <c>net10.0</c> reports a using directive unnecessary (IDE0005) because <c>string.Contains(char)</c> exists
/// there, while <c>netstandard2.0</c> needs the directive for the extension method that fills the gap.
/// Removing it breaks the <c>netstandard2.0</c> build. A language-version or nullable setting that differs by
/// framework does the same to IDE0090, IDE0034 or IDE0240. Found on the generated corpus's
/// <c>Corpus.Abstractions</c> (<c>netstandard2.0;net10.0</c>); issue #189.
/// </para>
/// <para>
/// The rule: a site, meaning rule, file, start and end, is applied once, and only when every compilation
/// that read its file reported it. A compilation read a file when
/// </para>
/// <list type="bullet">
/// <item>its log reported anything at all in that file, any rule at any severity; or</item>
/// <item>its compile list (<c>curb.files</c>, which the MSBuild integration writes beside the log) names the
/// file; or</item>
/// <item>it has no compile list and is the same project, built for another framework, as a compilation that
/// read the file.</item>
/// </list>
/// <para>
/// The third is the conservative case. A log that says nothing about a file may have compiled it and found
/// nothing, or may never have seen it (a <c>Compile</c> item with a framework condition). Without a compile
/// list the two cannot be told apart, so it is assumed to have compiled the file and its silence counts as
/// disagreement. With one, a file only one framework compiles is that framework's alone, and its sites apply.
/// </para>
/// <para>
/// A single-target project is a group of one, so every site it reports agrees with itself and nothing changes
/// for it. A site every framework reported is applied once, which is the cross-log de-duplication #178 added.
/// </para>
/// </remarks>
public sealed class TargetAgreement
{
	private readonly Dictionary<string, Compilation> _compilations = new(StringComparer.Ordinal);
	private readonly Dictionary<string, List<string>> _groups = new(StringComparer.Ordinal);
	private readonly Dictionary<string, HashSet<string>> _compiled = new(StringComparer.Ordinal);

	/// <summary>Per file, the compilations whose log mentioned it.</summary>
	private readonly Dictionary<string, HashSet<string>> _mentions = new(StringComparer.Ordinal);

	/// <summary>Per compilation, the files it reported a fixable site in, to check its compile list against.</summary>
	private readonly Dictionary<string, HashSet<string>> _fixableFiles = new(StringComparer.Ordinal);

	/// <summary>Every fixable site, in the order first seen, with the first report and who reported it.</summary>
	private readonly Dictionary<Site, (CleanupDiagnostic First, HashSet<string> By)> _sites = [];

	private readonly List<Site> _order = [];

	/// <summary>Records that a compilation exists, whether or not it reported anything.</summary>
	public void Declare(in Compilation compilation)
	{
		if (_compilations.TryAdd(compilation.Id, compilation))
		{
			if (!_groups.TryGetValue(compilation.Group, out var members))
				_groups[compilation.Group] = members = [];

			members.Add(compilation.Id);
		}
	}

	/// <summary>Records the files a compilation was given, from the compile list written beside its log.</summary>
	public void Compiled(in Compilation compilation, IEnumerable<string> files)
	{
		Declare(compilation);

		if (!_compiled.TryGetValue(compilation.Id, out var set))
			_compiled[compilation.Id] = set = new HashSet<string>(StringComparer.Ordinal);

		set.UnionWith(files);
	}

	/// <summary>Records one diagnostic a compilation reported, fixable or not: either way it proves the file was read.</summary>
	public void Report(in Compilation compilation, in CleanupDiagnostic diagnostic)
	{
		Declare(compilation);

		if (!_mentions.TryGetValue(diagnostic.FilePath, out var by))
			_mentions[diagnostic.FilePath] = by = new HashSet<string>(StringComparer.Ordinal);

		by.Add(compilation.Id);

		if (!Options.RuleCatalog.IsCleanupRule(diagnostic.RuleId))
			return;

		if (!_fixableFiles.TryGetValue(compilation.Id, out var files))
			_fixableFiles[compilation.Id] = files = new HashSet<string>(StringComparer.Ordinal);

		files.Add(diagnostic.FilePath);

		var site = Site.Of(diagnostic);
		if (!_sites.TryGetValue(site, out var entry))
		{
			_sites[site] = entry = (diagnostic, new HashSet<string>(StringComparer.Ordinal));
			_order.Add(site);
		}

		entry.By.Add(compilation.Id);
	}

	/// <summary>Splits every fixable site into the ones to apply, per file, and the ones declined with a reason.</summary>
	public TargetVerdict Resolve()
	{
		var agreed = new Dictionary<string, List<CleanupDiagnostic>>(StringComparer.Ordinal);
		var declined = new List<DeclinedSite>();
		var readers = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);

		foreach (var site in _order)
		{
			var (first, by) = _sites[site];

			if (!readers.TryGetValue(first.FilePath, out var read))
				readers[first.FilePath] = read = Readers(first.FilePath);

			var silent = read.Where(id => !by.Contains(id)).ToArray();
			if (silent.Length == 0)
			{
				if (!agreed.TryGetValue(first.FilePath, out var forFile))
					agreed[first.FilePath] = forFile = [];

				forFile.Add(first);
				continue;
			}

			var reported = string.Join(", ", by.Select(Label).Order(StringComparer.Ordinal));
			var notReported = string.Join(", ", silent.Select(Label).Order(StringComparer.Ordinal));
			declined.Add(new DeclinedSite(first,
				$"{first.RuleId}: reported for {reported} but not for {notReported}, which compiles this file too, "
				+ "so fixing it could break that build"));
		}

		return new TargetVerdict(agreed, declined);
	}

	/// <summary>The compilations that read a file, by the three rules in the remarks.</summary>
	private HashSet<string> Readers(string file)
	{
		var readers = new HashSet<string>(StringComparer.Ordinal);

		if (_mentions.TryGetValue(file, out var mentioned))
			readers.UnionWith(mentioned);

		foreach (var (id, files) in _compiled)
		{
			if (Trusted(id) && files.Contains(file))
				readers.Add(id);
		}

		// Siblings of every reader found so far that cannot say whether they compiled the file.
		foreach (var reader in readers.ToArray())
		{
			foreach (var sibling in _groups[_compilations[reader].Group])
			{
				if (!Trusted(sibling))
					readers.Add(sibling);
			}
		}

		return readers;
	}

	/// <summary>
	/// Whether a compilation's compile list can be believed. Absent, it cannot. Present, it must name every
	/// file its own log reported a fixable site in. A list that misses one spells paths differently from the
	/// log or is stale, and believing it would excuse that compilation from files it did compile.
	/// </summary>
	private bool Trusted(string id)
	{
		if (!_compiled.TryGetValue(id, out var files))
			return false;

		return !_fixableFiles.TryGetValue(id, out var reported) || reported.IsSubsetOf(files);
	}

	private string Label(string id) => _compilations[id].Label;

	/// <summary>A site, keyed regardless of severity: another log reporting it at another level is the same verdict.</summary>
	private readonly record struct Site(string RuleId, string FilePath, LinePosition Start, LinePosition? End)
	{
		public static Site Of(in CleanupDiagnostic diagnostic) =>
			new(diagnostic.RuleId.ToUpperInvariant(), diagnostic.FilePath, diagnostic.Start, diagnostic.End);
	}
}

/// <summary>What <see cref="TargetAgreement.Resolve"/> decided.</summary>
/// <param name="Agreed">Per file, one diagnostic per site every compilation that read the file reported.</param>
/// <param name="Declined">Sites some of those compilations did not report, each with the reason.</param>
public readonly record struct TargetVerdict(
	IReadOnlyDictionary<string, List<CleanupDiagnostic>> Agreed,
	IReadOnlyList<DeclinedSite> Declined);

/// <summary>A site left alone because the target frameworks disagreed about it.</summary>
/// <param name="Diagnostic">The first report of the site.</param>
/// <param name="Reason">Which frameworks reported it and which did not.</param>
public readonly record struct DeclinedSite(CleanupDiagnostic Diagnostic, string Reason);
