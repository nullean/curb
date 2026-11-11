using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace Nullean.Curb.Verification;

/// <summary>
/// Re-parses the formatted output and checks that it tokenises to exactly the same stream as the
/// input.
/// </summary>
/// <remarks>
/// <para>
/// The second safety net, and the one that catches what <see cref="ContentVerifier"/> cannot. That
/// check ignores whitespace by design, so it cannot see two tokens being welded together: dropping
/// the space in <c>out var x</c> yields the single identifier <c>outvar</c>, and moving a
/// <c>#endif</c> onto the end of a statement stops it being a directive at all. Both preserve every
/// non-whitespace character, and both stop the file compiling. Both were real bugs here, found only
/// by re-parsing.
/// </para>
/// <para>
/// Comparison is on <c>RawKind</c> plus the token's exact text, read as spans out of the two
/// sources, so it allocates nothing per token. Tokens are compared without descending into trivia —
/// comment content is <see cref="ContentVerifier"/>'s responsibility, while this establishes that
/// the code still means the same thing.
/// </para>
/// <para>
/// Reordering usings changes the token stream on purpose, so when it has happened the using block
/// is lifted out of the linear walk and checked separately: the two lists of directive texts are
/// sorted and compared, which is exact rather than lenient — a lost, duplicated or altered
/// directive all fail it — and the rest of the file is still compared token for token.
/// </para>
/// <para>
/// Costs one extra parse, so it is opt-in for <c>check</c> (which writes nothing and therefore
/// cannot corrupt anything) and on by default when formatting in place.
/// </para>
/// </remarks>
internal static class TokenStreamComparer
{
	public static bool Verify(
		SyntaxNode originalRoot,
		ReadOnlySpan<char> originalText,
		ReadOnlySpan<char> formatted,
		string? formattedStr,
		out string? failure,
		bool usingsReordered = false,
		bool trailingCommas = false,
		bool modifiersReordered = false,
		bool bracesAdded = false,
		bool namespaceUnwrapped = false,
		IReadOnlyList<TextSpan>? dropped = null,
		int arrowsAdded = 0,
		IReadOnlyList<InsertedToken>? inserted = null
	)
	{
		// Parse needs a string; reuse the caller's if it has one, otherwise materialize once.
		formattedStr ??= formatted.ToString();
		if (!CSharpSource.TryParse(formattedStr, out var reparsed, out var errors))
		{
			failure = errors.Count > 0
				? $"formatted output no longer parses: {errors[0].GetMessage(System.Globalization.CultureInfo.InvariantCulture)}"
				: "formatted output no longer parses";
			return false;
		}

		var formattedText = formatted;

		if (inserted is not null && !NoDuplicatedKeyword(reparsed.Root, formattedText, inserted, out failure))
			return false;

		if (usingsReordered && !SameDirectives(originalRoot, reparsed.Root, out failure))
			return false;

		if (usingsReordered && !GlobalUsingsStayFirst(originalRoot, reparsed.Root, out failure))
			return false;

		// The directives have been checked as a set; walking them in order would only re-discover the
		// reordering that was asked for. Each walk steps over the directives themselves rather than
		// the span from the first to the last, which once a namespace had directives of its own as
		// well as moved-in ones took the namespace's own declaration with it on the source side only.
		using var original = TokenWalker(originalRoot, usingsReordered);
		using var produced = TokenWalker(reparsed.Root, usingsReordered);

		var index = 0;

		// A step that consumed a token past the run it was checking leaves it here, so the next turn
		// of the loop compares that token instead of skipping over it.
		SyntaxToken before = default, after = default;
		bool carryOriginal = false, carryProduced = false;

		// Braces the output has and the source does not, counted so they have to balance.
		var braceDebt = 0;

		// The block namespace's closing brace, owed once its opening brace became a semicolon.
		var namespaceBraceOwed = false;

		// Dropped spans arrive in source order and are consulted only at a mismatch, so one cursor
		// through them is enough and a file that drops nothing never looks at them.
		var nextDropped = 0;
		var arrowsOwed = arrowsAdded;

		// Insertions arrive in output order and are consulted only where the streams diverge.
		var nextInserted = 0;

		while (true)
		{
			var hasOriginal = carryOriginal || Next(original, out before);
			var hasProduced = carryProduced || Next(produced, out after);
			carryOriginal = carryProduced = false;

			// The owed closing brace, when it is the last thing in the file and the output has already
			// finished.
			if (namespaceUnwrapped && namespaceBraceOwed && hasOriginal && !hasProduced && before.RawKind == (int)SyntaxKind.CloseBraceToken)
			{
				namespaceBraceOwed = false;
				continue;
			}

			if (!hasOriginal && !hasProduced)
			{
				if (braceDebt != 0)
				{
					failure = $"formatting left {braceDebt} added brace(s) unbalanced";
					return false;
				}

				if (namespaceBraceOwed)
				{
					failure = "formatting opened a file-scoped namespace but left its block's brace unaccounted for";
					return false;
				}

				if (arrowsOwed != 0)
				{
					failure = "an expression body dropped a block without putting an arrow in its place";
					return false;
				}

				if (inserted is not null && nextInserted != inserted.Count)
				{
					failure = $"{inserted.Count - nextInserted} declared insertion(s) never appeared in the output";
					return false;
				}

				failure = null;
				return true;
			}

			// A token the output carries and the source does not, matched on its declared offset and text.
			// Position rather than disagreement, for the reason InsertedToken records: an inserted word can
			// share a prefix with the token behind it, and then there is no mismatch to trigger on.
			//
			// The original token is carried forward rather than consumed, so it is still compared on the
			// next turn — which is what keeps this an insertion rather than a substitution.
			if (
				inserted is not null && nextInserted < inserted.Count && hasProduced && after.SpanStart == inserted[nextInserted].Offset && formattedText.Slice(
					after.Span.Start,
					after.Span.Length
				).SequenceEqual(inserted[nextInserted].Text)
			)
			{
				nextInserted++;
				carryOriginal = hasOriginal;
				continue;
			}

			// The tail of an added brace pair, settled by count for the same reason as in the content
			// check: the added `}` matched the enclosing block's own and left this one over.
			if (bracesAdded && !hasOriginal && hasProduced && after.RawKind == (int)SyntaxKind.CloseBraceToken && braceDebt > 0)
			{
				braceDebt--;
				continue;
			}

			if (!hasOriginal)
			{
				failure = $"formatting introduced an extra token at position {index}: '{Text(formattedText, after)}'";
				return false;
			}

			if (!hasProduced)
			{
				failure = $"formatting lost the token at position {index}: '{Text(originalText, before)}'";
				return false;
			}

			// A token an expression body dropped: the block's braces and its `return`. Tested before
			// the two are compared, not at a mismatch — a dropped `}` sits directly in front of the
			// enclosing block's own and compares equal to it, so waiting for a mismatch means the
			// skip never fires and the file runs one brace short at the end.
			//
			// Skipped only where the printer said it dropped them, and balanced against the arrows
			// that replaced them, so a token lost anywhere else is still a lost token.
			if (dropped is not null && hasOriginal)
			{
				while (nextDropped < dropped.Count && dropped[nextDropped].End <= before.SpanStart)
					nextDropped++;

				// Containment rather than one span per token, and the cursor does not advance on a match, so
				// a span may cover several tokens. Both matter: a zero-width token cannot be `Contains`-ed
				// by anything, since Contains is exclusive at the end, and C# has them — the omitted size in
				// `byte[]` is one. Under the old one-for-one accounting that token consumed the span
				// belonging to the `]` behind it, leaving the `]` itself unexcused and the walk one token out.
				// Found on a 17,000-file corpus, reported as "']' became '('".
				//
				// The check is still exactly as strict: a span only ever excuses tokens that lie inside it.
				if (nextDropped < dropped.Count && before.SpanStart >= dropped[nextDropped].Start && before.Span.End <= dropped[nextDropped].End)
				{
					carryProduced = hasProduced;
					continue;
				}
			}

			// The arrow that replaced a dropped block.
			if (
				arrowsOwed > 0 && hasProduced && after.RawKind == (int)SyntaxKind.EqualsGreaterThanToken &&
				(!hasOriginal || Mismatch(originalText, before, formattedText, after))
			)
			{
				arrowsOwed--;
				carryOriginal = true;
				continue;
			}

			// The namespace conversion: `{` became `;`, and the matching `}` is then owed. Permitted
			// once, so a brace lost anywhere else is still a lost brace.
			if (namespaceUnwrapped && hasOriginal && hasProduced)
			{
				if (!namespaceBraceOwed && before.RawKind == (int)SyntaxKind.OpenBraceToken && after.RawKind == (int)SyntaxKind.SemicolonToken)
				{
					namespaceBraceOwed = true;
					index++;
					continue;
				}

				if (namespaceBraceOwed && before.RawKind == (int)SyntaxKind.CloseBraceToken && after.RawKind != (int)SyntaxKind.CloseBraceToken)
				{
					namespaceBraceOwed = false;
					carryProduced = true;
					continue;
				}
			}

			// A brace the output added, held to balancing for the same reason as in the content check.
			if (bracesAdded && hasOriginal && hasProduced && Mismatch(originalText, before, formattedText, after))
			{
				if (after.RawKind == (int)SyntaxKind.OpenBraceToken)
				{
					braceDebt++;
					carryOriginal = true;
					continue;
				}

				if (after.RawKind == (int)SyntaxKind.CloseBraceToken && braceDebt > 0)
				{
					braceDebt--;
					carryOriginal = true;
					continue;
				}
			}

			// A permuted modifier run. Both sides start one at the same place, so collecting the
			// maximal run from each and comparing them as multisets is exact and needs no spans —
			// `public static` against `static public` passes, `public static` against `public` does
			// not. Modifiers are always parted by a space, so no boundary can move here.
			if (modifiersReordered && Mismatch(originalText, before, formattedText, after) && IsModifier(before) && IsModifier(after))
			{
				if (!SameModifierRun(original, originalText, ref before, ref carryOriginal, produced, formattedText, ref after, ref carryProduced, out failure))
					return false;

				// Both runs are accounted for; whatever follows them is compared on the next turn.
				continue;
			}

			// The declared token delta. A trailing comma that appeared or vanished shows up here as a
			// `,` on one side against the closer on the other, which needs no lookahead to spot: the
			// grammar allows at most one, so stepping over it once and re-comparing is exact. Any
			// other mismatch, including a comma anywhere but immediately before a `}` or `]`, still
			// fails.
			var isMismatch = Mismatch(originalText, before, formattedText, after);
			if (trailingCommas && isMismatch)
			{
				if (IsComma(before) && IsCloser(after) && Next(original, out var nextBefore))
				{
					before = nextBefore;
					isMismatch = Mismatch(originalText, before, formattedText, after);
				}
				else if (IsCloser(before) && IsComma(after) && Next(produced, out var nextAfter))
				{
					after = nextAfter;
					isMismatch = Mismatch(originalText, before, formattedText, after);
				}
			}

			if (isMismatch)
			{
				failure = $"formatting changed token {index}: '{Text(originalText, before)}' became " + $"'{Text(formattedText, after)}'";
				return false;
			}

			index++;
		}
	}

	/// <summary>
	/// Refuses an inserted keyword that sits beside the same keyword, whichever side that copy came from.
	/// </summary>
	/// <remarks>
	/// <para>
	/// The walk accepts any token that was declared inserted, and declaring is all a duplicate needs: a
	/// multi-targeted project reports each site once per target framework, cleanup planned the insertion once
	/// per report, both were declared, and <c>readonly readonly</c> passed (CS1004). The same word beside a
	/// copy the source already had is the other way to get there, if a rule's own "already present" gate
	/// ever regresses.
	/// </para>
	/// <para>
	/// Keywords only. No C# construct puts the same keyword twice in a row, so this refuses nothing
	/// legitimate. <c>var</c> parses as an identifier and is left alone: <c>var var = 1;</c> is legal.
	/// </para>
	/// </remarks>
	private static bool NoDuplicatedKeyword(SyntaxNode root, ReadOnlySpan<char> text, IReadOnlyList<InsertedToken> inserted, out string? failure)
	{
		foreach (var insertion in inserted)
		{
			var token = root.FindToken(insertion.Offset);
			if (token.SpanStart != insertion.Offset || !SyntaxFacts.IsKeywordKind((SyntaxKind)token.RawKind))
				continue;

			if (Same(text, token, token.GetPreviousToken()) || Same(text, token, token.GetNextToken()))
			{
				failure = $"'{insertion.Text}' was inserted beside another '{insertion.Text}', at output offset {insertion.Offset}";
				return false;
			}
		}

		failure = null;
		return true;

		static bool Same(ReadOnlySpan<char> text, SyntaxToken token, SyntaxToken neighbour) =>
			neighbour.RawKind == token.RawKind && Text(text, neighbour).SequenceEqual(Text(text, token));
	}

	private static bool Mismatch(ReadOnlySpan<char> originalText, SyntaxToken before, ReadOnlySpan<char> formattedText, SyntaxToken after) =>
		before.RawKind != after.RawKind || !Text(originalText, before).SequenceEqual(Text(formattedText, after));

	private static bool IsComma(SyntaxToken token) => token.RawKind == (int)SyntaxKind.CommaToken;

	/// <summary>The two closers the grammar permits a trailing comma before.</summary>
	private static bool IsCloser(SyntaxToken token) => token.RawKind is (int)SyntaxKind.CloseBraceToken or (int)SyntaxKind.CloseBracketToken;

	/// <summary>Pulls the next token.</summary>
	private static bool Next(IEnumerator<SyntaxToken> tokens, out SyntaxToken token)
	{
		if (tokens.MoveNext())
		{
			token = tokens.Current;
			return true;
		}

		token = default;
		return false;
	}

	private static bool IsModifier(SyntaxToken token) =>
		SyntaxFacts.IsAccessibilityModifier((SyntaxKind)token.RawKind) || ModifierKinds.Contains((SyntaxKind)token.RawKind);

	private static readonly HashSet<SyntaxKind> ModifierKinds =
	[
		SyntaxKind.StaticKeyword,
		SyntaxKind.ExternKeyword,
		SyntaxKind.NewKeyword,
		SyntaxKind.VirtualKeyword,
		SyntaxKind.AbstractKeyword,
		SyntaxKind.SealedKeyword,
		SyntaxKind.OverrideKeyword,
		SyntaxKind.ReadOnlyKeyword,
		SyntaxKind.UnsafeKeyword,
		SyntaxKind.VolatileKeyword,
		SyntaxKind.AsyncKeyword,
		SyntaxKind.PartialKeyword,
		SyntaxKind.RequiredKeyword,
		SyntaxKind.FileKeyword,
		SyntaxKind.RefKeyword,
		SyntaxKind.ConstKeyword,
		SyntaxKind.FixedKeyword,
		SyntaxKind.ExplicitKeyword,
		SyntaxKind.ImplicitKeyword,
	];

	/// <summary>
	/// Consumes the run of modifiers on each side and checks they are the same multiset.
	/// </summary>
	/// <remarks>
	/// Leaves each cursor on the first token past its run, which the caller then compares normally,
	/// so a run that matches costs nothing downstream and a run that does not fails here.
	/// </remarks>
	private static bool SameModifierRun(
		IEnumerator<SyntaxToken> original,
		ReadOnlySpan<char> originalText,
		ref SyntaxToken before,
		ref bool carryOriginal,
		IEnumerator<SyntaxToken> produced,
		ReadOnlySpan<char> formattedText,
		ref SyntaxToken after,
		ref bool carryProduced,
		out string? failure
	)
	{
		var expected = new List<string>();
		var actual = new List<string>();

		while (IsModifier(before))
		{
			expected.Add(Text(originalText, before).ToString());
			if (!Next(original, out before))
				break;

			carryOriginal = true;
		}

		while (IsModifier(after))
		{
			actual.Add(Text(formattedText, after).ToString());
			if (!Next(produced, out after))
				break;

			carryProduced = true;
		}

		expected.Sort(StringComparer.Ordinal);
		actual.Sort(StringComparer.Ordinal);

		if (!expected.SequenceEqual(actual))
		{
			failure = $"reordering modifiers changed them: '{string.Join(' ', expected)}' " + $"became '{string.Join(' ', actual)}'";
			return false;
		}

		failure = null;
		return true;
	}

	/// <summary>Compares the two using lists as multisets of their tokens.</summary>
	private static bool SameDirectives(SyntaxNode before, SyntaxNode after, out string? failure)
	{
		var original = Directives(before);
		var produced = Directives(after);

		if (original.Length != produced.Length)
		{
			failure = $"reordering usings changed how many there are: {original.Length} became {produced.Length}";
			return false;
		}

		Array.Sort(original, StringComparer.Ordinal);
		Array.Sort(produced, StringComparer.Ordinal);

		for (var i = 0; i < original.Length; i++)
		{
			if (string.Equals(original[i], produced[i], StringComparison.Ordinal))
				continue;

			failure = $"reordering usings altered one: '{original[i]}' is not among the directives written";
			return false;
		}

		failure = null;
		return true;
	}

	/// <summary>
	/// Refuses a reorder that put a <c>global using</c> inside a namespace, or after a directive that is not
	/// global.
	/// </summary>
	/// <remarks>
	/// Moving a directive across a namespace boundary changes what it means, which no comparison of tokens
	/// can see: every token is still there, only somewhere else. Neither place compiles for a global using
	/// (CS8914, CS8915), and moving one into a namespace once wrote exactly that. The printer no longer
	/// moves them; this is the structural check that fails safe if it ever does again. A source that was
	/// already out of order is not the formatter's doing, so only a placement the source did not have
	/// fails.
	/// </remarks>
	private static bool GlobalUsingsStayFirst(SyntaxNode before, SyntaxNode after, out string? failure)
	{
		if (MisplacesGlobalUsing(after) && !MisplacesGlobalUsing(before))
		{
			failure = "a global using was written inside a namespace or after a using that is not global";
			return false;
		}

		failure = null;
		return true;
	}

	private static bool MisplacesGlobalUsing(SyntaxNode root)
	{
		var seenLocal = false;
		foreach (var directive in root.DescendantNodes(
			descendIntoChildren: node => node is CompilationUnitSyntax or BaseNamespaceDeclarationSyntax
		).OfType<UsingDirectiveSyntax>())
		{
			var global = directive.GlobalKeyword.RawKind != 0;
			if (directive.Parent is not CompilationUnitSyntax)
			{
				if (global)
					return true;
				continue;
			}

			if (global && seenLocal)
				return true;
			seenLocal |= !global;
		}

		return false;
	}

	/// <remarks>
	/// Each directive as its tokens, one space apart, rather than as its text. The printer normalises
	/// the whitespace inside a directive as it does anywhere else, so <c>using System.Text ;</c> is
	/// written <c>using System.Text;</c>; compared as text the two differed and every such file was
	/// refused (issue #181). The tokens are what has to survive a reorder; the comments, which a token
	/// list cannot see, are held to the content check's multiset compare over the same region.
	/// </remarks>
	private static string[] Directives(SyntaxNode root) =>
		[
			.. root
				.DescendantNodes(descendIntoChildren: node => node is CompilationUnitSyntax or BaseNamespaceDeclarationSyntax)
				.OfType<UsingDirectiveSyntax>()
				.Select(directive => string.Join(' ', directive.DescendantTokens().Select(token => token.Text)))
		];

	private static ReadOnlySpan<char> Text(ReadOnlySpan<char> source, SyntaxToken token)
	{
		var span = token.Span;
		return source.Slice(span.Start, span.Length);
	}

	/// <summary>
	/// Walks the syntax tree in source order using an explicit stack over
	/// <c>ChildNodesAndTokens().Reverse()</c>.
	/// </summary>
	/// <param name="root">The tree.</param>
	/// <param name="skipDirectives">
	/// Leave out the using directives of the compilation unit and of its namespaces, which
	/// <see cref="SameDirectives"/> has already compared as a set.
	/// </param>
	private static IEnumerator<SyntaxToken> TokenWalker(SyntaxNode root, bool skipDirectives)
	{
		var stack = new Stack<SyntaxNodeOrToken>();
		stack.Push(root);
		while (stack.Count > 0)
		{
			var item = stack.Pop();
			if (item.IsToken)
			{
				yield return item.AsToken();
			}
			else
			{
				var node = item.AsNode()!;
				if (skipDirectives && node is UsingDirectiveSyntax { Parent: CompilationUnitSyntax or BaseNamespaceDeclarationSyntax })
					continue;

				foreach (var child in node.ChildNodesAndTokens().Reverse())
					stack.Push(child);
			}
		}
	}
}
