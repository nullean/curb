using System.IO.Abstractions;
using AwesomeAssertions;
using Microsoft.CodeAnalysis.CSharp;
using Nullean.Curb.Cli;
using Nullean.Curb.CorpusGenerator;

namespace Nullean.Curb.Tests.Corpus;

/// <summary>
/// The generated corpus every corpus gate runs against (tools/Nullean.Curb.CorpusGenerator). A gate's number may only
/// move when Curb, the tests or the generator do, so the corpus has to be a pure function of its seed and inputs; and it
/// must not already be Curb's output, or every gate comparing Curb with dotnet format or with itself passes for free.
/// </summary>
public class CorpusGeneratorTests
{
	/// <summary>Small enough to generate in well under a second; big enough to hold every template several times.</summary>
	private const double SizeMb = 0.4;

	private static readonly (string TestCase, string Source, string Settings)[] Cases =
	[
		("SpacingTests.Binary_operators", "class C { int M(int a,int b) => a+b; }", "csharp_space_around_binary_operators = before_and_after"),
		("BraceTests.Allman", "class C\n{\n    void M() { if (true) { } }\n}", ""),
		("BraceTests.Allman", "namespace N { class D { void M() { } } }", "csharp_new_line_before_open_brace = none"),
		("CommentTests.Trailing", "class E\n{\n    int x; // the x\n}", ""),
	];

	private static string Scratch() => Directory.CreateTempSubdirectory("curb-corpus-").FullName;

	/// <summary>An expectation dump in the shape ExpectationDump writes, its directories numbered in the given order.</summary>
	private static string Dump(IEnumerable<int> order)
	{
		var root = Scratch();
		var number = 0;
		foreach (var index in order)
		{
			var (testCase, source, settings) = Cases[index];
			var directory = Path.Combine(root, (++number).ToString("D5", System.Globalization.CultureInfo.InvariantCulture));
			Directory.CreateDirectory(directory);
			File.WriteAllText(Path.Combine(directory, ".editorconfig"), "root = true\n[*.cs]\n" + settings + "\n");
			File.WriteAllText(Path.Combine(directory, "Source.cs"), source + "\n");
			File.WriteAllText(Path.Combine(directory, "Expected.cs"), source + "\n");
			File.WriteAllText(Path.Combine(directory, "TestCase.txt"), testCase);
		}

		return root;
	}

	private static string Generate(int seed, string dump)
	{
		var output = Path.Combine(Scratch(), "corpus");
		CorpusGenerator.CorpusGenerator.Generate(new GeneratorOptions { Output = output, Expectations = dump, Seed = seed, SizeMb = SizeMb });
		return output;
	}

	private static SortedDictionary<string, byte[]> Contents(string root) =>
		new(
			Directory.GetFiles(root, "*", SearchOption.AllDirectories).ToDictionary(f => Path.GetRelativePath(root, f).Replace('\\', '/'), File.ReadAllBytes),
			StringComparer.Ordinal
		);

	[Test]
	public async Task The_same_seed_writes_a_byte_identical_corpus_whatever_order_the_dump_is_in()
	{
		// The dump's directory numbers come from tests finishing in parallel, so two runs number the same cases
		// differently. The corpus must not notice.
		var first = Contents(Generate(7, Dump([0, 1, 2, 3])));
		var second = Contents(Generate(7, Dump([3, 2, 1, 0])));

		second.Keys.Should().Equal(first.Keys);
		foreach (var (path, bytes) in first)
		{
			second[path].Should().Equal(bytes, $"{path} has to be byte-identical for the same seed");
		}
		first.Keys.Should().Contain(k => k.StartsWith("snippets/BraceTests/", StringComparison.Ordinal), "the dump's cases become snippets");
		await Task.CompletedTask;
	}

	[Test]
	public async Task Another_seed_writes_another_layout()
	{
		var dump = Dump([0, 1, 2, 3]);
		var first = Contents(Generate(1, dump));
		var second = Contents(Generate(2, dump));

		var shared = first.Keys.Intersect(second.Keys).Where(k => k.EndsWith(".cs", StringComparison.Ordinal)).ToList();
		var differing = shared.Count(k => !first[k].AsSpan().SequenceEqual(second[k]));
		differing.Should().BeGreaterThan(shared.Count / 2, "a seed is supposed to decide the layout of most files");
		await Task.CompletedTask;
	}

	[Test]
	public async Task The_corpus_is_not_already_curbs_output()
	{
		var corpus = Generate(1, Dump([0, 1, 2, 3]));
		var files = Directory.GetFiles(corpus, "*.cs", SearchOption.AllDirectories);

		var summary = FormattingRun.Execute(new FileSystem(), corpus, write: false, explicitFiles: files);

		var share = 100.0 * summary.Changed / summary.Files;
		Console.WriteLine($"Curb would change {summary.Changed} of {summary.Files} generated C# file(s) ({share:F1}%)");
		summary.Failed.Should().Be(0);
		summary.Unparsable.Should().Be(0);
		share.Should().BeGreaterThan(80, "a corpus Curb already formatted would make every formatting gate pass trivially");
		await Task.CompletedTask;
	}

	[Test]
	public async Task A_permutation_changes_layout_and_never_a_token()
	{
		const string source =
			"""
			using System;

			namespace N;

			/// <summary>A type.</summary>
			public sealed class C
			{
				private readonly int _x = 1; // trailing

				public int M(int a, string b)
				{
					#if DEBUG
					var s = $"{a + _x} and {b}";
					#endif
					return a switch { > 0 => checked(a + 1), _ => b.Length };
				}
			}
			""";

		var original = CSharpSyntaxTree.ParseText(source).GetRoot().DescendantTokens().Select(t => (t.RawKind, t.Text)).ToList();
		var changed = 0;
		for (var seed = 0; seed < 50; seed++)
		{
			var r = Rng.For(seed, "permutation");
			var result = Permuter.Apply(source, LayoutProfile.Draw(r), r);
			var tokens = CSharpSyntaxTree.ParseText(result.Text.TrimStart('\uFEFF')).GetRoot().DescendantTokens().Select(t => (t.RawKind, t.Text)).ToList();

			tokens.Should().Equal(original, $"seed {seed} must change layout only");
			if (result.Text.Replace("\r\n", "\n").TrimStart('\uFEFF') != source)
			{
				changed++;
			}
		}

		changed.Should().BeGreaterThan(40, "almost every profile changes something");
		await Task.CompletedTask;
	}
}
