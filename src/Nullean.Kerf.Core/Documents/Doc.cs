using System.Runtime.InteropServices;

namespace Nullean.Kerf.Documents;

/// <summary>The kind of a <see cref="Doc"/>. Determines how <c>A</c> and <c>B</c> are interpreted.</summary>
internal enum DocKind : byte
{
	/// <summary>Nothing. Skipped everywhere.</summary>
	Null = 0,

	/// <summary>Verbatim text from the original source. <c>A</c> = offset, <c>B</c> = length in chars.</summary>
	SrcText,

	/// <summary>Text Kerf inserts itself. <c>A</c> = index into <see cref="SyntheticText"/>.</summary>
	SynText,

	/// <summary>A break opportunity. <c>A</c> = <see cref="LineType"/>.</summary>
	Line,

	/// <summary>An ordered sequence of children. <c>B</c> = child count.</summary>
	Concat,

	/// <summary>Print flat if it fits, otherwise broken. <c>GroupId</c> names it so an <see cref="IfBreak"/> can consult it.</summary>
	Group,

	/// <summary>Ordered alternatives: the first that fits wins, else the last. <c>B</c> = option count.</summary>
	ConditionalGroup,

	/// <summary>Two alternatives chosen by a group's mode. <c>A</c> = slot count of the flat branch. <c>GroupId</c> = group to consult, 0 = enclosing.</summary>
	IfBreak,

	/// <summary>Shifts the indent of its subtree. <c>B</c> = signed level delta, or <see cref="Doc.IndentToRoot"/>.</summary>
	Indent,

	/// <summary>Prints its subtree flat regardless of width.</summary>
	ForceFlat,

	/// <summary>Excludes its subtree from width measurement.</summary>
	AlwaysFits,

	/// <summary>Forces every enclosing group to break.</summary>
	BreakParent,

	/// <summary>Trims whitespace already written to the output.</summary>
	Trim,
}

/// <summary>What a <see cref="DocKind.Line"/> does in each mode.</summary>
internal enum LineType : byte
{
	/// <summary>A space when flat, a newline when broken.</summary>
	Normal = 0,

	/// <summary>Nothing when flat, a newline when broken.</summary>
	Soft = 1,

	/// <summary>Always a newline. Forces enclosing groups to break.</summary>
	Hard = 2,

	/// <summary>
	/// Always a newline, emitted at column 0 with no indent. Used inside verbatim and raw string
	/// literals, whose content must not be re-indented.
	/// </summary>
	Literal = 3,
}

/// <summary>Modifiers that are orthogonal to <see cref="DocKind"/>.</summary>
[Flags]
internal enum DocFlags : byte
{
	None = 0,

	/// <summary>Do not trim trailing whitespace on the line this break closes. Significant inside raw strings.</summary>
	NoTrim = 1 << 0,

	/// <summary>
	/// A hard line carrying this does not force its immediately enclosing group to break when it is
	/// the first content in that group. Needed for leading documentation comments.
	/// </summary>
	SkipBreakIfFirstInGroup = 1 << 1,

	/// <summary>Preprocessor directive text: emitted, but excluded from width measurement.</summary>
	IsDirective = 1 << 2,

	/// <summary>Collapse consecutive equivalent breaks into one.</summary>
	Squash = 1 << 3,
}

/// <summary>
/// One node in the document IR.
/// </summary>
/// <remarks>
/// <para>
/// Documents are stored in a single pooled array in <b>preorder</b>: a node's children immediately
/// follow it, and <see cref="Length"/> counts the slots its whole subtree occupies, itself included.
/// Skipping a subtree is therefore <c>i += docs[i].Length</c>, and a leaf simply has
/// <see cref="Length"/> 1.
/// </para>
/// <para>
/// The struct is exactly 16 bytes and holds no references, so the backing array is never scanned by
/// the GC and four nodes share a cache line. Text leaves index into the original source rather than
/// owning strings, which is what makes both zero-allocation printing and the token-coverage
/// verifier possible.
/// </para>
/// </remarks>
[StructLayout(LayoutKind.Sequential)]
internal readonly struct Doc
{
	/// <summary>Sentinel for <see cref="DocKind.Indent"/> meaning "dedent to column 0".</summary>
	public const int IndentToRoot = int.MinValue;

	public readonly DocKind Kind;
	public readonly DocFlags Flags;

	/// <summary>Names a <see cref="DocKind.Group"/>, or selects one from an <see cref="DocKind.IfBreak"/>. 0 means unnamed/enclosing.</summary>
	public readonly ushort GroupId;

	/// <summary>Slots occupied by this subtree, including this node. Leaves are 1.</summary>
	public readonly int Length;

	public readonly int A;
	public readonly int B;

	public Doc(DocKind kind, int length = 1, int a = 0, int b = 0, ushort groupId = 0, DocFlags flags = DocFlags.None)
	{
		Kind = kind;
		Flags = flags;
		GroupId = groupId;
		Length = length;
		A = a;
		B = b;
	}

	/// <summary>Returns this node with its subtree length patched, used when closing a scope.</summary>
	public Doc WithLength(int length) => new(Kind, length, A, B, GroupId, Flags);

	/// <summary>Returns this node with <c>A</c> patched, used when closing an <see cref="DocKind.IfBreak"/>.</summary>
	public Doc WithA(int a) => new(Kind, Length, a, B, GroupId, Flags);

	public LineType LineType => (LineType)A;

	/// <summary>True for nodes that unconditionally force enclosing groups to break.</summary>
	public bool IsBreakish =>
		Kind == DocKind.BreakParent
		|| (Kind == DocKind.Line && (LineType is LineType.Hard or LineType.Literal));

	public bool HasChildren => Length > 1;
}
