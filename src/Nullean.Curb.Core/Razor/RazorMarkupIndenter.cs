using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace Nullean.Curb.Razor;

/// <summary>
/// Re-indents the markup of a <c>.razor</c> or <c>.cshtml</c> file by its nesting, the way Microsoft's Razor
/// formatter does, and changes nothing else. Experimental.
/// </summary>
/// <remarks>
/// <para>
/// Each line that starts in markup gets one indent per element and Razor code block it sits in: elements and
/// components nest, void elements (<c>&lt;br&gt;</c>, <c>&lt;img&gt;</c>, …) and self-closing tags do not, and
/// the body of an <c>@if</c>, <c>@foreach</c> or the like nests inside its braces, a C# brace in it as well. A
/// line that starts with the tag or brace that closes what it is in sits with what it closes.
/// </para>
/// <para>
/// Lines whose whitespace is not layout are left exactly as written: the content of <c>&lt;script&gt;</c>,
/// <c>&lt;style&gt;</c>, <c>&lt;pre&gt;</c> and <c>&lt;textarea&gt;</c>, the inside of a comment, the lines a tag's
/// attributes continue on, a C# statement continued on a second line, and the inside of a <c>@code</c>,
/// <c>@functions</c> or <c>@{ }</c> block, whose C# <see cref="RazorFormatter"/> formats against the block's own
/// line.
/// </para>
/// <para>
/// Only a line's leading whitespace ever changes. A file whose structure this cannot follow (a closing tag that
/// does not close the open element, an element left open, a <c>@switch</c>) is left as written, with the reason.
/// </para>
/// </remarks>
public static class RazorMarkupIndenter
{
	private static readonly HashSet<string> VoidElements =
	[
		with(StringComparer.OrdinalIgnoreCase),
		"area",
		"base",
		"br",
		"col",
		"embed",
		"hr",
		"img",
		"input",
		"link",
		"meta",
		"param",
		"source",
		"track",
		"wbr",
	];

	// Text whose whitespace is content. Microsoft's formatter re-indents the closing tag of a script or style,
	// whose content it leaves alone, and leaves a pre's or textarea's closing tag where it is too.
	private static readonly HashSet<string> RawElements = [with(StringComparer.OrdinalIgnoreCase), "script", "style", "pre", "textarea"];

	// Directives whose line is C# or a name to its end: `@inject UserManager<ApplicationUser> Users` holds no tag.
	private static readonly HashSet<string> LineDirectives =
	[
		with(StringComparer.Ordinal),
		"page",
		"inject",
		"inherits",
		"model",
		"typeparam",
		"implements",
		"attribute",
		"layout",
		"namespace",
		"rendermode",
		"preservewhitespace",
		"addTagHelper",
		"removeTagHelper",
		"tagHelperPrefix",
	];

	private static readonly HashSet<string> CodeConstructs = [with(StringComparer.Ordinal), "if", "for", "foreach", "while", "lock", "try", "do", "using",];

	private enum FrameKind : byte
	{
		Element,
		CodeBlock,
		CSharpBrace,
		Section
	}

	/// <summary>Something open at the current position: an element, a code block or a C# brace in one.</summary>
	/// <remarks>
	/// <c>Indents</c> says whether the lines inside it go a level in. An element does when its start tag begins
	/// its line: one opened mid-line, `text &lt;a href="…"&gt;link`, is inline, and the line that continues it
	/// stays with its parent, as Microsoft's formatter leaves it. <c>&lt;html&gt;</c> never does: its head and
	/// body sit at its own level.
	/// </remarks>
	private readonly record struct Frame(FrameKind Kind, string? Name, bool Indents = true);

	/// <summary>What a line inside a start tag that spans lines begins with.</summary>
	private enum TagLine : byte
	{
		/// <summary>An attribute, placed by <c>razor_attribute_indent_style</c>.</summary>
		Attribute,

		/// <summary>The tag's <c>&gt;</c> or <c>/&gt;</c>, which goes where the tag's line does.</summary>
		Closer,

		/// <summary>C# continuing an attribute's value, moved as far as the tag's line moved.</summary>
		CSharpValue,

		/// <summary>Text continuing a quoted value: its whitespace is part of the value, so it is kept.</summary>
		TextValue,
	}

	/// <summary>
	/// The source with each markup line's indentation replaced, or null and a reason when the file's structure
	/// cannot be followed.
	/// </summary>
	/// <param name="source">The whole file.</param>
	/// <param name="component">A <c>.razor</c> component, where <c>@code</c> is a directive; false for a view.</param>
	/// <param name="indentColumns">Columns per nesting level.</param>
	/// <param name="useTabs">Write indentation with tabs, then spaces.</param>
	/// <param name="tabWidth">Columns a tab advances.</param>
	/// <param name="attributes">Where an attribute that starts a line inside a start tag goes.</param>
	/// <param name="engineQuirks">
	/// Place attributes as Microsoft's Razor formatter does where it differs: with nothing after the tag's name on its
	/// line, they align with the tag rather than going a level in.
	/// </param>
	/// <param name="refusal">Why the file is left as written.</param>
	public static string? TryIndent(
		string source,
		bool component,
		int indentColumns,
		bool useTabs,
		int tabWidth,
		out string? refusal,
		RazorAttributeIndent attributes = RazorAttributeIndent.AlignWithFirst,
		bool engineQuirks = false
	)
	{
		var scanner = new Scanner(source, component, tabWidth);
		var lines = Lines(source);
		var builder = new StringBuilder(source.Length + 64);

		foreach (var (start, end) in lines)
		{
			var content = start;
			while (content < end && source[content] is ' ' or '\t')
				content++;

			var lineEnd = end;
			while (lineEnd > content && source[lineEnd - 1] is '\n' or '\r')
				lineEnd--;

			var sourceColumn = Column(source, start, content, 0, tabWidth);
			if (!scanner.InTag(content, sourceColumn, indentColumns, attributes, engineQuirks, out var column))
			{
				var depth = scanner.DepthForLine(start, content, lineEnd);
				if (scanner.Refusal is not null)
				{
					refusal = scanner.Refusal;
					return null;
				}

				column = depth < 0 ? -1 : depth * indentColumns;
			}

			if (column < 0 || content == lineEnd)
			{
				builder.Append(source, start, end - start);
				column = sourceColumn;
			}
			else
			{
				AppendIndent(builder, column, useTabs, tabWidth);
				builder.Append(source, content, end - content);
			}

			scanner.BeginLine(content, column);
			scanner.ScanLine(content, lineEnd);
			if (scanner.Refusal is not null)
			{
				refusal = scanner.Refusal;
				return null;
			}
		}

		refusal = scanner.Finish();
		return refusal is null ? builder.ToString() : null;
	}

	/// <summary>True when <paramref name="before"/> and <paramref name="after"/> differ only in each line's leading whitespace.</summary>
	public static bool OnlyIndentationChanged(string before, string after)
	{
		var a = Lines(before);
		var b = Lines(after);
		if (a.Count != b.Count)
			return false;

		for (var i = 0; i < a.Count; i++)
		{
			var x = before.AsSpan(a[i].Start, a[i].End - a[i].Start).TrimStart(" \t");
			var y = after.AsSpan(b[i].Start, b[i].End - b[i].Start).TrimStart(" \t");
			if (!x.SequenceEqual(y))
				return false;
		}

		return true;
	}

	private static void AppendIndent(StringBuilder builder, int columns, bool useTabs, int tabWidth)
	{
		var tabs = useTabs ? columns / Math.Max(1, tabWidth) : 0;
		builder.Append('\t', tabs).Append(' ', columns - (tabs * Math.Max(1, tabWidth)));
	}

	/// <summary>The column <paramref name="to"/> is at, reading from <paramref name="from"/> at <paramref name="column"/>.</summary>
	private static int Column(string source, int from, int to, int column, int tabWidth)
	{
		var width = Math.Max(1, tabWidth);
		for (var i = from; i < to; i++)
			column = source[i] == '\t' ? column + width - (column % width) : column + 1;
		return column;
	}

	/// <summary>Each line's start and end, its line break included.</summary>
	private static List<(int Start, int End)> Lines(string source)
	{
		var lines = new List<(int, int)>();
		var start = 0;
		for (var i = 0; i < source.Length; i++)
		{
			if (source[i] == '\n' || (source[i] == '\r' && (i + 1 >= source.Length || source[i + 1] != '\n')))
			{
				lines.Add((start, i + 1));
				start = i + 1;
			}
		}

		if (start < source.Length)
			lines.Add((start, source.Length));
		return lines;
	}

	private sealed class Scanner(string source, bool component, int tabWidth)
	{
		private readonly List<Frame> _stack = [];

		// The lines inside the last start tag that spanned lines, by where their content starts.
		private readonly Dictionary<int, TagLine> _tagLines = [];

		// The line being read: where its content starts, and the column it was written at.
		private int _lineContent;
		private int _lineColumn;

		// The last start tag that spanned lines: the column its line was written at and read from, and the columns
		// its tag and its first attribute were written at (-1 for no attribute on that line).
		private int _tagLineColumn;
		private int _tagLineSourceColumn;
		private int _tagColumn;
		private int _firstAttributeColumn;

		// Everything before this position has been read. A line starting before it lies inside something read
		// past its start (a tag's attributes, a comment, a C# block), and keeps its indentation.
		private int _readTo;

		// A C# block whose inside RazorFormatter formats: its closing brace, alone on its line, sits where the
		// block's own line does.
		private int _blockClose = -1;
		private int _blockDepth;

		// Inside a script, style, pre or textarea: its name.
		private string? _raw;

		// The previous C# line in a code block ended mid-statement, so this one continues it.
		private bool _continues;

		// A code construct's header ended a line whose brace is on a later one: that brace opens its block.
		private bool _pendingBrace;

		// Just past the brace that closed the last code block, for an `else`, `catch` or `finally` after it.
		private int _codeBlockEnd = -1;

		public string? Refusal { get; private set; }

		/// <summary>Says where the line being read starts and the column it was written at.</summary>
		public void BeginLine(int content, int column)
		{
			_lineContent = content;
			_lineColumn = column;
		}

		/// <summary>
		/// Whether the line whose content starts at <paramref name="content"/> lies inside a start tag that spans
		/// lines, and if so the column to write it at, or -1 to keep it as written.
		/// </summary>
		public bool InTag(int content, int sourceColumn, int indentColumns, RazorAttributeIndent attributes, bool engineQuirks, out int column)
		{
			if (!_tagLines.TryGetValue(content, out var kind))
			{
				column = -1;
				return false;
			}

			column = kind switch
			{
				TagLine.Closer => _tagLineColumn,
				TagLine.CSharpValue => Math.Max(0, sourceColumn + _tagLineColumn - _tagLineSourceColumn),
				TagLine.TextValue => -1,
				_ => attributes switch
				{
					RazorAttributeIndent.IndentByOne => _tagLineColumn + indentColumns,
					RazorAttributeIndent.IndentByTwo => _tagLineColumn + (2 * indentColumns),
					_ when _firstAttributeColumn >= 0 => _firstAttributeColumn,
					_ when engineQuirks => _tagColumn,
					_ => _tagLineColumn + indentColumns,
				},
			};
			return true;
		}

		/// <summary>The nesting depth to indent the line at, or -1 to keep it as written.</summary>
		public int DepthForLine(int start, int content, int lineEnd)
		{
			if (start < _readTo)
			{
				if (_blockClose >= 0 && content == _blockClose && SourceIsOnly(_blockClose + 1, lineEnd))
					return _blockDepth;
				return -1;
			}

			if (_raw is not null)
			{
				// A script's or style's closing tag goes where its opening tag did; a pre's or textarea's is text.
				var closesRaw = StartsWithClosingTag(content, lineEnd, _raw);
				return closesRaw && _raw is not ("pre" or "textarea") ? Depth - (_stack[^1].Indents ? 1 : 0) : -1;
			}

			if (content == lineEnd)
				return -1;

			if (InCode && _continues && source[content] is not ('<' or '@' or '}' or '{'))
				return -1;

			var depth = Depth;
			if (_stack.Count > 0 && _stack[^1].Indents && ClosesTop(content, lineEnd))
				depth--;
			return depth;
		}

		private bool InCode => _stack.Count > 0 && _stack[^1].Kind is FrameKind.CodeBlock or FrameKind.CSharpBrace;

		/// <summary>How many of the open frames indent the lines inside them.</summary>
		private int Depth
		{
			get
			{
				var depth = 0;
				foreach (var frame in _stack)
				{
					if (frame.Indents)
						depth++;
				}

				return depth;
			}
		}

		public string? Finish()
		{
			if (Refusal is not null)
				return Refusal;
			if (_raw is not null)
				return $"<{_raw}> is never closed";
			if (_stack.Count > 0)
			{
				var top = _stack[^1];
				return top.Kind == FrameKind.Element ? $"<{top.Name}> is never closed" : "a code block is never closed";
			}

			return null;
		}

		/// <summary>Reads the line from <paramref name="from"/>, and past its end into anything still open there.</summary>
		public void ScanLine(int from, int lineEnd)
		{
			var i = Math.Max(from, _readTo);

			// Forgotten once a line past the block's closing brace is reached, not before: the lines inside the
			// block are read past without being scanned, and the brace's own line still needs it.
			if (_blockClose >= 0 && from > _blockClose)
				_blockClose = -1;

			if (_raw is not null)
			{
				var close = source.AsSpan(i, lineEnd - i).IndexOf("</" + _raw, StringComparison.OrdinalIgnoreCase);
				if (close < 0)
				{
					_readTo = Math.Max(_readTo, lineEnd);
					return;
				}

				i += close;
				_raw = null;
			}

			var lastCode = -1;
			while (i < lineEnd && Refusal is null)
			{
				var c = source[i];
				if (c is ' ' or '\t')
				{
					i++;
					continue;
				}

				if (InCode && c != '<' && c != '@')
				{
					i = ScanCSharp(i, lineEnd, ref lastCode);
					continue;
				}

				if (c == '{' && _pendingBrace)
				{
					_pendingBrace = false;
					_stack.Add(new Frame(FrameKind.CodeBlock, null));
					i++;
					continue;
				}

				if (char.IsLetter(c) && FollowsCodeBlock(i) && Continuation(i) is var afterKeyword and >= 0)
				{
					i = CodeConstruct(afterKeyword);
					continue;
				}

				i = c switch
				{
					'<' => ScanTag(i),
					'@' => ScanTransition(i),
					'}' when _stack.Count > 0 && _stack[^1].Kind == FrameKind.Section => Pop(i),
					_ => i + 1,
				};

				if (_raw is not null)
				{
					// The rest of the line after an opening <script> is its content.
					var close = i < lineEnd ? source.AsSpan(i, lineEnd - i).IndexOf("</" + _raw, StringComparison.OrdinalIgnoreCase) : -1;
					if (close < 0)
					{
						i = lineEnd;
						break;
					}

					i += close;
					_raw = null;
				}
			}

			if (InCode)
				_continues = lastCode >= 0 && source[lastCode] is not (';' or '{' or '}');
			else
				_continues = false;

			_readTo = Math.Max(_readTo, i);
		}

		/// <summary>C# inside a code block's braces: its braces nest, everything else is read past.</summary>
		private int ScanCSharp(int i, int lineEnd, ref int lastCode)
		{
			// The lexer reads strings and comments whole, so a brace in either is not one.
			foreach (var token in SyntaxFactory.ParseTokens(source, i, i, CSharpSource.ParseOptions))
			{
				if (token.SpanStart >= lineEnd || token.IsKind(SyntaxKind.EndOfFileToken))
					break;

				if (token.Span.End > lineEnd)
				{
					// A string or comment that runs onto the next lines: those are its text.
					_readTo = token.Span.End;
					return token.Span.End;
				}

				if (token.IsKind(SyntaxKind.LessThanToken) && TokenStartsMarkup(token))
					return token.SpanStart;

				lastCode = token.Span.End - 1;
				if (token.IsKind(SyntaxKind.OpenBraceToken))
					_stack.Add(new Frame(FrameKind.CSharpBrace, null));
				else if (token.IsKind(SyntaxKind.CloseBraceToken))
				{
					if (_stack.Count == 0 || _stack[^1].Kind is not (FrameKind.CSharpBrace or FrameKind.CodeBlock))
					{
						Refusal = "a closing brace that closes nothing";
						return lineEnd;
					}

					var closed = _stack[^1].Kind;
					_stack.RemoveAt(_stack.Count - 1);
					if (closed == FrameKind.CodeBlock)
						_codeBlockEnd = token.Span.End;
					if (!InCode)
						return token.Span.End;
				}

				foreach (var trivia in token.TrailingTrivia)
				{
					if (trivia.IsKind(SyntaxKind.EndOfLineTrivia))
						return lineEnd;
				}
			}

			return lineEnd;
		}

		/// <summary>A <c>&lt;</c> in C# that starts a tag: a letter or <c>/</c> straight after it, at the start of a statement.</summary>
		private bool TokenStartsMarkup(SyntaxToken token)
		{
			var next = token.Span.End;
			if (next >= source.Length || !(char.IsLetter(source[next]) || source[next] == '/'))
				return false;

			var previous = token.GetPreviousToken();
			var before = token.SpanStart - 1;
			while (before >= 0 && source[before] is ' ' or '\t')
				before--;
			return before < 0 || source[before] is '\n' or '\r' or ';' or '{' or '}' or ')' || previous.RawKind == 0;
		}

		/// <summary>True when nothing but whitespace lies between the last code block's closing brace and <paramref name="i"/>.</summary>
		private bool FollowsCodeBlock(int i)
		{
			if (_codeBlockEnd < 0 || _codeBlockEnd > i)
				return false;
			for (var j = _codeBlockEnd; j < i; j++)
			{
				if (!char.IsWhiteSpace(source[j]))
					return false;
			}

			return true;
		}

		/// <summary>
		/// Past `else`, `else if`, `catch` or `finally` at <paramref name="i"/>, where its header (if any) starts; -1
		/// for any other word.
		/// </summary>
		private int Continuation(int i)
		{
			foreach (var keyword in (ReadOnlySpan<string>)["else", "catch", "finally"])
			{
				var end = i + keyword.Length;
				if (!source.AsSpan(i).StartsWith(keyword, StringComparison.Ordinal) || (end < source.Length && char.IsLetterOrDigit(source[end])))
					continue;

				if (keyword == "else")
				{
					var next = SkipWhitespace(end);
					if (
						source.AsSpan(next).StartsWith("if", StringComparison.Ordinal) && (next + 2 >= source.Length || !char.IsLetterOrDigit(source[next + 2]))
					)
						return next + 2;
				}

				return end;
			}

			return -1;
		}

		private int Pop(int i)
		{
			_stack.RemoveAt(_stack.Count - 1);
			return i + 1;
		}

		private int ScanTag(int i)
		{
			if (source.AsSpan(i).StartsWith("<!--"))
				return SkipPast(i + 4, "-->");

			if (i + 1 < source.Length && source[i + 1] == '!')
				return SkipPast(i + 2, ">");

			var closing = i + 1 < source.Length && source[i + 1] == '/';
			var nameStart = closing ? i + 2 : i + 1;
			var nameEnd = nameStart;
			while (nameEnd < source.Length && (char.IsLetterOrDigit(source[nameEnd]) || source[nameEnd] is '-' or '_' or '.' or ':'))
				nameEnd++;

			// `a < b` in text, or `<` before anything but a name: not a tag.
			if (nameEnd == nameStart || !char.IsLetter(source[nameStart]))
				return i + 1;

			var name = source[nameStart..nameEnd];
			var end = TagEnd(nameEnd);
			if (end < 0)
			{
				Refusal = $"<{name}> has no closing '>'";
				return source.Length;
			}

			if (closing)
			{
				if (_stack.Count == 0 || _stack[^1].Kind != FrameKind.Element || !string.Equals(_stack[^1].Name, name, StringComparison.OrdinalIgnoreCase))
				{
					Refusal = $"</{name}> does not close the element it is in";
					return source.Length;
				}

				_stack.RemoveAt(_stack.Count - 1);
				return end;
			}

			if (source.AsSpan(i, end - i).IndexOfAny('\n', '\r') >= 0)
				ReadTagLines(i, nameEnd, end);

			var selfClosing = source[end - 2] == '/';
			if (!selfClosing && !VoidElements.Contains(name))
			{
				_stack.Add(new Frame(FrameKind.Element, name, StartsLine(i) && !name.Equals("html", StringComparison.OrdinalIgnoreCase)));
				if (RawElements.Contains(name))
					_raw = name.ToLowerInvariant();
			}

			return end;
		}

		/// <summary>
		/// Records what each line inside the start tag at <paramref name="tag"/> begins with, and where the tag and
		/// its first attribute were written.
		/// </summary>
		private void ReadTagLines(int tag, int nameEnd, int end)
		{
			_tagLines.Clear();
			_tagLineColumn = _lineColumn;
			_tagLineSourceColumn = Column(source, LineStart(_lineContent), _lineContent, 0, tabWidth);
			_tagColumn = Column(source, _lineContent, tag, _lineColumn, tabWidth);
			_firstAttributeColumn = -1;

			var onTagLine = true;
			var i = nameEnd;
			while (i < end)
			{
				var c = source[i];
				if (c is '\n' or '\r')
				{
					i = SkipWhitespace(i + 1);
					if (i < end && source[i] is not ('\n' or '\r'))
						_tagLines[i] = source[i] == '>' || (source[i] == '/' && i + 1 < end && source[i + 1] == '>') ? TagLine.Closer : TagLine.Attribute;
					onTagLine = false;
					continue;
				}

				if (c is ' ' or '\t' or '>' || (c == '/' && i + 1 < end && source[i + 1] == '>'))
				{
					i++;
					continue;
				}

				if (onTagLine && _firstAttributeColumn < 0)
					_firstAttributeColumn = Column(source, _lineContent, i, _lineColumn, tabWidth);

				i = ReadAttribute(i, end);
			}
		}

		/// <summary>Reads past the attribute at <paramref name="i"/>, recording the lines its value continues on.</summary>
		private int ReadAttribute(int i, int end)
		{
			if (source[i] == '@' && i + 1 < end && source[i + 1] is '(' or '{')
				return MarkValueLines(i, Matching(i + 1), TagLine.CSharpValue);

			var csharp = source[i] == '@';
			while (i < end && source[i] is not (' ' or '\t' or '\n' or '\r' or '=' or '>') && !(source[i] == '/' && i + 1 < end && source[i + 1] == '>'))
				i++;

			var equals = SkipWhitespace(i);
			if (equals >= end || source[equals] != '=')
				return i;

			var value = SkipWhitespace(equals + 1);
			if (value >= end || source[value] is not ('"' or '\''))
			{
				while (value < end && source[value] is not (' ' or '\t' or '\n' or '\r' or '>'))
					value++;
				return value;
			}

			var quote = source[value];
			var inside = value + 1;
			var close = inside;
			if (source.AsSpan(inside).StartsWith("@("))
			{
				csharp = true;
				close = Matching(inside + 1);
			}

			close = close < 0 ? -1 : source.IndexOf(quote, close);
			if (close < 0 || close >= end)
				return end;

			return MarkValueLines(inside, close, csharp ? TagLine.CSharpValue : TagLine.TextValue) + 1;
		}

		/// <summary>Records each line that starts between <paramref name="from"/> and <paramref name="to"/> as <paramref name="kind"/>.</summary>
		private int MarkValueLines(int from, int to, TagLine kind)
		{
			if (to < 0)
				return source.Length;

			for (var i = from; i < to; i++)
			{
				if (source[i] == '\n' || (source[i] == '\r' && (i + 1 >= source.Length || source[i + 1] != '\n')))
					_tagLines[SkipWhitespace(i + 1)] = kind;
			}

			return to;
		}

		/// <summary>Just past the bracket that closes the one at <paramref name="open"/>, or -1.</summary>
		private int Matching(int open) =>
			RazorBlockScanner.PastMatch(
				source,
				open,
				source[open] == '(' ? SyntaxKind.OpenParenToken : SyntaxKind.OpenBraceToken,
				source[open] == '(' ? SyntaxKind.CloseParenToken : SyntaxKind.CloseBraceToken
			);

		private int LineStart(int i)
		{
			while (i > 0 && source[i - 1] is not ('\n' or '\r'))
				i--;
			return i;
		}

		/// <summary>Just past the <c>&gt;</c> that ends a tag whose name ends at <paramref name="i"/>, or -1.</summary>
		private int TagEnd(int i)
		{
			while (i < source.Length)
			{
				switch (source[i])
				{
					case '>':
						return i + 1;
					case '"' or '\'':
						var quote = source.IndexOf(source[i], i + 1);
						if (quote < 0)
							return -1;
						i = quote + 1;
						continue;
					case '@' when i + 1 < source.Length && source[i + 1] is '(' or '{':
						i = RazorBlockScanner.PastMatch(
							source,
							i + 1,
							source[i + 1] == '(' ? SyntaxKind.OpenParenToken : SyntaxKind.OpenBraceToken,
							source[i + 1] == '(' ? SyntaxKind.CloseParenToken : SyntaxKind.CloseBraceToken
						);
						if (i < 0)
							return -1;
						continue;
				}

				i++;
			}

			return -1;
		}

		private int ScanTransition(int i)
		{
			if (i + 1 >= source.Length)
				return i + 1;

			var next = source[i + 1];
			if (next == '@')
				return i + 2;
			if (next == '*')
				return SkipPast(i + 2, "*@");
			if (next == ':')
				return LineEnd(i);

			// `name@example.com` is text.
			if (i > 0 && char.IsLetterOrDigit(source[i - 1]))
				return i + 1;

			if (next == '{')
				return CSharpBlock(i, i + 1);

			var nameEnd = i + 1;
			while (nameEnd < source.Length && (char.IsLetterOrDigit(source[nameEnd]) || source[nameEnd] == '_'))
				nameEnd++;
			var name = source[(i + 1)..nameEnd];

			if ((component && name == "code") || name == "functions")
			{
				var brace = SkipWhitespace(nameEnd);
				return brace < source.Length && source[brace] == '{' ? CSharpBlock(i, brace) : nameEnd;
			}

			if (LineDirectives.Contains(name) && StartsLine(i))
				return LineEnd(nameEnd);

			if (name == "switch")
			{
				Refusal = "@switch is not followed yet";
				return source.Length;
			}

			if (name == "section")
			{
				var brace = source.IndexOf('{', nameEnd);
				if (brace < 0 || source.AsSpan(nameEnd, brace - nameEnd).ContainsAny('\n', '\r'))
					return nameEnd;
				_stack.Add(new Frame(FrameKind.Section, null));
				return brace + 1;
			}

			// `@using (…) { }` is a statement; `@using Some.Namespace` is a directive, text to the end of its line.
			if (name == "using" && (SkipWhitespace(nameEnd) >= source.Length || source[SkipWhitespace(nameEnd)] != '('))
				return LineEnd(nameEnd);

			if (CodeConstructs.Contains(name))
				return CodeConstruct(nameEnd);

			// An implicit or explicit expression, read whole.
			var past = RazorBlockScanner.SkipCode(source, i, component);
			if (past < 0)
			{
				Refusal = "an expression whose end cannot be found";
				return source.Length;
			}

			return past;
		}

		/// <summary>
		/// `@if (…) {`: reads past the header, which keeps its own lines, and opens a code block at the brace.
		/// </summary>
		private int CodeConstruct(int afterName)
		{
			var i = SkipWhitespace(afterName);
			if (i < source.Length && source[i] == '(')
			{
				i = RazorBlockScanner.PastMatch(source, i, SyntaxKind.OpenParenToken, SyntaxKind.CloseParenToken);
				if (i < 0)
				{
					Refusal = "a code block header whose end cannot be found";
					return source.Length;
				}
			}

			var brace = SkipWhitespaceAndLineBreaks(i);
			if (brace >= source.Length || source[brace] != '{')
			{
				Refusal = "a code block without braces";
				return source.Length;
			}

			// A brace on the header's line opens the block there; one on the next line opens it when that line
			// is reached, so the line gets the header's depth.
			if (source.AsSpan(i, brace - i).ContainsAny('\n', '\r'))
			{
				_pendingBrace = true;
				return i;
			}

			_stack.Add(new Frame(FrameKind.CodeBlock, null));
			return brace + 1;
		}

		/// <summary>`@{`, `@code {`, `@functions {`: its inside is RazorFormatter's, so it is read whole.</summary>
		private int CSharpBlock(int at, int brace)
		{
			var close = RazorBlockScanner.FindClose(source, brace);
			if (close < 0)
			{
				Refusal = "a code block is never closed";
				return source.Length;
			}

			var lineStart = at;
			while (lineStart > 0 && source[lineStart - 1] is ' ' or '\t')
				lineStart--;

			_blockClose = close;
			_blockDepth = Depth;
			_readTo = close;
			return close + 1;
		}

		private bool ClosesTop(int content, int lineEnd)
		{
			var top = _stack[^1];
			return top.Kind == FrameKind.Element ? StartsWithClosingTag(content, lineEnd, top.Name!) : source[content] == '}';
		}

		private bool StartsWithClosingTag(int content, int lineEnd, string name) =>
			source.AsSpan(content, lineEnd - content).StartsWith("</" + name, StringComparison.OrdinalIgnoreCase);

		private bool StartsLine(int i)
		{
			while (i > 0 && source[i - 1] is ' ' or '\t')
				i--;
			return i == 0 || source[i - 1] is '\n' or '\r';
		}

		private bool SourceIsOnly(int from, int lineEnd)
		{
			for (var i = from; i < lineEnd; i++)
			{
				if (source[i] is not (' ' or '\t'))
					return false;
			}

			return true;
		}

		private int SkipPast(int from, string terminator)
		{
			var end = source.IndexOf(terminator, from, StringComparison.Ordinal);
			return end < 0 ? source.Length : end + terminator.Length;
		}

		private int LineEnd(int i)
		{
			while (i < source.Length && source[i] is not ('\n' or '\r'))
				i++;
			return i;
		}

		private int SkipWhitespace(int i)
		{
			while (i < source.Length && source[i] is ' ' or '\t')
				i++;
			return i;
		}

		private int SkipWhitespaceAndLineBreaks(int i)
		{
			while (i < source.Length && char.IsWhiteSpace(source[i]))
				i++;
			return i;
		}
	}
}
