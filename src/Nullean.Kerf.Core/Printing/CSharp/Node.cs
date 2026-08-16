using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Nullean.Kerf.Printing.CSharp;

/// <summary>Raised when a document nests deeper than Kerf is willing to follow.</summary>
internal sealed class PrintTooDeepException(int depth) : Exception($"syntax nests more than {depth} levels deep");

/// <summary>
/// Dispatches a syntax node to its printer.
/// </summary>
/// <remarks>
/// <para>
/// Switching on <see cref="SyntaxNode.RawKind"/> — an int — compiles to a jump table, unlike a
/// switch over node types, which lowers to a chain of type tests. This is hand-written while the set
/// is small and becomes generated from <c>[NodePrinter]</c> attributes once it is not.
/// </para>
/// <para>
/// Anything without a printer falls through to <see cref="Unhandled"/>, which emits the node's
/// source verbatim. That is what lets Kerf be <b>safe but incomplete</b>: it never has to guess at
/// syntax it does not model, so printer coverage can grow one node at a time without ever risking
/// someone's code.
/// </para>
/// </remarks>
internal static class Node
{
	private const int MaxDepth = 200;

	public static void Print(SyntaxNode? node, PrintContext context)
	{
		if (node is null)
			return;

		if (++context.Depth > MaxDepth)
			throw new PrintTooDeepException(MaxDepth);

		switch ((SyntaxKind)node.RawKind)
		{
			case SyntaxKind.CompilationUnit:
				Printers.CompilationUnit((CompilationUnitSyntax)node, context);
				break;

			case SyntaxKind.UsingDirective:
				Printers.UsingDirective((UsingDirectiveSyntax)node, context);
				break;

			case SyntaxKind.FileScopedNamespaceDeclaration:
				Printers.FileScopedNamespace((FileScopedNamespaceDeclarationSyntax)node, context);
				break;

			case SyntaxKind.ClassDeclaration:
			case SyntaxKind.StructDeclaration:
			case SyntaxKind.InterfaceDeclaration:
			case SyntaxKind.RecordDeclaration:
			case SyntaxKind.RecordStructDeclaration:
				Printers.TypeDeclaration((TypeDeclarationSyntax)node, context);
				break;

			case SyntaxKind.MethodDeclaration:
				Printers.MethodDeclaration((MethodDeclarationSyntax)node, context);
				break;

			case SyntaxKind.ParameterList:
				Printers.ParameterList((ParameterListSyntax)node, context);
				break;

			case SyntaxKind.TypeParameterList:
				Printers.TypeParameterList((TypeParameterListSyntax)node, context);
				break;

			case SyntaxKind.TypeParameter:
				Printers.TypeParameter((TypeParameterSyntax)node, context);
				break;

			case SyntaxKind.ArrowExpressionClause:
				Printers.ArrowExpressionClause((ArrowExpressionClauseSyntax)node, context);
				break;

			case SyntaxKind.Parameter:
				Printers.Parameter((ParameterSyntax)node, context);
				break;

			case SyntaxKind.Block:
				Printers.Block((BlockSyntax)node, context);
				break;

			case SyntaxKind.ExpressionStatement:
				Printers.ExpressionStatement((ExpressionStatementSyntax)node, context);
				break;

			case SyntaxKind.ReturnStatement:
				Printers.ReturnStatement((ReturnStatementSyntax)node, context);
				break;

			case SyntaxKind.InvocationExpression:
				Printers.InvocationExpression((InvocationExpressionSyntax)node, context);
				break;

			case SyntaxKind.SimpleMemberAccessExpression:
				Printers.MemberAccessExpression((MemberAccessExpressionSyntax)node, context);
				break;

			case SyntaxKind.ArgumentList:
				Printers.ArgumentList((ArgumentListSyntax)node, context);
				break;

			case SyntaxKind.Argument:
				Printers.Argument((ArgumentSyntax)node, context);
				break;

			case SyntaxKind.IdentifierName:
			case SyntaxKind.PredefinedType:
			case SyntaxKind.QualifiedName:
			case SyntaxKind.NumericLiteralExpression:
			case SyntaxKind.StringLiteralExpression:
			case SyntaxKind.CharacterLiteralExpression:
			case SyntaxKind.TrueLiteralExpression:
			case SyntaxKind.FalseLiteralExpression:
			case SyntaxKind.NullLiteralExpression:
				Printers.Tokens(node, context);
				break;

			default:
				Unhandled(node, context);
				break;
		}

		context.Depth--;
	}

	/// <summary>
	/// Emits a node exactly as it appears in the source, preserving its own line structure and
	/// indentation, and records that it was not really formatted.
	/// </summary>
	internal static void Unhandled(SyntaxNode node, PrintContext context)
	{
		// node.Span covers the node's own text but NOT the trivia attached to its first and last
		// tokens, so emitting the span alone silently drops the doc comment above a member. The
		// trivia has to be printed through the normal path around the verbatim body.
		if (context.ExpandUnhandled)
		{
			// Cost model only -- see PrintContext.ExpandUnhandled.
			var emitted = 0;
			foreach (var token in node.DescendantTokens())
			{
				if (emitted++ > 0)
					context.Arena.Line();
				TokenPrinter.Print(token, context);
			}
			context.VerbatimTokens += emitted;
			context.PrintedTokens -= emitted;
			return;
		}

		var first = node.GetFirstToken();
		var last = node.GetLastToken();

		if (first.RawKind != 0)
			TokenPrinter.PrintLeadingTrivia(first, context);

		var span = node.Span;
		TokenPrinter.EmitVerbatimRange(context, span.Start, span.Length);

		if (last.RawKind != 0)
			TokenPrinter.PrintTrailingTrivia(last, context);

		var tokens = 0;
		foreach (var _ in node.DescendantTokens())
			tokens++;
		context.VerbatimTokens += tokens;
	}
}
