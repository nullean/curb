using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace Nullean.Curb.Cleanup;

/// <summary>What one rule needs to know about the file it is fixing.</summary>
/// <remarks>
/// Built once per file and shared by every rule, so the things a rule wants to ask — where the using
/// directives are, whether there is a <c>#if</c> — are answered once rather than per diagnostic.
/// </remarks>
internal sealed class CleanupContext
{
	private List<UsingDirectiveSyntax>? _usings;

	public CleanupContext(SyntaxNode root, SourceText text, ReadOnlySpan<char> source)
	{
		Root = root;
		Text = text;

		// A plain text scan, like FormattingSuppression's pragma guard. It over-reports — a `#if` inside
		// a string or a comment counts — and over-reporting means refusing to fix, which is the safe
		// direction for a rule whose evidence was computed for one symbol set.
		HasConditionalDirectives = source.Contains("#if", StringComparison.Ordinal);
	}

	public SyntaxNode Root { get; }

	public SourceText Text { get; }

	/// <summary>True when the file has conditional compilation, which several rules refuse to work in.</summary>
	public bool HasConditionalDirectives { get; }

	/// <summary>Every using directive in the file, in source order.</summary>
	public IReadOnlyList<UsingDirectiveSyntax> Usings => _usings ??= CollectUsings();

	/// <summary>Every using directive in the file, in source order.</summary>
	public IReadOnlyList<UsingDirectiveSyntax> UsingDirectives => Usings;

	private List<UsingDirectiveSyntax> CollectUsings()
	{
		var usings = new List<UsingDirectiveSyntax>();

		// C# allows a using directive in exactly two places, so this walks those rather than descending
		// the whole tree.
		if (Root is CompilationUnitSyntax unit)
		{
			usings.AddRange(unit.Usings);
			foreach (var member in unit.Members)
				CollectFrom(member, usings);
		}

		return usings;
	}

	private static void CollectFrom(MemberDeclarationSyntax member, List<UsingDirectiveSyntax> usings)
	{
		switch (member)
		{
			case FileScopedNamespaceDeclarationSyntax scoped:
				usings.AddRange(scoped.Usings);
				foreach (var nested in scoped.Members)
					CollectFrom(nested, usings);

				break;

			case NamespaceDeclarationSyntax block:
				usings.AddRange(block.Usings);
				foreach (var nested in block.Members)
					CollectFrom(nested, usings);

				break;
		}
	}
}

/// <summary>One code style rule Curb fixes from a diagnostic a build reported.</summary>
internal interface ICleanupRule
{
	/// <summary>The rule id this fixes, which must have a <see cref="Options.RuleOwner.Cleanup"/> row in the catalog.</summary>
	string RuleId { get; }

	/// <summary>
	/// True when the fix needs the diagnostic's end as well as its start, so a log that carries only a
	/// start is refused rather than half-applied.
	/// </summary>
	bool NeedsSpan { get; }

	/// <summary>
	/// Plans the edits, or refuses with a reason.
	/// </summary>
	/// <remarks>
	/// <para>
	/// Several edits rather than one, because a single diagnostic can describe several places. IDE0005
	/// covers a whole run of using directives, and removing the run as one contiguous range would take
	/// whatever sits between them — a comment, or the <c>#line</c> and <c>#nullable</c> directives that fill
	/// generated files. Found on a corpus; one edit per directive keeps them.
	/// </para>
	/// <para>
	/// The refusal is a first-class outcome, not an error. A rule that declines because the position no
	/// longer holds what the log described has done the right thing, and saying so is what lets the
	/// caller tell "Curb declined" apart from "Curb is broken".
	/// </para>
	/// </remarks>
	bool TryFix(CleanupContext context, in CleanupDiagnostic diagnostic, TextSpan span, ICollection<PlannedFix> into, out string? refusal);
}
