using Nullean.Curb.Documents;
using Nullean.Curb.Printing;

namespace Nullean.Curb.Xml;

/// <summary>What XML tells the shared layout printer about itself.</summary>
/// <remarks>
/// The only text XML inserts is a space: between attributes, and before <c>/&gt;</c>. Nothing can weld,
/// because every output is re-lexed by <see cref="XmlVerifier"/> anyway.
/// </remarks>
internal readonly struct XmlLayout : ILayoutLanguage
{
	private static readonly string[] Table = SyntheticText.Shared;

	public static string[] SyntheticTable => Table;

	public static bool CanWeld(char left, char right) => false;
}

/// <summary>
/// Builds the document for an XML tree.
/// </summary>
/// <remarks>
/// <para>
/// Preservation is the whole design. The printer owns two things and nothing else: the whitespace
/// between the nodes of element-only content, and the whitespace inside a start tag. Everything else
/// (text, CDATA, comments, processing instructions, the DOCTYPE, attribute values, end tags, and
/// the whole content of any element whose whitespace might mean something) is emitted as a slice of
/// the source.
/// </para>
/// <para>
/// A verbatim slice that spans lines is split at each line break into source runs and literal
/// lines. A literal line writes no indent and never trims, so text the printer does not own keeps
/// its trailing whitespace whatever <c>trim_trailing_whitespace</c> says, and the width the layout
/// printer measures restarts at each line instead of counting the whole run as one.
/// </para>
/// </remarks>
internal ref struct XmlPrinter
{
	/// <summary>The anchor register an aligned attribute list captures its first attribute's column into.</summary>
	private const int AttributeRegister = 0;

	/// <summary>The anchor register a start tag captures how far its line moved into.</summary>
	private const int ShiftRegister = 1;

	/// <summary>The anchor register a pinned element captures its line's indent into, for its children.</summary>
	private const int PinRegister = 2;

	private readonly ReadOnlySpan<char> _source;
	private readonly XmlTokenBuffer _tokens;
	private readonly XmlTree _tree;
	private readonly DocArena _arena;
	private readonly int _maxBlankLines;
	private readonly SelfClosingSpace _selfClosing;
	private readonly AttributeIndent _attributeIndent;
	private readonly bool _width;
	private readonly int _tabWidth;
	private readonly bool _trim;

	private XmlPrinter(ReadOnlySpan<char> source, XmlTokenBuffer tokens, XmlTree tree, DocArena arena, in XmlOptions options, in LayoutOptions layout)
	{
		_source = source;
		_tokens = tokens;
		_tree = tree;
		_arena = arena;
		_maxBlankLines = Math.Max(0, options.MaxBlankLines);
		_selfClosing = options.SpaceBeforeSelfClosing;
		_attributeIndent = options.AttributeIndent;
		_width = layout.MaxLineLength != LayoutOptions.Off;
		_tabWidth = Math.Max(1, layout.TabWidth);
		_trim = layout.TrimTrailingWhitespace;
	}

	public static void Print(ReadOnlySpan<char> source, XmlTokenBuffer tokens, XmlTree tree, DocArena arena, in XmlOptions options, in LayoutOptions layout)
	{
		var printer = new XmlPrinter(source, tokens, tree, arena, options, layout);
		using (arena.Concat())
			printer.PrintDocument();
	}

	private readonly void PrintDocument()
	{
		ref var document = ref _tree[XmlTree.Document];
		for (var child = document.FirstChild; child >= 0; child = _tree[child].NextSibling)
		{
			if (_tree[child].Kind != XmlNodeKind.Text)
			{
				PrintNode(child);
				continue;
			}

			// Whitespace before the first node and after the last is dropped: insert_final_newline owns
			// the end of the file, and nothing may precede an XML declaration.
			if (child != document.FirstChild && child != document.LastChild)
				LayoutWhitespace(_tokens[_tree[child].FirstToken], LinePinned(_tree[child].NextSibling));
		}
	}

	private readonly void PrintNode(int node)
	{
		if (_tree[node].Kind == XmlNodeKind.Element)
			PrintElement(node);
		else
			Verbatim(_tokens[_tree[node].FirstToken]);
	}

	private readonly void PrintElement(int element)
	{
		var node = _tree[element];
		PrintStartTag(node);

		if (node.Is(XmlNodeFlags.SelfClosing))
			return;

		if (!node.Is(XmlNodeFlags.Relayable))
		{
			// Text, mixed content, xml:space="preserve", an MSBuild value: everything from the start
			// tag's '>' to the end of the end tag is the author's.
			Verbatim(_tokens[node.TagClose].End, _tokens[node.LastToken].End);
			return;
		}

		// An element whose start tag stayed where it was, because an attribute value spans lines, lays
		// its children out from its own column rather than from its depth.
		if (IsPinned(element))
		{
			_arena.SourceLineAnchor(PinRegister, _tokens[node.FirstToken].Start);
			using (_arena.AlignedIndent(PinRegister, 0))
				PrintContent(node);
			return;
		}

		PrintContent(node);
	}

	private readonly void PrintContent(in XmlNode node)
	{
		var last = node.LastChild;
		var trailing = _tree[last].Kind == XmlNodeKind.Text;

		using (_arena.Indent())
		{
			for (var child = node.FirstChild; child >= 0; child = _tree[child].NextSibling)
			{
				if (child == last && trailing)
					break;

				if (_tree[child].Kind == XmlNodeKind.Text)
					LayoutWhitespace(_tokens[_tree[child].FirstToken], LinePinned(_tree[child].NextSibling));
				else
					PrintNode(child);
			}
		}

		// The whitespace before the end tag breaks to the element's own level, not its children's.
		if (trailing)
			LayoutWhitespace(_tokens[_tree[last].FirstToken], pinned: false);

		Verbatim(_tokens[node.LastToken]);
	}

	/// <summary>
	/// A node whose own text spans lines the printer cannot move: verbatim content, a multi-line
	/// comment, an attribute value with a line break in it.
	/// </summary>
	/// <remarks>
	/// Moving its first line alone would strand the rest, end tag included, at their old columns. So
	/// its whole line keeps the columns it had.
	/// </remarks>
	private readonly bool IsPinned(int index)
	{
		var node = _tree[index];
		switch (node.Kind)
		{
			case XmlNodeKind.Element:
				if (node.Is(XmlNodeFlags.MultiLineAttribute))
					return true;
				if (node.Is(XmlNodeFlags.SelfClosing) || node.Is(XmlNodeFlags.Relayable))
					return false;
				var start = _tokens[node.TagClose].End;
				return _source[start.._tokens[node.LastToken].End].IndexOfAny('\r', '\n') >= 0;

			case XmlNodeKind.Text:
				return false;

			default:
				return _tokens[node.FirstToken].Is(XmlTokenFlags.HasNewLine);
		}
	}

	/// <summary>True when a node on the line that starts with <paramref name="first"/> is pinned.</summary>
	private readonly bool LinePinned(int first)
	{
		for (var node = first; node >= 0; node = _tree[node].NextSibling)
		{
			if (_tree[node].Kind == XmlNodeKind.Text && _tokens[_tree[node].FirstToken].Is(XmlTokenFlags.HasNewLine))
				return false;
			if (IsPinned(node))
				return true;
		}

		return false;
	}

	/// <summary>
	/// Rewrites whitespace between two nodes of element-only content: the author's line breaks, at most
	/// <see cref="XmlOptions.MaxBlankLines"/> of them blank, then the indent of the node that follows.
	/// </summary>
	/// <remarks>
	/// Whitespace with no line break in it stays exactly as written. A break is never added where the
	/// author had none, so <c>&lt;a/&gt;&lt;b/&gt;</c> stays on one line.
	/// </remarks>
	private readonly void LayoutWhitespace(in XmlToken token, bool pinned)
	{
		var text = _source.Slice(token.Start, token.Length);
		var lines = 0;
		for (var i = 0; i < text.Length; i++)
		{
			if (text[i] == '\n' || (text[i] == '\r' && (i + 1 == text.Length || text[i + 1] != '\n')))
				lines++;
		}

		if (lines == 0)
		{
			_arena.SourceText(token.Start, token.Length);
			return;
		}

		// The runs between the line breaks: the first ends the line before, the middle ones are blank
		// lines, the last is the indent of the line after. Blank lines beyond the cap go, and with them
		// their whitespace; the ones kept are the last ones.
		var keep = Math.Min(lines - 1, _maxBlankLines);
		var run = 0;
		for (var segment = 0; segment <= lines; segment++)
		{
			var end = NextLineBreak(text, run);
			var kept = segment == 0 || segment > lines - 1 - keep;

			if (segment == lines)
			{
				// The indent of the next line: the printer's own, or the source's on a pinned line.
				if (pinned)
				{
					_arena.LiteralLine();
					if (end > run)
						_arena.SourceText(token.Start + run, end - run);
				}
				else
					_arena.HardLine();
				break;
			}

			if (kept)
			{
				// A blank line kept is a line break written before it.
				if (segment > 0)
					_arena.LiteralLine();

				// Trailing whitespace stays unless trim_trailing_whitespace was asked for, for XML.
				if (!_trim && end > run)
					_arena.SourceText(token.Start + run, end - run);
			}

			run = end + (text[end] == '\r' && end + 1 < text.Length && text[end + 1] == '\n' ? 2 : 1);
		}
	}

	private static int NextLineBreak(ReadOnlySpan<char> text, int from)
	{
		var at = text[from..].IndexOfAny('\r', '\n');
		return at < 0 ? text.Length : from + at;
	}

	private readonly void PrintStartTag(in XmlNode node)
	{
		var open = _tokens[node.FirstToken];
		var hasAttributes = node.TagClose - node.FirstToken > 1 && _tokens[node.FirstToken + 2].Kind == XmlTokenKind.AttributeName;

		// Width only ever adds breaks: a tag the author broke keeps their breaks, and one with a value
		// that spans lines is left as it is, since no layout of it could fit.
		var widthDriven = _width && hasAttributes && !node.Is(XmlNodeFlags.AuthorBrokeTag) && !node.Is(XmlNodeFlags.MultiLineAttribute);

		using (_arena.GroupIf(widthDriven, 0))
		{
			_arena.SourceText(open.Start, open.Length);
			if (widthDriven)
				PrintAttributesToWidth(node);
			else
				PrintAttributesAsWritten(node);
			PrintTagClose(node, widthDriven);
		}
	}

	private readonly void PrintAttributesAsWritten(in XmlNode node)
	{
		var aligned = false;
		var first = true;
		var asWritten = _attributeIndent == AttributeIndent.AsWritten;

		// Read back by every attribute on a line of its own, so each keeps its offset from where the
		// element's line now starts. A register left unset falls back to one level in.
		if (asWritten && node.Is(XmlNodeFlags.AuthorBrokeTag))
			_arena.LineShiftAnchor(ShiftRegister);

		using (_arena.Indent(_attributeIndent == AttributeIndent.DoubleIndent ? 2 : 1))
		{
			for (var i = node.FirstToken + 1; i < node.TagClose; i++)
			{
				if (_tokens[i].Kind != XmlTokenKind.AttributeName)
					continue;

				// The lexer guarantees whitespace before every attribute.
				if (_tokens[i - 1].Is(XmlTokenFlags.HasNewLine))
				{
					if (aligned)
						_arena.AlignedLine(AttributeRegister);
					else if (asWritten)
						_arena.AlignedLine(ShiftRegister, SourceIndent(_tokens[i - 1]));
					else
						_arena.HardLine();
				}
				else
				{
					_arena.Synthetic(SyntheticText.Space);
					if (first && _attributeIndent == AttributeIndent.AlignByFirstAttribute)
					{
						_arena.Anchor(AttributeRegister);
						aligned = true;
					}
				}

				first = false;
				PrintAttribute(i);
				i += 2;
			}
		}
	}

	/// <summary>
	/// One attribute per line when the tag does not fit on its own, otherwise one line with a space
	/// between each.
	/// </summary>
	private readonly void PrintAttributesToWidth(in XmlNode node)
	{
		var aligned = _attributeIndent == AttributeIndent.AlignByFirstAttribute;
		var first = true;
		using (_arena.Indent(_attributeIndent == AttributeIndent.DoubleIndent ? 2 : 1))
		{
			for (var i = node.FirstToken + 1; i < node.TagClose; i++)
			{
				if (_tokens[i].Kind != XmlTokenKind.AttributeName)
					continue;

				if (aligned && first)
				{
					_arena.Synthetic(SyntheticText.Space);
					_arena.Anchor(AttributeRegister);
				}
				else if (aligned)
					_arena.AlignedBreakOpportunity(AttributeRegister);
				else
					_arena.Line();

				first = false;
				PrintAttribute(i);
				i += 2;
			}
		}
	}

	private readonly void PrintAttribute(int name)
	{
		var nameToken = _tokens[name];
		_arena.SourceText(nameToken.Start, nameToken.Length);
		Verbatim(_tokens[name + 1]);
		Verbatim(_tokens[name + 2]);
	}

	private readonly void PrintTagClose(in XmlNode node, bool widthDriven)
	{
		var close = _tokens[node.TagClose];
		var before = _tokens[node.TagClose - 1];
		var spaced = before.Kind == XmlTokenKind.TagSpace;

		// The author put the close on a line of its own: it stays there, at the element's own indent.
		if (spaced && before.Is(XmlTokenFlags.HasNewLine) && !widthDriven)
		{
			if (_attributeIndent == AttributeIndent.AsWritten)
			{
				// Outside the attributes' indent, so an unset register falls back to the element's level.
				_arena.AlignedLine(ShiftRegister, SourceIndent(before));
			}
			else
				_arena.HardLine();
			_arena.SourceText(close.Start, close.Length);
			return;
		}

		if (close.Kind == XmlTokenKind.EmptyTagClose)
		{
			var space = _selfClosing switch
			{
				SelfClosingSpace.Always => true,
				SelfClosingSpace.Never => false,
				_ => spaced,
			};
			if (space)
				_arena.Synthetic(SyntheticText.Space);
		}

		_arena.SourceText(close.Start, close.Length);
	}

	/// <summary>The width in columns of the whitespace that ends <paramref name="space"/>, after its last line break.</summary>
	private readonly int SourceIndent(in XmlToken space)
	{
		var text = _source.Slice(space.Start, space.Length);
		var columns = 0;
		for (var i = text.LastIndexOfAny('\r', '\n') + 1; i < text.Length; i++)
			columns = text[i] == '\t' ? columns - (columns % _tabWidth) + _tabWidth : columns + 1;
		return columns;
	}

	private readonly void Verbatim(in XmlToken token) => Verbatim(token.Start, token.End);

	/// <summary>Emits the source from <paramref name="start"/> to <paramref name="end"/> as it is, line by line.</summary>
	private readonly void Verbatim(int start, int end)
	{
		var run = start;
		while (run < end)
		{
			var next = _source[run..end].IndexOfAny('\r', '\n');
			if (next < 0)
				break;

			var at = run + next;
			if (next > 0)
				_arena.SourceText(run, next);
			_arena.LiteralLine();
			run = at + (_source[at] == '\r' && at + 1 < end && _source[at + 1] == '\n' ? 2 : 1);
		}

		if (end > run)
			_arena.SourceText(run, end - run);
	}
}
