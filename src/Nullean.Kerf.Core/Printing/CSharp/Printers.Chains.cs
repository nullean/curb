using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Nullean.Kerf.Printing.CSharp;

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
		// The minimum governs whether Kerf *introduces* breaks, never whether it keeps the author's.
		// Declining a two-link chain outright joined `x.SynonymGraph()\n.Synonyms(y)` back onto one
		// line, and joining anything risks a line too long to break, which is what stopped a file
		// settling after two runs.
		if (CountLinks(node) < MinimumLinksToBreak && !SpansLines(node, context))
			return false;

		var links = CollectLinks(node, out var receiver);
		if (links is null || (links.Count < MinimumLinksToBreak && !SpansLines(node, context)))
			return false;

		var arena = context.Arena;

		// The author's own layout wins: a chain they opened out stays opened out, at their dots.
		var asWritten = SpansLines(node, context);

		Node.Print(receiver, context);

		// `builder.AddProject(…)` reads as one thing, so a plain identifier receiver keeps its first
		// call rather than being left stranded on a line of its own — but not when the author put
		// their own break there, which is theirs to keep.
		var attached = !asWritten
			&& receiver is IdentifierNameSyntax or PredefinedTypeSyntax or ThisExpressionSyntax or BaseExpressionSyntax
			? 1
			: 0;

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
			for (var i = attached; i < links.Count; i++)
			{
				var link = links[i];

				// Only the separator is conditional. The link itself is always emitted — dropping it
				// is how this lost `.Values` from a chain the first time round.
				if (!asWritten)
					arena.SoftLine();
				else if (!context.OnSameLine(PreviousEnd(links, receiver, i), link.DotToken.SpanStart))
					arena.HardLine();

				PrintLink(link, context);
			}
		}

		return true;
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
	private static bool BreaksAnywhere(
		List<ChainLink> links,
		ExpressionSyntax receiver,
		int from,
		PrintContext context)
	{
		for (var i = from; i < links.Count; i++)
		{
			if (!context.OnSameLine(PreviousEnd(links, receiver, i), links[i].DotToken.SpanStart))
				return true;
		}

		return false;
	}

	private static void PrintLink(in ChainLink link, PrintContext context)
	{
		TokenPrinter.Print(link.DotToken, context);
		Node.Print(link.Name, context);

		foreach (var trailer in link.Trailers)
			Node.Print(trailer, context);
	}

	/// <summary>
	/// Walks down an expression collecting its chain links, innermost first.
	/// </summary>
	/// <remarks>
	/// Only plain member access counts. A null-conditional access has a different shape — the whole
	/// tail hangs off one <c>?.</c> node — and a chain broken across one would need its own rules, so
	/// it terminates the walk instead of being guessed at.
	/// </remarks>
	private static List<ChainLink>? CollectLinks(ExpressionSyntax node, out ExpressionSyntax receiver)
	{
		List<ChainLink>? links = null;
		var current = node;
		List<SyntaxNode>? trailers = null;

		while (true)
		{
			switch (current)
			{
				case InvocationExpressionSyntax invocation:
					(trailers ??= []).Insert(0, invocation.ArgumentList);
					current = invocation.Expression;
					continue;

				case ElementAccessExpressionSyntax elementAccess:
					(trailers ??= []).Insert(0, elementAccess.ArgumentList);
					current = elementAccess.Expression;
					continue;

				case MemberAccessExpressionSyntax access when access.IsKind(SyntaxKind.SimpleMemberAccessExpression):
					(links ??= []).Insert(
						0,
						new ChainLink(access.OperatorToken, access.Name, trailers?.ToArray() ?? [], EndOf(access, trailers)));
					trailers = null;
					current = access.Expression;
					continue;

				default:
					// Anything still pending belongs to the receiver, not to a link of its own.
					receiver = trailers is null ? current : node;
					return trailers is null ? links : null;
			}
		}
	}

	private static int EndOf(MemberAccessExpressionSyntax access, List<SyntaxNode>? trailers) =>
		trailers is { Count: > 0 } ? trailers[^1].Span.End : access.Span.End;

	private static bool IsChainReceiverOf(SyntaxNode? parent, ExpressionSyntax node) =>
		parent switch
		{
			MemberAccessExpressionSyntax access => access.Expression == node,
			InvocationExpressionSyntax invocation => invocation.Expression == node,
			ElementAccessExpressionSyntax elementAccess => elementAccess.Expression == node,
			_ => false,
		};

	/// <summary>One <c>.Name(…)</c> step of a chain.</summary>
	private readonly struct ChainLink(SyntaxToken dotToken, SimpleNameSyntax name, SyntaxNode[] trailers, int end)
	{
		public SyntaxToken DotToken { get; } = dotToken;
		public SimpleNameSyntax Name { get; } = name;

		/// <summary>Argument and bracket lists hanging off this link, in source order.</summary>
		public SyntaxNode[] Trailers { get; } = trailers;

		/// <summary>Source offset just past this link, for asking where the author broke.</summary>
		public int End { get; } = end;
	}
}
