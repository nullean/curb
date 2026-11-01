using AwesomeAssertions;
using Microsoft.CodeAnalysis.Text;
using Nullean.Curb.Verification;

namespace Nullean.Curb.Tests.Verification;

/// <summary>
/// What the verifiers allow when <c>csharp_using_directive_placement</c> moves directives into a namespace, and
/// what they still refuse.
/// </summary>
/// <remarks>
/// Moving directives widens two checks: the content check lets the namespace's <c>{</c>-to-<c>;</c> substitution
/// fall inside the permuted region, and the round-trip comparer walks around the directives rather than through
/// them. Each is tested from both sides, since a widening that is too generous passes every formatting test.
/// </remarks>
public class UsingMoveVerificationTests
{
	private static bool RoundTrip(string source, string output, out string? failure)
	{
		CSharpSource.TryParse(source, out var parsed, out _).Should().BeTrue();
		return TokenStreamComparer.Verify(parsed.Root, source, output, output, out failure, usingsReordered: true, namespaceUnwrapped: true);
	}

	// ---- the round-trip comparer: global usings stay first ------------------------------------------

	[Test]
	public async Task A_directive_that_is_not_global_may_move_into_the_namespace()
	{
		RoundTrip(
			"global using System.Linq;\nusing System;\n\nnamespace N\n{\n    class C { }\n}\n",
			"global using System.Linq;\n\nnamespace N;\n\nusing System;\n\nclass C { }\n",
			out var failure).Should().BeTrue(failure);
		await Task.CompletedTask;
	}

	[Test]
	public async Task A_global_using_moved_into_a_namespace_is_refused()
	{
		// Every token is still there, so only a structural check can see it. It does not compile (CS8914).
		RoundTrip(
			"global using System.Linq;\nusing System;\n\nnamespace N\n{\n    class C { }\n}\n",
			"namespace N;\n\nglobal using System.Linq;\nusing System;\n\nclass C { }\n",
			out var failure).Should().BeFalse();
		failure.Should().Contain("global using");
		await Task.CompletedTask;
	}

	[Test]
	public async Task A_global_using_moved_into_a_block_namespace_is_refused()
	{
		RoundTrip(
			"global using System.Linq;\n\nnamespace N\n{\n    class C { }\n}\n",
			"namespace N\n{\n    global using System.Linq;\n\n    class C { }\n}\n",
			out _).Should().BeFalse();
		await Task.CompletedTask;
	}

	[Test]
	public async Task A_global_using_sorted_after_one_that_is_not_is_refused()
	{
		// CS8915.
		RoundTrip(
			"global using System.Linq;\nusing System;\n\nclass C { }\n",
			"using System;\nglobal using System.Linq;\n\nclass C { }\n",
			out _).Should().BeFalse();
		await Task.CompletedTask;
	}

	[Test]
	public async Task A_source_already_out_of_order_is_not_blamed_on_the_formatter()
	{
		RoundTrip(
			"using System;\nglobal using System.Linq;\n\nclass C { }\n",
			"using System;\nglobal using System.Linq;\n\nclass C { }\n",
			out var failure).Should().BeTrue(failure);
		await Task.CompletedTask;
	}

	// ---- the content check: the brace inside the permuted region ------------------------------------

	private const string Source = "using System;\nnamespace N\n{\n    class C { }\n}\n";

	/// <summary>From the first directive to the end of the opening brace, as the printer declares it.</summary>
	private static TextSpan[] Region => [TextSpan.FromBounds(0, Source.IndexOf('{', StringComparison.Ordinal) + 1)];

	private static bool Content(string output, bool namespaceUnwrapped = true) =>
		ContentVerifier.Verify(Source, output, out _, Region, namespaceUnwrapped: namespaceUnwrapped);

	[Test]
	public async Task The_brace_may_become_the_semicolon_inside_the_region()
	{
		Content("namespace N;\nusing System;\nclass C { }\n").Should().BeTrue();
		await Task.CompletedTask;
	}

	[Test]
	public async Task Not_without_the_namespace_conversion()
	{
		Content("namespace N;\nusing System;\nclass C { }\n", namespaceUnwrapped: false).Should().BeFalse();
		await Task.CompletedTask;
	}

	[Test]
	public async Task The_closing_brace_is_still_owed()
	{
		Content("namespace N;\nusing System;\nclass C { }\n}\n").Should().BeFalse("the block's closing brace was kept");
		await Task.CompletedTask;
	}

	[Test]
	public async Task Any_other_substitution_in_the_region_is_damage()
	{
		Content("namespace N,\nusing System;\nclass C { }\n").Should().BeFalse("the brace became a comma");
		Content("namespace N;\nusing Systen;\nclass C { }\n").Should().BeFalse("a directive was altered as well");
		await Task.CompletedTask;
	}

	[Test]
	public async Task The_substitution_is_not_allowed_twice()
	{
		// Taken in the region, so a second brace turned into a semicolon later in the file is damage.
		const string source = "using System;\nnamespace N\n{\n    class C { void M() { } }\n}\n";
		TextSpan[] region = [TextSpan.FromBounds(0, source.IndexOf('{', StringComparison.Ordinal) + 1)];
		ContentVerifier.Verify(source, "namespace N;\nusing System;\nclass C { void M() ; } }\n", out _, region, namespaceUnwrapped: true)
			.Should().BeFalse();
		await Task.CompletedTask;
	}
}
