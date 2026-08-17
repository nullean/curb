using AwesomeAssertions;
using Nullean.Kerf;
using Nullean.Kerf.Documents;
using Nullean.Kerf.Printing;

namespace Nullean.Kerf.Tests.Printing;

/// <summary>
/// The printer tracks whether it could have moved a token boundary, so that re-parsing the output
/// becomes a targeted fallback rather than a tax on every file. These pin down that it fires when it
/// must — a false negative would let corrupted output through unchecked.
/// </summary>
public class RoundTripRiskTests
{
	//                                     0123456789
	private const string Source = "out var a.b // c";

	private static FormatOptions Options => new() { EndOfLine = EndOfLine.Lf, InsertFinalNewLine = false };

	private static bool RiskOf(Action<DocArena> build)
	{
		var arena = new DocArena();
		build(arena);

		using var output = new OutputBuffer();
		var printer = new DocPrinter();
		printer.Print(arena, Source.AsMemory(), Options, output);
		return printer.RoundTripAtRisk;
	}

	[Test]
	public async Task Fires_when_a_gap_between_words_is_closed()
	{
		// "out" and "var" are separated in the source; emitting them adjacent yields "outvar".
		RiskOf(arena =>
		{
			arena.SourceText(0, 3);
			arena.SourceText(4, 3);
		}).Should().BeTrue();
		await Task.CompletedTask;
	}

	[Test]
	public async Task Does_not_fire_when_the_gap_is_kept()
	{
		RiskOf(arena =>
		{
			arena.SourceText(0, 3);
			arena.Synthetic(SyntheticText.Space);
			arena.SourceText(4, 3);
		}).Should().BeFalse();
		await Task.CompletedTask;
	}

	[Test]
	public async Task Does_not_fire_for_text_that_was_already_adjacent()
	{
		// "a" and "." sit next to each other in the source, so they cannot weld into anything new.
		RiskOf(arena =>
		{
			arena.SourceText(8, 1);
			arena.SourceText(9, 1);
		}).Should().BeFalse();
		await Task.CompletedTask;
	}

	[Test]
	public async Task Fires_when_a_directive_does_not_start_its_line()
	{
		RiskOf(arena =>
		{
			arena.SourceText(0, 3);
			arena.SourceText(4, 3, DocFlags.IsDirective);
		}).Should().BeTrue();
		await Task.CompletedTask;
	}

	[Test]
	public async Task Fires_when_content_follows_a_line_comment_on_the_same_line()
	{
		RiskOf(arena =>
		{
			arena.SourceText(12, 4, DocFlags.LineComment);
			arena.Synthetic(SyntheticText.Space);
			arena.SourceText(0, 3);
		}).Should().BeTrue();
		await Task.CompletedTask;
	}

	[Test]
	public async Task Does_not_fire_when_a_line_comment_is_closed_by_a_break()
	{
		RiskOf(arena =>
		{
			arena.SourceText(12, 4, DocFlags.LineComment);
			arena.HardLine();
			arena.SourceText(0, 3);
		}).Should().BeFalse();
		await Task.CompletedTask;
	}

	[Test]
	public async Task Fires_when_line_endings_are_being_rewritten()
	{
		// Verbatim string content is re-emitted with the configured ending, which changes the value
		// of a multi-line literal. Only files that actually contain a verbatim or raw string need
		// the second parse — the guard prevents unnecessary re-parses on files that have none.
		var arena = new DocArena();
		arena.SourceText(0, 3);

		using var output = new OutputBuffer();
		var printer = new DocPrinter();
		// Source contains @" so HasVerbatimOrRawString fires; CRLF source with LF target triggers the risk.
		printer.Print(arena, "@\"a\r\nb\"".AsMemory(), new FormatOptions { EndOfLine = EndOfLine.Lf }, output);

		printer.RoundTripAtRisk.Should().BeTrue();
		await Task.CompletedTask;
	}

	[Test]
	public async Task Weld_detector_knows_which_pairs_are_dangerous()
	{
		WeldDetector.CanWeld('t', 'v').Should().BeTrue("two identifier characters merge");
		WeldDetector.CanWeld('/', '/').Should().BeTrue("that starts a comment");
		WeldDetector.CanWeld('=', '=').Should().BeTrue("that is a different operator");
		WeldDetector.CanWeld('1', '.').Should().BeTrue("that becomes a numeric literal");

		WeldDetector.CanWeld(')', ';').Should().BeFalse("brackets never combine");
		WeldDetector.CanWeld(',', 'a').Should().BeFalse();
		WeldDetector.CanWeld('a', '(').Should().BeFalse();
		await Task.CompletedTask;
	}
}
