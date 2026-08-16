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
	/// <summary>
	/// Source span whose content was reordered, or default if nothing was.
	/// </summary>
	/// <remarks>
	/// <para>
	/// Reordering breaks the content verifier's linear compare, which is the point of it. Recording
	/// the spans lets the verifier switch to a multiset compare over exactly those regions and stay
	/// strict everywhere else, rather than being switched off for the file.
	/// </para>
	/// <para>
	/// A list rather than a single span because modifier ordering permutes a run per declaration,
	/// where using sorting permutes one block per file. Null in every file that reorders nothing,
	/// which is nearly all of them; entries arrive in source order, since printing walks the tree
	/// that way.
	/// </para>
	/// </remarks>
	public List<TextSpan>? ReorderedSpans { get; private set; }

	/// <summary>Records a region whose content was deliberately permuted.</summary>
	public void Reordered(TextSpan span) => (ReorderedSpans ??= []).Add(span);

	/// <summary>True when the using block was sorted, which the token comparer handles specially.</summary>
	public bool UsingsReordered { get; set; }

	/// <summary>True when any declaration's modifiers were put in the configured order.</summary>
	public bool ModifiersReordered { get; set; }

	/// <summary>True when a body was given braces the source did not have.</summary>
	public bool BracesAdded { get; set; }

	/// <summary>
	/// The group id of the parameter list most recently printed, or 0 before there is one.
	/// </summary>
	/// <remarks>
	/// So an expression body can indent itself against whether its own parameter list wrapped, which
	/// is a decision taken on this run rather than one readable from the source. Set by ParameterList
	/// and read by ArrowExpressionClause, which the printer always reaches in that order.
	/// </remarks>
	public ushort ParameterListGroup { get; set; }

	/// <summary>True when a block namespace was rewritten as a file-scoped one.</summary>
	public bool NamespaceUnwrapped { get; set; }

	/// <summary>
	/// Regions the file suppressed with <c>#pragma warning disable IDE0055</c>, or null for none.
	/// </summary>
	/// <remarks>Null in every file that has no such pragma, which is nearly all of them.</remarks>
	public List<TextSpan>? Suppressed { get; init; }

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

	/// <summary>
	/// When set, records how many tokens each unhandled <c>SyntaxKind</c> accounted for, so printer
	/// work can be aimed at whatever is actually costing coverage rather than guessed at.
	/// </summary>
	public Dictionary<int, int>? UnhandledByKind { get; init; }

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
