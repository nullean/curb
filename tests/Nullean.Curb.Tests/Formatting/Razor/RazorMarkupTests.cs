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

	private const string Attributes =
		"""
		<div>
		<button class="btn" type="button"
		@onclick="Go"
		  disabled="@off">
		Go
		</button>
		<input type="text"
		value="@x" />
		<MyComponent
		A="1"
		B="2" />
		<span>x</span><a href="y"
		title="z">y</a>
		<p
		>text</p>
		</div>
		""";

	[Test]
	public Task Continued_attributes_align_with_the_first_one() =>
		Formats(
			Attributes,
			"""
			<div>
			    <button class="btn" type="button"
			            @onclick="Go"
			            disabled="@off">
			        Go
			    </button>
			    <input type="text"
			           value="@x" />
			    <MyComponent
			        A="1"
			        B="2" />
			    <span>x</span><a href="y"
			                     title="z">y</a>
			    <p
			    >text</p>
			</div>
			""",
			markup: RazorMarkup.Indent);

	[Test]
	public Task Engine_quirks_align_attributes_with_a_tag_that_has_none_on_its_line() =>
		Formats(
			Attributes,
			"""
			<div>
			    <button class="btn" type="button"
			            @onclick="Go"
			            disabled="@off">
			        Go
			    </button>
			    <input type="text"
			           value="@x" />
			    <MyComponent
			    A="1"
			    B="2" />
			    <span>x</span><a href="y"
			                     title="z">y</a>
			    <p
			    >text</p>
			</div>
			""",
			markup: RazorMarkup.EngineQuirks);

	[Test]
	public Task Indent_by_one_puts_continued_attributes_a_level_in_from_the_tags_line() =>
		Formats(
			Attributes,
			"""
			<div>
			    <button class="btn" type="button"
			        @onclick="Go"
			        disabled="@off">
			        Go
			    </button>
			    <input type="text"
			        value="@x" />
			    <MyComponent
			        A="1"
			        B="2" />
			    <span>x</span><a href="y"
			        title="z">y</a>
			    <p
			    >text</p>
			</div>
			""",
			editorConfig: "razor_attribute_indent_style = indent_by_one",
			markup: RazorMarkup.Indent);

	[Test]
	public Task Indent_by_two_puts_continued_attributes_two_levels_in() =>
		Formats(
			Attributes,
			"""
			<div>
			    <button class="btn" type="button"
			            @onclick="Go"
			            disabled="@off">
			        Go
			    </button>
			    <input type="text"
			            value="@x" />
			    <MyComponent
			            A="1"
			            B="2" />
			    <span>x</span><a href="y"
			            title="z">y</a>
			    <p
			    >text</p>
			</div>
			""",
			editorConfig: "razor_attribute_indent_style = indent_by_two",
			markup: RazorMarkup.Indent);

	[Test]
	public Task Csharp_in_a_value_moves_with_its_tag_and_text_in_a_value_is_kept() =>
		Formats(
			"""
			<div>
			<button @onclick="@(() => Show("a",
			             "b"))"
			   class="x">y</button>
			<p title="a
			   b c"
			   id="q">z</p>
			<input @bind="x" @bind:event="oninput"
			       @bind:after="Done"
			       />
			</div>
			""",
			"""
			<div>
			    <button @onclick="@(() => Show("a",
			                 "b"))"
			            class="x">y</button>
			    <p title="a
			   b c"
			       id="q">z</p>
			    <input @bind="x" @bind:event="oninput"
			           @bind:after="Done"
			    />
			</div>
			""",
			markup: RazorMarkup.Indent);

	[Test]
	public Task An_explicit_expression_in_any_attributes_value_moves_with_its_tag() =>
		Formats(
			"""
			<div>
			<p class="@(Busy
			              ? "busy" : "idle")">x</p>
			</div>
			""",
			"""
			<div>
			    <p class="@(Busy
			                  ? "busy" : "idle")">x</p>
			</div>
			""",
			markup: RazorMarkup.Indent);

	[Test]
	public Task Csharp_in_a_directive_attributes_value_moves_with_its_tag() =>
		Formats(
			"""
			<div>
			<button @onclick="() => Show(1,
			                  2)"
			   class="x">y</button>
			</div>
			""",
			"""
			<div>
			    <button @onclick="() => Show(1,
			                      2)"
			            class="x">y</button>
			</div>
			""",
			markup: RazorMarkup.Indent);

	[Test]
	public Task A_tab_indented_tag_is_measured_by_tab_width_when_its_csharp_moves() =>
		Formats(
			"<div>\n\t\t<button @onclick=\"@(() => Show(1,\n\t\t\t\t2))\">y</button>\n</div>\n",
			"<div>\n    <button @onclick=\"@(() => Show(1,\n            2))\">y</button>\n</div>\n",
			editorConfig: "tab_width = 4",
			markup: RazorMarkup.Indent);

	[Test]
	public Task Aligned_attributes_use_tabs_then_spaces() =>
		Formats(
			"<div>\n<button class=\"btn\" type=\"button\"\n@onclick=\"Go\">\nGo\n</button>\n<input @bind=\"x\"\n@bind:after=\"Done\" />\n</div>\n",
			"<div>\n\t<button class=\"btn\" type=\"button\"\n\t\t\t@onclick=\"Go\">\n\t\tGo\n\t</button>\n\t<input @bind=\"x\"\n\t\t   @bind:after=\"Done\" />\n</div>\n",
			editorConfig: "indent_style = tab\ntab_width = 4",
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
