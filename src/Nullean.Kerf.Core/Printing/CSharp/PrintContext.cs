using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis;
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

	/// <summary>
	/// The group id of the argument list most recently finished, or 0 if it had none.
	/// </summary>
	/// <remarks>
	/// <para>
	/// The same trick as <see cref="ParameterListGroup"/>, for the object creation that has to decide
	/// where its trailing initializer's brace goes: <c>dotnet format</c> puts that brace on its own line
	/// whenever the creation opened out, and "did the creation open out" is a decision taken on this run.
	/// </para>
	/// <para>
	/// Written as the last act of printing a list, which is what makes it the *outermost* list's id.
	/// Argument lists nest, so an inner list writes its own id first and the enclosing one overwrites
	/// it on the way out — which is the id the creation printer is asking about.
	/// </para>
	/// </remarks>
	public ushort ArgumentListGroup { get; set; }

	/// <summary>
	/// The group of the most recently printed construct that brings its own braces, or 0.
	/// </summary>
	/// <remarks>
	/// For the shape an argument list declines to lay out: <c>new HttpClient(new SocketsHttpHandler
	/// { … })</c> gets no list group at all, because the sole argument positions its own contents. The
	/// creation's trailing initializer still has to know whether any of that opened out, so what it aims
	/// at is the argument's own group instead of the list's.
	/// </remarks>
	public ushort OwnBlockGroup { get; set; }

	/// <summary>
	/// The group enclosing the type member about to be printed, attribute sections included, or 0.
	/// </summary>
	/// <remarks>
	/// <para>
	/// What <c>csharp_place_*_attribute_on_same_line = if_owner_is_single_line</c> aims at. The attribute
	/// has to be inside the same measurement as the member, or the question is circular: joining the
	/// attribute lengthens the line, which changes whether the member fits, which decides whether to join
	/// the attribute. One group makes that a single decision instead of an argument between two runs.
	/// </para>
	/// <para>
	/// Cleared by the first reader, which is what keeps it to the member it was set for. A local function
	/// nested in a method body has attributes of its own and no group of its own, and would otherwise aim
	/// at whatever member it happened to be inside.
	/// </para>
	/// </remarks>
	public ushort MemberGroup { get; set; }

	/// <summary>Source spans a rewrite legitimately dropped, or null when nothing was dropped.</summary>
	/// <remarks>
	/// Expression bodies are the only thing that drops source today: a block's braces and its
	/// <c>return</c> go, and <c>=&gt;</c> arrives. Declaring the spans lets the content check skip
	/// exactly those characters and stay strict over everything else, rather than the whole file
	/// being excused because one member was rewritten.
	/// </remarks>
	public List<TextSpan>? DroppedSpans { get; private set; }

	/// <summary>Records source that a rewrite deliberately did not emit.</summary>
	public void Dropped(TextSpan span) => (DroppedSpans ??= []).Add(span);

	/// <summary>
	/// How many <c>=&gt;</c> the printer put in that the source did not have.
	/// </summary>
	/// <remarks>
	/// Declared rather than inferred from what was dropped, because the two do not correspond: a
	/// property collapsed from a block getter drops two opening braces — the accessor list's and the
	/// getter's — and adds one arrow, while a getter that already used an arrow drops braces and adds
	/// none, since the source's own arrow carries over.
	/// </remarks>
	public int ArrowsAdded { get; set; }

	/// <summary>True when a block body was replaced by an expression body.</summary>
	public bool ExpressionBodyAdded { get; set; }

	/// <summary>
	/// The compilation unit's using directives, when they are to be printed inside the namespace.
	/// </summary>
	/// <remarks>Set by the compilation unit just before it prints the namespace, and taken by it.</remarks>
	public SyntaxList<UsingDirectiveSyntax> UsingsToPlaceInside { get; set; }

	/// <summary>Takes the held directives, leaving none behind.</summary>
	public SyntaxList<UsingDirectiveSyntax> TakeUsingsToPlaceInside()
	{
		var held = UsingsToPlaceInside;
		UsingsToPlaceInside = default;
		return held;
	}

	/// <summary>True when a file header was inserted that the source did not have.</summary>
	public bool FileHeaderAdded { get; set; }

	/// <summary>True when a block namespace was rewritten as a file-scoped one.</summary>
	public bool NamespaceUnwrapped { get; set; }

	/// <summary>
	/// Regions the file suppressed with <c>#pragma warning disable IDE0055</c>, or null for none.
	/// </summary>
	/// <remarks>Null in every file that has no such pragma, which is nearly all of them.</remarks>
	public List<TextSpan>? Suppressed { get; init; }

	public int PrintedTokens { get; set; }

	/// <summary>The last token emitted by <see cref="TokenPrinter.Print"/>, for avoiding a Roslyn tree walk.</summary>
	public SyntaxToken PreviousToken { get; set; }

	/// <summary>
	/// Shared trailer buffer for chain printing. Each <c>TryPrintChain</c> frame records its base
	/// index, appends to this list, and truncates back on exit — nested chains append past the outer
	/// frame's slice and clean up only what they added, so re-entrancy is safe.
	/// </summary>
	public List<SyntaxNode> TrailerBuffer { get; } = [];

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

	/// <summary>
	/// True when the two positions sit on the same source line, whatever the configuration says.
	/// </summary>
	/// <remarks>
	/// Private, which is the point. A printer deciding <em>where a line break goes</em> has to go through
	/// <see cref="AuthorBroke"/> or <see cref="AuthorJoined"/>, both of which answer "no evidence" under
	/// <c>csharp_keep_existing_linebreaks = false</c> so that the call site falls through to width. Leaving
	/// this reachable made that a review rule; hiding it makes it a compile error. Anything genuinely
	/// needing the raw answer — how far apart two comments are — reads <c>Text.Lines</c> itself, as
	/// <c>TokenPrinter</c> does.
	/// </remarks>
	private bool OnSameLine(int start, int end) =>
		Text.Lines.GetLineFromPosition(start).LineNumber == Text.Lines.GetLineFromPosition(end).LineNumber;

	/// <summary>The author put a break between these positions, and Kerf honours it.</summary>
	/// <remarks>
	/// <para>
	/// One of the two doors onto the author's layout, and the reason they exist is
	/// <c>docs/layout-decisions.md</c>: a break rule may read layout the author owns and never layout
	/// Kerf decides. Routing every such question through a named predicate is what makes the rule
	/// checkable — <c>OnSameLine</c> appearing in a break decision is now visible in review.
	/// </para>
	/// <para>
	/// False whenever <c>csharp_keep_existing_linebreaks</c> is off, so each call site falls through to
	/// the width-driven branch it already has. Which of the two predicates a site wants is decided by
	/// what it does with the answer, not by how the condition reads: a site that stays flat when the
	/// answer is true wants <see cref="AuthorJoined"/>.
	/// </para>
	/// </remarks>
	public bool AuthorBroke(int start, int end) =>
		Options.KeepExistingLinebreaks && !OnSameLine(start, end);

	/// <summary>The author kept these positions on one line, and Kerf honours it.</summary>
	/// <remarks>The other door; see <see cref="AuthorBroke"/>. Also false in deterministic mode.</remarks>
	public bool AuthorJoined(int start, int end) =>
		Options.KeepExistingLinebreaks && OnSameLine(start, end);

	/// <summary>Blank lines between two positions, capped at one — runs of blank lines collapse.</summary>
	public int BlankLinesBetween(int endOfPrevious, int startOfNext)
	{
		if (startOfNext <= endOfPrevious)
			return 0;
		var previousLine = Text.Lines.GetLineFromPosition(endOfPrevious).LineNumber;
		var nextLine = Text.Lines.GetLineFromPosition(startOfNext).LineNumber;
		return Math.Max(0, nextLine - previousLine - 1);
	}

	/// <summary>
	/// How many blank lines to emit between two members, given what the author left and what the
	/// configuration asks for.
	/// </summary>
	/// <remarks>
	/// The author's count, raised to <paramref name="minimum"/> and capped at
	/// <c>csharp_keep_blank_lines_in_declarations</c>. Both default to leaving Kerf where it was: a
	/// minimum of none and a cap of one, which is the collapsing it has always done.
	/// </remarks>
	public int DeclarationSeparation(int endOfPrevious, int startOfNext, int minimum = 0)
	{
		var written = BlankLinesBetween(endOfPrevious, startOfNext);
		return Math.Min(Math.Max(written, minimum), Options.KeepBlankLinesInDeclarations);
	}

	/// <summary>The same between statements, capped by <c>csharp_keep_blank_lines_in_code</c>.</summary>
	public int CodeSeparation(int endOfPrevious, int startOfNext)
	{
		var written = BlankLinesBetween(endOfPrevious, startOfNext);
		return Math.Min(written, Options.KeepBlankLinesInCode);
	}

	/// <summary>Emits <paramref name="count"/> blank lines.</summary>
	public void BlankLines(int count)
	{
		for (var i = 0; i < count; i++)
			Arena.HardLine();
	}
}
