using System.Buffers;
using Nullean.Curb.Documents;

namespace Nullean.Curb.Printing;

/// <summary>
/// Decides, before printing starts, which groups are forced to break because their subtree contains
/// an unconditional line break.
/// </summary>
/// <remarks>
/// <para>
/// Preorder storage turns this into two forward scans over contiguous memory. The first builds a
/// prefix sum of unconditional breaks; the second reads, for each group covering slots
/// <c>[i, i + Length)</c>, whether that range contains any — a single subtraction. There is no
/// traversal, no marker stack and no cycle guard, because cycles are structurally impossible.
/// </para>
/// <para>
/// Break bits live in a side table so <see cref="Doc"/> can stay readonly.
/// </para>
/// </remarks>
internal sealed class BreakPropagation
{
	private int[] _hardPrefix = [];

	/// <summary>
	/// For each boundary <c>i</c>, the index of the last doc before it that prints anything, or -1.
	/// </summary>
	private int[] _lastContent = [];

	private ulong[] _broken = [];
	private int[] _forceFlatEnds = [];
	private readonly List<int> _groupIndices = [];

	/// <summary>True if the group at <paramref name="index"/> must print broken.</summary>
	public bool IsBroken(int index) => (_broken[index >> 6] & (1UL << (index & 63))) != 0;

	/// <summary>
	/// True if <c>[start, end)</c> contains an unconditional break, for a caller measuring a range
	/// that is not itself a group — <see cref="Printing.DocPrinter.PrintFill"/>, which decides an
	/// item's or a pair's flatness by calling <c>Fits</c> directly rather than through
	/// <see cref="Printing.DocPrinter.ResolveGroupMode"/>&#8203;'s <see cref="IsBroken"/> guard, and
	/// so needs the same check <see cref="Run"/> already has the numbers for.
	/// </summary>
	/// <remarks>
	/// The same prefix sum <see cref="IsBroken"/> reads, subtracted between two arbitrary points
	/// instead of at one group's boundary — <see cref="Run"/> already builds it over every index, not
	/// just group ones, so this costs nothing beyond the one it already pays.
	/// </remarks>
	public bool HasHardBreak(int start, int end) => _hardPrefix[end] - _hardPrefix[start] > 0;

	public void Run(DocArena arena)
	{
		var count = arena.Count;
		EnsureCapacity(count);

		var docs = arena.Docs;
		_groupIndices.Clear();

		// ---- pass 1: prefix sum of unconditional breaks -----------------------------------------
		// A hard line inside a ForceFlat does not break anything, because ForceFlat wins. A literal
		// line does, even there: its newline is content, not layout.
		var forceFlatDepth = 0;
		var running = 0;
		var lastContent = -1;
		_hardPrefix[0] = 0;
		_lastContent[0] = -1;

		for (var i = 0; i < count; i++)
		{
			while (forceFlatDepth > 0 && i >= _forceFlatEnds[forceFlatDepth - 1])
				forceFlatDepth--;

			var doc = docs[i];

			if (doc.Kind == DocKind.ForceFlat)
			{
				if (forceFlatDepth == _forceFlatEnds.Length)
					Array.Resize(ref _forceFlatEnds, Math.Max(8, _forceFlatEnds.Length * 2));
				_forceFlatEnds[forceFlatDepth++] = i + doc.Length;
			}

			if (doc.Kind is DocKind.Group or DocKind.ConditionalGroup)
				_groupIndices.Add(i);

			var counts = doc.IsBreakish
				&& (forceFlatDepth == 0 || (doc.Kind == DocKind.Line && doc.LineType == LineType.Literal));

			if (counts)
				running++;

			_hardPrefix[i + 1] = running;

			if (doc.Kind is DocKind.SrcText or DocKind.SynText or DocKind.ExternalText or DocKind.Line or DocKind.BreakParent)
				lastContent = i;
			_lastContent[i + 1] = lastContent;
		}

		// ---- pass 2: a group breaks if its subtree contains any --------------------------------
		Array.Clear(_broken, 0, (count >> 6) + 1);

		foreach (var i in _groupIndices)
		{
			var doc = docs[i];
			var end = i + doc.Length;
			var hard = _hardPrefix[end] - _hardPrefix[i];

			// A trailing // comment that is the last thing in the group ends the group's line, not a line
			// inside it: nothing of the group follows it. Counting it forced the group open, so a comment
			// after the last switch arm broke the arm after `=>` (issue #127). The printer still breaks
			// it in a flat group, and the next break outside merges into it.
			var last = _lastContent[end];
			if (hard > 0 && last >= i && IsLineCommentBreak(docs[last]) && _hardPrefix[last + 1] > _hardPrefix[last])
				hard--;

			if (hard > 0)
				_broken[i >> 6] |= 1UL << (i & 63);
		}
	}

	/// <summary>True for the break that ends a trailing <c>//</c> comment. See <see cref="DocArena.LineCommentBreak"/>.</summary>
	internal static bool IsLineCommentBreak(in Doc doc) =>
		doc.Kind == DocKind.Line && doc.LineType == LineType.Hard && doc.B == Doc.EndsLineComment;

	private void EnsureCapacity(int count)
	{
		if (_hardPrefix.Length < count + 1)
		{
			if (_hardPrefix.Length > 0)
				ArrayPool<int>.Shared.Return(_hardPrefix);
			_hardPrefix = ArrayPool<int>.Shared.Rent(count + 1);
		}

		if (_lastContent.Length < count + 1)
		{
			if (_lastContent.Length > 0)
				ArrayPool<int>.Shared.Return(_lastContent);
			_lastContent = ArrayPool<int>.Shared.Rent(count + 1);
		}

		var words = (count >> 6) + 1;
		if (_broken.Length < words)
		{
			if (_broken.Length > 0)
				ArrayPool<ulong>.Shared.Return(_broken);
			_broken = ArrayPool<ulong>.Shared.Rent(words);
		}

		if (_forceFlatEnds.Length == 0)
			_forceFlatEnds = new int[16];
	}
}
