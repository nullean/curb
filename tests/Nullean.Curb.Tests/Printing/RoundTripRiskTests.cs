using AwesomeAssertions;
using Nullean.Curb;
using Nullean.Curb.Documents;
using Nullean.Curb.Printing;

namespace Nullean.Curb.Tests.Printing;

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
	public async Task Does_not_fire_merely_because_the_file_holds_a_multi_line_literal()
	{
		// A verbatim string's newlines are content, and DocArena.SourceLine now reproduces them, so
		// a file whose endings differ from the configured one no longer has its literals rewritten
		// and no longer owes a second parse for that reason alone. Before that, every mixed-ending
		// file containing @" or """ paid for one.
		var arena = new DocArena();
		arena.SourceText(0, 3);

		using var output = new OutputBuffer();
		var printer = new DocPrinter();
		printer.Print(arena, "@\"a\r\nb\"".AsMemory(), new FormatOptions { EndOfLine = EndOfLine.Lf }, output);

		printer.RoundTripAtRisk.Should().BeFalse();
		await Task.CompletedTask;
	}

	[Test]
	public async Task A_source_line_keeps_its_own_ending_rather_than_the_configured_one()
	{
		var arena = new DocArena();
		arena.SourceText(0, 3);
		arena.SourceLine(crLf: true);
		arena.SourceText(4, 3);
		arena.SourceLine(crLf: false);
		arena.SourceText(8, 1);

		using var output = new OutputBuffer();
		var printer = new DocPrinter();
		printer.Print(arena, Source.AsMemory(), Options, output);

		// Options say LF; both endings come from the document, not from the configuration.
		output.ToString().Should().Be("out\r\nvar\na");
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
