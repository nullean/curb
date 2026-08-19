using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace Nullean.Curb.Cleanup.Rules;

/// <summary>Whether a node has anything but whitespace between its own tokens.</summary>
internal static class Interior
{
	/// <summary>
	/// True when trivia carrying content sits <em>inside</em> the node, so deleting the node would take it.
	/// </summary>
	/// <remarks>
	/// <para>
	/// Interior only. Leading trivia of the first token lies before <c>Span.Start</c> and trailing trivia
	/// after <c>Span.End</c>, so a comment on the line above a using directive does not count — that one is
	/// kept by widening over whitespace alone and is meant to survive.
	/// </para>
	/// <para>
	/// Both halves of this were found on a corpus rather than imagined.
	/// <c>new Dictionary&lt;string, /*isBanned*/bool&gt;()</c> puts a comment inside a type. And Razor's
	/// generated code splits a single using directive across line directives:
	/// </para>
	/// <code>
	/// using global::Microsoft.AspNetCore.Components
	/// #line default
	/// #line hidden
	///     ;
	/// </code>
	/// <para>
	/// The directive's span runs from <c>using</c> to that <c>;</c> and contains both <c>#line</c>s, so
	/// removing the directive removed them. Refusing is the only honest answer: the alternative is deciding
	/// which parts of somebody's generated file are load-bearing.
	/// </para>
	/// </remarks>
	public static bool CarriesContent(SyntaxNode node)
	{
		var span = node.Span;

		foreach (var trivia in node.DescendantTrivia(descendIntoTrivia: true))
		{
			if (trivia.SpanStart < span.Start || trivia.SpanStart >= span.End)
				continue;

			if (trivia.IsKind(SyntaxKind.WhitespaceTrivia) || trivia.IsKind(SyntaxKind.EndOfLineTrivia))
				continue;

			return true;
		}

		return false;
	}
}
