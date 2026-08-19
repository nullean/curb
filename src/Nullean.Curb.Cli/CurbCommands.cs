using System.ComponentModel.DataAnnotations;
using System.IO.Abstractions;
using Nullean.Argh;
using Nullean.Curb.EditorConfig;
using Nullean.Curb.Options;

namespace Nullean.Curb.Cli;

/// <summary>
/// The command surface. One public method per <c>curb</c> command; <see cref="Nullean.Argh.ArghApp.Map{T}"/>
/// turns each into a command named from its kebab-cased method name, its parameters into flags and
/// positionals, and its XML doc into <c>--help</c> text.
/// </summary>
/// <remarks>
/// <see cref="ExistingAttribute"/> below bypasses <see cref="IFileSystem"/> (it emits a raw
/// <c>File.Exists</c>/<c>Directory.Exists</c>) — a deliberate exception confined to this parsing
/// boundary, which no test drives through <c>MockFileSystem</c> anyway.
/// </remarks>
internal sealed class CurbCommands
{
	private readonly FileSystem _fileSystem = new();

	/// <summary>Format files in place.</summary>
	/// <param name="path">A file or directory to format.</param>
	/// <param name="files">-f, --files, Format only these, instead of walking <paramref name="path"/>. May be given more than once.</param>
	/// <param name="msbuildListFile">
	/// What the MSBuild integration passes instead of <paramref name="files"/>: a file listing one path
	/// per line, since a project's compile set can be too long for a command line to hold directly.
	/// </param>
	/// <param name="cache">-c, --cache, Skip files a previous run watched format to themselves.</param>
	/// <param name="noVerify">Skip re-parsing output to prove the token stream is unchanged.</param>
	public int Format(
		[Argument] string path = ".",
		List<FileInfo>? files = null,
		[Existing] FileInfo? msbuildListFile = null,
		string? cache = null,
		bool noVerify = false
	) =>
		FormattingRun.Execute(_fileSystem, path, write: true, verify: !noVerify, explicitFiles: ResolveExplicitFiles(files, msbuildListFile), cachePath: cache).ExitCode;

	/// <summary>Exit non-zero if anything would change.</summary>
	/// <param name="path">A file or directory to check.</param>
	/// <param name="files">-f, --files, Check only these, instead of walking <paramref name="path"/>. May be given more than once.</param>
	/// <param name="msbuildListFile">
	/// What the MSBuild integration passes instead of <paramref name="files"/>: a file listing one path
	/// per line, since a project's compile set can be too long for a command line to hold directly.
	/// </param>
	/// <param name="cache">-c, --cache, Skip files a previous run watched format to themselves.</param>
	/// <param name="noVerify">Skip re-parsing output to prove the token stream is unchanged.</param>
	/// <param name="expandUnhandled">Benchmark-only cost model: pretend every syntax kind has a dedicated printer.</param>
	/// <param name="coverage">Report which syntax kinds are still emitted verbatim.</param>
	public int Check(
		[Argument] string path = ".",
		List<FileInfo>? files = null,
		[Existing] FileInfo? msbuildListFile = null,
		string? cache = null,
		bool noVerify = false,
		bool expandUnhandled = false,
		bool coverage = false
	) =>
		FormattingRun.Execute(
			_fileSystem,
			path,
			write: false,
			expandUnhandled: expandUnhandled,
			verify: !noVerify,
			coverageReport: coverage,
			explicitFiles: ResolveExplicitFiles(files, msbuildListFile),
			cachePath: cache
		).ExitCode;

	/// <summary>
	/// Apply the code style fixes a build already reported.
	/// </summary>
	/// <remarks>
	/// Cleanup never builds. Build first, then run it: <c>dotnet build &amp;&amp; curb cleanup</c>.
	/// </remarks>
	/// <param name="path">Where to look for <c>curb.sarif</c> logs, and which files to clean.</param>
	/// <param name="sarifLogs">
	/// -s, --sarif-log, The log to read, instead of searching <paramref name="path"/> for
	/// <c>curb.sarif</c>. May be given more than once, so a solution's per-project logs can all be
	/// handed over in one run.
	/// </param>
	/// <param name="files">-f, --files, Restrict cleanup to these, instead of every file the logs mention. May be given more than once.</param>
	/// <param name="msbuildListFile">
	/// What the MSBuild integration passes instead of <paramref name="files"/>: a file listing one path
	/// per line, since a project's compile set can be too long for a command line to hold directly.
	/// </param>
	/// <param name="check">Report what would be fixed, change nothing.</param>
	/// <param name="forward">
	/// Hand what Curb cannot fix to <c>dotnet format</c>, scoped to the reported rules and files, and
	/// report both timings.
	/// </param>
	public int Cleanup(
		[Argument] string path = ".",
		List<string>? sarifLogs = null,
		List<FileInfo>? files = null,
		[Existing] FileInfo? msbuildListFile = null,
		bool check = false,
		bool forward = false
	) =>
		CleanupRun.Execute(_fileSystem, path, write: !check, logs: sarifLogs?.ToArray(), explicitFiles: ResolveExplicitFiles(files, msbuildListFile), forward: forward);

	/// <summary>Show which code style rules Curb fixes, and which it does not.</summary>
	/// <param name="cleanupIds">Print only the ids <c>curb cleanup</c> fixes, space separated, for scripting.</param>
	public static int Rules(bool cleanupIds = false)
	{
		if (cleanupIds)
		{
			// Machine-readable, so the build scripts do not carry a second copy of the list that can
			// drift from the catalog.
			Console.WriteLine(string.Join(' ', RuleCatalog.All.Where(entry => entry.Owner == RuleOwner.Cleanup).Select(entry => entry.Id)));

			return 0;
		}

		// Driven by the catalog rather than a hand-written list, for the same reason print-config is:
		// a rule Curb stops fixing, or starts fixing, should change this output without anyone
		// remembering to edit it.
		var groups = new (RuleOwner Owner, string Heading)[]
		{
			(RuleOwner.Cleanup, "fixed by `curb cleanup`, from a diagnostic your build reported"),
			(RuleOwner.Formatting, "already satisfied by `curb format`, before the compiler looks"),
			(RuleOwner.Never, "never fixed by Curb — `--forward` still hands on the ones another tool may do"),
			(RuleOwner.DotnetFormatStyle, "not Curb's — use `dotnet format style`"),
		};

		foreach (var (owner, heading) in groups)
		{
			var entries = RuleCatalog.All.Where(entry => entry.Owner == owner).ToArray();
			Console.WriteLine($"# {entries.Length} rule(s) {heading}");

			foreach (var entry in entries)
			{
				Console.WriteLine($"  {entry.Id}  {entry.Title}");
				if (entry.Refusal is { } refusal)
				{
					// The scope is the part that decides what --forward does with it, so it is said out
					// loud rather than left for someone to infer from the wording.
					var scope = entry.Scope == RefusalScope.AnyTool ? "no tool, unattended" : "not Curb";
					Console.WriteLine($"            [{scope}] {refusal}");
				}
			}

			Console.WriteLine();
		}

		return 0;
	}

	/// <summary>Dump the document IR for a file.</summary>
	/// <param name="path">The C# file to dump.</param>
	public int DocTree([Argument][Existing][FileExtensions(Extensions = "cs")] FileInfo path)
	{
		var full = _fileSystem.Path.GetFullPath(path.FullName);
		var options = EditorConfigOptionsBinder.Bind(new CurbEditorConfig(_fileSystem).For(full));
		using var formatter = new CSharpFormatter();
		Console.WriteLine(formatter.DumpDocumentTree(_fileSystem.File.ReadAllText(full), options));
		return 0;
	}

	/// <summary>Show the resolved options and any diagnostics for a file.</summary>
	/// <param name="path">The C# file to resolve options for.</param>
	public int PrintConfig([Argument][Existing][FileExtensions(Extensions = "cs")] FileInfo path)
	{
		var full = _fileSystem.Path.GetFullPath(path.FullName);
		var config = new CurbEditorConfig(_fileSystem).For(full);

		var diagnostics = new List<CurbDiagnostic>();
		var options = EditorConfigOptionsBinder.Bind(config, diagnostics);

		// Driven by the catalog rather than a hand-written list, so the command reports every
		// option Curb implements and cannot quietly fall behind as more are onboarded.
		var keys = OptionCatalog.ImplementedKeys.Order(StringComparer.Ordinal).ToArray();
		var width = keys.Max(key => key.Length);

		Console.WriteLine($"# {full}");
		Console.WriteLine();
		var implementedFormatting = OptionCatalog.FormattingKeys.Count(OptionCatalog.IsImplemented);
		Console.WriteLine(
			$"# resolved — {implementedFormatting} of {OptionCatalog.FormattingKeys.Count} IDE0055 options " +
				$"implemented, plus the {OptionCatalog.CoreKeys.Count} core keys"
		);
		Console.WriteLine();

		// Capped, so one long list value does not push every comment off to the right.
		var valueWidth = Math.Min(22, keys.Max(key => (OptionValues.Of(options, key) ?? "").Length));
		foreach (var key in keys)
		{
			var value = OptionValues.Of(options, key) ?? "";
			// A value someone actually set is the one worth looking at, so mark the rest.
			var origin = config.Properties.ContainsKey(key) ? "" : "  # default";
			Console.WriteLine($"{key.PadRight(width)} = {value.PadRight(origin.Length > 0 ? valueWidth : 0)}{origin}");
		}

		var unimplemented = OptionCatalog.FormattingKeys.Where(key => !OptionCatalog.IsImplemented(key)).Order(StringComparer.Ordinal).ToArray();

		if (unimplemented.Length > 0)
		{
			Console.WriteLine();
			Console.WriteLine($"# {unimplemented.Length} known option(s) not implemented yet");
			foreach (var key in unimplemented)
			{
				Console.WriteLine($"# {key}");
			}
		}

		if (diagnostics.Count > 0)
		{
			Console.WriteLine();
			Console.WriteLine($"# {diagnostics.Count} diagnostic(s)");
			foreach (var diagnostic in diagnostics.OrderBy(d => d.Id, StringComparer.Ordinal))
			{
				Console.WriteLine(diagnostic.ToString());
			}
		}

		return 0;
	}

	/// <summary>
	/// Merges <c>--files</c> with the contents of <c>--msbuild-list-file</c> (one path per line, blank
	/// lines dropped) into the array <see cref="FormattingRun"/> and <see cref="CleanupRun"/> expect.
	/// <see langword="null"/> when neither was given, which both runs read as "walk the target instead".
	/// </summary>
	private string[]? ResolveExplicitFiles(List<FileInfo>? files, FileInfo? msbuildListFile)
	{
		if (files is null && msbuildListFile is null)
			return null;

		var fromListFile = msbuildListFile is null
			? []
			: _fileSystem.File.ReadAllLines(msbuildListFile.FullName)
				.Select(line => line.Trim())
				.Where(line => line.Length > 0)
				.Select(_fileSystem.Path.GetFullPath);

		return [.. files?.Select(f => f.FullName) ?? [], .. fromListFile];
	}
}
