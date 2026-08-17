using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace Nullean.Kerf.Cleanup.Rules;

/// <summary>
/// Shared ground for the rules that add a modifier to a declaration.
/// </summary>
/// <remarks>
/// <para>
/// Both of them start from the same measured fact: the compiler reports these at the declaration's
/// <em>name</em>, not at the declaration. IDE0044 on <c>private string _name;</c> came back at the column
/// of <c>_name</c>, and IDE0040 on <c>int _count;</c> at the column of <c>_count</c>. So the fixer walks
/// up from an identifier rather than expecting to land on a member.
/// </para>
/// <para>
/// That walk is also the node-kind gate. The token at the reported position must be the name of the
/// member it belongs to — not merely some identifier inside one — which is what makes a log describing a
/// file that has since changed fail rather than apply somewhere arbitrary.
/// </para>
/// </remarks>
internal static class Modifiers
{
	/// <summary>
	/// Roslyn's default modifier order, which is also the default of
	/// <c>csharp_preferred_modifier_order</c>.
	/// </summary>
	/// <remarks>
	/// Needed because appending is not good enough. <c>readonly</c> comes before <c>unsafe</c>, so
	/// inserting at the end of the list would produce <c>private unsafe readonly</c> — legal C#, and then
	/// reported by IDE0036 on the next build. Fixing one rule by breaking another is not a fix.
	/// </remarks>
	private static readonly SyntaxKind[] Order =
	[
		SyntaxKind.PublicKeyword, SyntaxKind.PrivateKeyword, SyntaxKind.ProtectedKeyword,
		SyntaxKind.InternalKeyword, SyntaxKind.FileKeyword, SyntaxKind.StaticKeyword,
		SyntaxKind.ExternKeyword, SyntaxKind.NewKeyword, SyntaxKind.VirtualKeyword,
		SyntaxKind.AbstractKeyword, SyntaxKind.SealedKeyword, SyntaxKind.OverrideKeyword,
		SyntaxKind.ReadOnlyKeyword, SyntaxKind.UnsafeKeyword, SyntaxKind.RequiredKeyword,
		SyntaxKind.VolatileKeyword, SyntaxKind.AsyncKeyword,
	];

	/// <summary>
	/// Finds the member whose name sits at <paramref name="span"/>, or refuses.
	/// </summary>
	public static bool TryFindMember(
		CleanupContext context,
		TextSpan span,
		out MemberDeclarationSyntax member,
		out string? refusal)
	{
		member = null!;

		var token = context.Root.FindToken(span.Start);

		if (token.SpanStart != span.Start || !token.IsKind(SyntaxKind.IdentifierToken))
		{
			refusal = "the reported position is not the start of a declaration's name";
			return false;
		}

		if (token.Parent?.FirstAncestorOrSelf<MemberDeclarationSyntax>() is not { } found)
		{
			refusal = "the reported position is not inside a member declaration";
			return false;
		}

		// The token has to be the member's own name. Without this an identifier anywhere inside a member
		// would satisfy the gate, and a stale position would add a modifier to whatever encloses it.
		if (!NameOf(found).Contains(token))
		{
			refusal = "the reported position names something other than the member it belongs to";
			return false;
		}

		member = found;
		refusal = null;
		return true;
	}

	/// <summary>The tokens that can legitimately be the reported name of a member.</summary>
	/// <remarks>
	/// A field declares several names at once — <c>int a, b;</c> — so its answer is every declarator's
	/// identifier, and the diagnostic names the one it is about.
	/// </remarks>
	private static IEnumerable<SyntaxToken> NameOf(MemberDeclarationSyntax member)
	{
		switch (member)
		{
			case BaseFieldDeclarationSyntax field:
				foreach (var declarator in field.Declaration.Variables)
					yield return declarator.Identifier;

				break;

			case MethodDeclarationSyntax method:
				yield return method.Identifier;
				break;

			case PropertyDeclarationSyntax property:
				yield return property.Identifier;
				break;

			case EventDeclarationSyntax @event:
				yield return @event.Identifier;
				break;

			case DelegateDeclarationSyntax @delegate:
				yield return @delegate.Identifier;
				break;

			case BaseTypeDeclarationSyntax type:
				yield return type.Identifier;
				break;

			case ConstructorDeclarationSyntax constructor:
				yield return constructor.Identifier;
				break;

			case DestructorDeclarationSyntax destructor:
				yield return destructor.Identifier;
				break;
		}
	}

	/// <summary>Where a keyword belongs in a member's modifier list, as an offset into the source.</summary>
	public static int InsertionPoint(MemberDeclarationSyntax member, SyntaxKind keyword)
	{
		var rank = Array.IndexOf(Order, keyword);

		foreach (var modifier in member.Modifiers)
		{
			var existing = Array.IndexOf(Order, modifier.Kind());

			// An unrecognised modifier is treated as coming after, so the keyword lands in front of it
			// rather than at the end of a list this table does not fully describe.
			if (existing < 0 || existing > rank)
				return modifier.SpanStart;
		}

		// Nothing already there belongs after the keyword, so it goes at the end of the list — in front of
		// whatever the modifiers were qualifying, not in front of the list itself.
		return member.Modifiers.Count > 0
			? member.Modifiers[^1].GetNextToken().SpanStart
			: AfterAttributes(member);
	}

	/// <summary>
	/// The first offset inside a member at which a modifier may be written.
	/// </summary>
	/// <remarks>
	/// Not <c>member.Span.Start</c>: on a member carrying an attribute that is the <c>[</c>, and writing a
	/// keyword there would produce <c>private [Obsolete] int x;</c>, which does not compile.
	/// </remarks>
	public static int AfterAttributes(MemberDeclarationSyntax member)
	{
		if (member.Modifiers.Count > 0)
			return member.Modifiers[0].SpanStart;

		if (member.AttributeLists.Count > 0)
			return member.AttributeLists[^1].GetLastToken().GetNextToken().SpanStart;

		return member.Span.Start;
	}
}

/// <summary>
/// IDE0044 — adds <c>readonly</c> to a field only ever assigned where a readonly field may be.
/// </summary>
/// <remarks>
/// <para>
/// The analyser proved the field is never written outside a constructor, which is the part needing a
/// compilation. A wrong verdict — a write through <c>ref</c>, <c>Interlocked</c> or
/// <c>Unsafe.AsRef</c> that it missed — becomes a compile error on the next build, which is the loudest
/// possible failure and the reason this rule is safe to take on trust.
/// </para>
/// <para>
/// On a struct field this also brings defensive-copy semantics, which is a performance change rather than
/// a correctness one. Roslyn's own fixer applies it, and agreeing with Roslyn is the whole product claim.
/// </para>
/// </remarks>
internal sealed class ReadOnlyFields : ICleanupRule
{
	public string RuleId => "IDE0044";

	// The position identifies one field declaration, so a start is enough.
	public bool NeedsSpan => false;

	public bool TryFix(CleanupContext context, in CleanupDiagnostic diagnostic, TextSpan span, ICollection<PlannedFix> into, out string? refusal)
	{

		if (!Modifiers.TryFindMember(context, span, out var member, out refusal))
			return false;

		if (member is not FieldDeclarationSyntax field)
		{
			refusal = $"the reported position names a {member.Kind()}, and only a field can be made readonly";
			return false;
		}

		foreach (var modifier in field.Modifiers)
		{
			if (modifier.IsKind(SyntaxKind.ReadOnlyKeyword) || modifier.IsKind(SyntaxKind.ConstKeyword))
			{
				refusal = $"the field is already {modifier.Kind()}, so the log describes a file that has changed";
				return false;
			}
		}

		// `int a, b;` declares two fields in one statement, and readonly would apply to both. The compiler
		// reports the one it means; applying it to the pair would change a field nobody asked about.
		if (field.Declaration.Variables.Count > 1)
		{
			refusal = "the declaration holds more than one field, and readonly cannot be applied to one of them alone";
			return false;
		}

		refusal = null;
		into.Add(PlannedFix.InsertKeyword(Modifiers.InsertionPoint(field, SyntaxKind.ReadOnlyKeyword), "readonly"));
		return true;
	}
}

/// <summary>
/// IDE0040 — writes out the accessibility a declaration was relying on by default.
/// </summary>
/// <remarks>
/// <para>
/// Purely additive and purely syntactic: the keyword written is the one C# already applied, so the
/// program's meaning is unchanged by construction. Which keyword that is depends only on where the
/// declaration sits, and the parent node says.
/// </para>
/// <para>
/// It also composes with the formatter: accessibility sorts first in Roslyn's order, so the keyword goes
/// at the head of the modifier list and IDE0036 has nothing to say about the result.
/// </para>
/// </remarks>
internal sealed class AccessibilityModifiers : ICleanupRule
{
	public string RuleId => "IDE0040";

	public bool NeedsSpan => false;

	public bool TryFix(CleanupContext context, in CleanupDiagnostic diagnostic, TextSpan span, ICollection<PlannedFix> into, out string? refusal)
	{

		if (!Modifiers.TryFindMember(context, span, out var member, out refusal))
			return false;

		foreach (var modifier in member.Modifiers)
		{
			if (modifier.Kind() is SyntaxKind.PublicKeyword or SyntaxKind.PrivateKeyword
				or SyntaxKind.ProtectedKeyword or SyntaxKind.InternalKeyword or SyntaxKind.FileKeyword)
			{
				refusal = "the declaration already states its accessibility, so the log describes a file that has changed";
				return false;
			}
		}

		// A partial member's other half may carry the accessibility, and adding a second one there would
		// not compile. Refused rather than reasoned about, since deciding it needs the whole type.
		foreach (var modifier in member.Modifiers)
		{
			if (modifier.IsKind(SyntaxKind.PartialKeyword))
			{
				refusal = "the declaration is partial, and its accessibility may be stated on another part";
				return false;
			}
		}

		if (DefaultAccessibility(member) is not { } keyword)
		{
			refusal = $"a {member.Kind()} has no accessibility that can be written out";
			return false;
		}

		refusal = null;
		into.Add(PlannedFix.InsertKeyword(Modifiers.AfterAttributes(member), keyword));
		return true;
	}

	/// <summary>
	/// The accessibility C# already gives this declaration, from its position alone.
	/// </summary>
	/// <remarks>
	/// An interface member is <c>public</c>, anything else inside a type is <c>private</c>, and a type at
	/// namespace or file scope is <c>internal</c>. Null where writing one is not possible — an explicit
	/// interface implementation or an enum member cannot carry an accessibility at all.
	/// </remarks>
	private static string? DefaultAccessibility(MemberDeclarationSyntax member)
	{
		if (member is EnumMemberDeclarationSyntax)
			return null;

		if (member is MethodDeclarationSyntax { ExplicitInterfaceSpecifier: not null }
			or PropertyDeclarationSyntax { ExplicitInterfaceSpecifier: not null }
			or EventDeclarationSyntax { ExplicitInterfaceSpecifier: not null })
		{
			return null;
		}

		return member.Parent switch
		{
			InterfaceDeclarationSyntax => "public",
			TypeDeclarationSyntax => "private",
			CompilationUnitSyntax or BaseNamespaceDeclarationSyntax => "internal",
			_ => null,
		};
	}
}

/// <summary>
/// IDE0250 — marks a struct <c>readonly</c> when nothing in it mutates.
/// </summary>
/// <remarks>
/// Reported at the type's name, like the field rules. A mistake does not compile: if some member did
/// assign to a field, <c>readonly struct</c> is an error rather than a different program.
/// </remarks>
internal sealed class ReadOnlyStructs : ICleanupRule
{
	public string RuleId => "IDE0250";

	public bool NeedsSpan => false;

	public bool TryFix(CleanupContext context, in CleanupDiagnostic diagnostic, TextSpan span, ICollection<PlannedFix> into, out string? refusal)
	{
		if (!Modifiers.TryFindMember(context, span, out var member, out refusal))
			return false;

		// `record struct` is a RecordDeclarationSyntax, and `readonly record struct` is valid, so both
		// shapes count — but a class or an interface does not.
		var isStruct = member switch
		{
			StructDeclarationSyntax => true,
			RecordDeclarationSyntax record => record.ClassOrStructKeyword.IsKind(SyntaxKind.StructKeyword),
			_ => false,
		};

		if (!isStruct)
		{
			refusal = $"a {member.Kind()} cannot be readonly; only a struct can";
			return false;
		}

		if (member.Modifiers.Any(SyntaxKind.ReadOnlyKeyword))
		{
			refusal = "the struct is already readonly, so the log describes a file that has changed";
			return false;
		}

		refusal = null;
		into.Add(PlannedFix.InsertKeyword(Modifiers.InsertionPoint(member, SyntaxKind.ReadOnlyKeyword), "readonly"));
		return true;
	}
}

/// <summary>
/// IDE0251 — marks a struct member <c>readonly</c> when it does not mutate the instance.
/// </summary>
/// <remarks>
/// <para>
/// The same insertion as IDE0250, one level down, and reported at the member's name.
/// </para>
/// <para>
/// <c>readonly</c> on a member of an already-<c>readonly</c> struct compiles — checked, since applying
/// IDE0250 and IDE0251 to the same type in one pass would otherwise be a conflict. In practice the
/// analyser stops reporting the member once the type is readonly, so the pair rarely arrives together.
/// </para>
/// </remarks>
internal sealed class ReadOnlyMembers : ICleanupRule
{
	public string RuleId => "IDE0251";

	public bool NeedsSpan => false;

	public bool TryFix(CleanupContext context, in CleanupDiagnostic diagnostic, TextSpan span, ICollection<PlannedFix> into, out string? refusal)
	{
		if (!Modifiers.TryFindMember(context, span, out var member, out refusal))
			return false;

		if (member is not (MethodDeclarationSyntax or PropertyDeclarationSyntax or EventDeclarationSyntax))
		{
			refusal = $"a {member.Kind()} is not a member this applies to";
			return false;
		}

		if (member.Modifiers.Any(SyntaxKind.ReadOnlyKeyword))
		{
			refusal = "the member is already readonly, so the log describes a file that has changed";
			return false;
		}

		// A static member has no instance to leave alone, and `static readonly` on a method does not
		// compile. Cheap to check, and it is the shape a stale position would most likely land on.
		if (member.Modifiers.Any(SyntaxKind.StaticKeyword))
		{
			refusal = "a static member has no instance state, so readonly does not apply";
			return false;
		}

		// Only inside a struct. On a class member `readonly` is an error, so a log that has drifted onto
		// one must not be applied.
		if (member.Parent is not TypeDeclarationSyntax parent
			|| !(parent is StructDeclarationSyntax
				|| (parent is RecordDeclarationSyntax record && record.ClassOrStructKeyword.IsKind(SyntaxKind.StructKeyword))))
		{
			refusal = "the member is not declared in a struct, where readonly would not compile";
			return false;
		}

		refusal = null;
		into.Add(PlannedFix.InsertKeyword(Modifiers.InsertionPoint(member, SyntaxKind.ReadOnlyKeyword), "readonly"));
		return true;
	}
}
