using Microsoft.CodeAnalysis.Text;
using Nullean.Kerf.Documents;

namespace Nullean.Kerf.Printing.CSharp;

/// <summary>
/// Per-file state threaded through every syntax printer.
/// </summary>
/// <remarks>
/// A class rather than a struct: it is passed to every printer call and carries mutable counters, so
/// copying it would be both wasteful and wrong. The options it holds are a struct and are read
/// through helpers, never poked at directly by a printer.
/// </remarks>
internal sealed class PrintContext(DocArena arena, SourceText text, FormatOptions options)
{
	public DocArena Arena { get; } = arena;

	public SourceText Text { get; } = text;

	public FormatOptions Options { get; } = options;

	/// <summary>Tokens emitted by a printer that understands them.</summary>
	public int PrintedTokens { get; set; }

	/// <summary>Tokens emitted verbatim because no printer handles their node yet.</summary>
	public int VerbatimTokens { get; set; }

	/// <summary>How deep the printer recursion currently is, so runaway nesting fails cleanly.</summary>
	public int Depth { get; set; }

	/// <summary>
	/// Benchmark-only: make unhandled nodes emit one document per token instead of one verbatim span
	/// per line.
	/// </summary>
	/// <remarks>
	/// The verbatim path is much cheaper than a real printer — a whole class body can collapse to a
	/// handful of documents. Timing a partially-covered formatter therefore flatters it. Setting this
	/// forces every token through the same per-token emission a finished printer would use, which
	/// gives an honest upper bound on the cost of full printer coverage. Output layout is meaningless
	/// in this mode; only its cost is.
	/// </remarks>
	public bool ExpandUnhandled { get; init; }

	/// <summary>Share of tokens that went through a real printer. The number M2 exists to drive up.</summary>
	public double Coverage =>
		PrintedTokens + VerbatimTokens == 0 ? 1 : (double)PrintedTokens / (PrintedTokens + VerbatimTokens);

	/// <summary>True when the two positions sit on the same source line.</summary>
	public bool OnSameLine(int start, int end) =>
		Text.Lines.GetLineFromPosition(start).LineNumber == Text.Lines.GetLineFromPosition(end).LineNumber;

	/// <summary>Blank lines between two positions, capped at one — runs of blank lines collapse.</summary>
	public int BlankLinesBetween(int endOfPrevious, int startOfNext)
	{
		if (startOfNext <= endOfPrevious)
			return 0;
		var previousLine = Text.Lines.GetLineFromPosition(endOfPrevious).LineNumber;
		var nextLine = Text.Lines.GetLineFromPosition(startOfNext).LineNumber;
		return nextLine - previousLine > 1 ? 1 : 0;
	}
}
