using System.Buffers;
using System.Runtime.InteropServices;

namespace Nullean.Curb.Xml;

/// <summary>What a <see cref="XmlToken"/> covers.</summary>
internal enum XmlTokenKind : byte
{
	/// <summary>Character data between markup, entity references kept raw.</summary>
	Text,

	/// <summary>The <c>&lt;?xml … ?&gt;</c> declaration, which may only open the document.</summary>
	XmlDeclaration,

	/// <summary>Any other <c>&lt;?target … ?&gt;</c>.</summary>
	ProcessingInstruction,

	/// <summary><c>&lt;!-- … --&gt;</c>.</summary>
	Comment,

	/// <summary><c>&lt;![CDATA[ … ]]&gt;</c>.</summary>
	CData,

	/// <summary><c>&lt;!DOCTYPE … &gt;</c>, internal subset included.</summary>
	Doctype,

	/// <summary><c>&lt;</c> and the element name.</summary>
	StartTagOpen,

	/// <summary>Whitespace inside a start tag: before an attribute, or before its close.</summary>
	TagSpace,

	/// <summary>An attribute's name.</summary>
	AttributeName,

	/// <summary>An attribute's <c>=</c>, with whatever whitespace surrounds it.</summary>
	AttributeEquals,

	/// <summary>An attribute's value, quotes included.</summary>
	AttributeValue,

	/// <summary>The <c>&gt;</c> that ends a start tag.</summary>
	TagClose,

	/// <summary>The <c>/&gt;</c> that ends an empty-element tag.</summary>
	EmptyTagClose,

	/// <summary>A whole end tag, <c>&lt;/name&gt;</c>, whitespace before its <c>&gt;</c> included.</summary>
	EndTag,
}

/// <summary>Facts about a token the lexer and the tree establish once, so nothing re-scans it.</summary>
[Flags]
internal enum XmlTokenFlags : byte
{
	None = 0,

	/// <summary>Text made only of spaces, tabs and line breaks.</summary>
	WhitespaceOnly = 1 << 0,

	/// <summary>The token contains a line break.</summary>
	HasNewLine = 1 << 1,

	/// <summary>
	/// Whitespace-only text the printer may rewrite: it sits in element-only content outside
	/// <c>xml:space="preserve"</c>, or between the document's top-level nodes. Set by the tree.
	/// </summary>
	Layout = 1 << 2,

	/// <summary>
	/// Layout whitespace between the document's top-level nodes, which the printer may drop outright:
	/// before the root, insert_final_newline owns the end. Inside an element layout whitespace may be
	/// rewritten but never removed.
	/// </summary>
	Droppable = 1 << 3,
}

/// <summary>One lexeme of an XML document.</summary>
/// <remarks>
/// Twelve bytes and no references, so a pooled array of them is never scanned by the GC. Tokens
/// cover the source exactly: concatenated in order they are the source, every byte of it, which is
/// what lets the printer emit anything it does not own as a slice of the original.
/// </remarks>
[StructLayout(LayoutKind.Sequential)]
internal readonly struct XmlToken(XmlTokenKind kind, int start, int length, XmlTokenFlags flags = XmlTokenFlags.None)
{
	public readonly int Start = start;
	public readonly int Length = length;
	public readonly XmlTokenKind Kind = kind;
	public readonly XmlTokenFlags Flags = flags;

	public int End => Start + Length;

	public bool Is(XmlTokenFlags flag) => (Flags & flag) != 0;

	public XmlToken With(XmlTokenFlags flag) => new(Kind, Start, Length, Flags | flag);
}

/// <summary>The pooled array a lexer writes its tokens into, reused from file to file.</summary>
internal sealed class XmlTokenBuffer : IDisposable
{
	private XmlToken[] _tokens = ArrayPool<XmlToken>.Shared.Rent(256);

	public int Count { get; private set; }

	public ReadOnlySpan<XmlToken> Tokens => _tokens.AsSpan(0, Count);

	public ref XmlToken this[int index] => ref _tokens[index];

	/// <summary>Empties the buffer, sized for a source of <paramref name="sourceLength"/> characters.</summary>
	public void Reset(int sourceLength)
	{
		Count = 0;

		// Project files run at roughly one token per twelve characters.
		var wanted = sourceLength / 8;
		if (wanted > _tokens.Length)
			Grow(wanted);
	}

	public void Add(in XmlToken token)
	{
		if (Count == _tokens.Length)
			Grow(_tokens.Length * 2);
		_tokens[Count++] = token;
	}

	private void Grow(int capacity)
	{
		var next = ArrayPool<XmlToken>.Shared.Rent(capacity);
		_tokens.AsSpan(0, Count).CopyTo(next);
		ArrayPool<XmlToken>.Shared.Return(_tokens);
		_tokens = next;
	}

	public void Dispose()
	{
		if (_tokens.Length == 0)
			return;
		ArrayPool<XmlToken>.Shared.Return(_tokens);
		_tokens = [];
		Count = 0;
	}
}
