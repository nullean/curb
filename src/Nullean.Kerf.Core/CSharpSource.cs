using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Text;

namespace Nullean.Kerf;

/// <summary>
/// A parsed C# source file, held together with the original text so that printers can emit
/// spans of the source rather than allocating strings for every token.
/// </summary>
/// <remarks>
/// Kerf always parses at <see cref="LanguageVersion.Preview"/>: a formatter must never reject
/// syntax merely because it is newer than the formatter's own printers. Syntax it does not yet
/// understand is emitted verbatim instead.
/// </remarks>
public sealed class CSharpSource
{
	private CSharpSource(SourceText text, SyntaxNode root)
	{
		Text = text;
		Root = root;
	}

	/// <summary>The original source text. Text leaves in the document IR index into this.</summary>
	public SourceText Text { get; }

	/// <summary>The parsed compilation unit.</summary>
	public SyntaxNode Root { get; }

	private static readonly CSharpParseOptions ParseOptions = new(LanguageVersion.Preview);

	/// <summary>Parses <paramref name="source"/>. Returns <c>false</c> if it does not compile.</summary>
	/// <remarks>
	/// Kerf refuses to format source with syntax errors. Roslyn's recovery would happily produce a
	/// tree, but re-printing from a guessed tree is exactly how a formatter destroys code.
	/// </remarks>
	public static bool TryParse(string source, out CSharpSource parsed, out IReadOnlyList<Diagnostic> errors)
	{
		var text = SourceText.From(source);
		var tree = CSharpSyntaxTree.ParseText(text, ParseOptions);
		var root = tree.GetRoot();

		var diagnostics = tree.GetDiagnostics()
			.Where(d => d.Severity == DiagnosticSeverity.Error)
			.ToArray();

		if (diagnostics.Length > 0)
		{
			parsed = null!;
			errors = diagnostics;
			return false;
		}

		parsed = new CSharpSource(text, root);
		errors = [];
		return true;
	}
}
