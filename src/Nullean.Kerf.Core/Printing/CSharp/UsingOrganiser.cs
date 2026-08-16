using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Nullean.Kerf.Options;

namespace Nullean.Kerf.Printing.CSharp;

/// <summary>
/// Reorders a file's <c>using</c> directives.
/// </summary>
/// <remarks>
/// <para>
/// The only transform in the printer: everything else Kerf does is layout, and this moves tokens.
/// Order has no effect on C# semantics — aliases, static usings and extension-method lookup are all
/// order-independent — so the risk is not correctness of the program but of the edit.
/// </para>
/// <para>
/// It therefore refuses far more than it accepts. One preprocessor directive anywhere in the file
/// and it does nothing at all, which is what dotnet format does too: a <c>#if</c> around one
/// directive makes the whole list unorderable, not just that entry.
/// </para>
/// </remarks>
internal static class UsingOrganiser
{
	/// <summary>
	/// Returns the directives in the order they should be printed, or null to print them as written.
	/// </summary>
	/// <remarks>
	/// Null rather than a copy of the input, so the caller can take the allocation-free path — which
	/// is every file, on every repository that has not opted in.
	/// </remarks>
	public static UsingDirectiveSyntax[]? Order(SyntaxNode container, SyntaxList<UsingDirectiveSyntax> usings, in FormatOptions options)
	{
		if (options.SortUsings == UsingOrder.AsWritten || usings.Count < 2)
			return null;

		// Any preprocessor directive anywhere and the file is left alone, which is what dotnet
		// format does. Checking the directives themselves is not enough: a using inside an inactive
		// `#if` is parsed as disabled text rather than as a directive node, so it is invisible here
		// while still being a using that must not be sorted past. A #region around part of the
		// block is the same hazard. The whole-file test is the only one that catches both.
		if (container.ContainsDirectives)
			return null;

		// A banner with a comment after it would have to be split from the directive's own trivia to
		// pin one half and move the other. Rather than cut trivia in two, leave the file alone.
		if (HasContentAfterBanner(usings[0]))
			return null;

		var ordered = new UsingDirectiveSyntax[usings.Count];
		for (var i = 0; i < ordered.Length; i++)
			ordered[i] = usings[i];

		var systemFirst = options.SortUsings == UsingOrder.SystemFirst;
		Array.Sort(ordered, (left, right) => Compare(left, right, systemFirst));

		// A list already in order is not a reorder. Saying so matters: the caller treats any reorder
		// as needing the re-parse verification, and on a repository whose usings are already sorted
		// — which is every repository that has this option set — that would be a second parse of
		// almost every file to confirm nothing moved.
		if (options.SeparateImportDirectiveGroups)
			return ordered;

		for (var i = 0; i < ordered.Length; i++)
		{
			if (ordered[i] != usings[i])
				return ordered;
		}

		return null;
	}

	/// <summary>True when a blank line belongs between these two directives.</summary>
	/// <remarks>
	/// A group is a run sharing a first namespace segment, which is only meaningful once the list is
	/// sorted — hence <c>dotnet_separate_import_directive_groups</c> implying a sort.
	/// </remarks>
	public static bool StartsNewGroup(UsingDirectiveSyntax previous, UsingDirectiveSyntax next) =>
		!FirstSegment(previous).Equals(FirstSegment(next), StringComparison.Ordinal);

	/// <summary>
	/// Splits a file banner off the first directive's leading trivia.
	/// </summary>
	/// <remarks>
	/// <para>
	/// A licence header sits in the leading trivia of whatever comes first, so sorting would carry
	/// it away with that directive. Everything up to and including the last blank line is treated as
	/// belonging to the file rather than to the directive, and stays where it is.
	/// </para>
	/// <para>
	/// This is a deliberate divergence. dotnet format pins the whole of the first directive's
	/// leading trivia, blank line or not, so a comment written immediately above the first using is
	/// left behind when that using moves and silently reads as a comment on whatever sorts to the
	/// top instead. Requiring the blank line keeps a comment with the directive it describes.
	/// </para>
	/// </remarks>
	public static int BannerEnd(UsingDirectiveSyntax first)
	{
		var end = first.FullSpan.Start;
		var blankLines = 0;

		foreach (var trivia in first.GetLeadingTrivia())
		{
			if (trivia.IsKind(SyntaxKind.EndOfLineTrivia))
			{
				blankLines++;
				// Two consecutive line endings with nothing between them close the banner.
				if (blankLines >= 2)
					end = trivia.FullSpan.End;
				continue;
			}

			if (!trivia.IsKind(SyntaxKind.WhitespaceTrivia))
				blankLines = 0;
		}

		return end;
	}

	/// <summary>True when the first directive carries a comment of its own as well as a file banner.</summary>
	private static bool HasContentAfterBanner(UsingDirectiveSyntax first)
	{
		var bannerEnd = BannerEnd(first);
		if (bannerEnd == first.FullSpan.Start)
			return false;

		foreach (var trivia in first.GetLeadingTrivia())
		{
			if (trivia.FullSpan.Start < bannerEnd)
				continue;
			if (!trivia.IsKind(SyntaxKind.WhitespaceTrivia) && !trivia.IsKind(SyntaxKind.EndOfLineTrivia))
				return true;
		}

		return false;
	}

	private static int Compare(UsingDirectiveSyntax left, UsingDirectiveSyntax right, bool systemFirst)
	{
		// Aliases and `using static` sort after plain directives, matching what the IDE produces.
		var rank = Rank(left).CompareTo(Rank(right));
		if (rank != 0)
			return rank;

		if (systemFirst)
		{
			var system = (IsSystem(right) ? 1 : 0).CompareTo(IsSystem(left) ? 1 : 0);
			if (system != 0)
				return system;
		}

		return string.CompareOrdinal(Name(left), Name(right));
	}

	private static int Rank(UsingDirectiveSyntax directive)
	{
		if (directive.Alias is not null)
			return 2;
		return directive.StaticKeyword.RawKind != 0 ? 1 : 0;
	}

	private static bool IsSystem(UsingDirectiveSyntax directive)
	{
		var name = Name(directive);
		return name.Equals("System", StringComparison.Ordinal)
			|| name.StartsWith("System.", StringComparison.Ordinal);
	}

	/// <remarks>
	/// Allocates, which the hot-path rules forbid — but this runs only for a file whose
	/// <c>.editorconfig</c> asked for sorting, and only over its using block.
	/// </remarks>
	private static string Name(UsingDirectiveSyntax directive) =>
		directive.NamespaceOrType?.ToString() ?? string.Empty;

	private static ReadOnlySpan<char> FirstSegment(UsingDirectiveSyntax directive)
	{
		var name = Name(directive).AsSpan();
		var dot = name.IndexOf('.');
		return dot < 0 ? name : name[..dot];
	}
}
