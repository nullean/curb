using Nullean.Curb.Razor;

namespace Nullean.Curb.Tests.Formatting.Razor;

/// <summary>
/// Markup re-indented by its nesting under <c>curb_razor_format_markup</c>, as Microsoft's Razor formatter does it
/// (issue #88). Every expectation is checked against that engine by <c>./build.sh verifyrazorexpectations</c>.
/// </summary>
public class RazorMarkupTests : RazorFormattingTest
{
	[Test]
	public Task Elements_and_components_nest_and_void_elements_do_not() =>
		Formats(
			"""
			@page "/probe"
			<div>
			<section class="a">
			<h1>Title</h1>
			<p>
			some text
			  more text
			</p>
			<img src="x.png">
			<br />
			<MyComponent Value="1">
			<ChildContent>
			<span>x</span>
			</ChildContent>
			</MyComponent>
			</section>
			</div>
			""",
			"""
			@page "/probe"
			<div>
			    <section class="a">
			        <h1>Title</h1>
			        <p>
			            some text
			            more text
			        </p>
			        <img src="x.png">
			        <br />
			        <MyComponent Value="1">
			            <ChildContent>
			                <span>x</span>
			            </ChildContent>
			        </MyComponent>
			    </section>
			</div>
			""",
			markup: RazorMarkup.Indent);

	[Test]
	public Task Code_blocks_nest_like_elements() =>
		Formats(
			"""
			<ul>
			@if (show)
			{
			<li>shown</li>
			@foreach (var i in items)
			{
			var label = i.ToString();
			<li>@label</li>
			}
			}
			else
			{
			<li>hidden</li>
			}
			@* a comment *@
			</ul>
			""",
			"""
			<ul>
			    @if (show)
			    {
			        <li>shown</li>
			        @foreach (var i in items)
			        {
			            var label = i.ToString();
			            <li>@label</li>
			        }
			    }
			    else
			    {
			        <li>hidden</li>
			    }
			    @* a comment *@
			</ul>
			""",
			markup: RazorMarkup.Indent);

	[Test]
	public Task Html_does_not_indent_its_head_and_body() =>
		Formats(
			"""
			<!DOCTYPE html>
			<html lang="en">
			<head>
			<meta charset="utf-8" />
			<title>x</title>
			</head>
			<body>
			<div>
			<p>a</p>
			</div>
			</body>
			</html>
			""",
			"""
			<!DOCTYPE html>
			<html lang="en">
			<head>
			    <meta charset="utf-8" />
			    <title>x</title>
			</head>
			<body>
			    <div>
			        <p>a</p>
			    </div>
			</body>
			</html>
			""",
			markup: RazorMarkup.Indent);

	[Test]
	public Task An_element_opened_mid_line_does_not_indent_the_line_it_continues_on() =>
		Formats(
			"""
			<p>
			Some text <a href="https://example.com/a/very/long/link">a link
			that wraps</a>.
			</p>
			""",
			"""
			<p>
			    Some text <a href="https://example.com/a/very/long/link">a link
			    that wraps</a>.
			</p>
			""",
			markup: RazorMarkup.Indent);

	[Test]
	public Task Script_style_and_pre_content_is_left_as_written() =>
		Formats(
			"""
			<div>
			<script>
			function f() {
			return 1;
			}
			</script>
			<pre>
			  keep
			    this
			</pre>
			</div>
			""",
			"""
			<div>
			    <script>
			function f() {
			return 1;
			}
			    </script>
			    <pre>
			  keep
			    this
			</pre>
			</div>
			""",
			markup: RazorMarkup.Indent);

	[Test]
	public Task An_indented_code_block_moves_to_its_nesting_and_its_csharp_follows() =>
		Formats(
			"""
			<h1>Counter</h1>
			    @code {
			private int count=0;
			    }
			""",
			"""
			<h1>Counter</h1>
			@code {
			    private int count = 0;
			}
			""",
			editorConfig: "",
			markup: RazorMarkup.Indent);

	[Test]
	public Task A_directive_with_a_generic_type_is_not_a_tag() =>
		Formats(
			"""
			@inject UserManager<ApplicationUser> Users
			<div>
			<p>@Users</p>
			</div>
			""",
			"""
			@inject UserManager<ApplicationUser> Users
			<div>
			    <p>@Users</p>
			</div>
			""",
			markup: RazorMarkup.Indent);

	[Test]
	public Task Markup_follows_indent_style() =>
		Formats(
			"<div>\n<p>x</p>\n</div>\n",
			"<div>\n\t<p>x</p>\n</div>\n",
			editorConfig: "indent_style = tab",
			markup: RazorMarkup.Indent);

	[Test]
	public Task Engine_quirks_indent_by_tab_width_rather_than_indent_size() =>
		Formats(
			"<div>\n<p>x</p>\n</div>\n",
			"<div>\n    <p>x</p>\n</div>\n",
			editorConfig: "indent_size = 2\ntab_width = 4",
			markup: RazorMarkup.EngineQuirks);

	[Test]
	public Task By_default_markup_indents_by_indent_size() =>
		Formats(
			"<div>\n<p>x</p>\n</div>\n",
			"<div>\n  <p>x</p>\n</div>\n",
			editorConfig: "indent_size = 2\ntab_width = 4",
			markup: RazorMarkup.Indent);

	[Test]
	public Task A_closing_tag_that_closes_nothing_leaves_the_markup_as_written() =>
		Rejects(
			"""
			<div>
			    <p>x</p>
			</span>
			""",
			"markup left as written, </span> does not close the element it is in",
			markup: RazorMarkup.Indent);

	[Test]
	public Task An_element_left_open_leaves_the_markup_as_written() =>
		Rejects(
			"""
			<div>
			  <p>x</p>
			""",
			"markup left as written, <div> is never closed",
			markup: RazorMarkup.Indent);

	[Test]
	public Task Without_the_key_markup_is_left_as_written() =>
		Unchanged(
			"""
			<div>
			<p>x</p>
			</div>
			""");
}
