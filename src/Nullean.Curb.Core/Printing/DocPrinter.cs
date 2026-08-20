using Nullean.Curb.Documents;

namespace Nullean.Curb.Printing;

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
	private int _tabWidth = 4;
	private bool _useTabs;
	private bool _trimTrailingWhitespace;

	/// <summary>Columns captured by <see cref="DocKind.Anchor"/>, read back by an aligned break.</summary>
	private readonly int[] _anchors = new int[4];

	// --- round-trip risk tracking ------------------------------------------------------------
	private int _lastSourceEnd;
	private bool _insideLineComment;

	/// <summary>
	/// True when this document could have changed the token stream, so the output is worth
	/// re-parsing.
	/// </summary>
	/// <remarks>
	/// Re-parsing every file to prove nothing broke doubles the cost of a run. But the only damage
	/// re-parsing can find is a changed token boundary, and the printer knows where that risk is: it
	/// happens when two things separated in the source come out adjacent, with characters that lex
	/// together. Tracking that during printing costs a comparison per emitted run and lets the
	/// expensive check become a targeted fallback rather than a blanket tax.
	/// </remarks>
	public bool RoundTripAtRisk { get; private set; }

	public void Print(DocArena arena, ReadOnlyMemory<char> source, in FormatOptions options, OutputBuffer output)
	{
		_arena = arena;
		_source = source;
		_output = output;
		_indenter = new Indenter(options.UseTabs, options.IndentSize, options.TabWidth);
		_tabWidth = Math.Max(1, options.TabWidth);
		_useTabs = options.UseTabs;
		_endOfLine = options.ResolveEndOfLine(source.Span);
		_width = options.MaxLineLength;
		_trimTrailingWhitespace = options.TrimTrailingWhitespace;
		_column = 0;
		_depth = 0;
		_lastSourceEnd = -1;
		_insideLineComment = false;

		// Verbatim runs are re-emitted line by line with the configured ending, so when that differs
		// from the source's own ending the content of a multi-line string literal changes. The
		// boundary tracking below cannot see that — but only files that actually contain a verbatim
		// or raw string literal are at risk, so check for the relevant delimiters before forcing a
		// round-trip parse.
		var sourceNewLine = source.Span.IndexOf('\n');
		var sourceUsesCrLf = sourceNewLine > 0 && source.Span[sourceNewLine - 1] == '\r';
		RoundTripAtRisk = sourceNewLine >= 0 && sourceUsesCrLf != (_endOfLine == "\r\n")
			&& HasVerbatimOrRawString(source.Span);

		_breaks.Run(arena);
		EnsureGroupModes(arena.Count);

		RunSegment(0, arena.Count, 0, PrintMode.Break, false);

		// insert_final_newline terminates the last line; a file with no lines has nothing to
		// terminate, so an empty result stays empty rather than becoming a lone newline.
		if (options.InsertFinalNewLine && output.Length > 0)
			output.EnsureSingleTrailingNewLine(_endOfLine);
		else
			output.RemoveTrailingNewLines();
	}

	/// <summary>
	/// Walks <c>[start, end)</c> under one freshly pushed scope, and returns once that scope has
	/// closed.
	/// </summary>
	/// <remarks>
	/// The whole document is one call from <see cref="Print"/> with <c>[0, count)</c>. A construct that
	/// needs to make more than one flat-or-broken decision within itself — today, only
	/// <see cref="PrintFill"/> — calls back in in for one child at a time instead, which is safe because
	/// scopes are a stack: pushing more on top while an outer call is still on it is exactly what a
	/// stack is for, and <paramref name="start"/>/<paramref name="end"/> bound each call to its own
	/// slice of the arena regardless of how deep it is nested.
	/// </remarks>
	private void RunSegment(int start, int end, int indent, PrintMode mode, bool suppressWidth)
	{
		var baseDepth = _depth;
		Push(new Scope(end, indent, mode, -1, suppressWidth));

		var i = start;
		while (i < end)
		{
			// Close every scope the cursor has passed, honouring resume points as we go. Bounded to
			// this call's own scopes: an outer call's scopes below baseDepth are none of this one's
			// business, and closing past them would pop a scope this call never pushed.
			while (_depth > baseDepth + 1 && i >= _scopes[_depth - 1].End)
			{
				var closed = _scopes[--_depth];
				if (closed.Resume > i)
					i = closed.Resume;
			}

			if (i >= end)
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
					{
						var text = _source.Span.Slice(doc.A, doc.B);
						TrackRoundTripRisk(doc, text);
						Emit(text, doc.Flags, scope.SuppressWidth);
						_lastSourceEnd = doc.A + doc.B;
						i++;
						break;
					}

				case DocKind.SynText:
					Emit(SyntheticText.Get(doc.A), doc.Flags, scope.SuppressWidth);
					i++;
					break;

				case DocKind.ExternalText:
					Emit(_arena.ExternalTexts[doc.A], doc.Flags, scope.SuppressWidth);
					i++;
					break;

				case DocKind.Anchor:
					_anchors[doc.A] = _column;
					i++;
					break;

				case DocKind.Trim:
					_column -= _output.TrimTrailingWhitespace();
					i++;
					break;

				case DocKind.Line:
					PrintLine(doc, scope);
					i++;
					break;

				case DocKind.Indent:
					{
						// An indent aimed at another group applies only when that group broke. A
						// group the walk has not reached has no mode, and an indent nobody asked for
						// is the safer of the two answers.
						if (doc.GroupId != 0 && _groupModes[doc.GroupId] != PrintMode.Break)
						{
							Push(new Scope(i + doc.Length, scope.Indent, scope.Mode, -1, scope.SuppressWidth));
							i++;
							break;
						}

						var indentLevel = doc.B == Doc.IndentToRoot ? 0 : scope.Indent + doc.B;
						Push(new Scope(i + doc.Length, Math.Max(0, indentLevel), scope.Mode, -1, scope.SuppressWidth));
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
						var groupMode = ResolveGroupMode(i, doc, scope);
						if (doc.GroupId != 0)
							_groupModes[doc.GroupId] = groupMode;
						Push(new Scope(i + doc.Length, scope.Indent, groupMode, -1, scope.SuppressWidth));
						i++;
						break;
					}

				case DocKind.ConditionalGroup:
					{
						var chosen = ChooseOption(i, doc, scope);
						var conditionalMode = _breaks.IsBroken(i) ? PrintMode.Break : PrintMode.Flat;
						if (doc.GroupId != 0)
							_groupModes[doc.GroupId] = conditionalMode;

						Push(new Scope(chosen + _arena[chosen].Length, scope.Indent, conditionalMode, i + doc.Length, scope.SuppressWidth));
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

				case DocKind.Fill:
					PrintFill(i, doc, scope);
					i += doc.Length;
					break;

				default:
					// Every DocKind is handled above, so this is unreachable today. It throws rather
					// than skipping so that a kind added later fails loudly here instead of being
					// silently dropped from the output.
					throw new InvalidOperationException($"doc {i} has unhandled kind {doc.Kind}");
			}
		}

		// Pop back to where this call started. The loop above only pops scopes strictly finished
		// before `end`; the one pushed here closes exactly at `end`, so it — and, defensively, anything
		// still open under it — is popped explicitly rather than by one more iteration that never runs.
		_depth = baseDepth;
	}

	/// <summary>
	/// Packs a <see cref="DocKind.Fill"/>'s alternating item/separator children onto a line, breaking a
	/// separator only when the pair it joins would not fit.
	/// </summary>
	/// <remarks>
	/// <para>
	/// The decision is the same one Prettier's <c>fill</c> makes, adapted to run incrementally rather
	/// than by recursing on "the rest": for every item but the last, measure the item together with its
	/// trailing separator and the item after it (<c>pairFits</c>). If that fits, both print flat — the
	/// separator stays a space. If not, the separator breaks, and the item alone still prints flat when
	/// it fits by itself; only an item that does not even fit alone prints broken.
	/// </para>
	/// <para>
	/// <see cref="Fits"/> already measures "flat here, then whatever follows up to the next real break"
	/// — exactly what a fill decision needs — so no new measurement primitive was needed, only this
	/// orchestration. Reusing it for a range that does not start where the enclosing scope's own
	/// candidate started is why <see cref="Fits"/> takes explicit bounds rather than reading them off
	/// the scope.
	/// </para>
	/// </remarks>
	private void PrintFill(int index, in Doc doc, in Scope scope)
	{
		var end = index + doc.Length;

		// Already decided flat, or no width to exceed, and nothing inside forces a break regardless:
		// every item and separator prints flat without spending a Fits call on each pair — the same
		// short-circuit ResolveGroupMode takes for a nested group. Guarded on HasHardBreak because an
		// unconditional line — most commonly a trailing // comment on an element — renders as a bare
		// space rather than the newline it must be once it is reached inside a scope already
		// committed to flat (see PrintLine), the same reason ResolveGroupMode checks IsBroken before
		// either of its own shortcuts; a fill's items are not a group ResolveGroupMode ever resolves,
		// so nothing else stands between one and that failure mode.
		if (!_breaks.HasHardBreak(index, end))
		{
			if (scope.Mode is PrintMode.Flat or PrintMode.ForceFlat)
			{
				RunSegment(index + 1, end, scope.Indent, scope.Mode, scope.SuppressWidth);
				return;
			}

			if (_width == FormatOptions.Off)
			{
				RunSegment(index + 1, end, scope.Indent, PrintMode.Flat, scope.SuppressWidth);
				return;
			}
		}

		var child = index + 1;
		while (child < end)
		{
			var itemStart = child;
			var itemEnd = itemStart + _arena[itemStart].Length;

			if (itemEnd >= end)
			{
				// The last item: no separator follows it, so there is no pair to look ahead to.
				var lastMode = !_breaks.HasHardBreak(itemStart, itemEnd) && Fits(itemStart, itemEnd, scope)
					? PrintMode.Flat
					: PrintMode.Break;
				RunSegment(itemStart, itemEnd, scope.Indent, lastMode, scope.SuppressWidth);
				break;
			}

			var sepStart = itemEnd;
			var sepEnd = sepStart + _arena[sepStart].Length;
			var nextItemEnd = sepEnd + _arena[sepEnd].Length;

			var pairFits = !_breaks.HasHardBreak(itemStart, nextItemEnd) && Fits(itemStart, nextItemEnd, scope);
			var itemFitsAlone = !_breaks.HasHardBreak(itemStart, itemEnd) && Fits(itemStart, itemEnd, scope);
			var itemMode = pairFits || itemFitsAlone ? PrintMode.Flat : PrintMode.Break;
			var sepMode = pairFits ? PrintMode.Flat : PrintMode.Break;

			RunSegment(itemStart, itemEnd, scope.Indent, itemMode, scope.SuppressWidth);
			RunSegment(sepStart, sepEnd, scope.Indent, sepMode, scope.SuppressWidth);

			// To the item just printed, past its own separator — not past the next item too, which
			// this iteration only measured as pairFits's lookahead and never actually printed.
			child = sepEnd;
		}
	}

	private void PrintLine(in Doc doc, in Scope scope)
	{
		var type = doc.LineType;

		// A line aimed at another group prints only if that group broke, and prints nothing at all
		// otherwise — see DocArena.LineIfBroken.
		if (doc.GroupId != 0 && _groupModes[doc.GroupId] != PrintMode.Break)
			return;

		if (doc.Flags.HasFlag(DocFlags.OnlyIfNotAtLineStart) && _output.AtLineStart())
			return;

		// A literal line is content, not layout: it breaks even inside a ForceFlat, and carries no
		// indent because whatever it sits inside (a raw string) owns its own leading whitespace.
		if (type == LineType.Literal)
		{
			_output.Append(_endOfLine);
			_column = 0;
			_insideLineComment = false;
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

		if (_trimTrailingWhitespace && !doc.Flags.HasFlag(DocFlags.NoTrim))
			_output.TrimTrailingWhitespace();

		if (doc.Flags.HasFlag(DocFlags.Reindent))
		{
			// Reuse the current line when nothing has been written to it, so trivia that already
			// broke does not turn into a blank line.
			_output.TrimTrailingWhitespace();
			if (!_output.AtLineStart())
				_output.Append(_endOfLine);
			_output.Append(_indenter.For(scope.Indent));
			_column = _indenter.ColumnsFor(scope.Indent);
			_insideLineComment = false;
			return;
		}

		if (doc.Flags.HasFlag(DocFlags.AlignToAnchor))
		{
			// The comment above has already ended its own line, so reuse it rather than opening
			// another — the same reason Reindent exists.
			_output.TrimTrailingWhitespace();
			if (!_output.AtLineStart())
				_output.Append(_endOfLine);

			// Tabs as far as they reach, then spaces for the remainder — what dotnet format writes,
			// and the only way to land on a column no tab stop falls on while still honouring
			// indent_style. Under `indent_style = space` the loop simply never fires.
			var target = Math.Max(0, _anchors[doc.B]);
			var at = 0;

			while (_useTabs && at + _tabWidth <= target)
			{
				_output.Append('\t');
				at += _tabWidth;
			}

			for (; at < target; at++)
				_output.Append(' ');

			_column = target;
			_insideLineComment = false;
			return;
		}

		_output.Append(_endOfLine);
		_output.Append(_indenter.For(scope.Indent));
		_column = _indenter.ColumnsFor(scope.Indent);
		_insideLineComment = false;
	}

	/// <summary>
	/// Notes whether this run of text could have moved a token boundary, which is the only thing
	/// re-parsing the output would discover.
	/// </summary>
	private void TrackRoundTripRisk(in Doc doc, ReadOnlySpan<char> text)
	{
		if (RoundTripAtRisk || text.IsEmpty)
			return;

		// Anything written while a // comment is open is swallowed by it.
		if (_insideLineComment)
		{
			RoundTripAtRisk = true;
			return;
		}

		// A directive is only a directive when it starts its line.
		if (doc.Flags.HasFlag(DocFlags.IsDirective) && !_output.AtLineStart())
		{
			RoundTripAtRisk = true;
			return;
		}

		if (doc.Flags.HasFlag(DocFlags.LineComment))
			_insideLineComment = true;

		// Two runs that the source kept apart are only at risk if we have closed the gap. Runs that
		// were already adjacent in the source cannot weld — they lexed fine there and are unchanged.
		if (_lastSourceEnd < 0 || doc.A <= _lastSourceEnd)
			return;

		var previous = _output.LastChar();
		if (previous is '\0' or ' ' or '\t' or '\n' or '\r')
			return;

		if (WeldDetector.CanWeld(previous, text[0]))
			RoundTripAtRisk = true;
	}

	/// <summary>
	/// Returns true when the source contains at least one verbatim or raw string delimiter.
	/// Only such strings have their line endings rewritten by the verbatim-run emitter, so only
	/// they can have their token text changed when the configured line ending differs from the
	/// source's own.
	/// </summary>
	private static bool HasVerbatimOrRawString(ReadOnlySpan<char> source)
	{
		// Fast exit: no double-quote means no string literals at all.
		if (source.IndexOf('"') < 0)
			return false;

		// @"..." and $@"..." both contain the two-character sequence @" (in $@" it appears at
		// offset 1), so one search covers both. @$"..." has @ followed by $ followed by ", so
		// it requires its own check. Raw string literals start with """.
		return source.Contains("@\"", StringComparison.Ordinal)
			|| source.Contains("@$\"", StringComparison.Ordinal)
			|| source.Contains("\"\"\"", StringComparison.Ordinal);
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

				case DocKind.ExternalText:
					remaining -= TextWidth(_arena.ExternalTexts[doc.A]);
					break;

				case DocKind.Line:
					{
						var type = doc.LineType;
						if (type == LineType.Literal)
							return true;

						// Aimed at another group: it is a real break when that group broke, and not
						// there at all when it did not. Measuring it as an ordinary hard line would
						// charge a space for something that prints nothing.
						if (doc.GroupId != 0)
						{
							if (_groupModes[doc.GroupId] == PrintMode.Break)
								return true;
							break;
						}

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
						// Measure only the branch that would actually print — including when the
						// branch is chosen by another group's mode rather than by this scope's. The
						// print pass has always honoured doc.GroupId here; measurement did not, so an
						// aimed IfBreak was measured against the wrong branch and the group around it
						// could be sized for text it would never emit.
						//
						// A target the walk has not reached yet has no mode, and there is nothing
						// better to assume than the enclosing scope.
						var aimed = doc.GroupId != 0 && _groupModes[doc.GroupId] != PrintMode.Unset;
						var targetBroken = aimed
							? _groupModes[doc.GroupId] == PrintMode.Break
							: !flat && scope.Mode == PrintMode.Break;
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
	private int TextWidth(ReadOnlySpan<char> text)
	{
		var width = 0;
		for (var i = 0; i < text.Length; i++)
		{
			// A tab advances to the next stop rather than by one, wherever it appears — inside a
			// verbatim run as much as in an indent. Counting it as one character is what let lines
			// of tab-indented source measure narrower than they print.
			if (text[i] == '\t')
			{
				width += _tabWidth - ((_column + width) % _tabWidth);
				continue;
			}

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

		// Filled rather than cleared: zero is Flat, and a group nobody has reached has not chosen
		// flat. One pass over a few hundred bytes per file.
		Array.Fill(_groupModes, PrintMode.Unset);
	}
}
