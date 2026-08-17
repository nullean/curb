using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace Nullean.Kerf.Cleanup.Rules;

/// <summary>
/// IDE0005 — removes using directives the compiler said nothing needs.
/// </summary>
/// <remarks>
/// <para>
/// The rule Kerf could never reach from syntax alone, and the one with the most to gain from consuming a
/// verdict: whether a name is used is exactly the question a compilation answers.
/// </para>
/// <para>
/// <b>One diagnostic covers a run, not a directive.</b> Roslyn emits one IDE0005 per maximal contiguous
/// run of unnecessary directives. Measured on five directives where the third was needed: two results,
/// spanning lines 1–2 and 4–5, correctly skipping the third. So the span is a delete instruction, and a
/// log carrying only a start — MSBuild's console output — cannot serve this rule. Given starts alone the
/// extent is not recoverable: one unused directive followed by a needed one followed by an unused one
/// reports two starts, while two unused followed by a needed one reports a single start, and assuming
/// either shape deletes a directive something needs.
/// </para>
/// <para>
/// <b>Refused where a <c>#if</c> is present.</b> The compiler decided for one symbol set, and a
/// directive needed only under another would be reported as unnecessary and then lost. A refusal rather
/// than a guess, as with the file-scoped namespace conversion.
/// </para>
/// </remarks>
internal sealed class UnnecessaryUsings : ICleanupRule
{
	public string RuleId => "IDE0005";

	public bool NeedsSpan => true;

	public bool TryFix(CleanupContext context, in CleanupDiagnostic diagnostic, TextSpan span, out PlannedFix fix, out string? refusal)
	{
		fix = default;

		if (context.HasConditionalDirectives)
		{
			refusal = "the file has a #if, so an unused directive under one symbol set may be needed under another";
			return false;
		}

		// The node-kind gate: the span must be exactly a run of whole using directives.
		//
		// Stated as containment rather than by walking the nodes inside the span, because
		// SyntaxNode.DescendantNodes(span) filters on full spans and counts a touch at either boundary as
		// an intersection — so the member declaration that begins where the last directive's trivia ends
		// would be swept in, and every valid run would be refused.
		List<UsingDirectiveSyntax>? run = null;

		foreach (var directive in context.UsingDirectives)
		{
			if (span.Contains(directive.Span))
			{
				(run ??= []).Add(directive);
				continue;
			}

			// A directive the span cuts in half means the span no longer describes what is here.
			if (span.OverlapsWith(directive.Span))
			{
				refusal = "the reported span covers only part of a using directive, so the log is stale";
				return false;
			}
		}

		if (run is null)
		{
			refusal = "the reported span holds no whole using directive";
			return false;
		}

		if (run[0].Span.Start != span.Start)
		{
			refusal = "the reported position is not the start of a using directive";
			return false;
		}

		// The span ends where the run does. Anything beyond the last directive's trivia is something else
		// the log claimed, and deleting to it would take source no directive owns.
		if (span.End > run[^1].FullSpan.End)
		{
			refusal = "the reported span reaches past the last using directive it covers";
			return false;
		}

		var dropped = new List<TextSpan>();
		var start = int.MaxValue;
		var end = 0;

		foreach (var directive in run)
		{
			PlannedFix.CollectTokens(directive, dropped);

			var line = Widen(context.Text, directive.Span);
			start = Math.Min(start, line.Start);
			end = Math.Max(end, line.End);
		}

		refusal = null;
		fix = PlannedFix.Delete(TextSpan.FromBounds(start, end), dropped);
		return true;
	}

	/// <summary>
	/// Grows a directive's span to the whole line, but only over whitespace.
	/// </summary>
	/// <remarks>
	/// Removing the line rather than the tokens is what stops a hole being left where the directive was.
	/// It stops at anything that is not whitespace, in both directions, so a comment on the line keeps
	/// its place — the content verifier would reject losing one, and deleting somebody's comment because
	/// it sat next to a redundant import is not a trade worth making.
	/// </remarks>
	private static TextSpan Widen(SourceText text, TextSpan span)
	{
		var line = text.Lines.GetLineFromPosition(span.Start);

		var start = span.Start;
		if (IsBlank(text, line.Start, span.Start))
			start = line.Start;

		var end = span.End;
		if (span.End <= line.End && IsBlank(text, span.End, line.End))
			end = line.EndIncludingLineBreak;

		return TextSpan.FromBounds(start, end);
	}

	private static bool IsBlank(SourceText text, int start, int end)
	{
		for (var i = start; i < end; i++)
		{
			if (!char.IsWhiteSpace(text[i]))
				return false;
		}

		return true;
	}
}
