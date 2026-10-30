using AwesomeAssertions;
using Nullean.Curb.Razor;

namespace Nullean.Curb.Tests.Razor;

/// <summary>
/// The last check before a Razor file is returned: the output re-scanned, every byte outside the replaced
/// blocks compared with the source. Fed tampered outputs directly, since a working splice never produces one.
/// </summary>
public class RazorMarkupCheckTests
{
	private const string Source = "<p>a</p>\n@code {\nint x=1;\n}\n<p>b</p>\n@code {\nRenderFragment F => @<p/>;\n}\n<p>c</p>\n";
	private const string Replacement = "    int x = 1;\n";
	private const string Output = "<p>a</p>\n@code {\n    int x = 1;\n}\n<p>b</p>\n@code {\nRenderFragment F => @<p/>;\n}\n<p>c</p>\n";

	private static bool Check(string output, out string? failure, string? replacement = Replacement)
	{
		var blocks = new List<RazorBlock>();
		RazorBlockScanner.Scan(Source, blocks);
		blocks.Should().HaveCount(2);
		return RazorFormatter.MarkupUnchanged(Source, blocks, output, [replacement, null], [], component: true, out failure);
	}

	[Test]
	public async Task The_real_output_passes()
	{
		Check(Output, out var failure).Should().BeTrue(failure);
		await Task.CompletedTask;
	}

	[Test]
	[Arguments("<p>a</p>\n", "<p>A</p>\n")]
	[Arguments("@code {\n    int", "@code  {\n    int")]
	[Arguments("<p>b</p>", "<p>B</p>")]
	[Arguments("<p>c</p>\n", "<p>c</p>")]
	[Arguments("RenderFragment F", "RenderFragment  F")]
	[Arguments("\n}\n<p>b", "\n }\n<p>b")]
	public async Task A_changed_byte_outside_the_replaced_block_fails(string from, string to)
	{
		Output.Should().Contain(from);
		var tampered = Output.Replace(from, to, StringComparison.Ordinal);

		Check(tampered, out var failure).Should().BeFalse();
		failure.Should().NotBeNullOrEmpty();
		await Task.CompletedTask;
	}

	[Test]
	public async Task A_block_that_holds_something_other_than_its_replacement_fails()
	{
		Check(Output.Replace("int x = 1;", "int x = 2;", StringComparison.Ordinal), out _).Should().BeFalse();
		await Task.CompletedTask;
	}

	[Test]
	public async Task A_block_appearing_or_vanishing_fails()
	{
		Check(Output + "@code {\nint y;\n}\n", out var failure).Should().BeFalse();
		failure.Should().Contain("3 @code/@functions block(s) where the source had 2");
		Check(Output.Replace("@code {\nRenderFragment", "@kode {\nRenderFragment", StringComparison.Ordinal), out _).Should().BeFalse();
		await Task.CompletedTask;
	}

	[Test]
	public async Task A_block_whose_refusal_changes_fails()
	{
		// The refused block's closing brace now shares its line, so it scans differently.
		Check(Output.Replace(";\n}\n<p>c", "; }\n<p>c", StringComparison.Ordinal), out var failure).Should().BeFalse();
		failure.Should().Contain("scans differently");
		await Task.CompletedTask;
	}
}

/// <summary>How the members are found again in the formatter's output for the wrapping class.</summary>
public class RazorUnwrapTests
{
	[Test]
	[Arguments("class __CurbRazor__\n{\n    int x;\n}\n", "    int x;\n")]
	[Arguments("class __CurbRazor__ {\n    int x;\n}\n", "    int x;\n")]
	[Arguments("class __CurbRazor__\r\n{\r\n    int x;\r\n}\r\n", "    int x;\r\n")]
	[Arguments("class __CurbRazor__\n    {\n        int x;\n    }\n", "        int x;\n")]
	[Arguments("// {header}\nclass __CurbRazor__\n{\n    int x;\n}\n", "    int x;\n")]
	[Arguments("class __CurbRazor__\n{\n}", "")]
	public async Task The_members_between_the_wrapper_braces_are_found(string formatted, string members)
	{
		RazorFormatter.TryUnwrap(formatted, out var start, out var end).Should().BeTrue();
		formatted[start..end].Should().Be(members);
		await Task.CompletedTask;
	}

	[Test]
	[Arguments("class __CurbRazor__ { }\n")]
	[Arguments("class __CurbRazor__\n{ int x;\n}\n")]
	[Arguments("class __CurbRazor__\n{\n    int x; }\n")]
	[Arguments("class __CurbRazor__\n{\n    int x;\n} // trailing\n")]
	public async Task A_wrapper_whose_braces_share_a_line_is_not_unwrapped(string formatted)
	{
		RazorFormatter.TryUnwrap(formatted, out _, out _).Should().BeFalse();
		await Task.CompletedTask;
	}
}
