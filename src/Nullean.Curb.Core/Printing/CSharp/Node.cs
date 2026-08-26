using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Nullean.Curb.Printing.CSharp;

/// <summary>Raised when a document nests deeper than Curb is willing to follow.</summary>
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
/// source verbatim. That is what lets Curb be <b>safe but incomplete</b>: it never has to guess at
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

		// `#pragma warning disable IDE0055` around a region means exactly what it says, so anything
		// wholly inside one comes out as written. Members and statements only: those are the units a
		// reader draws the pragma around, and going finer would leave a construct half formatted.
		//
		// The null check carries every file that has no such pragma, which is nearly all of them.
		if (context.Suppressed is { } suppressed
			&& node is MemberDeclarationSyntax or StatementSyntax
			&& FormattingSuppression.Covers(suppressed, node.FullSpan))
		{
			Printers.PrintVerbatim(node, context);
			context.Depth--;
			return;
		}

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

			case SyntaxKind.ExtensionBlockDeclaration:
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

			case SyntaxKind.LabeledStatement:
				Printers.LabeledStatement((LabeledStatementSyntax)node, context);
				break;

			case SyntaxKind.FixedStatement:
				Printers.FixedStatement((FixedStatementSyntax)node, context);
				break;

			case SyntaxKind.UnsafeStatement:
				Printers.UnsafeStatement((UnsafeStatementSyntax)node, context);
				break;

			case SyntaxKind.EventDeclaration:
				Printers.EventDeclaration((EventDeclarationSyntax)node, context);
				break;

			case SyntaxKind.FieldDeclaration:
			case SyntaxKind.EventFieldDeclaration:
				Printers.FieldDeclaration((BaseFieldDeclarationSyntax)node, context);
				break;

			case SyntaxKind.LocalDeclarationStatement:
				Printers.LocalDeclarationStatement((LocalDeclarationStatementSyntax)node, context);
				break;

			case SyntaxKind.VariableDeclaration:
				Printers.VariableDeclaration((VariableDeclarationSyntax)node, context);
				break;

			case SyntaxKind.VariableDeclarator:
				Printers.VariableDeclarator((VariableDeclaratorSyntax)node, context);
				break;

			case SyntaxKind.EqualsValueClause:
				Printers.EqualsValueClause((EqualsValueClauseSyntax)node, context);
				break;

			case SyntaxKind.PropertyDeclaration:
				Printers.PropertyDeclaration((PropertyDeclarationSyntax)node, context);
				break;

			case SyntaxKind.AccessorList:
				Printers.AccessorList((AccessorListSyntax)node, context);
				break;

			case SyntaxKind.GetAccessorDeclaration:
			case SyntaxKind.SetAccessorDeclaration:
			case SyntaxKind.InitAccessorDeclaration:
			case SyntaxKind.AddAccessorDeclaration:
			case SyntaxKind.RemoveAccessorDeclaration:
				Printers.AccessorDeclaration((AccessorDeclarationSyntax)node, context);
				break;

			case SyntaxKind.ConstructorDeclaration:
				Printers.ConstructorDeclaration((ConstructorDeclarationSyntax)node, context);
				break;

			case SyntaxKind.DestructorDeclaration:
				Printers.DestructorDeclaration((DestructorDeclarationSyntax)node, context);
				break;

			case SyntaxKind.AttributeList:
				Printers.AttributeList((AttributeListSyntax)node, context);
				break;

			case SyntaxKind.Attribute:
				Printers.Attribute((AttributeSyntax)node, context);
				break;

			case SyntaxKind.AttributeArgument:
				Printers.AttributeArgument((AttributeArgumentSyntax)node, context);
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

			// Single-token nodes are the most common thing in any file. Printing their one token
			// directly avoids allocating a DescendantTokens iterator for each of them.
			case SyntaxKind.IdentifierName:
				TokenPrinter.Print(((IdentifierNameSyntax)node).Identifier, context);
				break;

			case SyntaxKind.PredefinedType:
				TokenPrinter.Print(((PredefinedTypeSyntax)node).Keyword, context);
				break;

			case SyntaxKind.NumericLiteralExpression:
			case SyntaxKind.StringLiteralExpression:
			case SyntaxKind.CharacterLiteralExpression:
			case SyntaxKind.TrueLiteralExpression:
			case SyntaxKind.FalseLiteralExpression:
			case SyntaxKind.NullLiteralExpression:
			case SyntaxKind.DefaultLiteralExpression:
			case SyntaxKind.Utf8StringLiteralExpression:
				TokenPrinter.Print(((LiteralExpressionSyntax)node).Token, context);
				break;

			case SyntaxKind.ThisExpression:
				TokenPrinter.Print(((ThisExpressionSyntax)node).Token, context);
				break;

			case SyntaxKind.BaseExpression:
				TokenPrinter.Print(((BaseExpressionSyntax)node).Token, context);
				break;

			case SyntaxKind.QualifiedName:
				{
					var qualified = (QualifiedNameSyntax)node;
					Print(qualified.Left, context);
					TokenPrinter.Print(qualified.DotToken, context);
					Print(qualified.Right, context);
					break;
				}

			case SyntaxKind.MemberBindingExpression:
				{
					var binding = (MemberBindingExpressionSyntax)node;
					TokenPrinter.Print(binding.OperatorToken, context);
					Print(binding.Name, context);
					break;
				}

			// ---- statements ----------------------------------------------------------------
			case SyntaxKind.IfStatement:
				Printers.IfStatement((IfStatementSyntax)node, context);
				break;
			case SyntaxKind.WhileStatement:
				Printers.WhileStatement((WhileStatementSyntax)node, context);
				break;
			case SyntaxKind.DoStatement:
				Printers.DoStatement((DoStatementSyntax)node, context);
				break;
			case SyntaxKind.ForStatement:
				Printers.ForStatement((ForStatementSyntax)node, context);
				break;
			case SyntaxKind.ForEachStatement:
				Printers.ForEachStatement((ForEachStatementSyntax)node, context);
				break;
			case SyntaxKind.ForEachVariableStatement:
				Printers.ForEachVariableStatement((ForEachVariableStatementSyntax)node, context);
				break;
			case SyntaxKind.TryStatement:
				Printers.TryStatement((TryStatementSyntax)node, context);
				break;
			case SyntaxKind.UsingStatement:
				Printers.UsingStatement((UsingStatementSyntax)node, context);
				break;
			case SyntaxKind.LockStatement:
				Printers.LockStatement((LockStatementSyntax)node, context);
				break;
			case SyntaxKind.SwitchStatement:
				Printers.SwitchStatement((SwitchStatementSyntax)node, context);
				break;
			case SyntaxKind.ThrowStatement:
				Printers.ThrowStatement((ThrowStatementSyntax)node, context);
				break;
			case SyntaxKind.YieldReturnStatement:
			case SyntaxKind.YieldBreakStatement:
				Printers.YieldStatement((YieldStatementSyntax)node, context);
				break;
			case SyntaxKind.BreakStatement:
			case SyntaxKind.ContinueStatement:
			case SyntaxKind.EmptyStatement:
				Printers.KeywordStatement((StatementSyntax)node, context);
				break;
			case SyntaxKind.LocalFunctionStatement:
				Printers.LocalFunctionStatement((LocalFunctionStatementSyntax)node, context);
				break;
			case SyntaxKind.CaseSwitchLabel:
				Printers.CaseSwitchLabel((CaseSwitchLabelSyntax)node, context);
				break;
			case SyntaxKind.CasePatternSwitchLabel:
				Printers.CasePatternSwitchLabel((CasePatternSwitchLabelSyntax)node, context);
				break;
			case SyntaxKind.DefaultSwitchLabel:
				Printers.DefaultSwitchLabel((DefaultSwitchLabelSyntax)node, context);
				break;
			case SyntaxKind.GlobalStatement:
				Printers.GlobalStatement((GlobalStatementSyntax)node, context);
				break;

			// ---- expressions ---------------------------------------------------------------
			case SyntaxKind.AddExpression:
			case SyntaxKind.SubtractExpression:
			case SyntaxKind.MultiplyExpression:
			case SyntaxKind.DivideExpression:
			case SyntaxKind.ModuloExpression:
			case SyntaxKind.LeftShiftExpression:
			case SyntaxKind.RightShiftExpression:
			case SyntaxKind.UnsignedRightShiftExpression:
			case SyntaxKind.LogicalOrExpression:
			case SyntaxKind.LogicalAndExpression:
			case SyntaxKind.BitwiseOrExpression:
			case SyntaxKind.BitwiseAndExpression:
			case SyntaxKind.ExclusiveOrExpression:
			case SyntaxKind.EqualsExpression:
			case SyntaxKind.NotEqualsExpression:
			case SyntaxKind.LessThanExpression:
			case SyntaxKind.LessThanOrEqualExpression:
			case SyntaxKind.GreaterThanExpression:
			case SyntaxKind.GreaterThanOrEqualExpression:
			case SyntaxKind.IsExpression:
			case SyntaxKind.AsExpression:
			case SyntaxKind.CoalesceExpression:
				Printers.BinaryExpression((BinaryExpressionSyntax)node, context);
				break;

			case SyntaxKind.SimpleAssignmentExpression:
			case SyntaxKind.AddAssignmentExpression:
			case SyntaxKind.SubtractAssignmentExpression:
			case SyntaxKind.MultiplyAssignmentExpression:
			case SyntaxKind.DivideAssignmentExpression:
			case SyntaxKind.ModuloAssignmentExpression:
			case SyntaxKind.AndAssignmentExpression:
			case SyntaxKind.OrAssignmentExpression:
			case SyntaxKind.ExclusiveOrAssignmentExpression:
			case SyntaxKind.LeftShiftAssignmentExpression:
			case SyntaxKind.RightShiftAssignmentExpression:
			case SyntaxKind.UnsignedRightShiftAssignmentExpression:
			case SyntaxKind.CoalesceAssignmentExpression:
				Printers.AssignmentExpression((AssignmentExpressionSyntax)node, context);
				break;

			case SyntaxKind.ConditionalExpression:
				Printers.ConditionalExpression((ConditionalExpressionSyntax)node, context);
				break;

			case SyntaxKind.UnaryPlusExpression:
			case SyntaxKind.UnaryMinusExpression:
			case SyntaxKind.BitwiseNotExpression:
			case SyntaxKind.LogicalNotExpression:
			case SyntaxKind.PreIncrementExpression:
			case SyntaxKind.PreDecrementExpression:
			case SyntaxKind.AddressOfExpression:
			case SyntaxKind.PointerIndirectionExpression:
			case SyntaxKind.IndexExpression:
				Printers.PrefixUnaryExpression((PrefixUnaryExpressionSyntax)node, context);
				break;

			case SyntaxKind.PostIncrementExpression:
			case SyntaxKind.PostDecrementExpression:
			case SyntaxKind.SuppressNullableWarningExpression:
				Printers.PostfixUnaryExpression((PostfixUnaryExpressionSyntax)node, context);
				break;

			case SyntaxKind.AwaitExpression:
				Printers.AwaitExpression((AwaitExpressionSyntax)node, context);
				break;
			case SyntaxKind.ParenthesizedExpression:
				Printers.ParenthesizedExpression((ParenthesizedExpressionSyntax)node, context);
				break;
			case SyntaxKind.CastExpression:
				Printers.CastExpression((CastExpressionSyntax)node, context);
				break;
			case SyntaxKind.ObjectCreationExpression:
				Printers.ObjectCreationExpression((ObjectCreationExpressionSyntax)node, context);
				break;
			case SyntaxKind.ImplicitObjectCreationExpression:
				Printers.ImplicitObjectCreationExpression((ImplicitObjectCreationExpressionSyntax)node, context);
				break;
			case SyntaxKind.ObjectInitializerExpression:
			case SyntaxKind.CollectionInitializerExpression:
			case SyntaxKind.ArrayInitializerExpression:
			case SyntaxKind.ComplexElementInitializerExpression:
			case SyntaxKind.WithInitializerExpression:
				Printers.InitializerExpression((InitializerExpressionSyntax)node, context);
				break;
			case SyntaxKind.CollectionExpression:
				Printers.CollectionExpression((CollectionExpressionSyntax)node, context);
				break;
			case SyntaxKind.SimpleLambdaExpression:
				Printers.SimpleLambdaExpression((SimpleLambdaExpressionSyntax)node, context);
				break;
			case SyntaxKind.ParenthesizedLambdaExpression:
				Printers.ParenthesizedLambdaExpression((ParenthesizedLambdaExpressionSyntax)node, context);
				break;
			case SyntaxKind.AnonymousMethodExpression:
				Printers.AnonymousMethodExpression((AnonymousMethodExpressionSyntax)node, context);
				break;
			case SyntaxKind.AnonymousObjectCreationExpression:
				Printers.AnonymousObjectCreationExpression((AnonymousObjectCreationExpressionSyntax)node, context);
				break;
			case SyntaxKind.AnonymousObjectMemberDeclarator:
				Printers.AnonymousObjectMemberDeclarator((AnonymousObjectMemberDeclaratorSyntax)node, context);
				break;
			case SyntaxKind.ElementAccessExpression:
				Printers.ElementAccessExpression((ElementAccessExpressionSyntax)node, context);
				break;
			case SyntaxKind.BracketedArgumentList:
				Printers.BracketedArgumentList((BracketedArgumentListSyntax)node, context);
				break;
			case SyntaxKind.ConditionalAccessExpression:
				Printers.ConditionalAccessExpression((ConditionalAccessExpressionSyntax)node, context);
				break;
			case SyntaxKind.SwitchExpression:
				Printers.SwitchExpression((SwitchExpressionSyntax)node, context);
				break;
			case SyntaxKind.SwitchExpressionArm:
				Printers.SwitchExpressionArm((SwitchExpressionArmSyntax)node, context);
				break;

			// Interior layout is content, not formatting.
			case SyntaxKind.InterpolatedStringExpression:
				Printers.VerbatimExpression(node, context);
				break;

			// ---- names and types -----------------------------------------------------------
			case SyntaxKind.GenericName:
				Printers.GenericName((GenericNameSyntax)node, context);
				break;
			case SyntaxKind.TypeArgumentList:
				Printers.TypeArgumentList((TypeArgumentListSyntax)node, context);
				break;
			case SyntaxKind.NullableType:
				Printers.NullableType((NullableTypeSyntax)node, context);
				break;
			case SyntaxKind.ArrayRankSpecifier:
				Printers.ArrayRankSpecifier((ArrayRankSpecifierSyntax)node, context);
				break;
			case SyntaxKind.ArrayType:
				Printers.ArrayType((ArrayTypeSyntax)node, context);
				break;
			case SyntaxKind.BaseList:
				Printers.BaseList((BaseListSyntax)node, context);
				break;
			case SyntaxKind.TypeParameterConstraintClause:
				Printers.TypeParameterConstraintClause((TypeParameterConstraintClauseSyntax)node, context);
				break;

			// ---- patterns, tuples, remaining members ---------------------------------------
			case SyntaxKind.IsPatternExpression:
				Printers.IsPatternExpression((IsPatternExpressionSyntax)node, context);
				break;
			case SyntaxKind.DeclarationPattern:
				Printers.DeclarationPattern((DeclarationPatternSyntax)node, context);
				break;
			case SyntaxKind.RecursivePattern:
				Printers.RecursivePattern((RecursivePatternSyntax)node, context);
				break;
			case SyntaxKind.Subpattern:
				Printers.Subpattern((SubpatternSyntax)node, context);
				break;
			case SyntaxKind.AndPattern:
			case SyntaxKind.OrPattern:
				Printers.BinaryPattern((BinaryPatternSyntax)node, context);
				break;
			case SyntaxKind.NotPattern:
				Printers.UnaryPattern((UnaryPatternSyntax)node, context);
				break;
			case SyntaxKind.RelationalPattern:
				Printers.RelationalPattern((RelationalPatternSyntax)node, context);
				break;
			case SyntaxKind.ListPattern:
				Printers.ListPattern((ListPatternSyntax)node, context);
				break;
			case SyntaxKind.ParenthesizedPattern:
				Printers.ParenthesizedPattern((ParenthesizedPatternSyntax)node, context);
				break;
			case SyntaxKind.ConstantPattern:
				Node.Print(((ConstantPatternSyntax)node).Expression, context);
				break;
			case SyntaxKind.TypePattern:
				Node.Print(((TypePatternSyntax)node).Type, context);
				break;
			case SyntaxKind.DiscardPattern:
				TokenPrinter.Print(((DiscardPatternSyntax)node).UnderscoreToken, context);
				break;
			case SyntaxKind.SingleVariableDesignation:
				TokenPrinter.Print(((SingleVariableDesignationSyntax)node).Identifier, context);
				break;
			case SyntaxKind.DiscardDesignation:
				TokenPrinter.Print(((DiscardDesignationSyntax)node).UnderscoreToken, context);
				break;
			case SyntaxKind.ParenthesizedVariableDesignation:
				Printers.ParenthesizedVariableDesignation((ParenthesizedVariableDesignationSyntax)node, context);
				break;
			case SyntaxKind.DeclarationExpression:
				Printers.DeclarationExpression((DeclarationExpressionSyntax)node, context);
				break;

			case SyntaxKind.TupleExpression:
				Printers.TupleExpression((TupleExpressionSyntax)node, context);
				break;
			case SyntaxKind.TupleType:
				Printers.TupleType((TupleTypeSyntax)node, context);
				break;
			case SyntaxKind.TupleElement:
				Printers.TupleElement((TupleElementSyntax)node, context);
				break;
			case SyntaxKind.WithExpression:
				Printers.WithExpression((WithExpressionSyntax)node, context);
				break;
			case SyntaxKind.ThrowExpression:
				Printers.ThrowExpression((ThrowExpressionSyntax)node, context);
				break;
			case SyntaxKind.TypeOfExpression:
				Printers.TypeOfExpression((TypeOfExpressionSyntax)node, context);
				break;
			case SyntaxKind.SimpleBaseType:
				Printers.SimpleBaseType((SimpleBaseTypeSyntax)node, context);
				break;
			case SyntaxKind.PrimaryConstructorBaseType:
				Printers.PrimaryConstructorBaseType((PrimaryConstructorBaseTypeSyntax)node, context);
				break;
			case SyntaxKind.ImplicitElementAccess:
				Printers.ImplicitElementAccess((ImplicitElementAccessSyntax)node, context);
				break;
			case SyntaxKind.RangeExpression:
				Printers.RangeExpression((RangeExpressionSyntax)node, context);
				break;
			case SyntaxKind.SpreadElement:
				Printers.SpreadElement((SpreadElementSyntax)node, context);
				break;
			case SyntaxKind.ExpressionElement:
				Printers.ExpressionElement((ExpressionElementSyntax)node, context);
				break;
			case SyntaxKind.ArrayCreationExpression:
				Printers.ArrayCreationExpression((ArrayCreationExpressionSyntax)node, context);
				break;
			case SyntaxKind.ImplicitArrayCreationExpression:
				Printers.ImplicitArrayCreationExpression((ImplicitArrayCreationExpressionSyntax)node, context);
				break;

			case SyntaxKind.EnumDeclaration:
				Printers.EnumDeclaration((EnumDeclarationSyntax)node, context);
				break;
			case SyntaxKind.EnumMemberDeclaration:
				Printers.EnumMemberDeclaration((EnumMemberDeclarationSyntax)node, context);
				break;
			case SyntaxKind.OperatorDeclaration:
				Printers.OperatorDeclaration((OperatorDeclarationSyntax)node, context);
				break;
			case SyntaxKind.ConversionOperatorDeclaration:
				Printers.ConversionOperatorDeclaration((ConversionOperatorDeclarationSyntax)node, context);
				break;
			case SyntaxKind.IndexerDeclaration:
				Printers.IndexerDeclaration((IndexerDeclarationSyntax)node, context);
				break;
			case SyntaxKind.BracketedParameterList:
				Printers.BracketedParameterList((BracketedParameterListSyntax)node, context);
				break;
			case SyntaxKind.NamespaceDeclaration:
				Printers.NamespaceDeclaration((NamespaceDeclarationSyntax)node, context);
				break;
			case SyntaxKind.DelegateDeclaration:
				Printers.DelegateDeclaration((DelegateDeclarationSyntax)node, context);
				break;

			case SyntaxKind.ElementBindingExpression:
				Printers.ElementBindingExpression((ElementBindingExpressionSyntax)node, context);
				break;
			case SyntaxKind.VarPattern:
				Printers.VarPattern((VarPatternSyntax)node, context);
				break;
			case SyntaxKind.DefaultExpression:
				Printers.DefaultExpression((DefaultExpressionSyntax)node, context);
				break;
			case SyntaxKind.StackAllocArrayCreationExpression:
				Printers.StackAllocArrayCreationExpression((StackAllocArrayCreationExpressionSyntax)node, context);
				break;
			case SyntaxKind.SlicePattern:
				Printers.SlicePattern((SlicePatternSyntax)node, context);
				break;

			// Type-parameter constraints: `class`, `struct`, `new()`, or a type. All are short and
			// contain no interior whitespace decisions.
			case SyntaxKind.TypeConstraint:
				Node.Print(((TypeConstraintSyntax)node).Type, context);
				break;
			case SyntaxKind.ClassConstraint:
			case SyntaxKind.StructConstraint:
			case SyntaxKind.ConstructorConstraint:
			case SyntaxKind.DefaultConstraint:
				Printers.Tokens(node, context);
				break;

			case SyntaxKind.QueryExpression:
				Printers.QueryExpression((QueryExpressionSyntax)node, context);
				break;
			case SyntaxKind.FromClause:
				Printers.FromClause((FromClauseSyntax)node, context);
				break;
			case SyntaxKind.WhereClause:
				Printers.WhereClause((WhereClauseSyntax)node, context);
				break;
			case SyntaxKind.SelectClause:
				Printers.SelectClause((SelectClauseSyntax)node, context);
				break;
			case SyntaxKind.GroupClause:
				Printers.GroupClause((GroupClauseSyntax)node, context);
				break;
			case SyntaxKind.LetClause:
				Printers.LetClause((LetClauseSyntax)node, context);
				break;
			case SyntaxKind.OrderByClause:
				Printers.OrderByClause((OrderByClauseSyntax)node, context);
				break;
			case SyntaxKind.AscendingOrdering:
			case SyntaxKind.DescendingOrdering:
				Printers.Ordering((OrderingSyntax)node, context);
				break;
			case SyntaxKind.JoinClause:
				Printers.JoinClause((JoinClauseSyntax)node, context);
				break;

			case SyntaxKind.FieldExpression:
				Printers.FieldExpression((FieldExpressionSyntax)node, context);
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

		if (context.UnhandledByKind is not { } byKind)
			return;

		byKind.TryGetValue(node.RawKind, out var existing);
		byKind[node.RawKind] = existing + tokens;
	}
}
