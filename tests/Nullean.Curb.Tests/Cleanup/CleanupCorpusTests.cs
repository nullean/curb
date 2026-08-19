using System.Collections.Concurrent;
using System.Diagnostics;
using AwesomeAssertions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using Nullean.Curb.Cleanup;

namespace Nullean.Curb.Tests.Cleanup;

/// <summary>
/// The adversarial gate: feed a real corpus verdicts Curb has no business trusting, and require that it
/// never produces damage.
/// </summary>
/// <remarks>
/// <para>
/// Every other cleanup test hands over a diagnostic the compiler would really have reported. This one does
/// the opposite on purpose. It walks the corpus for every site each rule <em>could</em> apply to and claims
/// the rule fires there — so it deletes using directives the file needs, writes <c>var</c> where the type
/// was load-bearing, and drops type names that were not inferable. Those are wrong verdicts by
/// construction.
/// </para>
/// <para>
/// The point is that a wrong verdict must still not produce broken output. Curb's premise is that it trusts
/// the compiler's answer and owns only the rewrite; this measures the second half of that on its own. What
/// is asserted is therefore about the machinery rather than the verdicts:
/// </para>
/// <list type="number">
/// <item>No file ever fails verification. The declared deltas describe exactly what each fix does.</item>
/// <item>Anything rewritten still parses. A fix may make a file that does not compile — deleting a needed
/// import does — but it may never make one the parser rejects.</item>
/// <item>Re-running with the same, now stale, diagnostics changes nothing. That is the node-kind gate,
/// which is what makes consuming a verdict from an earlier build safe, measured across a real corpus
/// instead of on one hand-written case.</item>
/// </list>
/// <para>
/// Nothing is written. The corpus is read, cleaned in memory and thrown away, so this can be pointed at a
/// checkout somebody is working in.
/// </para>
/// <para>
/// Skipped unless <c>CURB_CLEANUP_CORPUS</c> names a directory, the same shape as the expectation dump.
/// Driven by <c>./build.sh cleanupsafety --corpus &lt;path&gt;</c>.
/// </para>
/// </remarks>
public class CleanupCorpusTests
{
	private static string? Corpus =>
		Environment.GetEnvironmentVariable("CURB_CLEANUP_CORPUS") is { Length: > 0 } path && Directory.Exists(path)
			? path
			: null;

	[Test]
	public async Task No_wrong_verdict_produces_damage()
	{
		// Not a skip attribute, so `./build.sh test` stays fast without the case disappearing from the run
		// list. Driven by ./build.sh cleanupsafety --corpus <path>.
		if (Corpus is not { } corpus)
		{
			Console.WriteLine("no CURB_CLEANUP_CORPUS; run ./build.sh cleanupsafety --corpus <path>");
			return;
		}

		var files = Directory
			.EnumerateFiles(corpus, "*.cs", SearchOption.AllDirectories)
			.Where(path => !path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
			.Where(path => !path.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
			.ToArray();

		files.Should().NotBeEmpty("the corpus has to hold C#");

		var considered = 0;
		var unparsable = 0;
		var changed = 0;
		var applied = 0;
		var refused = 0;
		var failures = new ConcurrentBag<string>();

		var stopwatch = Stopwatch.StartNew();

		Parallel.ForEach(
			files,
			new ParallelOptions { MaxDegreeOfParallelism = Environment.ProcessorCount },
			() => new CSharpCleaner(),
			(path, state, cleaner) =>
			{
				string source;
				try
				{
					source = File.ReadAllText(path);
				}
				catch (IOException)
				{
					return cleaner;
				}

				if (!CSharpSource.TryParse(source, out var parsed, out _))
				{
					Interlocked.Increment(ref unparsable);
					return cleaner;
				}

				var diagnostics = Synthesise(path, parsed.Root, parsed.Text);
				if (diagnostics.Count == 0)
					return cleaner;

				Interlocked.Increment(ref considered);

				var result = cleaner.Clean(source, diagnostics);
				Interlocked.Add(ref refused, result.Refusals.Count);

				// 1. Verification is never allowed to fail. A declared delta that does not describe the edit
				//    would show up here and nowhere else.
				if (result.Status == CleanupStatus.VerificationFailed)
				{
					failures.Add($"{path}: verification failed — {result.Message}");
					return cleaner;
				}

				if (!result.Changed || result.Text is null)
					return cleaner;

				Interlocked.Increment(ref changed);
				Interlocked.Add(ref applied, result.Applied);

				// 2. The output has to parse. Wrong verdicts make files that do not compile; none of them
				//    may make a file the parser rejects.
				if (!CSharpSource.TryParse(result.Text, out _, out var errors))
				{
					var detail = errors.Count > 0
						? errors[0].GetMessage(System.Globalization.CultureInfo.InvariantCulture)
						: "unknown";

					failures.Add($"{path}: output does not parse — {detail}");
					return cleaner;
				}

				// 3. The same diagnostics against the output describe a file that has moved, so every one of
				//    them has to be refused. This is the node-kind gate, at corpus scale.
				var second = cleaner.Clean(result.Text, diagnostics);
				if (second.Changed)
					failures.Add($"{path}: a second pass with the same stale diagnostics changed the file again");

				return cleaner;
			},
			// Nothing to release: a cleaner owns no pooled buffers, unlike a formatter.
			_ => { });

		stopwatch.Stop();

		var summary =
			$"corpus: {files.Length} file(s), {considered} with candidate sites, {unparsable} unparsable, "
			+ $"{applied} fix(es) applied across {changed} file(s), {refused} refused, "
			+ $"in {stopwatch.ElapsedMilliseconds}ms";

		Console.WriteLine(summary);

		// Written as well as printed: the test framework keeps a passing test's console output to itself, and
		// a gate whose numbers are only visible when it fails is a gate nobody watches drift.
		if (Environment.GetEnvironmentVariable("CURB_CLEANUP_SUMMARY") is { Length: > 0 } destination)
			File.WriteAllText(destination, summary + Environment.NewLine);

		foreach (var failure in failures.Take(20))
			Console.WriteLine($"  {failure}");

		failures.Should().BeEmpty("a wrong verdict may leave a file that does not compile, but never one that is damaged");

		// A corpus that produced no fixes would pass every assertion above while measuring nothing.
		applied.Should().BeGreaterThan(0, "the sweep has to actually rewrite something to be a test");
		changed.Should().BeGreaterThan(0);

		await Task.CompletedTask;
	}

	/// <summary>
	/// Claims every rule fires everywhere it could, which is what makes this adversarial.
	/// </summary>
	/// <remarks>
	/// Shapes mimic the real diagnostics as measured: IDE0005 spans a maximal run of consecutive directives
	/// rather than one directive, and the modifier and type rules point at the name or the type rather than
	/// at the declaration.
	/// </remarks>
	private static List<CleanupDiagnostic> Synthesise(string path, SyntaxNode root, SourceText text)
	{
		var diagnostics = new List<CleanupDiagnostic>();

		void Add(string ruleId, TextSpan span, bool withEnd)
		{
			var start = text.Lines.GetLinePosition(span.Start);
			var end = withEnd ? text.Lines.GetLinePosition(span.End) : (LinePosition?)null;
			diagnostics.Add(new CleanupDiagnostic(ruleId, path, start, end, DiagnosticLevel.Warning));
		}

		// IDE0005, one per maximal run of consecutive using directives — the measured shape.
		foreach (var list in UsingLists(root))
		{
			for (var i = 0; i < list.Count;)
			{
				var j = i;
				while (j + 1 < list.Count && Adjacent(list[j], list[j + 1]))
					j++;

				Add("IDE0005", TextSpan.FromBounds(list[i].Span.Start, list[j].Span.End), withEnd: true);
				i = j + 1;
			}
		}

		foreach (var node in root.DescendantNodes())
		{
			switch (node)
			{
				// IDE0044 and IDE0040, at the declaration's name.
				case FieldDeclarationSyntax field:
					foreach (var declarator in field.Declaration.Variables)
					{
						Add("IDE0044", declarator.Identifier.Span, withEnd: true);
						Add("IDE0040", declarator.Identifier.Span, withEnd: true);
					}

					break;

				case MethodDeclarationSyntax method:
					Add("IDE0040", method.Identifier.Span, withEnd: true);
					Add("IDE0251", method.Identifier.Span, withEnd: true);
					break;

				case PropertyDeclarationSyntax property:
					Add("IDE0040", property.Identifier.Span, withEnd: true);
					Add("IDE0251", property.Identifier.Span, withEnd: true);
					break;

				case BaseTypeDeclarationSyntax type:
					Add("IDE0040", type.Identifier.Span, withEnd: true);

					// IDE0250, at the type's name. Claimed on classes and interfaces too, so the rule's own
					// gate is what has to refuse them.
					Add("IDE0250", type.Identifier.Span, withEnd: true);
					break;

				// IDE0090, at the `new` keyword.
				case ObjectCreationExpressionSyntax creation:
					Add("IDE0090", creation.NewKeyword.Span, withEnd: true);
					break;

				// IDE0007, at the type.
				case LocalDeclarationStatementSyntax local:
					Add("IDE0007", local.Declaration.Type.Span, withEnd: true);
					break;

				case ForEachStatementSyntax loop:
					Add("IDE0007", loop.Type.Span, withEnd: true);
					break;

				// IDE0034, at the `default` keyword.
				case DefaultExpressionSyntax @default:
					Add("IDE0034", @default.Keyword.Span, withEnd: true);
					break;

				// IDE0071, at the dot of the call. Claimed for every member access, not only ToString, so the
				// rule's own gate is what has to reject the rest.
				case MemberAccessExpressionSyntax access when access.Parent is InvocationExpressionSyntax:
					Add("IDE0071", access.OperatorToken.Span, withEnd: true);
					break;
			}
		}

		// IDE0240, at the `#` of every nullable directive. Trivia, so it comes from a trivia walk rather than
		// the node walk above.
		foreach (var trivia in root.DescendantTrivia(descendIntoTrivia: true))
		{
			if (trivia.IsKind(SyntaxKind.NullableDirectiveTrivia))
				Add("IDE0240", trivia.Span, withEnd: true);
		}

		return diagnostics;
	}

	/// <summary>True when nothing but trivia separates two directives, which is what makes them one run.</summary>
	private static bool Adjacent(UsingDirectiveSyntax left, UsingDirectiveSyntax right) =>
		right.Span.Start >= left.Span.End;

	private static IEnumerable<IReadOnlyList<UsingDirectiveSyntax>> UsingLists(SyntaxNode root)
	{
		if (root is CompilationUnitSyntax unit)
		{
			yield return [.. unit.Usings];

			foreach (var member in unit.Members)
			{
				foreach (var nested in FromMember(member))
					yield return nested;
			}
		}
	}

	private static IEnumerable<IReadOnlyList<UsingDirectiveSyntax>> FromMember(MemberDeclarationSyntax member)
	{
		switch (member)
		{
			case BaseNamespaceDeclarationSyntax space:
				yield return [.. space.Usings];

				foreach (var nested in space.Members)
				{
					foreach (var deeper in FromMember(nested))
						yield return deeper;
				}

				break;
		}
	}
}
