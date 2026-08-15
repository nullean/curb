using Nullean.Kerf.Documents;

namespace Nullean.Kerf.Printing;

/// <summary>
/// Lays a document arena out into text.
/// </summary>
/// <remarks>
/// <para>
/// Because documents are stored in preorder, printing is a single forward walk over the array with a
/// stack of <i>scopes</i> rather than a stack of pending commands: a scope records where a region
/// ends and what indent and mode apply inside it, and is popped when the cursor passes its end.
/// Nothing is pushed per child, and the walk has perfect locality.
/// </para>
/// <para>
/// A scope may also carry a resume point, which is how constructs that emit only one of several
/// branches — <see cref="DocKind.IfBreak"/> and <see cref="DocKind.ConditionalGroup"/> — skip the
/// branches they did not take.
/// </para>
/// </remarks>
internal sealed class DocPrinter
{
	private readonly struct Scope(int end, int indent, PrintMode mode, int resume, bool suppressWidth)
	{
		public readonly int End = end;
		public readonly int Indent = indent;
		public readonly PrintMode Mode = mode;

		/// <summary>Where to continue once this scope closes, or -1 to just carry on.</summary>
		public readonly int Resume = resume;

		/// <summary>Inside an <see cref="DocKind.AlwaysFits"/>: emit, but do not count toward the width.</summary>
		public readonly bool SuppressWidth = suppressWidth;
	}

	private readonly BreakPropagation _breaks = new();
	private Scope[] _scopes = new Scope[64];
	private PrintMode[] _groupModes = new PrintMode[64];
	private int _depth;

	private DocArena _arena = null!;
	private ReadOnlyMemory<char> _source;
	private OutputBuffer _output = null!;
	private Indenter _indenter = null!;
	private string _endOfLine = "\n";
	private int _width;
	private int _column;

	public void Print(DocArena arena, ReadOnlyMemory<char> source, in FormatOptions options, OutputBuffer output)
	{
		_arena = arena;
		_source = source;
		_output = output;
		_indenter = new Indenter(options.UseTabs, options.IndentSize);
		_endOfLine = options.ResolveEndOfLine(source.Span);
		_width = options.MaxLineLength;
		_column = 0;
		_depth = 0;

		_breaks.Run(arena);
		EnsureGroupModes(arena.Count);

		Walk(options);

		if (options.InsertFinalNewLine)
			output.EnsureSingleTrailingNewLine(_endOfLine);
		else
			output.RemoveTrailingNewLines();
	}

	private void Walk(in FormatOptions options)
	{
		var count = _arena.Count;
		Push(new Scope(count, 0, PrintMode.Break, -1, false));

		var i = 0;
		while (i < count)
		{
			// Close every scope the cursor has passed, honouring resume points as we go.
			while (_depth > 1 && i >= _scopes[_depth - 1].End)
			{
				var closed = _scopes[--_depth];
				if (closed.Resume > i)
					i = closed.Resume;
			}

			if (i >= count)
				break;

			var scope = _scopes[_depth - 1];
			var doc = _arena[i];

			switch (doc.Kind)
			{
				case DocKind.Null:
				case DocKind.BreakParent:
				case DocKind.Concat:
					i++;
					break;

				case DocKind.SrcText:
					Emit(_source.Span.Slice(doc.A, doc.B), doc.Flags, scope.SuppressWidth);
					i++;
					break;

				case DocKind.SynText:
					Emit(SyntheticText.Get(doc.A), doc.Flags, scope.SuppressWidth);
					i++;
					break;

				case DocKind.Trim:
					_column -= _output.TrimTrailingWhitespace();
					i++;
					break;

				case DocKind.Line:
					PrintLine(doc, scope, options);
					i++;
					break;

				case DocKind.Indent:
					{
						var indent = doc.B == Doc.IndentToRoot ? 0 : scope.Indent + doc.B;
						Push(new Scope(i + doc.Length, Math.Max(0, indent), scope.Mode, -1, scope.SuppressWidth));
						i++;
						break;
					}

				case DocKind.ForceFlat:
					Push(new Scope(i + doc.Length, scope.Indent, PrintMode.ForceFlat, -1, scope.SuppressWidth));
					i++;
					break;

				case DocKind.AlwaysFits:
					Push(new Scope(i + doc.Length, scope.Indent, scope.Mode, -1, true));
					i++;
					break;

				case DocKind.Group:
					{
						var mode = ResolveGroupMode(i, doc, scope);
						if (doc.GroupId != 0)
							_groupModes[doc.GroupId] = mode;
						Push(new Scope(i + doc.Length, scope.Indent, mode, -1, scope.SuppressWidth));
						i++;
						break;
					}

				case DocKind.ConditionalGroup:
					{
						var chosen = ChooseOption(i, doc, scope);
						var mode = _breaks.IsBroken(i) ? PrintMode.Break : PrintMode.Flat;
						if (doc.GroupId != 0)
							_groupModes[doc.GroupId] = mode;

						Push(new Scope(chosen + _arena[chosen].Length, scope.Indent, mode, i + doc.Length, scope.SuppressWidth));
						i = chosen;
						break;
					}

				case DocKind.IfBreak:
					{
						var targetBroken = doc.GroupId != 0
							? _groupModes[doc.GroupId] == PrintMode.Break
							: scope.Mode == PrintMode.Break;

						var flatStart = i + 1;
						var breakStart = flatStart + doc.A;
						var chosen = targetBroken ? breakStart : flatStart;

						Push(new Scope(chosen + _arena[chosen].Length, scope.Indent, scope.Mode, i + doc.Length, scope.SuppressWidth));
						i = chosen;
						break;
					}

				default:
					// Every DocKind is handled above, so this is unreachable today. It throws rather
					// than skipping so that a kind added later fails loudly here instead of being
					// silently dropped from the output.
					throw new InvalidOperationException($"doc {i} has unhandled kind {doc.Kind}");
			}
		}
	}

	private void PrintLine(in Doc doc, in Scope scope, in FormatOptions options)
	{
		var type = doc.LineType;

		// A literal line is content, not layout: it breaks even inside a ForceFlat, and carries no
		// indent because whatever it sits inside (a raw string) owns its own leading whitespace.
		if (type == LineType.Literal)
		{
			_output.Append(_endOfLine);
			_column = 0;
			return;
		}

		if (scope.Mode is PrintMode.Flat or PrintMode.ForceFlat)
		{
			if (type is LineType.Normal or LineType.Hard)
			{
				_output.Append(' ');
				if (!scope.SuppressWidth)
					_column++;
			}
			return;
		}

		if (doc.Flags.HasFlag(DocFlags.OnlyIfNotAtLineStart) && _output.AtLineStart())
			return;

		if (options.TrimTrailingWhitespace && !doc.Flags.HasFlag(DocFlags.NoTrim))
			_output.TrimTrailingWhitespace();

		_output.Append(_endOfLine);
		_output.Append(_indenter.For(scope.Indent));
		_column = _indenter.ColumnsFor(scope.Indent);
	}

	private void Emit(ReadOnlySpan<char> text, DocFlags flags, bool suppressWidth)
	{
		_output.Append(text);

		// Directives are emitted but never counted: an #if must not push the code around it into
		// wrapping that it would not otherwise need.
		if (suppressWidth || flags.HasFlag(DocFlags.IsDirective))
			return;

		_column += TextWidth(text);
	}

	private PrintMode ResolveGroupMode(int index, in Doc doc, in Scope scope)
	{
		if (_breaks.IsBroken(index))
			return PrintMode.Break;

		// Already flat: nothing inside can reintroduce a break, so skip measuring.
		if (scope.Mode is PrintMode.Flat or PrintMode.ForceFlat)
			return scope.Mode;

		// With reflow off there is no width to exceed, so every unbroken group is flat.
		if (_width == FormatOptions.Off)
			return PrintMode.Flat;

		return Fits(index, index + doc.Length, scope) ? PrintMode.Flat : PrintMode.Break;
	}

	/// <summary>Picks the first alternative that fits, falling back to the last and most expanded one.</summary>
	private int ChooseOption(int index, in Doc doc, in Scope scope)
	{
		var end = index + doc.Length;
		var last = index + 1;

		for (var option = index + 1; option < end; option += _arena[option].Length)
		{
			last = option;
			if (_width == FormatOptions.Off || Fits(option, option + _arena[option].Length, scope))
				return option;
		}

		return last;
	}

	/// <summary>
	/// Measures whether <c>[start, stop)</c> printed flat, plus whatever follows it up to the next
	/// break, still fits in the remaining columns.
	/// </summary>
	/// <remarks>
	/// The lookahead past <paramref name="stop"/> matters: a group that fits on its own can still be
	/// pushed over the limit by the text that trails it, and measuring only the group would wrap in
	/// the wrong place. Preorder makes "what comes next" simply the next index, so this is a forward
	/// scan rather than a walk back through a command stack.
	/// </remarks>
	private bool Fits(int start, int stop, in Scope scope)
	{
		var remaining = _width - _column;
		if (remaining < 0)
			return false;

		var enclosingEnd = scope.End;
		var i = start;

		while (i < enclosingEnd)
		{
			var doc = _arena[i];

			// Inside the candidate we are measuring a flat layout; past it, the enclosing mode
			// applies and a real break means we got to the end of the line intact.
			var flat = i < stop;

			switch (doc.Kind)
			{
				case DocKind.AlwaysFits:
					i += doc.Length;
					continue;

				case DocKind.SrcText:
					if (!doc.Flags.HasFlag(DocFlags.IsDirective))
						remaining -= TextWidth(_source.Span.Slice(doc.A, doc.B));
					break;

				case DocKind.SynText:
					remaining -= TextWidth(SyntheticText.Get(doc.A));
					break;

				case DocKind.Line:
					{
						var type = doc.LineType;
						if (type == LineType.Literal)
							return true;

						if (flat)
						{
							if (type is LineType.Normal or LineType.Hard)
								remaining--;
							break;
						}

						// A break in the trailing context ends the line, so everything so far fits.
						if (scope.Mode == PrintMode.Break)
							return true;

						if (type is LineType.Normal or LineType.Hard)
							remaining--;
						break;
					}

				case DocKind.IfBreak:
					{
						// Measure only the branch that would actually print.
						var targetBroken = !flat && scope.Mode == PrintMode.Break;
						var flatStart = i + 1;
						var chosen = targetBroken ? flatStart + doc.A : flatStart;
						var chosenLength = _arena[chosen].Length;

						// Skip the node and the branch not taken by measuring the chosen one in place.
						if (!Fits(chosen, chosen + chosenLength, scope))
							return false;

						i += doc.Length;
						continue;
					}

				default:
					break;
			}

			if (remaining < 0)
				return false;

			i++;
		}

		return remaining >= 0;
	}

	/// <summary>
	/// Columns a run of text occupies.
	/// </summary>
	/// <remarks>
	/// Surrogate pairs count once. East Asian wide characters should count twice; that table is not
	/// wired up yet and is tracked for when the syntax printers start handling string literals.
	/// </remarks>
	private static int TextWidth(ReadOnlySpan<char> text)
	{
		var width = 0;
		for (var i = 0; i < text.Length; i++)
		{
			if (char.IsHighSurrogate(text[i]) && i + 1 < text.Length && char.IsLowSurrogate(text[i + 1]))
				i++;
			width++;
		}
		return width;
	}

	private void Push(Scope scope)
	{
		if (_depth == _scopes.Length)
			Array.Resize(ref _scopes, _scopes.Length * 2);
		_scopes[_depth++] = scope;
	}

	private void EnsureGroupModes(int count)
	{
		if (_groupModes.Length < count + 1)
			_groupModes = new PrintMode[count + 1];
		else
			Array.Clear(_groupModes);
	}
}
