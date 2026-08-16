using AwesomeAssertions;
using Microsoft.CodeAnalysis.Text;
using Nullean.Kerf.Verification;

namespace Nullean.Kerf.Tests.Verification;

/// <summary>
/// The content verifier's declared deltas, and — the part that matters — what they still refuse.
/// </summary>
/// <remarks>
/// <para>
/// Every rule that changes tokens widens this check, and a widening that is too generous is
/// invisible: the tests all pass and the net quietly stops catching the thing it exists for. So each
/// delta is tested from both sides. The permitted case is one assertion; the damage the delta must
/// not excuse is several.
/// </para>
/// <para>
/// Verified content is whitespace-blind by design, so these strings differ only in the tokens the
/// delta is about.
/// </para>
/// </remarks>
public class DeclaredDeltaTests
{
	private static bool Verify(
		string source,
		string output,
		IReadOnlyList<TextSpan>? reordered = null,
		bool trailingCommas = false,
		bool bracesAdded = false) =>
		ContentVerifier.Verify(source, output, out _, reordered, trailingCommas, bracesAdded);

	// ---- no delta declared ------------------------------------------------------------------------

	[Test]
	public async Task Whitespace_alone_is_never_a_change()
	{
		Verify("class C{int x;}", "class C\n{\n\tint x;\n}\n").Should().BeTrue();
		await Task.CompletedTask;
	}

	[Test]
	public async Task Without_a_declared_delta_nothing_may_move()
	{
		Verify("var a = new[] { 1, 2 };", "var a = new[] { 1, 2, };").Should().BeFalse("a comma appeared");
		Verify("public static void M()", "static public void M()").Should().BeFalse("modifiers moved");
		Verify("if (a) Foo();", "if (a) { Foo(); }").Should().BeFalse("braces appeared");
		await Task.CompletedTask;
	}

	// ---- trailing commas --------------------------------------------------------------------------

	[Test]
	public async Task A_trailing_comma_may_appear_or_vanish()
	{
		Verify("new[] { 1, 2 }", "new[] { 1, 2, }", trailingCommas: true).Should().BeTrue();
		Verify("new[] { 1, 2, }", "new[] { 1, 2 }", trailingCommas: true).Should().BeTrue();
		Verify("[ 1, 2 ]", "[ 1, 2, ]", trailingCommas: true).Should().BeTrue();
		await Task.CompletedTask;
	}

	[Test]
	public async Task The_comma_delta_does_not_excuse_anything_else()
	{
		// The allowance is only for a comma against a closing brace or bracket. Everything the rule
		// could plausibly have been written loosely enough to permit is still refused.
		Verify("new[] { 1, x }", "new[] { 1, }", trailingCommas: true)
			.Should().BeFalse("an element was dropped");
		Verify("new[] { 1, 2 }", "new[] { 1, 2, 2 }", trailingCommas: true)
			.Should().BeFalse("an element was duplicated");
		Verify("M(a, b)", "M(a, b,)", trailingCommas: true)
			.Should().BeFalse("a comma before a parenthesis is not legal C# and is not the declared delta");
		Verify("new[] { 1, 2 }", "new[] { 1, 2 },", trailingCommas: true)
			.Should().BeFalse("a comma outside the closer is not the declared delta");
		await Task.CompletedTask;
	}

	// ---- permuted regions -------------------------------------------------------------------------

	[Test]
	public async Task A_declared_region_may_be_permuted()
	{
		var region = new[] { TextSpan.FromBounds(0, "static public".Length) };
		Verify("static public void M()", "public static void M()", region).Should().BeTrue();
		await Task.CompletedTask;
	}

	[Test]
	public async Task Permutation_is_a_multiset_compare_not_a_licence()
	{
		var region = new[] { TextSpan.FromBounds(0, "static public".Length) };

		Verify("static public void M()", "public void M()", region)
			.Should().BeFalse("a modifier was dropped");
		Verify("static public void M()", "public static async void M()", region)
			.Should().BeFalse("a modifier was invented");
		Verify("static public void M()", "public statuc void M()", region)
			.Should().BeFalse("a modifier was misspelled");
		await Task.CompletedTask;
	}

	[Test]
	public async Task Several_regions_are_each_checked()
	{
		// The generalisation modifier ordering needed: using sorting permutes one block per file,
		// modifiers permute a run per declaration.
		const string source = "static public int A; static internal int B;";
		var second = source.IndexOf("static internal", StringComparison.Ordinal);
		var regions = new[]
		{
			TextSpan.FromBounds(0, "static public".Length),
			TextSpan.FromBounds(second, second + "static internal".Length),
		};

		Verify(source, "public static int A; internal static int B;", regions).Should().BeTrue();
		Verify(source, "public static int A; internal static int C;", regions)
			.Should().BeFalse("a name outside the permuted runs changed");
		await Task.CompletedTask;
	}

	// ---- added braces -----------------------------------------------------------------------------

	[Test]
	public async Task Braces_may_be_added_around_a_statement()
	{
		Verify("if (a) Foo();", "if (a) { Foo(); }", bracesAdded: true).Should().BeTrue();
		Verify("if (a) Foo(); else Bar();", "if (a) { Foo(); } else { Bar(); }", bracesAdded: true).Should().BeTrue();
		await Task.CompletedTask;
	}

	[Test]
	public async Task An_added_brace_still_has_to_balance()
	{
		Verify("if (a) Foo();", "if (a) { Foo();", bracesAdded: true)
			.Should().BeFalse("the pair was left open");
		Verify("if (a) Foo();", "if (a) Foo(); }", bracesAdded: true)
			.Should().BeFalse("a closing brace was never opened");
		await Task.CompletedTask;
	}

	[Test]
	public async Task The_brace_delta_does_not_excuse_losing_the_body()
	{
		// The case the counted pair is built to reject. Stepping over an added brace is only safe
		// because everything between still has to match exactly.
		Verify("if (a) Foo();", "if (a) { }", bracesAdded: true)
			.Should().BeFalse("the body was dropped");
		Verify("if (a) Foo();", "if (a) { Bar(); }", bracesAdded: true)
			.Should().BeFalse("the body was replaced");
		Verify("if (a) { Foo(); }", "if (a) { }", bracesAdded: true)
			.Should().BeFalse("a body that already had braces was emptied");
		await Task.CompletedTask;
	}

	[Test]
	public async Task An_added_brace_next_to_an_existing_one_is_settled_by_count()
	{
		// Braces are indistinguishable, so an added `}` sits directly in front of the enclosing
		// block's own and matches it. The pair is then only resolvable by counting, which is what the
		// end-of-file check does — and what a naive local rule got wrong.
		Verify("void M() { if (a) Foo(); }", "void M() { if (a) { Foo(); } }", bracesAdded: true)
			.Should().BeTrue();
		await Task.CompletedTask;
	}
}
