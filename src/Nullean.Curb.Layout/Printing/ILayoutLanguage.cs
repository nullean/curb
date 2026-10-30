using Nullean.Curb.Documents;

namespace Nullean.Curb.Printing;

/// <summary>
/// What the layout printer has to know about the language it lays out, and nothing else.
/// </summary>
/// <remarks>
/// <para>
/// The printer is shared, but two of its answers are not: which strings a <see cref="DocKind.SynText"/>
/// may name, and whether two characters it closes the gap between could lex as one token. Both are
/// asked per emitted run, so they are static members of a struct type argument rather than virtual
/// calls: <see cref="DocPrinter{TLanguage}"/> is specialised per language and each call inlines, which
/// keeps the printer exactly as fast as when it was C#-only.
/// </para>
/// <para>
/// Implement it on an empty <c>struct</c>. A class would compile, but the runtime then shares one
/// instantiation across reference types and the calls stop inlining.
/// </para>
/// </remarks>
internal interface ILayoutLanguage
{
	/// <summary>
	/// The strings a <see cref="DocKind.SynText"/> may name, by id. Starts with
	/// <see cref="SyntheticText.Shared"/>; the language's own strings follow from
	/// <see cref="SyntheticText.LanguageBase"/>.
	/// </summary>
	static abstract string[] SyntheticTable { get; }

	/// <summary>
	/// True when <paramref name="left"/> followed directly by <paramref name="right"/> might lex as one
	/// token, so a gap the printer closed between them is worth re-parsing.
	/// </summary>
	/// <remarks>
	/// Conservative by contract: a false positive costs one extra parse, a false negative lets damage
	/// through. A language whose verifier re-lexes every output can answer false throughout.
	/// </remarks>
	static abstract bool CanWeld(char left, char right);
}
