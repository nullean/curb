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
		if (CountLinks(node) < MinimumLinksToBreak && !SpansLines(node, context))
			return false;

		// Record the trailer buffer base before collecting. All trailer nodes appended by this
		// TryPrintChain frame (and restored by the finally below) live at [trailerBase..buffer.Count).
		// Nested TryPrintChain calls for inner chains append past this frame's slice and truncate only
		// what they added — the outer frame's slice is untouched throughout.
		var trailerBase = context.TrailerBuffer.Count;
		try
		{
			var links = CollectLinks(node, context, out var receiver);
			if (links is null || (links.Count < MinimumLinksToBreak && !SpansLines(node, context)))
				return false;



			var arena = context.Arena;

			// The author's own layout wins: a chain they opened out stays opened out, at their dots.
			var asWritten = SpansLines(node, context);

			Node.Print(receiver, context);

			// `builder.AddProject(…)` reads as one thing, so a plain identifier receiver keeps its first
			// call rather than being left stranded on a line of its own — but not when the author put
			// their own break there, which is theirs to keep.
			//
			// csharp_wrap_before_first_method_call overrides the judgement in either direction: true
			// strands every receiver, false attaches every first call.
			var attached = context.Options.WrapBeforeFirstMethodCall switch
			{
				true => 0,
				false => 1,
				null when !asWritten
					&& receiver is IdentifierNameSyntax or PredefinedTypeSyntax or ThisExpressionSyntax or BaseExpressionSyntax => 1,
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

	/// <summary>Counts a chain's links without allocating, so a non-chain costs one walk.</summary>
	private static int CountLinks(ExpressionSyntax node)
	{
		var count = 0;
		var current = node;

		while (true)
		{
			switch (current)
			{
				case InvocationExpressionSyntax invocation:
					current = invocation.Expression;
					continue;

				case ElementAccessExpressionSyntax elementAccess:
					current = elementAccess.Expression;
					continue;

				case MemberAccessExpressionSyntax access when access.IsKind(SyntaxKind.SimpleMemberAccessExpression):
					count++;
					current = access.Expression;
					continue;

				default:
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

		while (true)
		{
			switch (current)
			{
				case InvocationExpressionSyntax invocation:
					// Append outward: innermost trailers arrive last; reversed below before storing.
					trailerBuffer.Add(invocation.ArgumentList);
					current = invocation.Expression;
					continue;

				case ElementAccessExpressionSyntax elementAccess:
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
						continue;
					}

				default:
					{
						// Anything still pending belongs to the receiver, not to a link of its own.
						var hasReceiverTrailers = trailerBuffer.Count > trailersStart;
						if (hasReceiverTrailers)
						{
							// Remove the receiver's pending trailers — they are not part of any link.
							trailerBuffer.RemoveRange(trailersStart, trailerBuffer.Count - trailersStart);
							receiver = node;
						}
						else
						{
							receiver = current;
						}
						// Links were appended outermost-first; reverse into source order before returning.
						links?.Reverse();
						return hasReceiverTrailers ? null : links;
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
