using System.Runtime.InteropServices;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Nullean.Curb.Printing.CSharp;

/// <summary>Fluent member chains — <c>a.B(…).C(…).D(…)</c>.</summary>
internal static partial class Printers
{
	/// <summary>How many links a chain needs before it is allowed to break at the dots.</summary>
	/// <remarks>
	/// Two calls read fine on one line and breaking them is noise; three is where a fluent chain
	/// starts being a list of steps. CSharpier draws the line in the same place.
	/// </remarks>
	private const int MinimumLinksToBreak = 3;

	/// <summary>
	/// Prints <paramref name="node"/> as a member chain if it is the outermost link of one.
	/// </summary>
	/// <remarks>
	/// <para>
	/// Without this a chain has no break opportunity anywhere along its length: each member access
	/// prints its receiver and its name with nothing between them, so the only place the printer can
	/// break is inside an argument list. A chain too long for the line then comes apart in the middle
	/// of a call's arguments, which is worse to read than either leaving it alone or breaking it at
	/// the dots.
	/// </para>
	/// <para>
	/// Returns false for anything that is not a chain root, or is too short to be worth breaking, so
	/// the caller falls through to the ordinary path.
	/// </para>
	/// </remarks>
	public static bool TryPrintChain(ExpressionSyntax node, PrintContext context)
	{
		// A link whose parent is the next link is printed by that parent, not on its own.
		if (IsChainReceiverOf(node.Parent, node))
			return false;

		// Count before collecting. Most expressions that reach here are not chains at all, and the
		// ones that are are usually two links; allocating a list and an array per call site to find
		// that out costs more than the whole chain printer saves.
		//
		// The minimum governs whether Curb *introduces* breaks, never whether it keeps the author's.
		// Declining a two-link chain outright joined `x.SynonymGraph()\n.Synonyms(y)` back onto one
		// line, and joining anything risks a line too long to break, which is what stopped a file
		// settling after two runs.
		var linkCount = CountLinks(node, out var receiverIsCallShaped);
		var effectiveLinks = receiverIsCallShaped ? linkCount + 1 : linkCount;
		if (effectiveLinks < MinimumLinksToBreak && !SpansLines(node, context))
			return false;

		// Record the trailer buffer base before collecting. All trailer nodes appended by this
		// TryPrintChain frame (and restored by the finally below) live at [trailerBase..buffer.Count).
		// Nested TryPrintChain calls for inner chains append past this frame's slice and truncate only
		// what they added — the outer frame's slice is untouched throughout.
		var trailerBase = context.TrailerBuffer.Count;
		try
		{
			var links = CollectLinks(node, context, out var receiver);
			if (links is null || (effectiveLinks < MinimumLinksToBreak && !SpansLines(node, context)))
				return false;



			var arena = context.Arena;

			// The author's own layout wins: a chain they opened out stays opened out, at their dots.
			var asWritten = SpansLines(node, context);

			Node.Print(receiver, context);

			// Every receiver stands alone once a chain is breaking at all, unset behaving exactly like
			// csharp_wrap_before_first_method_call = true — uniform stacking reads more consistently
			// than a first call that stays attached only because its receiver happened to be a plain
			// identifier. false is the one override left: it attaches the first call regardless of
			// receiver shape, including a call or creation that would otherwise stand alone too.
			var attached = context.Options.WrapBeforeFirstMethodCall switch
			{
				false => 1,
				_ => 0,
			};

			for (var i = 0; i < attached && i < links.Count; i++)
				PrintLink(links[i], context);

			// A chain that will not break must not open an indent scope: anything inside it that breaks
			// on its own — an anonymous type, an initializer — would be pushed a level right of where it
			// belongs, which is a change dotnet format does not make.
			if (asWritten && !BreaksAnywhere(links, receiver, attached, context))
			{
				for (var i = attached; i < links.Count; i++)
					PrintLink(links[i], context);

				return true;
			}

			using (arena.Group())
			using (arena.Indent())
			{
				// csharp_max_chained_method_calls_on_line, deterministic mode only. Forces the break this
				// group would otherwise only take when the joined chain does not fit. Guarded the same way
				// csharp_max_initializer_elements_on_line's count is: not for a chain sitting in a call's
				// arguments, where the break is measured by whatever encloses it.
				if (context.Options.MaxChainedMethodCallsOnLine is { } maxCalls
					&& links.Count > maxCalls && !IsInsideArguments(node))
					arena.BreakParent();

				// csharp_wrap_after_dot_in_method_calls puts the dot on the tail of the line it follows
				// rather than the head of the line it introduces. Only the dot moves; where the chain
				// breaks is decided exactly as before, which is why the break predicate had to stop asking
				// about the dot's position first.
				var dotTrails = context.Options.WrapAfterDotInMethodCalls;

				for (var i = attached; i < links.Count; i++)
				{
					var link = links[i];

					if (dotTrails)
						TokenPrinter.Print(link.DotToken, context);

					// Only the separator is conditional. The link itself is always emitted — dropping it
					// is how this lost `.Values` from a chain the first time round.
					if (!asWritten)
						arena.SoftLine();
					else if (context.AuthorBroke(PreviousEnd(links, receiver, i), link.Name.SpanStart))
						arena.HardLine();

					PrintLink(link, context, dotAlreadyPrinted: dotTrails);
				}
			}

			return true;
		}
		finally
		{
			// Restore the buffer to the state it was in before this chain was collected. This frame's
			// trailer nodes are at [trailerBase..Count) and are no longer needed after printing.
			context.TrailerBuffer.RemoveRange(trailerBase, context.TrailerBuffer.Count - trailerBase);
		}
	}

	/// <summary>
	/// Counts a chain's links without allocating, so a non-chain costs one walk. Also reports
	/// whether the receiver itself is call-shaped.
	/// </summary>
	/// <remarks>
	/// A bare call (<c>GetFactory()</c>), an indexer, or a creation with its own arguments
	/// (<c>new Foo(a, b)</c>) counts as one extra link toward the minimum: <c>new
	/// Foo(a, b).Bar().Baz()</c> is exactly as chain-like as <c>foo.Bar().Baz().Qux()</c> — three
	/// genuinely separate steps — even though only two of them are member-access dots. Without
	/// this, a chain hanging off a call-shaped receiver could sit at the two-link "not really a
	/// chain" floor forever under reflow: <see cref="SpansLines"/>, the escape hatch preservation
	/// mode gets for a chain the author already opened out, reads nothing under
	/// <c>csharp_keep_existing_linebreaks = false</c>, so a receiver that is itself long enough to
	/// need several lines had no way to earn its tail a break at all — the tail broke inside
	/// whichever argument list overflowed first instead of at its own dots.
	/// </remarks>
	private static int CountLinks(ExpressionSyntax node, out bool receiverIsCallShaped)
	{
		var count = 0;
		var current = node;
		var lastWasInvocationOrElementAccess = false;

		while (true)
		{
			switch (current)
			{
				case InvocationExpressionSyntax invocation:
					lastWasInvocationOrElementAccess = true;
					current = invocation.Expression;
					continue;

				case ElementAccessExpressionSyntax elementAccess:
					lastWasInvocationOrElementAccess = true;
					current = elementAccess.Expression;
					continue;

				case MemberAccessExpressionSyntax access when access.IsKind(SyntaxKind.SimpleMemberAccessExpression):
					count++;
					lastWasInvocationOrElementAccess = false;
					current = access.Expression;
					continue;

				default:
					receiverIsCallShaped = lastWasInvocationOrElementAccess
						|| current is ObjectCreationExpressionSyntax { ArgumentList.Arguments.Count: > 0 }
						|| current is ImplicitObjectCreationExpressionSyntax { ArgumentList.Arguments.Count: > 0 };
					return count;
			}
		}
	}

	private static int PreviousEnd(List<ChainLink> links, ExpressionSyntax receiver, int index) =>
		index == 0 ? receiver.Span.End : links[index - 1].End;

	/// <summary>True when the author put a break before any link this printer is responsible for.</summary>
	/// <remarks>
	/// Asked of the link's *name*, not its dot, so that it sees a break whichever side of it the
	/// author left the dot on. Asking about the dot missed `foo.` / `Bar()` entirely — the dot is on
	/// the previous line there, so the link read as unbroken and the whole chain was joined back up,
	/// against the rule that nothing joins lines the author broke.
	/// </remarks>
	private static bool BreaksAnywhere(
		List<ChainLink> links,
		ExpressionSyntax receiver,
		int from,
		PrintContext context)
	{
		for (var i = from; i < links.Count; i++)
		{
			if (context.AuthorBroke(PreviousEnd(links, receiver, i), links[i].Name.SpanStart))
				return true;
		}

		return false;
	}

	private static void PrintLink(in ChainLink link, PrintContext context, bool dotAlreadyPrinted = false)
	{
		if (!dotAlreadyPrinted)
			TokenPrinter.Print(link.DotToken, context);

		Node.Print(link.Name, context);

		var buffer = context.TrailerBuffer;
		var end = link.TrailersStart + link.TrailersCount;
		for (var i = link.TrailersStart; i < end; i++)
			Node.Print(buffer[i], context);
	}

	/// <summary>
	/// Walks down an expression collecting its chain links, innermost first.
	/// </summary>
	/// <remarks>
	/// Only plain member access counts. A null-conditional access has a different shape — the whole
	/// tail hangs off one <c>?.</c> node — and a chain broken across one would need its own rules, so
	/// it terminates the walk instead of being guessed at.
	/// </remarks>
	/// <remarks>
	/// Trailers (argument and bracket lists) are appended to <see cref="PrintContext.TrailerBuffer"/>
	/// rather than allocated per-link. Each link's slice is recorded as <c>(start, count)</c> indices
	/// into that buffer. The caller (TryPrintChain) truncates the buffer back to its entry point on
	/// exit, so nested chains that land here mid-print append past the outer frame's slice and only
	/// clean up what they added.
	/// </remarks>
	private static List<ChainLink>? CollectLinks(ExpressionSyntax node, PrintContext context, out ExpressionSyntax receiver)
	{
		List<ChainLink>? links = null;
		var current = node;
		var trailerBuffer = context.TrailerBuffer;
		var trailersStart = trailerBuffer.Count;

		// The outermost invocation/indexer wrapper seen since the last link (or since the start), so a
		// receiver that is itself a call or an indexer — `GetFactory()`, `factories[0]` — can be handed
		// back whole rather than disqualifying the chain: without this, `GetFactory().Add(x).Add(y)…`
		// fell all the way through to ordinary printing and broke inside whichever argument list
		// happened to overflow first, rather than at the chain's own dots.
		ExpressionSyntax? receiverBoundary = null;

		while (true)
		{
			switch (current)
			{
				case InvocationExpressionSyntax invocation:
					receiverBoundary ??= current;
					// Append outward: innermost trailers arrive last; reversed below before storing.
					trailerBuffer.Add(invocation.ArgumentList);
					current = invocation.Expression;
					continue;

				case ElementAccessExpressionSyntax elementAccess:
					receiverBoundary ??= current;
					trailerBuffer.Add(elementAccess.ArgumentList);
					current = elementAccess.Expression;
					continue;

				case MemberAccessExpressionSyntax access when access.IsKind(SyntaxKind.SimpleMemberAccessExpression):
					{
						// Reverse the slice for this link into source order in-place.
						var trailersCount = trailerBuffer.Count - trailersStart;
						if (trailersCount > 1)
							CollectionsMarshal.AsSpan(trailerBuffer).Slice(trailersStart, trailersCount).Reverse();
						var end = trailersCount > 0 ? trailerBuffer[trailersStart + trailersCount - 1].Span.End : access.Span.End;
						(links ??= []).Add(new ChainLink(access.OperatorToken, access.Name, trailersStart, trailersCount, end));
						trailersStart = trailerBuffer.Count;
						current = access.Expression;
						receiverBoundary = null;
						continue;
					}

				default:
					{
						// Anything still pending belongs to the receiver, not to a link of its own.
						var hasReceiverTrailers = trailerBuffer.Count > trailersStart;
						if (hasReceiverTrailers)
						{
							// Remove the receiver's pending trailers — they are not part of any link. The
							// receiver is everything from receiverBoundary down, printed as one unbroken
							// unit by the ordinary printer for whatever kind of expression it is.
							trailerBuffer.RemoveRange(trailersStart, trailerBuffer.Count - trailersStart);
							receiver = receiverBoundary ?? node;
						}
						else
						{
							receiver = current;
						}
						// Links were appended outermost-first; reverse into source order before returning.
						links?.Reverse();
						return links;
					}
			}
		}
	}

	private static bool IsChainReceiverOf(SyntaxNode? parent, ExpressionSyntax node) =>
		parent switch
		{
			MemberAccessExpressionSyntax access => access.Expression == node,
			InvocationExpressionSyntax invocation => invocation.Expression == node,
			ElementAccessExpressionSyntax elementAccess => elementAccess.Expression == node,
			_ => false,
		};

	/// <summary>One <c>.Name(…)</c> step of a chain.</summary>
	private readonly struct ChainLink(SyntaxToken dotToken, SimpleNameSyntax name, int trailersStart, int trailersCount, int end)
	{
		public SyntaxToken DotToken { get; } = dotToken;
		public SimpleNameSyntax Name { get; } = name;

		/// <summary>Start index into <see cref="PrintContext.TrailerBuffer"/> for this link's trailers.</summary>
		public int TrailersStart { get; } = trailersStart;

		/// <summary>Number of trailers for this link in the shared buffer.</summary>
		public int TrailersCount { get; } = trailersCount;

		/// <summary>Source offset just past this link, for asking where the author broke.</summary>
		public int End { get; } = end;
	}
}
