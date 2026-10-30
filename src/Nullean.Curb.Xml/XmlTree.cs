using System.Buffers;
using System.Runtime.InteropServices;

namespace Nullean.Curb.Xml;

/// <summary>What a <see cref="XmlNode"/> is.</summary>
internal enum XmlNodeKind : byte
{
	Document,
	Element,
	Text,
	Comment,
	ProcessingInstruction,
	CData,
	Doctype,
	XmlDeclaration,
}

/// <summary>What the tree established about an element, so the printer decides without looking again.</summary>
[Flags]
internal enum XmlNodeFlags : byte
{
	None = 0,

	/// <summary>Written <c>&lt;a/&gt;</c>, so it has no content and no end tag.</summary>
	SelfClosing = 1 << 0,

	/// <summary>Inside <c>xml:space="preserve"</c>, its own or inherited.</summary>
	PreserveSpace = 1 << 1,

	/// <summary>
	/// Its content is only elements, comments and processing instructions separated by whitespace, or
	/// only whitespace across lines, and nothing asks for that whitespace to be kept: the printer may
	/// re-indent it.
	/// </summary>
	Relayable = 1 << 2,

	/// <summary>An MSBuild element whose inner XML MSBuild reads as a value, whitespace included.</summary>
	MsBuildValue = 1 << 3,

	/// <summary>The author broke the start tag across lines between its name, attributes and close.</summary>
	AuthorBrokeTag = 1 << 4,

	/// <summary>An attribute value, or the whitespace around an <c>=</c>, spans lines.</summary>
	MultiLineAttribute = 1 << 5,
}

/// <summary>One node of a document: an element, or a run of text, a comment and so on.</summary>
/// <remarks>
/// Stored in a pooled array in document order, linked by index rather than by reference. An element
/// spans the tokens from its <c>&lt;name</c> to its end tag; every other node is one token.
/// </remarks>
[StructLayout(LayoutKind.Sequential)]
internal struct XmlNode
{
	public int FirstToken;
	public int LastToken;

	/// <summary>For an element, the token that closes its start tag: <c>&gt;</c> or <c>/&gt;</c>.</summary>
	public int TagClose;

	public int Parent;
	public int FirstChild;
	public int LastChild;
	public int NextSibling;
	public XmlNodeKind Kind;
	public XmlNodeFlags Flags;

	public readonly bool Is(XmlNodeFlags flag) => (Flags & flag) != 0;
}

/// <summary>
/// The element structure of a lexed document, checked for well-formedness on the way.
/// </summary>
/// <remarks>
/// <para>
/// Built in one pass over the tokens with a stack of open elements. Anything the lexer could not see
/// in a single token is checked here: end tags that do not match, elements left open, attributes
/// written twice, namespace prefixes nobody declared, text or a second element outside the root. Any
/// of those refuses the file. Curb formats what it can prove it read correctly and repairs nothing.
/// </para>
/// <para>
/// It also decides, once per element, whether the printer may touch the whitespace inside it, and
/// marks the whitespace tokens it may rewrite with <see cref="XmlTokenFlags.Layout"/> so the verifier
/// reads the same decision rather than making its own.
/// </para>
/// </remarks>
internal sealed class XmlTree : IDisposable
{
	/// <summary>How deep elements may nest before the file is refused rather than printed.</summary>
	public const int MaxDepth = 512;

	private XmlNode[] _nodes = ArrayPool<XmlNode>.Shared.Rent(64);
	private int[] _open = ArrayPool<int>.Shared.Rent(32);
	private (int Start, int Length, int Depth)[] _prefixes = new (int, int, int)[8];
	private int _prefixCount;

	public int Count { get; private set; }

	public ref XmlNode this[int index] => ref _nodes[index];

	/// <summary>The document node, whose children are the top-level nodes.</summary>
	public const int Document = 0;

	/// <summary>True when the build stopped because elements nest deeper than <see cref="MaxDepth"/>.</summary>
	public bool TooDeep { get; private set; }

	/// <summary>Builds the tree over <paramref name="tokens"/>, flagging the layout whitespace among them.</summary>
	public bool Build(ReadOnlySpan<char> source, XmlTokenBuffer tokens, XmlProfile profile, out XmlError error)
	{
		Count = 0;
		TooDeep = false;
		_prefixCount = 0;
		error = default;

		Add(new XmlNode { Kind = XmlNodeKind.Document, Parent = -1, FirstChild = -1, LastChild = -1, NextSibling = -1, LastToken = tokens.Count - 1 });

		var depth = 0;
		var sawRoot = false;
		var count = tokens.Count;

		for (var i = 0; i < count; i++)
		{
			var token = tokens[i];
			var parent = depth == 0 ? Document : _open[depth - 1];

			switch (token.Kind)
			{
				case XmlTokenKind.Text:
					if (depth == 0)
					{
						if (!token.Is(XmlTokenFlags.WhitespaceOnly))
						{
							error = new XmlError(token.Start, "text outside the root element");
							return false;
						}

						// Between top-level nodes nothing reads whitespace, so it is always layout.
						tokens[i] = token.With(XmlTokenFlags.Layout | XmlTokenFlags.Droppable);
					}

					AddLeaf(XmlNodeKind.Text, i, parent);
					break;

				case XmlTokenKind.XmlDeclaration:
					AddLeaf(XmlNodeKind.XmlDeclaration, i, parent);
					break;

				case XmlTokenKind.ProcessingInstruction:
					AddLeaf(XmlNodeKind.ProcessingInstruction, i, parent);
					break;

				case XmlTokenKind.Comment:
					AddLeaf(XmlNodeKind.Comment, i, parent);
					break;

				case XmlTokenKind.CData:
					if (depth == 0)
					{
						error = new XmlError(token.Start, "a CDATA section outside the root element");
						return false;
					}

					AddLeaf(XmlNodeKind.CData, i, parent);
					break;

				case XmlTokenKind.Doctype:
					if (sawRoot)
					{
						error = new XmlError(token.Start, "a DOCTYPE after the root element");
						return false;
					}

					AddLeaf(XmlNodeKind.Doctype, i, parent);
					break;

				case XmlTokenKind.StartTagOpen:
					{
						if (depth == 0)
						{
							if (sawRoot)
							{
								error = new XmlError(token.Start, "a second root element");
								return false;
							}

							sawRoot = true;
						}

						if (!StartElement(source, tokens, ref i, parent, depth, profile, out var element, out error))
							return false;

						if (_nodes[element].Is(XmlNodeFlags.SelfClosing))
						{
							PopPrefixes(depth + 1);
							break;
						}

						if (depth + 1 > MaxDepth)
						{
							TooDeep = true;
							error = new XmlError(token.Start, $"elements nest deeper than {MaxDepth} levels");
							return false;
						}

						if (depth == _open.Length)
							GrowOpen();
						_open[depth++] = element;
						break;
					}

				case XmlTokenKind.EndTag:
					{
						if (depth == 0)
						{
							error = new XmlError(token.Start, "an end tag with no element open");
							return false;
						}

						var element = _open[depth - 1];
						if (!NameOf(source, tokens[_nodes[element].FirstToken]).SequenceEqual(EndTagName(source, token)))
						{
							error = new XmlError(token.Start, "an end tag that does not match the open element");
							return false;
						}

						_nodes[element].LastToken = i;
						Classify(tokens, element);
						PopPrefixes(depth);
						depth--;
						break;
					}

				default:
					// Attribute tokens are consumed by StartElement; anything else here is a lexer bug.
					error = new XmlError(token.Start, $"an unexpected {token.Kind}");
					return false;
			}
		}

		if (depth > 0)
		{
			error = new XmlError(tokens[_nodes[_open[depth - 1]].FirstToken].Start, "an element that is never closed");
			return false;
		}

		if (!sawRoot)
		{
			error = new XmlError(0, "no root element");
			return false;
		}

		return true;
	}

	/// <summary>The element name a <see cref="XmlTokenKind.StartTagOpen"/> token carries.</summary>
	public static ReadOnlySpan<char> NameOf(ReadOnlySpan<char> source, in XmlToken startTagOpen) =>
		source.Slice(startTagOpen.Start + 1, startTagOpen.Length - 1);

	private static ReadOnlySpan<char> EndTagName(ReadOnlySpan<char> source, in XmlToken endTag)
	{
		var text = source.Slice(endTag.Start + 2, endTag.Length - 2);
		return text[..XmlLexer.NameLength(text)];
	}

	private bool StartElement(
		ReadOnlySpan<char> source,
		XmlTokenBuffer tokens,
		ref int i,
		int parent,
		int depth,
		XmlProfile profile,
		out int element,
		out XmlError error)
	{
		error = default;
		var open = i;
		var name = NameOf(source, tokens[open]);
		var flags = _nodes[parent].Flags & XmlNodeFlags.PreserveSpace;

		// First pass over the attributes: duplicates, xml:space, and the prefixes this element declares,
		// which are in scope for its own name and attributes as well as its descendants.
		var j = open + 1;
		for (; tokens[j].Kind is not (XmlTokenKind.TagClose or XmlTokenKind.EmptyTagClose); j++)
		{
			var token = tokens[j];
			switch (token.Kind)
			{
				case XmlTokenKind.TagSpace when token.Is(XmlTokenFlags.HasNewLine):
					flags |= XmlNodeFlags.AuthorBrokeTag;
					break;

				case XmlTokenKind.AttributeEquals or XmlTokenKind.AttributeValue when token.Is(XmlTokenFlags.HasNewLine):
					flags |= XmlNodeFlags.MultiLineAttribute;
					break;

				case XmlTokenKind.AttributeName:
					{
						var attribute = source.Slice(token.Start, token.Length);
						for (var k = open + 1; k < j; k++)
						{
							if (tokens[k].Kind == XmlTokenKind.AttributeName && source.Slice(tokens[k].Start, tokens[k].Length).SequenceEqual(attribute))
							{
								element = -1;
								error = new XmlError(token.Start, "an attribute written twice on one element");
								return false;
							}
						}

						var value = tokens[j + 2];
						var unquoted = source.Slice(value.Start + 1, value.Length - 2);
						if (attribute.SequenceEqual("xml:space"))
						{
							if (unquoted.SequenceEqual("preserve"))
								flags |= XmlNodeFlags.PreserveSpace;
							else if (unquoted.SequenceEqual("default"))
								flags &= ~XmlNodeFlags.PreserveSpace;
						}
						else if (attribute.StartsWith("xmlns:"))
							DeclarePrefix(token.Start + 6, token.Length - 6, depth + 1);
						break;
					}
			}
		}

		if (!PrefixDeclared(source, name))
		{
			element = -1;
			error = new XmlError(tokens[open].Start + 1, "an element prefix that no xmlns declares");
			return false;
		}

		for (var k = open + 1; k < j; k++)
		{
			if (tokens[k].Kind == XmlTokenKind.AttributeName && !PrefixDeclared(source, source.Slice(tokens[k].Start, tokens[k].Length)))
			{
				element = -1;
				error = new XmlError(tokens[k].Start, "an attribute prefix that no xmlns declares");
				return false;
			}
		}

		if (tokens[j].Kind == XmlTokenKind.EmptyTagClose)
			flags |= XmlNodeFlags.SelfClosing;

		if (profile == XmlProfile.MsBuild && IsMsBuildValue(source, tokens, name, parent))
			flags |= XmlNodeFlags.MsBuildValue;

		element = Add(new XmlNode
		{
			Kind = XmlNodeKind.Element,
			FirstToken = open,
			TagClose = j,
			LastToken = j,
			Parent = parent,
			FirstChild = -1,
			LastChild = -1,
			NextSibling = -1,
			Flags = flags,
		});
		Link(parent, element);
		i = j;
		return true;
	}

	/// <summary>
	/// An element MSBuild reads the inner XML of: a property, an item's metadata, a project
	/// extension block, or an inline task's body.
	/// </summary>
	/// <remarks>
	/// MSBuild takes such a value from the element's inner XML, whitespace included, whenever it holds
	/// more than a single text node, so re-indenting what is inside it changes the value. Matched by
	/// position rather than by a list of names, since properties and item types are user-defined.
	/// </remarks>
	private bool IsMsBuildValue(ReadOnlySpan<char> source, XmlTokenBuffer tokens, ReadOnlySpan<char> name, int parent)
	{
		if (name.SequenceEqual("ProjectExtensions"))
			return true;

		if (parent == Document)
			return false;

		var parentName = NameOf(source, tokens[_nodes[parent].FirstToken]);
		if (parentName.SequenceEqual("PropertyGroup") || (name.SequenceEqual("Task") && parentName.SequenceEqual("UsingTask")))
			return true;

		var grandparent = _nodes[parent].Parent;
		if (grandparent == Document)
			return false;

		var grandparentName = NameOf(source, tokens[_nodes[grandparent].FirstToken]);
		return grandparentName.SequenceEqual("ItemGroup") || grandparentName.SequenceEqual("ItemDefinitionGroup");
	}

	/// <summary>
	/// Decides whether an element's content may be laid out, and if so marks its whitespace as layout.
	/// </summary>
	private void Classify(XmlTokenBuffer tokens, int element)
	{
		ref var node = ref _nodes[element];
		if (node.Is(XmlNodeFlags.PreserveSpace) || node.Is(XmlNodeFlags.MsBuildValue))
			return;

		// Only elements, comments and processing instructions with whitespace between them. Text that
		// is not whitespace, or a CDATA section, makes every run of whitespace beside it content.
		var structural = false;
		for (var child = node.FirstChild; child >= 0; child = _nodes[child].NextSibling)
		{
			switch (_nodes[child].Kind)
			{
				case XmlNodeKind.Text when !tokens[_nodes[child].FirstToken].Is(XmlTokenFlags.WhitespaceOnly):
				case XmlNodeKind.CData:
					return;
				case XmlNodeKind.Element or XmlNodeKind.Comment or XmlNodeKind.ProcessingInstruction:
					structural = true;
					break;
			}
		}

		// Whitespace alone on one line is a value, not layout: `<NoWarn> </NoWarn>` is a space. Across
		// lines it is an element the author opened out with nothing in it yet, and its end tag has to
		// follow its start tag wherever that moves.
		if (!structural && (node.FirstChild < 0 || !tokens[_nodes[node.FirstChild].FirstToken].Is(XmlTokenFlags.HasNewLine)))
			return;

		node.Flags |= XmlNodeFlags.Relayable;
		for (var child = node.FirstChild; child >= 0; child = _nodes[child].NextSibling)
		{
			if (_nodes[child].Kind == XmlNodeKind.Text)
			{
				var token = _nodes[child].FirstToken;
				tokens[token] = tokens[token].With(XmlTokenFlags.Layout);
			}
		}
	}

	private void DeclarePrefix(int start, int length, int depth)
	{
		if (_prefixCount == _prefixes.Length)
			Array.Resize(ref _prefixes, _prefixes.Length * 2);
		_prefixes[_prefixCount++] = (start, length, depth);
	}

	private void PopPrefixes(int depth)
	{
		while (_prefixCount > 0 && _prefixes[_prefixCount - 1].Depth >= depth)
			_prefixCount--;
	}

	private bool PrefixDeclared(ReadOnlySpan<char> source, ReadOnlySpan<char> qualifiedName)
	{
		var colon = qualifiedName.IndexOf(':');
		if (colon <= 0)
			return true;

		var prefix = qualifiedName[..colon];
		if (prefix.SequenceEqual("xml") || prefix.SequenceEqual("xmlns"))
			return true;

		for (var i = _prefixCount - 1; i >= 0; i--)
		{
			if (source.Slice(_prefixes[i].Start, _prefixes[i].Length).SequenceEqual(prefix))
				return true;
		}

		return false;
	}

	private void AddLeaf(XmlNodeKind kind, int token, int parent)
	{
		var node = Add(new XmlNode
		{
			Kind = kind,
			FirstToken = token,
			LastToken = token,
			TagClose = token,
			Parent = parent,
			FirstChild = -1,
			LastChild = -1,
			NextSibling = -1,
			Flags = _nodes[parent].Flags & XmlNodeFlags.PreserveSpace,
		});
		Link(parent, node);
	}

	private void Link(int parent, int child)
	{
		ref var p = ref _nodes[parent];
		if (p.LastChild < 0)
			p.FirstChild = child;
		else
			_nodes[p.LastChild].NextSibling = child;
		p.LastChild = child;
	}

	private int Add(in XmlNode node)
	{
		if (Count == _nodes.Length)
		{
			var next = ArrayPool<XmlNode>.Shared.Rent(_nodes.Length * 2);
			_nodes.AsSpan(0, Count).CopyTo(next);
			ArrayPool<XmlNode>.Shared.Return(_nodes);
			_nodes = next;
		}

		_nodes[Count] = node;
		return Count++;
	}

	private void GrowOpen()
	{
		var next = ArrayPool<int>.Shared.Rent(_open.Length * 2);
		_open.CopyTo(next, 0);
		ArrayPool<int>.Shared.Return(_open);
		_open = next;
	}

	public void Dispose()
	{
		if (_nodes.Length > 0)
			ArrayPool<XmlNode>.Shared.Return(_nodes);
		if (_open.Length > 0)
			ArrayPool<int>.Shared.Return(_open);
		_nodes = [];
		_open = [];
	}
}
