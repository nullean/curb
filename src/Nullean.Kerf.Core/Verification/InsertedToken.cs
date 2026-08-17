namespace Nullean.Kerf.Verification;

/// <summary>A token the output carries and the source does not, declared exactly.</summary>
/// <remarks>
/// <para>
/// The offset is what makes this safe. An earlier version declared only the text and consumed it wherever
/// the two sides first disagreed — which fails the moment the inserted word shares a prefix with what
/// follows it. <c>string version = x</c> becoming <c>var version = x</c> puts <c>var</c> in front of
/// <c>version</c>, both sides read <c>v</c>, there is no mismatch to trigger on, and the walk desynchronises.
/// Found on a 17,000-file corpus; it is the same trap the dropped-brace handling already carries a comment
/// about.
/// </para>
/// <para>
/// With the offset there is nothing to infer: the output must hold precisely this text at precisely this
/// position, and the source must hold nothing there.
/// </para>
/// </remarks>
/// <param name="Offset">Where the token starts in the output.</param>
/// <param name="Text">The token's exact text.</param>
internal readonly record struct InsertedToken(int Offset, string Text);
