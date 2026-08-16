using System.Buffers;
using Microsoft.CodeAnalysis.Text;

namespace Nullean.Kerf.Documents;

/// <summary>
/// The pooled buffer documents are built into, plus the writer that builds them.
/// </summary>
/// <remarks>
/// <para>
/// Printers construct documents by opening and closing scopes rather than returning node objects.
/// Opening writes a placeholder and returns its index; closing patches that placeholder's subtree
/// length in place — the same trick a binary serialiser uses for length prefixes, and O(1).
/// </para>
/// <para>
/// One arena is reused for every file in a run: <see cref="Reset"/> sets a counter back to zero.
/// That is what keeps a whole-repository format at near-zero steady-state allocation.
/// </para>
/// </remarks>
internal sealed class DocArena
{
	private Doc[] _docs;
	private int _count;
	private ushort _nextGroupId = 1;

	/// <summary>Roughly how many document slots a character of source turns into, used to pre-size the buffer.</summary>
	private const double SlotsPerSourceChar = 0.3;

	public DocArena(int capacity = 1024) => _docs = ArrayPool<Doc>.Shared.Rent(Math.Max(capacity, 16));

	public int Count => _count;

	public ReadOnlySpan<Doc> Docs => _docs.AsSpan(0, _count);

	public Doc this[int index] => _docs[index];

	/// <summary>Clears the arena for reuse, optionally pre-sizing for a source of the given length.</summary>
	public void Reset(int sourceLength = 0)
	{
		_count = 0;
		_nextGroupId = 1;
		_externalTexts?.Clear();

		var wanted = (int)(sourceLength * SlotsPerSourceChar);
		if (wanted > _docs.Length)
			Grow(wanted);
	}

	/// <summary>Allocates a fresh group id. Ids are dense so the printer can map them to modes with an array.</summary>
	public ushort NextGroupId() => _nextGroupId++;

	// ---- leaves ---------------------------------------------------------------------------------

	/// <summary>Emits a span of the original source verbatim.</summary>
	public void SourceText(TextSpan span) => Add(new Doc(DocKind.SrcText, a: span.Start, b: span.Length));

	/// <summary>Emits a span of the original source verbatim.</summary>
	public void SourceText(int offset, int length, DocFlags flags = DocFlags.None) =>
		Add(new Doc(DocKind.SrcText, a: offset, b: length, flags: flags));

	/// <summary>Emits one of the fixed <see cref="SyntheticText"/> strings.</summary>
	public void Synthetic(int id) => Add(new Doc(DocKind.SynText, a: id));

	/// <summary>Text the configuration supplied, such as a <c>file_header_template</c> line.</summary>
	public void ExternalText(string text)
	{
		_externalTexts ??= [];
		_externalTexts.Add(text);
		Add(new Doc(DocKind.ExternalText, a: _externalTexts.Count - 1));
	}

	/// <summary>Emits a header comment line: <c>// </c> and the text, or a bare <c>//</c> when empty.</summary>
	public void HeaderLine(string text) => ExternalText(text.Length == 0 ? "//" : "// " + text);

	private List<string>? _externalTexts;

	/// <summary>The configuration-supplied strings this document references.</summary>
	public IReadOnlyList<string> ExternalTexts => (IReadOnlyList<string>?)_externalTexts ?? [];

	/// <summary>A space when flat, a newline when broken.</summary>
	public void Line(DocFlags flags = DocFlags.None) => AddLine(LineType.Normal, flags);

	/// <summary>Nothing when flat, a newline when broken.</summary>
	public void SoftLine(DocFlags flags = DocFlags.None) => AddLine(LineType.Soft, flags);

	/// <summary>Always a newline; forces enclosing groups to break.</summary>
	public void HardLine(DocFlags flags = DocFlags.None) => AddLine(LineType.Hard, flags);

	/// <summary>Always a newline emitted at column 0, for content that must not be re-indented.</summary>
	public void LiteralLine(DocFlags flags = DocFlags.None) => AddLine(LineType.Literal, flags);

	/// <summary>
	/// A newline, but only when the group <paramref name="groupId"/> names ended up broken.
	/// </summary>
	/// <remarks>
	/// The third of the aimed primitives, and the one a construct needs when its choice is between a
	/// real break and none — where <see cref="IfBreak"/> cannot help, because a hard line inside the
	/// branch that is *not* taken still forces every enclosing group to break: break propagation
	/// prefix-sums the whole subtree and cannot know which branch will print.
	///
	/// Emitting nothing when the named group is flat is the point: this is the difference between
	/// putting a brace on its own line and leaving it where it is.
	/// </remarks>
	public void LineIfBroken(ushort groupId, DocFlags flags = DocFlags.None) =>
		AddLine(LineType.Hard, flags, groupId);

	/// <summary>Forces every enclosing group to break without emitting anything.</summary>
	public void BreakParent() => Add(new Doc(DocKind.BreakParent));

	/// <summary>Trims trailing whitespace already written.</summary>
	public void Trim() => Add(new Doc(DocKind.Trim));

	private void AddLine(LineType type, DocFlags flags, ushort groupId = 0) =>
		Add(new Doc(DocKind.Line, a: (int)type, flags: flags, groupId: groupId));

	// ---- scopes ---------------------------------------------------------------------------------

	public DocScope Concat() => Open(new Doc(DocKind.Concat));

	public DocScope Group(ushort groupId = 0) => Open(new Doc(DocKind.Group, groupId: groupId));

	/// <summary>Opens a named group only when <paramref name="condition"/> holds.</summary>
	/// <remarks>Writes no document at all otherwise, so a construct nobody aims at costs nothing.</remarks>
	public DocScope GroupIf(bool condition, ushort groupId) => condition ? Group(groupId) : default;

	public DocScope ConditionalGroup(ushort groupId = 0) => Open(new Doc(DocKind.ConditionalGroup, groupId: groupId));

	/// <summary>Shifts the subtree by <paramref name="levels"/>, which may be negative.</summary>
	public DocScope Indent(int levels = 1) => Open(new Doc(DocKind.Indent, b: levels));

	/// <summary>Places the subtree at column 0 regardless of the enclosing indent.</summary>
	public DocScope IndentToRoot() => Open(new Doc(DocKind.Indent, b: Doc.IndentToRoot));

	/// <summary>Opens an indent scope only when <paramref name="condition"/> holds.</summary>
	/// <remarks>
	/// Writes no document at all when the condition is false, so an option that is off costs nothing
	/// — which matters for the several brace sites that consult csharp_indent_braces, since almost
	/// nobody turns it on and every one of them is on the hot path.
	/// </remarks>
	public DocScope IndentIf(bool condition, int levels = 1) => condition ? Indent(levels) : default;

	/// <summary>
	/// Opens an indent scope only when the group <paramref name="groupId"/> names ends up broken.
	/// </summary>
	/// <remarks>
	/// The conditional half of an <c>IfBreak</c> without its duplication: an <c>IfBreak</c> has to
	/// hold both renderings of whatever it wraps, so indenting a large subtree that way would build
	/// it into the arena twice. This wraps it once and shifts it, or does not.
	///
	/// The group has to be one the printer reaches first — a parameter list ahead of the arrow body
	/// that hangs off it — since a mode is only known once its group has been entered.
	/// </remarks>
	public DocScope IndentIfBroken(ushort groupId, int levels = 1) =>
		Open(new Doc(DocKind.Indent, b: levels, groupId: groupId));

	/// <summary>Captures the current output column into <paramref name="register"/>.</summary>
	public void Anchor(int register) => Add(new Doc(DocKind.Anchor, a: register));

	/// <summary>A hard break that indents to a column captured by <see cref="Anchor"/>.</summary>
	public void AlignedLine(int register) =>
		Add(new Doc(DocKind.Line, a: (int)LineType.Hard, b: register, flags: DocFlags.AlignToAnchor));

	/// <summary>
	/// A break opportunity that lands on a captured column when it is taken, and is a space when it
	/// is not.
	/// </summary>
	/// <remarks>
	/// The soft half of <see cref="AlignedLine"/>. A query whose clauses fit stays on one line; one
	/// that does not gets them under the <c>from</c>, which is where dotnet format puts them however
	/// the break came about.
	/// </remarks>
	public void AlignedBreakOpportunity(int register) =>
		Add(new Doc(DocKind.Line, a: (int)LineType.Normal, b: register, flags: DocFlags.AlignToAnchor));

	public DocScope ForceFlat() => Open(new Doc(DocKind.ForceFlat));

	/// <summary>Opens a force-flat scope only when <paramref name="condition"/> holds.</summary>
	public DocScope ForceFlatIf(bool condition) => condition ? ForceFlat() : default;

	public DocScope AlwaysFits() => Open(new Doc(DocKind.AlwaysFits));

	/// <summary>
	/// Opens an <see cref="DocKind.IfBreak"/>. Write exactly two branches inside it, flat first,
	/// each via <see cref="IfBreakScope.Branch"/>.
	/// </summary>
	public IfBreakScope IfBreak(ushort groupId = 0)
	{
		var index = _count;
		Add(new Doc(DocKind.IfBreak, groupId: groupId));
		return new IfBreakScope(this, index);
	}

	private DocScope Open(Doc doc)
	{
		var index = _count;
		Add(doc);
		return new DocScope(this, index);
	}

	internal void Close(int index)
	{
		var doc = _docs[index];
		var length = _count - index;

		if (doc.Kind is DocKind.Concat or DocKind.ConditionalGroup)
		{
			// Count direct children by walking the subtree one child at a time.
			var children = 0;
			for (var i = index + 1; i < _count; i += _docs[i].Length)
				children++;
			_docs[index] = new Doc(doc.Kind, length, doc.A, children, doc.GroupId, doc.Flags);
			return;
		}

		_docs[index] = doc.WithLength(length);
	}

	internal void CloseIfBreak(int index)
	{
		var flatLength = _count > index + 1 ? _docs[index + 1].Length : 0;
		_docs[index] = _docs[index].WithLength(_count - index).WithA(flatLength);
	}

	private void Add(Doc doc)
	{
		if (_count == _docs.Length)
			Grow(_docs.Length * 2);
		_docs[_count++] = doc;
	}

	private void Grow(int capacity)
	{
		var next = ArrayPool<Doc>.Shared.Rent(capacity);
		_docs.AsSpan(0, _count).CopyTo(next);
		ArrayPool<Doc>.Shared.Return(_docs);
		_docs = next;
	}
}

/// <summary>Closes a document scope, patching its subtree length, when disposed.</summary>
internal readonly struct DocScope(DocArena arena, int index) : IDisposable
{
	/// <summary>A default scope holds no arena — see <see cref="DocArena.IndentIf"/> — and closes nothing.</summary>
	public void Dispose() => arena?.Close(index);
}

/// <summary>Writes the two branches of an <see cref="DocKind.IfBreak"/>. Flat branch first.</summary>
internal readonly struct IfBreakScope(DocArena arena, int index) : IDisposable
{
	/// <summary>Opens one branch. Call twice: first the flat branch, then the broken one.</summary>
	public DocScope Branch() => arena.Concat();

	public void Dispose() => arena.CloseIfBreak(index);
}
