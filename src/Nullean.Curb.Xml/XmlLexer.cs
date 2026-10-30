using System.Buffers;
using System.Globalization;

namespace Nullean.Curb.Xml;

/// <summary>Where and why a document is not well-formed XML.</summary>
/// <param name="Position">The source offset the problem was found at.</param>
/// <param name="Message">What is wrong, without the position.</param>
internal readonly record struct XmlError(int Position, string Message)
{
	/// <summary>The message with a one-based <c>line:column</c> in front, for the run's report.</summary>
	public string Describe(ReadOnlySpan<char> source)
	{
		var line = 1;
		var lineStart = 0;
		var end = Math.Min(Position, source.Length);
		for (var i = 0; i < end; i++)
		{
			if (source[i] != '\n')
				continue;
			line++;
			lineStart = i + 1;
		}

		return string.Create(CultureInfo.InvariantCulture, $"({line},{end - lineStart + 1}): {Message}");
	}
}

/// <summary>
/// Splits XML into <see cref="XmlToken"/>s that cover every character of it, and refuses anything
/// that is not well-formed at the level of a single token.
/// </summary>
/// <remarks>
/// <para>
/// Lossless by construction: each token starts where the previous one ended, and nothing is
/// normalised, decoded or skipped. Entity references stay raw inside the text and attribute values
/// they appear in, and a declaration, comment, CDATA section, processing instruction or DOCTYPE is
/// one token whatever it contains. The printer can then emit anything it does not lay out as a
/// slice of the source, and the verifier can compare the output's tokens with the input's.
/// </para>
/// <para>
/// What it checks is what a single token can get wrong: names, quoting, <c>--</c> inside a comment,
/// a reference that is not one, <c>&lt;</c> inside an attribute value, a character XML forbids.
/// Nesting and duplicate attributes are <see cref="XmlTree"/>'s, since they need the tokens around.
/// Either way a document that is not well-formed is refused, never repaired.
/// </para>
/// </remarks>
internal ref struct XmlLexer
{
	/// <summary>Characters XML 1.0 does not allow anywhere in a document, references aside.</summary>
	private static readonly SearchValues<char> Forbidden = SearchValues.Create(
		"\0\u0001\u0002\u0003\u0004\u0005\u0006\u0007\u0008\u000B\u000C\u000E\u000F"
		+ "\u0010\u0011\u0012\u0013\u0014\u0015\u0016\u0017\u0018\u0019\u001A\u001B\u001C\u001D\u001E\u001F￾￿");

	private static readonly SearchValues<char> TextSpecials = SearchValues.Create("&]");
	private static readonly SearchValues<char> ValueSpecials = SearchValues.Create("&<");
	private static readonly SearchValues<char> LineBreaks = SearchValues.Create("\r\n");
	private const string Whitespace = " \t\r\n";

	private readonly ReadOnlySpan<char> _source;
	private readonly XmlTokenBuffer _tokens;
	private int _position;
	private bool _sawDoctype;
	private XmlError _error;

	private XmlLexer(ReadOnlySpan<char> source, XmlTokenBuffer tokens)
	{
		_source = source;
		_tokens = tokens;
	}

	/// <summary>Lexes <paramref name="source"/> into <paramref name="tokens"/>, which it resets first.</summary>
	/// <returns>False, with <paramref name="error"/> set, when the source is not well-formed.</returns>
	public static bool Lex(ReadOnlySpan<char> source, XmlTokenBuffer tokens, out XmlError error)
	{
		tokens.Reset(source.Length);
		var lexer = new XmlLexer(source, tokens);
		var ok = lexer.Run();
		error = lexer._error;

#if DEBUG
		if (ok)
			AssertCovers(source, tokens.Tokens);
#endif
		return ok;
	}

	/// <summary>True when the tokens, concatenated, are exactly the source.</summary>
	public static bool Covers(ReadOnlySpan<char> source, ReadOnlySpan<XmlToken> tokens)
	{
		var at = 0;
		foreach (var token in tokens)
		{
			if (token.Start != at || token.Length <= 0)
				return false;
			at = token.End;
		}

		return at == source.Length;
	}

#if DEBUG
	private static void AssertCovers(ReadOnlySpan<char> source, ReadOnlySpan<XmlToken> tokens)
	{
		if (!Covers(source, tokens))
			throw new InvalidOperationException("the XML lexer's tokens do not cover the source exactly");
	}
#endif

	private bool Run()
	{
		var forbidden = _source.IndexOfAny(Forbidden);
		if (forbidden >= 0)
			return Fail(forbidden, "a character XML does not allow");

		while (_position < _source.Length)
		{
			var ok = _source[_position] == '<' ? LexMarkup() : LexText();
			if (!ok)
				return false;
		}

		return true;
	}

	private bool Fail(int position, string message)
	{
		_error = new XmlError(position, message);
		return false;
	}

	private readonly void Add(XmlTokenKind kind, int start, XmlTokenFlags flags = XmlTokenFlags.None)
	{
		_tokens.Add(new XmlToken(kind, start, _position - start, flags));
	}

	private bool LexText()
	{
		var start = _position;
		var rest = _source[start..];
		var length = rest.IndexOf('<');
		if (length < 0)
			length = rest.Length;

		var text = rest[..length];
		for (var i = text.IndexOfAny(TextSpecials); i >= 0;)
		{
			if (text[i] == ']')
			{
				if (text[i..].StartsWith("]]>"))
					return Fail(start + i, "']]>' outside a CDATA section");
				i++;
			}
			else
			{
				var reference = ReferenceLength(text[i..]);
				if (reference < 0)
					return Fail(start + i, "an '&' that does not start a valid reference");
				i += reference;
			}

			var next = text[i..].IndexOfAny(TextSpecials);
			i = next < 0 ? -1 : i + next;
		}

		_position = start + length;
		var flags = text.IndexOfAnyExcept(Whitespace) < 0 ? XmlTokenFlags.WhitespaceOnly : XmlTokenFlags.None;
		if (text.IndexOfAny(LineBreaks) >= 0)
			flags |= XmlTokenFlags.HasNewLine;
		Add(XmlTokenKind.Text, start, flags);
		return true;
	}

	/// <summary>
	/// The length of the reference at the start of <paramref name="text"/>, or -1 when it is not one.
	/// </summary>
	/// <remarks>
	/// Without a DOCTYPE only XML's five predefined entities exist, so any other name is an undefined
	/// entity and the document is not well-formed. With one, a name may be declared in it, which this
	/// does not try to resolve.
	/// </remarks>
	private readonly int ReferenceLength(ReadOnlySpan<char> text)
	{
		if (text.Length < 3)
			return -1;

		if (text[1] == '#')
		{
			var hex = text.Length > 2 && text[2] == 'x';
			var digitsStart = hex ? 3 : 2;
			var i = digitsStart;
			var value = 0L;
			for (; i < text.Length && i - digitsStart < 8; i++)
			{
				var c = text[i];
				int digit;
				if (c is >= '0' and <= '9')
					digit = c - '0';
				else if (hex && (c | 0x20) is >= 'a' and <= 'f')
					digit = (c | 0x20) - 'a' + 10;
				else
					break;
				value = (value * (hex ? 16 : 10)) + digit;
			}

			if (i == digitsStart || i >= text.Length || text[i] != ';' || !IsCharacter(value))
				return -1;
			return i + 1;
		}

		var nameLength = NameLength(text[1..]);
		if (nameLength == 0 || 1 + nameLength >= text.Length || text[1 + nameLength] != ';')
			return -1;

		var name = text.Slice(1, nameLength);
		if (!_sawDoctype && name is not ("amp" or "lt" or "gt" or "quot" or "apos"))
			return -1;

		return nameLength + 2;
	}

	private static bool IsCharacter(long value) =>
		value is 0x9 or 0xA or 0xD or (>= 0x20 and <= 0xD7FF) or (>= 0xE000 and <= 0xFFFD) or (>= 0x10000 and <= 0x10FFFF);

	private bool LexMarkup()
	{
		var start = _position;
		var rest = _source[start..];
		if (rest.Length < 2)
			return Fail(start, "a '<' at the end of the document");

		switch (rest[1])
		{
			case '?':
				return LexProcessingInstruction(start, rest);
			case '!':
				if (rest.StartsWith("<!--"))
					return LexComment(start, rest);
				if (rest.StartsWith("<![CDATA["))
					return LexDelimited(start, rest, 9, "]]>", XmlTokenKind.CData, "an unterminated CDATA section");
				if (rest.StartsWith("<!DOCTYPE"))
					return LexDoctype(start, rest);
				return Fail(start, "an unrecognised '<!' declaration");
			case '/':
				return LexEndTag(start, rest);
			default:
				return LexStartTag(start, rest);
		}
	}

	private bool LexProcessingInstruction(int start, ReadOnlySpan<char> rest)
	{
		var target = NameLength(rest[2..]);
		if (target == 0)
			return Fail(start + 2, "a processing instruction without a target");

		var end = rest.IndexOf("?>");
		if (end < 0)
			return Fail(start, "an unterminated processing instruction");

		var afterTarget = 2 + target;
		if (afterTarget < end && !IsWhitespace(rest[afterTarget]))
			return Fail(start + afterTarget, "a processing instruction target not followed by whitespace");

		var name = rest.Slice(2, target);
		var kind = XmlTokenKind.ProcessingInstruction;
		if (name.Equals("xml", StringComparison.OrdinalIgnoreCase))
		{
			if (start != 0 || !name.SequenceEqual("xml"))
				return Fail(start, "an XML declaration anywhere but the very start of the document");
			kind = XmlTokenKind.XmlDeclaration;
		}

		_position = start + end + 2;
		Add(kind, start, rest[..(end + 2)].IndexOfAny(LineBreaks) >= 0 ? XmlTokenFlags.HasNewLine : XmlTokenFlags.None);
		return true;
	}

	private bool LexComment(int start, ReadOnlySpan<char> rest)
	{
		var end = rest[4..].IndexOf("-->");
		if (end < 0)
			return Fail(start, "an unterminated comment");

		var body = rest.Slice(4, end);
		var doubleHyphen = body.IndexOf("--");
		if (doubleHyphen >= 0)
			return Fail(start + 4 + doubleHyphen, "'--' inside a comment");
		if (body.Length > 0 && body[^1] == '-')
			return Fail(start + 4 + end - 1, "a comment ending in '--->'");

		_position = start + 4 + end + 3;
		Add(XmlTokenKind.Comment, start, body.IndexOfAny(LineBreaks) >= 0 ? XmlTokenFlags.HasNewLine : XmlTokenFlags.None);
		return true;
	}

	private bool LexDelimited(int start, ReadOnlySpan<char> rest, int open, string close, XmlTokenKind kind, string unterminated)
	{
		var end = rest[open..].IndexOf(close);
		if (end < 0)
			return Fail(start, unterminated);

		_position = start + open + end + close.Length;
		Add(kind, start, rest[..(open + end)].IndexOfAny(LineBreaks) >= 0 ? XmlTokenFlags.HasNewLine : XmlTokenFlags.None);
		return true;
	}

	/// <summary>A DOCTYPE, internal subset and all, as one verbatim token.</summary>
	/// <remarks>
	/// Quoted literals and comments are skipped as wholes, so a <c>]</c> or <c>&gt;</c> inside one
	/// does not end the subset or the declaration early.
	/// </remarks>
	private bool LexDoctype(int start, ReadOnlySpan<char> rest)
	{
		if (_sawDoctype)
			return Fail(start, "a second DOCTYPE");

		var depth = 0;
		for (var i = 9; i < rest.Length; i++)
		{
			switch (rest[i])
			{
				case '"' or '\'':
					var close = rest[(i + 1)..].IndexOf(rest[i]);
					if (close < 0)
						return Fail(start + i, "an unterminated literal in a DOCTYPE");
					i += close + 1;
					break;
				case '<' when rest[i..].StartsWith("<!--"):
					var commentEnd = rest[(i + 4)..].IndexOf("-->");
					if (commentEnd < 0)
						return Fail(start + i, "an unterminated comment in a DOCTYPE");
					i += 4 + commentEnd + 2;
					break;
				case '[':
					depth++;
					break;
				case ']':
					depth--;
					break;
				case '>' when depth == 0:
					_sawDoctype = true;
					_position = start + i + 1;
					Add(XmlTokenKind.Doctype, start, rest[..i].IndexOfAny(LineBreaks) >= 0 ? XmlTokenFlags.HasNewLine : XmlTokenFlags.None);
					return true;
			}
		}

		return Fail(start, "an unterminated DOCTYPE");
	}

	private bool LexEndTag(int start, ReadOnlySpan<char> rest)
	{
		var name = NameLength(rest[2..]);
		if (name == 0)
			return Fail(start + 2, "an end tag without a name");

		var i = 2 + name;
		while (i < rest.Length && IsWhitespace(rest[i]))
			i++;

		if (i >= rest.Length || rest[i] != '>')
			return Fail(start + i, "an end tag not closed by '>'");

		_position = start + i + 1;
		Add(XmlTokenKind.EndTag, start, rest[..i].IndexOfAny(LineBreaks) >= 0 ? XmlTokenFlags.HasNewLine : XmlTokenFlags.None);
		return true;
	}

	private bool LexStartTag(int start, ReadOnlySpan<char> rest)
	{
		var name = NameLength(rest[1..]);
		if (name == 0)
			return Fail(start + 1, "a '<' that does not start a tag");

		_position = start + 1 + name;
		Add(XmlTokenKind.StartTagOpen, start);

		var separated = false;
		while (_position < _source.Length)
		{
			var c = _source[_position];

			if (IsWhitespace(c))
			{
				var spaceStart = _position;
				while (_position < _source.Length && IsWhitespace(_source[_position]))
					_position++;
				Add(XmlTokenKind.TagSpace, spaceStart,
					_source[spaceStart.._position].IndexOfAny(LineBreaks) >= 0 ? XmlTokenFlags.HasNewLine : XmlTokenFlags.None);
				separated = true;
				continue;
			}

			if (c == '>')
			{
				_position++;
				Add(XmlTokenKind.TagClose, _position - 1);
				return true;
			}

			if (c == '/')
			{
				if (_position + 1 >= _source.Length || _source[_position + 1] != '>')
					return Fail(_position, "a '/' in a tag not followed by '>'");
				_position += 2;
				Add(XmlTokenKind.EmptyTagClose, _position - 2);
				return true;
			}

			if (!separated)
				return Fail(_position, "an attribute not separated from what precedes it by whitespace");

			if (!LexAttribute())
				return false;
			separated = false;
		}

		return Fail(start, "an unterminated start tag");
	}

	private bool LexAttribute()
	{
		var nameStart = _position;
		var name = NameLength(_source[nameStart..]);
		if (name == 0)
			return Fail(nameStart, "an unexpected character in a tag");

		_position += name;
		Add(XmlTokenKind.AttributeName, nameStart);

		var equalsStart = _position;
		while (_position < _source.Length && IsWhitespace(_source[_position]))
			_position++;
		if (_position >= _source.Length || _source[_position] != '=')
			return Fail(_position, "an attribute without '='");
		_position++;
		while (_position < _source.Length && IsWhitespace(_source[_position]))
			_position++;
		Add(XmlTokenKind.AttributeEquals, equalsStart,
			_source[equalsStart.._position].IndexOfAny(LineBreaks) >= 0 ? XmlTokenFlags.HasNewLine : XmlTokenFlags.None);

		var valueStart = _position;
		if (_position >= _source.Length || _source[_position] is not ('"' or '\''))
			return Fail(_position, "an attribute value without quotes");

		var close = _source[(valueStart + 1)..].IndexOf(_source[valueStart]);
		if (close < 0)
			return Fail(valueStart, "an unterminated attribute value");

		var value = _source.Slice(valueStart + 1, close);
		for (var i = value.IndexOfAny(ValueSpecials); i >= 0;)
		{
			if (value[i] == '<')
				return Fail(valueStart + 1 + i, "a '<' inside an attribute value");

			var reference = ReferenceLength(value[i..]);
			if (reference < 0)
				return Fail(valueStart + 1 + i, "an '&' that does not start a valid reference");
			i += reference;

			var next = value[i..].IndexOfAny(ValueSpecials);
			i = next < 0 ? -1 : i + next;
		}

		_position = valueStart + close + 2;
		Add(XmlTokenKind.AttributeValue, valueStart, value.IndexOfAny(LineBreaks) >= 0 ? XmlTokenFlags.HasNewLine : XmlTokenFlags.None);
		return true;
	}

	private static bool IsWhitespace(char c) => c is ' ' or '\t' or '\r' or '\n';

	/// <summary>The length of the XML name at the start of <paramref name="text"/>, or 0 when there is none.</summary>
	/// <remarks>ASCII exactly as XML 1.0 defines it; beyond ASCII, letters, digits, marks and connectors.</remarks>
	internal static int NameLength(ReadOnlySpan<char> text)
	{
		if (text.IsEmpty || !IsNameStart(text[0]))
			return 0;

		var i = 1;
		while (i < text.Length && IsNameChar(text[i]))
			i++;
		return i;
	}

	private static bool IsNameStart(char c) =>
		c < 0x80
			? (c | 0x20) is >= 'a' and <= 'z' || c is '_' or ':'
			: char.IsLetter(c) || char.IsSurrogate(c);

	private static bool IsNameChar(char c)
	{
		if (c < 0x80)
			return ((c | 0x20) is >= 'a' and <= 'z') || c is (>= '0' and <= '9') or '_' or ':' or '-' or '.';

		if (char.IsLetterOrDigit(c) || char.IsSurrogate(c) || c == '·')
			return true;

		return CharUnicodeInfo.GetUnicodeCategory(c) is UnicodeCategory.NonSpacingMark or UnicodeCategory.SpacingCombiningMark
			or UnicodeCategory.ConnectorPunctuation;
	}
}
