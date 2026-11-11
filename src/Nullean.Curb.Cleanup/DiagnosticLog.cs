using System.Text;

namespace Nullean.Curb.Cleanup;

/// <summary>
/// Reads a build's diagnostics, whichever of the two formats they arrived in.
/// </summary>
/// <remarks>
/// Deduplication is not optional. A diagnostic appears twice in MSBuild's console output — once in the
/// build stream and once in the trailing summary — and once per target framework, so a project with two
/// target frameworks reports every site four times. Measured: five sites produced twenty console lines.
/// SARIF logs are per target framework, so a solution's logs repeat a shared file's diagnostics too.
/// </remarks>
public static class DiagnosticLog
{
	/// <summary>Reads a log, deduplicated, in the order the diagnostics were first seen.</summary>
	/// <returns>False when the bytes looked like SARIF but could not be read.</returns>
	public static bool TryRead(ReadOnlySpan<byte> utf8, out List<CleanupDiagnostic> diagnostics, out string? failure)
	{
		var seen = new HashSet<CleanupDiagnostic>();
		diagnostics = [];

		if (SarifReader.LooksLikeSarif(utf8))
		{
			if (!SarifReader.TryRead(utf8, new Deduplicating(seen, diagnostics), out failure))
			{
				// A partial read is worse than none: it would fix what the readable part of the log
				// happened to mention and call that a clean run.
				diagnostics.Clear();
				return false;
			}
		}
		else
		{
			// Not SARIF, so it is a console log. Decoded rather than read as bytes because a log is text
			// the whole way down and there is no large payload to skip past.
			BuildLogReader.Read(Encoding.UTF8.GetString(utf8), new Deduplicating(seen, diagnostics));
			failure = null;
		}

		return true;
	}

	/// <summary>
	/// Reads a log keeping which compilation reported each diagnostic, deduplicated per compilation.
	/// </summary>
	/// <remarks>
	/// What <see cref="TargetAgreement"/> needs, which <see cref="TryRead(ReadOnlySpan{byte}, out List{CleanupDiagnostic}, out string?)"/>
	/// folds away. A SARIF log is one compilation, so every diagnostic's <see cref="LoggedDiagnostic.Context"/>
	/// is null and <paramref name="contexts"/> stays empty. A console log carries every compilation of a build,
	/// each line naming its own; a site two target frameworks reported is kept once for each.
	/// </remarks>
	/// <param name="utf8">The log's bytes, SARIF or console output.</param>
	/// <param name="diagnostics">What it reported, in the order first seen.</param>
	/// <param name="contexts">Every project and target framework a console log names, whether or not it reported anything Curb owns.</param>
	/// <param name="failure">Why the log could not be read, when it could not.</param>
	/// <returns>False when the bytes looked like SARIF but could not be read.</returns>
	public static bool TryRead(ReadOnlySpan<byte> utf8, out List<LoggedDiagnostic> diagnostics, out HashSet<string> contexts, out string? failure)
	{
		var seen = new HashSet<LoggedDiagnostic>();
		diagnostics = [];
		contexts = [with(StringComparer.Ordinal)];

		if (SarifReader.LooksLikeSarif(utf8))
		{
			var plain = new List<CleanupDiagnostic>();
			if (!SarifReader.TryRead(utf8, plain, out failure))
			{
				diagnostics.Clear();
				return false;
			}

			foreach (var diagnostic in plain)
			{
				var logged = new LoggedDiagnostic(diagnostic, null);
				if (seen.Add(logged))
					diagnostics.Add(logged);
			}

			return true;
		}

		var all = new List<LoggedDiagnostic>();
		BuildLogReader.Read(Encoding.UTF8.GetString(utf8), all, contexts);
		foreach (var logged in all)
		{
			if (seen.Add(logged))
				diagnostics.Add(logged);
		}

		failure = null;
		return true;
	}

	/// <summary>Keeps only the rules <c>curb cleanup</c> attempts, leaving the rest to be reported.</summary>
	public static List<CleanupDiagnostic> Fixable(IReadOnlyList<CleanupDiagnostic> diagnostics)
	{
		var fixable = new List<CleanupDiagnostic>();
		foreach (var diagnostic in diagnostics)
		{
			if (Options.RuleCatalog.IsCleanupRule(diagnostic.RuleId))
				fixable.Add(diagnostic);
		}

		return fixable;
	}

	/// <summary>
	/// Adds to a list only what a set has not seen. A collection rather than a post-pass so the readers
	/// stay free of any opinion about duplicates.
	/// </summary>
	private sealed class Deduplicating(HashSet<CleanupDiagnostic> seen, List<CleanupDiagnostic> target) : ICollection<CleanupDiagnostic>
	{
		public void Add(CleanupDiagnostic item)
		{
			if (seen.Add(item))
				target.Add(item);
		}

		public int Count => target.Count;
		public bool IsReadOnly => false;
		public void Clear() => throw new NotSupportedException();
		public bool Contains(CleanupDiagnostic item) => seen.Contains(item);
		public void CopyTo(CleanupDiagnostic[] array, int index) => target.CopyTo(array, index);
		public bool Remove(CleanupDiagnostic item) => throw new NotSupportedException();
		public IEnumerator<CleanupDiagnostic> GetEnumerator() => target.GetEnumerator();
		System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();
	}
}

/// <summary>A diagnostic and the compilation that reported it.</summary>
/// <param name="Diagnostic">What was reported.</param>
/// <param name="Context">
/// MSBuild's <c>project::TargetFramework=tfm</c> suffix for a console log line, or null for a SARIF log, which
/// is one compilation throughout.
/// </param>
public readonly record struct LoggedDiagnostic(CleanupDiagnostic Diagnostic, string? Context);
