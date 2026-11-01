using AwesomeAssertions;
using Microsoft.CodeAnalysis.Text;
using Nullean.Curb.Verification;

namespace Nullean.Curb.Tests.Verification;

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
		bool bracesAdded = false,
		IReadOnlyList<string>? inserted = null,
		IReadOnlyList<TextSpan>? dropped = null) =>
		ContentVerifier.Verify(source, output, out _, reordered, trailingCommas, bracesAdded,
			dropped: dropped, inserted: Locate(output, inserted));

	/// <summary>
	/// Turns declared words into the offsets the verifier matches on, by finding each in the output in turn.
	/// </summary>
	/// <remarks>
	/// A word the output does not contain gets an offset nothing can match, so "declared but never appeared"
	/// stays a failure rather than becoming an exception.
	/// </remarks>
	private static List<InsertedToken>? Locate(string output, IReadOnlyList<string>? inserted)
	{
		if (inserted is null)
			return null;

		var tokens = new List<InsertedToken>(inserted.Count);
		var from = 0;

		foreach (var text in inserted)
		{
			var at = output.IndexOf(text, from, StringComparison.Ordinal);
			tokens.Add(new InsertedToken(at, text));
			from = at < 0 ? from : at + text.Length;
		}

		return tokens;
	}

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

	// ---- inserted words ---------------------------------------------------------------------------
	//
	// What the cleanup modifier rules declare. Given as exact text rather than a count, so the allowance
	// is "precisely this word, once" — which is what these assertions are checking has teeth.

	[Test]
	public async Task A_declared_word_may_appear()
	{
		Verify("private string _n;", "private readonly string _n;", inserted: ["readonly"]).Should().BeTrue();
		Verify("int _n;", "private int _n;", inserted: ["private"]).Should().BeTrue();
		await Task.CompletedTask;
	}

	[Test]
	public async Task Several_declared_words_may_appear()
	{
		Verify("string _n; int _c;", "private string _n; private int _c;", inserted: ["private", "private"])
			.Should().BeTrue();

		await Task.CompletedTask;
	}

	[Test]
	public async Task A_word_that_was_not_declared_is_still_damage()
	{
		Verify("private string _n;", "private readonly string _n;").Should().BeFalse("nothing was declared");
		Verify("private string _n;", "private static readonly string _n;", inserted: ["readonly"])
			.Should().BeFalse("only one of the two words was declared");

		await Task.CompletedTask;
	}

	[Test]
	public async Task A_declared_word_does_not_excuse_a_different_one()
	{
		Verify("int _n;", "private int _n;", inserted: ["internal"]).Should().BeFalse();
		await Task.CompletedTask;
	}

	[Test]
	public async Task A_declared_word_must_be_a_whole_word()
	{
		// Without the boundary check a declared `readonly` would be satisfied by `readonlyish`, which is a
		// different identifier and a different program.
		Verify("private string _n;", "private readonlyish string _n;", inserted: ["readonly"]).Should().BeFalse();
		await Task.CompletedTask;
	}

	[Test]
	public async Task A_declared_word_that_never_appears_is_a_failure()
	{
		// The insertion has to be used. Otherwise a rule could declare an allowance, fail to make the edit,
		// and still be told the output was fine.
		Verify("int _n;", "int _n;", inserted: ["private"]).Should().BeFalse();
		Verify("string _n; int _c;", "private string _n; int _c;", inserted: ["private", "private"])
			.Should().BeFalse("only one of the two declared insertions was made");

		await Task.CompletedTask;
	}

	[Test]
	public async Task A_declared_word_does_not_excuse_losing_content()
	{
		Verify("private string _n;", "private readonly string;", inserted: ["readonly"])
			.Should().BeFalse("the field name went missing");

		Verify("private string _n; int _c;", "private readonly string _n;", inserted: ["readonly"])
			.Should().BeFalse("a whole declaration went missing");

		await Task.CompletedTask;
	}

	[Test]
	public async Task A_declared_word_may_share_a_prefix_with_the_token_behind_it()
	{
		// The case a 17,000-file corpus found. `var` inserted in front of a variable called `version` leaves
		// both sides reading `v`, so an insertion consumed at the first disagreement is never consumed at
		// all and the walk desynchronises. Matching on the declared offset is what fixes it, and this is the
		// regression.
		var dropped = new[] { new TextSpan("private ".Length, "string".Length) };

		Verify("private string version = x;", "private var version = x;", inserted: ["var"], dropped: dropped)
			.Should().BeTrue();

		Verify("private string value = x;", "private var value = x;", inserted: ["var"], dropped: dropped)
			.Should().BeTrue();

		await Task.CompletedTask;
	}

	[Test]
	public async Task A_declared_word_at_the_wrong_place_is_still_damage()
	{
		// The offset is part of the declaration, not a hint. A word that turns up somewhere else is not the
		// insertion that was declared.
		var inserted = new[] { new InsertedToken(0, "private") };

		ContentVerifier.Verify("int _n; int _m;", "int _n; private int _m;", out _, inserted: inserted)
			.Should().BeFalse();

		await Task.CompletedTask;
	}

	[Test]
	public async Task A_declared_word_does_not_excuse_altering_content()
	{
		Verify("private string _n;", "private readonly string _m;", inserted: ["readonly"])
			.Should().BeFalse("the field was renamed");

		await Task.CompletedTask;
	}

	// ---- the same word twice at one site ----------------------------------------------------------

	// A multi-targeted project reports each site once per target framework, and cleanup applied the
	// insertion once per report. Both insertions were declared, so a check that only asked "was every
	// inserted word declared?" passed `readonly readonly`, which is CS1004. A site takes a word once.

	[Test]
	public async Task A_word_declared_twice_at_one_site_is_damage()
	{
		Verify("private string _n;", "private readonly readonly string _n;", inserted: ["readonly", "readonly"])
			.Should().BeFalse("the content check must refuse a duplicated insertion");

		Compare("private string _n;", "private readonly readonly string _n;", ["readonly", "readonly"])
			.Should().BeFalse("the token check must refuse a duplicated insertion");

		Verify("int _n;", "private private int _n;", inserted: ["private", "private"]).Should().BeFalse();
		Compare("int _n;", "private private int _n;", ["private", "private"]).Should().BeFalse();

		await Task.CompletedTask;
	}

	[Test]
	public async Task A_word_inserted_beside_the_same_keyword_is_damage()
	{
		// One declared insertion, but next to a copy the source already had. The rules refuse this up front;
		// the token check refuses it too, so that gate regressing cannot write CS1004 either.
		var after = new[] { new InsertedToken("private readonly ".Length, "readonly") };
		CompareAt("private readonly string _n;", "private readonly readonly string _n;", after).Should().BeFalse();

		var before = new[] { new InsertedToken("private ".Length, "readonly") };
		CompareAt("private readonly string _n;", "private readonly readonly string _n;", before).Should().BeFalse();

		await Task.CompletedTask;
	}

	[Test]
	public async Task Different_words_at_one_site_are_still_allowed()
	{
		// IDE0040 and IDE0044 on one field both insert at the head of an empty modifier list.
		Verify("int _n;", "private readonly int _n;", inserted: ["private", "readonly"]).Should().BeTrue();
		Compare("int _n;", "private readonly int _n;", ["private", "readonly"]).Should().BeTrue();

		// The same word at two different sites is two fixes, not a duplicate.
		Compare("string _n; int _c;", "private string _n; private int _c;", ["private", "private"]).Should().BeTrue();

		await Task.CompletedTask;
	}

	// ---- welded tokens ----------------------------------------------------------------------------

	[Test]
	public async Task A_declared_word_welded_onto_the_name_after_it_is_damage()
	{
		// IDE0007 on `List<string>names`: the type went and `var` landed against the name.
		var dropped = new[] { new TextSpan(0, "List<string>".Length) };

		Verify("List<string>names = x;", "varnames = x;", inserted: ["var"], dropped: dropped).Should().BeFalse();
		CompareAt("List<string>names = x;", "varnames = x;", [new InsertedToken(0, "var")], dropped).Should().BeFalse();

		Verify("List<string>names = x;", "var names = x;", inserted: ["var"], dropped: dropped).Should().BeTrue();
		CompareAt("List<string>names = x;", "var names = x;", [new InsertedToken(0, "var")], dropped).Should().BeTrue();

		await Task.CompletedTask;
	}

	[Test]
	public async Task A_declared_word_welded_onto_the_name_before_it_is_damage()
	{
		// The content check looked only at the character after an inserted word, so `x` then `readonly`,
		// read as the one identifier `xreadonly`, satisfied it. The boundary holds on both sides now.
		var inserted = new[] { new InsertedToken("int x".Length, "readonly") };

		ContentVerifier.Verify("int x;", "int xreadonly;", out _, inserted: inserted).Should().BeFalse();

		await Task.CompletedTask;
	}

	/// <summary>The re-parse check, with insertions located the way <see cref="Locate"/> finds them.</summary>
	private static bool Compare(string source, string output, IReadOnlyList<string> inserted) =>
		CompareAt(source, output, Locate(output, inserted)!);

	/// <summary>The re-parse check over a class body, so a field declaration parses as one.</summary>
	private static bool CompareAt(
		string source, string output, IReadOnlyList<InsertedToken> inserted, IReadOnlyList<TextSpan>? dropped = null)
	{
		const string prefix = "class C { ";
		var original = prefix + source + " }";
		var produced = prefix + output + " }";
		CSharpSource.TryParse(original, out var parsed, out _).Should().BeTrue();

		var shifted = inserted.Select(token => token with { Offset = token.Offset + prefix.Length }).ToList();
		var shiftedDropped = dropped?.Select(span => new TextSpan(span.Start + prefix.Length, span.Length)).ToList();

		return TokenStreamComparer.Verify(parsed.Root, original, produced, produced, out _, dropped: shiftedDropped, inserted: shifted);
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
