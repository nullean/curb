using System.Text;
using Microsoft.CodeAnalysis.Text;
using Nullean.Kerf.Cleanup.Rules;
using Nullean.Kerf.Verification;

namespace Nullean.Kerf.Cleanup;

/// <summary>Why a file was not cleaned.</summary>
public enum CleanupStatus
{
	/// <summary>Cleaned successfully. <see cref="CleanupResult.Changed"/> says whether anything moved.</summary>
	Cleaned,

	/// <summary>The source does not parse. Kerf never rewrites from a recovered tree.</summary>
	SyntaxError,

	/// <summary>The fixes produced output that lost or altered content, so all of them were discarded.</summary>
	VerificationFailed,
}

/// <summary>The outcome of cleaning one file.</summary>
/// <param name="Status">Whether it worked, and if not, why.</param>
/// <param name="Changed">True when the output differs from the input.</param>
/// <param name="Text">The cleaned text, or null when nothing changed or nothing was produced.</param>
/// <param name="Applied">How many diagnostics were fixed.</param>
/// <param name="Refusals">Why each unfixed diagnostic was left alone. Never an error — see <see cref="ICleanupRule"/>.</param>
/// <param name="Unfixed">
/// The diagnostics handed over that no fix was applied for, so a caller can forward them somewhere that
/// can. Parallel to <paramref name="Refusals"/>, which carries the prose.
/// </param>
/// <param name="Message">Detail for a non-successful status.</param>
public readonly record struct CleanupResult(
	CleanupStatus Status,
	bool Changed,
	string? Text,
	int Applied,
	IReadOnlyList<string> Refusals,
	IReadOnlyList<CleanupDiagnostic> Unfixed,
	string? Message)
{
	public bool Success => Status == CleanupStatus.Cleaned;
}

/// <summary>
/// Applies code style fixes to one file, from diagnostics a build already reported.
/// </summary>
/// <remarks>
/// <para>
/// Nothing here loads a compilation, resolves a reference or binds anything. The compiler decided
/// whether each rule applies — that is the expensive half and it is already paid for — so what is left
/// is a rewrite at a known position, which is syntax.
/// </para>
/// <para>
/// One instance is not thread-safe; give each worker its own, as with <see cref="CSharpFormatter"/>.
/// </para>
/// </remarks>
public sealed class CSharpCleaner
{
	private static readonly ICleanupRule[] Rules =
	[
		new UnnecessaryUsings(),
		new ReadOnlyFields(),
		new AccessibilityModifiers(),
		new ImplicitObjectCreation(),
		new ImplicitTypes(),
	];

	/// <summary>
	/// The rules a fixer exists for. Held against <see cref="Options.RuleCatalog"/> by a test, so a
	/// catalog row claiming <see cref="Options.RuleOwner.Cleanup"/> without a fixer behind it is a
	/// failure rather than a diagnostic that is quietly skipped.
	/// </summary>
	internal static IReadOnlyList<string> ImplementedRuleIds { get; } = [.. Rules.Select(rule => rule.RuleId)];

	private readonly List<(PlannedFix Fix, CleanupDiagnostic Diagnostic)> _planned = [];
	private readonly List<string> _refusals = [];
	private readonly List<CleanupDiagnostic> _unfixed = [];
	private readonly List<TextSpan> _dropped = [];
	private readonly List<string> _inserted = [];

	/// <summary>Cleans one file. Returns unchanged output rather than throwing when there is nothing to do.</summary>
	/// <param name="source">The file's text, which must be what the build compiled.</param>
	/// <param name="diagnostics">The diagnostics reported for this file, in any order.</param>
	public CleanupResult Clean(string source, IReadOnlyList<CleanupDiagnostic> diagnostics)
	{
		_planned.Clear();
		_refusals.Clear();
		_unfixed.Clear();
		_dropped.Clear();
		_inserted.Clear();

		if (!CSharpSource.TryParse(source, out var parsed, out var errors))
		{
			var detail = errors.Count > 0
				? errors[0].GetMessage(System.Globalization.CultureInfo.InvariantCulture)
				: "the source does not parse";

			return new CleanupResult(CleanupStatus.SyntaxError, false, null, 0, [], [.. diagnostics], detail);
		}

		var context = new CleanupContext(parsed.Root, parsed.Text, source);

		foreach (var diagnostic in diagnostics)
		{
			var rule = FindRule(diagnostic.RuleId);
			if (rule is null)
			{
				// Not ours, so not our business — the caller filters to the catalog before getting here.
				// Unless the catalog says it *is* ours, in which case the two have drifted and saying so
				// is better than skipping a diagnostic somebody was told would be fixed.
				if (Options.RuleCatalog.IsCleanupRule(diagnostic.RuleId))
					_refusals.Add($"{diagnostic.RuleId}: the catalog claims this rule but no fixer implements it");

				_unfixed.Add(diagnostic);
				continue;
			}

			if (rule.NeedsSpan && !diagnostic.HasSpan)
			{
				// MSBuild's console output carries a start only. For IDE0005 that is not enough to know
				// how many directives the diagnostic covers, so it is refused rather than guessed at.
				_refusals.Add($"{diagnostic.RuleId}: the log carries no end position, and this rule needs one");
				_unfixed.Add(diagnostic);
				continue;
			}

			// The cheap half of the staleness check: a position outside the file cannot be applied to it.
			if (!diagnostic.TryResolve(parsed.Text, out var span))
			{
				_refusals.Add($"{diagnostic.RuleId}: the reported position is not inside the file, so the log is stale");
				_unfixed.Add(diagnostic);
				continue;
			}

			if (rule.TryFix(context, diagnostic, span, out var fix, out var refusal))
			{
				_planned.Add((fix, diagnostic));
			}
			else
			{
				_refusals.Add($"{diagnostic.RuleId}: {refusal}");
				_unfixed.Add(diagnostic);
			}
		}

		if (_planned.Count == 0)
			return new CleanupResult(CleanupStatus.Cleaned, false, null, 0, [.. _refusals], [.. _unfixed], null);

		_planned.Sort(static (left, right) => left.Fix.Removed.Start.CompareTo(right.Fix.Removed.Start));
		DropOverlaps();

		if (_planned.Count == 0)
			return new CleanupResult(CleanupStatus.Cleaned, false, null, 0, [.. _refusals], [.. _unfixed], null);

		var output = Apply(source);

		// The declared-delta verifiers, unchanged and not switched off. The fixes said which tokens they
		// would remove; everything else in the file is still held to a strict compare.
		if (!ContentVerifier.Verify(source, output, out var contentFailure, dropped: _dropped, inserted: _inserted))
			return new CleanupResult(CleanupStatus.VerificationFailed, false, null, 0, [.. _refusals], [.. diagnostics], contentFailure);

		if (!TokenStreamComparer.Verify(parsed.Root, source, output, out var tokenFailure, dropped: _dropped, inserted: _inserted))
			return new CleanupResult(CleanupStatus.VerificationFailed, false, null, 0, [.. _refusals], [.. diagnostics], tokenFailure);

		return new CleanupResult(CleanupStatus.Cleaned, true, output, _planned.Count, [.. _refusals], [.. _unfixed], null);
	}

	private static ICleanupRule? FindRule(string ruleId)
	{
		foreach (var rule in Rules)
		{
			if (rule.RuleId.Equals(ruleId, StringComparison.OrdinalIgnoreCase))
				return rule;
		}

		return null;
	}

	/// <summary>
	/// Removes both of any two fixes whose spans touch.
	/// </summary>
	/// <remarks>
	/// Both, not one. Dropping one and keeping the other would make a second pass produce different
	/// output from the first, and one pass being the whole story is the contract
	/// <c>docs/layout-decisions.md</c> is built on. Dropping both means the next pass sees the same
	/// overlap and drops it again, so the output is a fixed point.
	/// </remarks>
	private void DropOverlaps()
	{
		var keep = new bool[_planned.Count];
		Array.Fill(keep, true);

		for (var i = 1; i < _planned.Count; i++)
		{
			if (_planned[i - 1].Fix.Removed.End <= _planned[i].Fix.Removed.Start)
				continue;

			keep[i - 1] = false;
			keep[i] = false;
		}

		for (var i = _planned.Count - 1; i >= 0; i--)
		{
			if (keep[i])
				continue;

			_refusals.Add($"{_planned[i].Diagnostic.RuleId}: its fix overlaps another, so neither was applied");
			_unfixed.Add(_planned[i].Diagnostic);
			_planned.RemoveAt(i);
		}
	}

	private string Apply(string source)
	{
		var output = new StringBuilder(source.Length);
		var cursor = 0;

		foreach (var (fix, _) in _planned)
		{
			output.Append(source, cursor, fix.Removed.Start - cursor);
			output.Append(fix.Inserted);
			cursor = fix.Removed.End;

			_dropped.AddRange(fix.DroppedTokens);
			_inserted.AddRange(fix.InsertedTokens);
		}

		output.Append(source, cursor, source.Length - cursor);
		return output.ToString();
	}
}
