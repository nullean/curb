using Microsoft.CodeAnalysis.Text;

namespace Nullean.Kerf.Cleanup;

/// <summary>How loudly the build reported a diagnostic.</summary>
/// <remarks>
/// Matters for forwarding rather than for fixing. An info-level diagnostic is invisible at normal build
/// verbosity, and handing one to <c>dotnet format</c> would fix every occurrence of that rule in the file
/// on the strength of something nobody saw.
/// </remarks>
public enum DiagnosticLevel
{
	/// <summary>The log did not say. What MSBuild's console output leaves us with, since the severity word is localised.</summary>
	Unknown,

	/// <summary>Reported, but not as a problem.</summary>
	Note,

	/// <summary>A build warning.</summary>
	Warning,

	/// <summary>A build error.</summary>
	Error,
}

/// <summary>
/// One code style diagnostic a build reported, reduced to what a fix needs: which rule, which file,
/// and where.
/// </summary>
/// <remarks>
/// <para>
/// This is the whole of Kerf's input for a semantic rule. The compiler decided <em>whether</em> the
/// rule applies — that is the expensive half, and it is already paid for — so what is left is a
/// rewrite at a known position, which is syntax.
/// </para>
/// <para>
/// <see cref="End"/> is nullable because the two log formats do not carry the same information. SARIF
/// gives a full span; MSBuild's console output gives only a start. For most rules the start is enough,
/// because the position identifies a single node. IDE0005 is the exception and the reason SARIF is the
/// primary format: one diagnostic covers a whole contiguous run of unnecessary using directives, and
/// the run's extent cannot be recovered from start positions alone.
/// </para>
/// </remarks>
/// <param name="RuleId">The rule id, for example <c>IDE0005</c>.</param>
/// <param name="FilePath">Absolute path to the file. Both log formats report one.</param>
/// <param name="Start">Where the diagnostic starts. Zero-based, as <see cref="LinePosition"/> is.</param>
/// <param name="End">Where it ends, when the log said. Null when only a start was reported.</param>
/// <param name="Level">How loudly it was reported, when the log said.</param>
public readonly record struct CleanupDiagnostic(
	string RuleId,
	string FilePath,
	LinePosition Start,
	LinePosition? End = null,
	DiagnosticLevel Level = DiagnosticLevel.Unknown)
{
	/// <summary>True when the log carried a full span rather than only a start.</summary>
	public bool HasSpan => End is not null;

	/// <summary>
	/// Resolves the diagnostic against the file it describes.
	/// </summary>
	/// <remarks>
	/// Both formats count a column in UTF-16 code units with a tab as one — SARIF says so with
	/// <c>columnKind: utf16CodeUnits</c>, and MSBuild's console output was measured to agree — so this
	/// is a plain line start plus character offset, with no tab expansion.
	/// </remarks>
	/// <returns>False when the position is not inside the text, which is how a stale log is rejected.</returns>
	public bool TryResolve(SourceText text, out TextSpan span)
	{
		span = default;

		if (!TryOffset(text, Start, out var start))
			return false;

		if (End is not { } end)
		{
			span = new TextSpan(start, 0);
			return true;
		}

		if (!TryOffset(text, end, out var finish) || finish < start)
			return false;

		span = TextSpan.FromBounds(start, finish);
		return true;
	}

	private static bool TryOffset(SourceText text, LinePosition position, out int offset)
	{
		offset = 0;
		if (position.Line < 0 || position.Line >= text.Lines.Count)
			return false;

		var line = text.Lines[position.Line];

		// Against End rather than EndIncludingLineBreaks: a column may sit one past the last character,
		// which is how an exclusive end on the final token is reported, but never inside the newline.
		offset = line.Start + position.Character;
		return offset <= line.End;
	}
}
