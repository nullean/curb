using AwesomeAssertions;
using Nullean.Curb.Razor;

namespace Nullean.Curb.Tests.Razor;

/// <summary>Which lines <see cref="RazorBlockScanner"/> takes for a block, and where it says each one ends.</summary>
public class RazorBlockScannerTests
{
	private static List<RazorBlock> Scan(string source)
	{
		var blocks = new List<RazorBlock>();
		RazorBlockScanner.Scan(source, blocks);
		return blocks;
	}

	/// <summary>The inner text the formatter would be handed, or null for a refused block.</summary>
	private static string? Inner(string source, RazorBlock block) =>
		block.Refusal == RazorBlockRefusal.None ? source[block.InnerStart..block.InnerEnd] : null;

	[Test]
	public async Task The_inner_text_is_whole_lines_between_the_brace_lines()
	{
		const string source = "<p/>\n@code {  \n  int x;\n  }  \n<p/>\n";
		var blocks = Scan(source);

		blocks.Should().ContainSingle();
		Inner(source, blocks[0]).Should().Be("  int x;\n");
		source[blocks[0].CloseBrace].Should().Be('}');
		blocks[0].IsFunctions.Should().BeFalse();
		await Task.CompletedTask;
	}

	[Test]
	public async Task Functions_and_a_brace_on_the_next_line_are_found()
	{
		const string source = "@functions\n{\nint x;\n}\n@code\t{\nint y;\n}\n";
		var blocks = Scan(source);

		blocks.Should().HaveCount(2);
		blocks[0].IsFunctions.Should().BeTrue();
		Inner(source, blocks[0]).Should().Be("int x;\n");
		Inner(source, blocks[1]).Should().Be("int y;\n");
		await Task.CompletedTask;
	}

	[Test]
	public async Task Crlf_and_cr_line_breaks_end_lines()
	{
		Inner("@code {\r\nint x;\r\n}\r\n", Scan("@code {\r\nint x;\r\n}\r\n")[0]).Should().Be("int x;\r\n");
		Inner("@code {\rint x;\r}\r", Scan("@code {\rint x;\r}\r")[0]).Should().Be("int x;\r");
		await Task.CompletedTask;
	}

	[Test]
	[Arguments("string a = \"}\";")]
	[Arguments("string a = @\"\n}\n\";")]
	[Arguments("string a = $\"{(true ? \"}\" : \"{\")} }} {{\";")]
	[Arguments("string a = $$\"\"\"{{x}} } {\"\"\";")]
	[Arguments("string a = \"\"\"\n    }\n    \"\"\";")]
	[Arguments("string a = $\"\"\"\n    {x} }\n    \"\"\";")]
	[Arguments("char a = '}';")]
	[Arguments("// }")]
	[Arguments("/* }\n} */")]
	[Arguments("#if NEVER\n}\n#endif")]
	public async Task A_brace_the_lexer_does_not_call_a_brace_is_not_counted(string member)
	{
		var source = $"@code {{\n{member}\n}}\n<p>after</p>\n";
		var blocks = Scan(source);

		blocks.Should().ContainSingle();
		blocks[0].Refusal.Should().Be(RazorBlockRefusal.None);
		Inner(source, blocks[0]).Should().Be(member + "\n");
		await Task.CompletedTask;
	}

	[Test]
	public async Task Nested_braces_are_counted()
	{
		const string source = "@code {\nvoid M()\n{\nif (x) { }\n}\n}\n";
		Inner(source, Scan(source)[0]).Should().Be("void M()\n{\nif (x) { }\n}\n");
		await Task.CompletedTask;
	}

	[Test]
	[Arguments("@* @code {\nint x;\n} *@\n")]
	[Arguments("@*\n@code {\nint x;\n}\n*@\n")]
	[Arguments("<!--\n@code {\nint x;\n}\n-->\n")]
	[Arguments("<!-- x --><!--\n@code {\n}\n-->\n")]
	[Arguments("@@code {\nint x;\n}\n")]
	[Arguments("<p>@code {\nint x;\n}</p>\n")]
	[Arguments("@code { int x; }\n")]
	[Arguments("@code {")]
	[Arguments("@code { // comment\nint x;\n}\n")]
	[Arguments("@codeBehind {\nint x;\n}\n")]
	[Arguments("@code\n\n{\nint x;\n}\n")]
	public async Task Not_a_block(string source)
	{
		Scan(source).Should().BeEmpty();
		await Task.CompletedTask;
	}

	[Test]
	public async Task A_view_has_functions_blocks_but_no_code_blocks()
	{
		const string source = "@code {\nint x;\n}\n@functions {\nint y;\n}\n";
		var blocks = new List<RazorBlock>();
		RazorBlockScanner.Scan(source, blocks, component: false);

		blocks.Should().ContainSingle().Which.IsFunctions.Should().BeTrue();
		Scan(source).Should().HaveCount(2);
		await Task.CompletedTask;
	}

	[Test]
	public async Task An_escaped_at_before_a_star_does_not_open_a_comment()
	{
		// `@@*` is a literal `@*`, not a Razor comment, so the block after it is still found.
		const string source = "<p>a@@*b</p>\n@code {\nint x;\n}\n";
		Scan(source).Should().ContainSingle().Which.Refusal.Should().Be(RazorBlockRefusal.None);
		await Task.CompletedTask;
	}

	[Test]
	public async Task A_comment_that_closes_lets_the_next_line_be_a_block()
	{
		const string source = "@* x *@\n@code {\nint x;\n}\n<!-- y -->\n@code {\nint y;\n}\n";
		Scan(source).Should().HaveCount(2);
		await Task.CompletedTask;
	}

	[Test]
	[Arguments("@code {\nint x;\n} <p/>\n")]
	[Arguments("@code {\nint x; }\n")]
	[Arguments("@code {\nint x;\n/* c */ }\n")]
	public async Task A_closing_brace_that_is_not_alone_is_refused(string source)
	{
		Scan(source).Should().ContainSingle().Which.Refusal.Should().Be(RazorBlockRefusal.CloseBraceNotAlone);
		await Task.CompletedTask;
	}

	[Test]
	public async Task An_unclosed_block_is_refused_and_nothing_after_it_is_a_block()
	{
		const string source = "@code {\nint x;\n@code {\nint y;\n}\n";
		var blocks = Scan(source);

		// The first brace never closes, so the second block cannot be told apart from its body.
		blocks.Should().ContainSingle().Which.Refusal.Should().Be(RazorBlockRefusal.Unclosed);
		await Task.CompletedTask;
	}

	[Test]
	[Arguments("@{\n    var s = @\"\n@code {\n    int    x;\n}\n\";\n}\n")]
	[Arguments("@{\n    var s = \"\"\"\n@code {\n    int    x;\n}\n\"\"\";\n}\n")]
	[Arguments("@{\n    var s = $@\"{1}\n@code {{\n    int    x;\n}}\n\";\n}\n")]
	[Arguments("@{\n    // \"\n    /*\n@code {\n    int    x;\n}\n*/\n}\n")]
	[Arguments("@if (true)\n{\n    var s = @\"\n@code {\n    int    x;\n}\n\";\n}\n")]
	[Arguments("@if (a) { <p/> } else if (b) { <p/> } else\n{\n    var s = @\"\n@code {\n    int    x;\n}\n\";\n}\n")]
	[Arguments("@foreach (var i in xs)\n{\n    var s = @\"\n@code {\n}\n\";\n}\n")]
	[Arguments("@for (var i = 0; i < 1; i++) {\n    var s = @\"\n@code {\n}\n\";\n}\n")]
	[Arguments("@while (x) {\n    var s = @\"\n@code {\n}\n\";\n}\n")]
	[Arguments("@do {\n    var s = @\"\n@code {\n}\n\";\n} while (x);\n")]
	[Arguments("@switch (x) {\n    case 1: var s = @\"\n@code {\n}\n\"; break;\n}\n")]
	[Arguments("@lock (x) {\n    var s = @\"\n@code {\n}\n\";\n}\n")]
	[Arguments("@using (var d = D()) {\n    var s = @\"\n@code {\n}\n\";\n}\n")]
	[Arguments("@try { } catch (Exception e) when (e is X) { } finally {\n    var s = @\"\n@code {\n}\n\";\n}\n")]
	[Arguments("<p>@(@\"\n@code {\n    int    x;\n}\n\")</p>\n")]
	[Arguments("<p>@Html.Raw(@\"\n@code {\n    int    x;\n}\n\")</p>\n")]
	[Arguments("<p>@items[@\"\n@code {\n}\n\"]</p>\n")]
	[Arguments("@code { // members\n    string s = @\"\n@code {\n    int    x;\n}\n\";\n}\n")]
	[Arguments("@functions\n{ string s = @\"\n@code {\n    int    x;\n}\n\";\n}\n")]
	public async Task A_block_shaped_line_inside_csharp_is_never_a_block(string source)
	{
		Scan(source).Where(block => block.Refusal == RazorBlockRefusal.None).Should().BeEmpty();
		await Task.CompletedTask;
	}

	[Test]
	public async Task A_real_block_after_skipped_code_is_still_found()
	{
		const string source = "@{\n    var s = \"}\";\n}\n@if (x) { <p>@y</p> }\n<p>@Model.Items[0].Name, mail a@b.com</p>\n@code {\nint x;\n}\n";
		var blocks = Scan(source);

		blocks.Should().ContainSingle();
		Inner(source, blocks[0]).Should().Be("int x;\n");
		await Task.CompletedTask;
	}

	[Test]
	[Arguments("@{\n    var s = 1;\n\n@code {\nint x;\n}\n")]
	[Arguments("@(Foo(\n@code {\nint x;\n}\n")]
	[Arguments("@if (x) <p>no braces</p>\n@code {\nint x;\n}\n")]
	[Arguments("@if (x) {\n<p>{</p>\n}\n@code {\nint x;\n}\n")]
	public async Task After_code_whose_end_cannot_be_found_nothing_is_a_block(string source)
	{
		Scan(source).Should().BeEmpty();
		await Task.CompletedTask;
	}

	[Test]
	public async Task An_at_after_a_letter_is_text_not_a_transition()
	{
		// `support@if.org` is an address: read as `@if`, its missing body would stop the scan.
		const string source = "<p>support@if.org, team@(x</p>\n@code {\nint x;\n}\n";
		Scan(source).Should().ContainSingle();
		await Task.CompletedTask;
	}

	[Test]
	public async Task A_directive_using_is_not_a_statement()
	{
		const string source = "@using System.Linq\n@using (var x = y) { }\n@code {\nint x;\n}\n";
		Scan(source).Should().ContainSingle();
		await Task.CompletedTask;
	}

	[Test]
	public async Task Scanning_resumes_after_a_block()
	{
		// The `@code {` line inside the first block's string is the first block's text, not a block.
		const string source = "@code {\nstring s = @\"\n@code {\n\";\n}\n<p/>\n";
		var blocks = Scan(source);

		blocks.Should().ContainSingle();
		Inner(source, blocks[0]).Should().Be("string s = @\"\n@code {\n\";\n");
		await Task.CompletedTask;
	}

	[Test]
	public async Task Lines_are_counted_for_messages()
	{
		RazorBlockScanner.LineOf("a\nb\r\nc\rd", 0).Should().Be(1);
		RazorBlockScanner.LineOf("a\nb\r\nc\rd", 2).Should().Be(2);
		RazorBlockScanner.LineOf("a\nb\r\nc\rd", 5).Should().Be(3);
		RazorBlockScanner.LineOf("a\nb\r\nc\rd", 7).Should().Be(4);
		await Task.CompletedTask;
	}
}
