namespace Nullean.Curb.Tests.Formatting.Razor;

/// <summary>
/// The C# inside <c>@code</c> and <c>@functions</c> blocks, formatted as class members; every other byte
/// of the file left exactly as written.
/// </summary>
public class RazorFormattingTests : RazorFormattingTest
{
	[Test]
	public Task A_blazor_component_has_its_code_block_formatted_and_its_markup_left_alone() =>
		Formats(
			"""
			@page "/counter"
			@using   Microsoft.AspNetCore.Components
			<h1  class="title">Counter</h1>

			<p role="status">Current count: @currentCount</p>
			    <button class="btn btn-primary" @onclick="IncrementCount">Click me</button>

			@code {
			  private int currentCount=0;
			      [Parameter] public int Step { get; set; }

			  private void IncrementCount()
			  {
			    currentCount+=Step;
			  }
			}
			""",
			"""
			@page "/counter"
			@using   Microsoft.AspNetCore.Components
			<h1  class="title">Counter</h1>

			<p role="status">Current count: @currentCount</p>
			    <button class="btn btn-primary" @onclick="IncrementCount">Click me</button>

			@code {
			    private int currentCount = 0;
			    [Parameter]
			    public int Step { get; set; }

			    private void IncrementCount()
			    {
			        currentCount += Step;
			    }
			}
			""");

	[Test]
	public Task Functions_in_a_cshtml_view_is_formatted() =>
		Formats(
			"""
			@model IndexModel
			@{
			    ViewData["Title"]   =   "Home";
			}
			<div>@Greeting( "x" )</div>
			@functions {
			public string Greeting(string name)=>$"Hello {name}";
			}
			""",
			"""
			@model IndexModel
			@{
			    ViewData["Title"]   =   "Home";
			}
			<div>@Greeting( "x" )</div>
			@functions {
			    public string Greeting(string name) => $"Hello {name}";
			}
			""");

	[Test]
	public Task The_brace_on_the_next_line_is_a_block_too() =>
		Formats(
			"""
			<p>hi</p>
			@code
			{
			int x=1;
			}
			""",
			"""
			<p>hi</p>
			@code
			{
			    int x = 1;
			}
			""");

	[Test]
	public Task An_already_formatted_block_is_unchanged() =>
		Unchanged(
			"""
			<p>hi</p>

			@code {
			    private string? _name;

			    protected override void OnInitialized()
			    {
			        _name = "x";
			    }
			}

			""");

	[Test]
	public Task An_indented_block_is_indented_to_its_own_column() =>
		Formats(
			"""
			<div>
			    @code {
			    int x=1;
			    void M()
			    {
			    x++;
			    }
			    }
			</div>
			""",
			"""
			<div>
			    @code {
			        int x = 1;
			        void M()
			        {
			            x++;
			        }
			    }
			</div>
			""");

	[Test]
	public Task An_indented_block_keeps_the_lines_of_a_multi_line_string_as_written() =>
		Formats(
			""""
			<div>
			  @code {
			  string a = @"line one
			second line { not a brace }
			    third";
			  string b = """
			      raw {
			      """;
			  }
			</div>
			"""",
			""""
			<div>
			  @code {
			      string a = @"line one
			second line { not a brace }
			    third";
			      string b = """
			      raw {
			      """;
			  }
			</div>
			"""");

	[Test]
	public Task An_indented_block_keeps_a_block_comment_stable() =>
		Formats(
			"""
			<div>
			  @code {
			  /* one
			     two { */
			  int x;
			  }
			</div>
			""",
			"""
			<div>
			  @code {
			      /* one
			     two { */
			      int x;
			  }
			</div>
			""");

	[Test]
	public Task An_indented_block_keeps_an_inactive_branch_as_written() =>
		Formats(
			"""
			<div>
			  @code {
			#if NEVER
			  int   y;  {
			#endif
			  int x;
			  }
			</div>
			""",
			"""
			<div>
			  @code {
			#if NEVER
			  int   y;  {
			#endif
			      int x;
			  }
			</div>
			""");

	[Test]
	public Task Tabs_follow_indent_style() =>
		Formats(
			"<p/>\n\t@code {\n    int x=1;\n\t}\n",
			"<p/>\n\t@code {\n\t\tint x = 1;\n\t}\n",
			"indent_style = tab\ntab_width = 4\nindent_size = 4");

	/// <summary>The block's own leading whitespace is kept as written; only the indentation after it is the configured style.</summary>
	[Test]
	public Task A_tab_indented_block_keeps_its_tab_and_adds_spaces_when_indent_style_is_space() =>
		Formats(
			"<p/>\n\t@code {\n\tint x=1;\n\t}\n",
			"<p/>\n\t@code {\n\t    int x = 1;\n\t}\n",
			"indent_style = space\ntab_width = 4");

	[Test]
	public Task A_space_indented_block_keeps_its_spaces_and_adds_a_tab_when_indent_style_is_tab() =>
		Formats(
			"<div>\n  @code {\n  int x=1;\n  }\n</div>\n",
			"<div>\n  @code {\n  \tint x = 1;\n  }\n</div>\n",
			"indent_style = tab\ntab_width = 4\nindent_size = 4");

	[Test]
	public Task Crlf_files_keep_crlf_inside_the_block() =>
		Formats(
			"<p>a</p>\r\n@code {\r\nint x=1;\r\n\r\nvoid M(){x++;}\r\n}\r\n<p>b</p>\r\n",
			"<p>a</p>\r\n@code {\r\n    int x = 1;\r\n\r\n    void M() { x++; }\r\n}\r\n<p>b</p>\r\n");

	[Test]
	public Task Crlf_files_keep_crlf_even_when_end_of_line_says_lf() =>
		Formats(
			"<p>a</p>\r\n@code {\r\nint x=1;\r\n}\r\n",
			"<p>a</p>\r\n@code {\r\n    int x = 1;\r\n}\r\n",
			"end_of_line = lf");

	[Test]
	public Task Strings_holding_braces_do_not_end_the_block() =>
		Formats(
			"""""
			@code {
			string a="}";
			string b=@"
			}
			";
			string c=$"{a} }} {{ {(b.Length > 0 ? "}" : "{")}";
			string d=$$"""{{a}} } {""";
			string e="""
			    }
			    """;
			char f='}';
			}
			<p>after</p>
			""""",
			"""""
			@code {
			    string a = "}";
			    string b = @"
			}
			";
			    string c = $"{a} }} {{ {(b.Length > 0 ? "}" : "{")}";
			    string d = $$"""{{a}} } {""";
			    string e = """
			    }
			    """;
			    char f = '}';
			}
			<p>after</p>
			""""");

	[Test]
	public Task Comments_holding_braces_do_not_end_the_block() =>
		Formats(
			"""
			@code {
			// }
			/* }
			}
			*/
			/// <summary>}</summary>
			int x=1;
			}
			<p>after</p>
			""",
			"""
			@code {
			    // }
			    /* }
			}
			*/
			    /// <summary>}</summary>
			    int x = 1;
			}
			<p>after</p>
			""");

	[Test]
	public Task A_code_directive_inside_a_razor_comment_is_not_a_block() =>
		Formats(
			"""
			@*
			@code {
			int   commented;
			}
			*@
			@code {
			int x=1;
			}
			""",
			"""
			@*
			@code {
			int   commented;
			}
			*@
			@code {
			    int x = 1;
			}
			""");

	[Test]
	public Task A_code_directive_on_the_same_line_as_a_razor_comment_opening_is_not_a_block() =>
		Unchanged(
			"""
			@* @code {
			int   commented;
			} *@
			<p>x</p>
			""");

	[Test]
	public Task A_code_directive_inside_an_html_comment_is_not_a_block() =>
		Unchanged(
			"""
			<!--
			@code {
			int   commented;
			}
			-->
			<!-- @code {
			int   alsoCommented;
			} -->
			<p>x</p>
			""");

	[Test]
	public Task An_escaped_at_is_not_a_block() =>
		Unchanged(
			"""
			<pre>
			@@code {
			int   shown;
			}
			</pre>
			""");

	[Test]
	public Task A_longer_name_is_not_the_directive() =>
		Unchanged(
			"""
			@codeBehind {
			int   x;
			}
			@functionsX {
			int   y;
			}
			""");

	[Test]
	public Task A_block_on_one_line_is_not_a_candidate() =>
		Unchanged(
			"""
			@code { int   x; }
			<p>x</p>
			""");

	[Test]
	public Task A_block_shaped_line_in_a_verbatim_string_in_a_code_block_is_left_alone() =>
		Unchanged(
			"""
			@{
			    var s = @"
			@code {
			    int    x;
			}
			";
			}
			""");

	[Test]
	public Task A_block_shaped_line_in_a_raw_string_in_a_code_block_is_left_alone() =>
		Unchanged(
			""""
			@{
			    var s = """
			@code {
			    int    x;
			}
			""";
			}
			"""");

	[Test]
	public Task Braces_in_regular_strings_and_chars_in_a_code_block_do_not_end_it_early() =>
		Formats(
			"""
			@{
			    var s = "@code { }";
			    var t = "}";
			    var u = '}';
			}
			@code {
			int x=1;
			}
			""",
			"""
			@{
			    var s = "@code { }";
			    var t = "}";
			    var u = '}';
			}
			@code {
			    int x = 1;
			}
			""");

	[Test]
	public Task A_block_shaped_line_in_a_string_inside_if_is_left_alone() =>
		Unchanged(
			"""
			@if (show)
			{
			    var s = @"
			@code {
			    int    x;
			}
			";
			    <p>@s</p>
			}
			""");

	[Test]
	public Task A_block_shaped_line_in_a_string_inside_an_explicit_expression_is_left_alone() =>
		Unchanged(
			"""
			<p>@(@"
			@code {
			    int    x;
			}
			")</p>
			""");

	[Test]
	public Task A_block_shaped_line_inside_a_code_block_with_a_comment_on_its_brace_line_is_left_alone() =>
		Unchanged(
			"""
			@code { // members
			    string s = @"
			@code {
			    int    x;
			}
			";
			}
			""");

	[Test]
	public Task A_real_block_after_skipped_code_is_still_formatted() =>
		Formats(
			"""
			@{
			    var s = @"
			@code {
			    int    x;
			}
			";
			}
			@code {
			int y=1;
			}
			""",
			"""
			@{
			    var s = @"
			@code {
			    int    x;
			}
			";
			}
			@code {
			    int y = 1;
			}
			""");

	[Test]
	public Task Nothing_after_an_unclosed_code_block_is_formatted() =>
		Unchanged(
			"""
			@{
			    var s = 1;

			@code {
			int y=1;
			}
			""");

	[Test]
	public Task A_closing_brace_that_shares_its_line_is_refused() =>
		Rejects(
			"""
			@code {
			int   x;
			} <p>after</p>
			""",
			"its closing brace is not alone on its line");

	[Test]
	public Task A_closing_brace_after_code_on_its_line_is_refused() =>
		Rejects(
			"""
			@code {
			int   x;   }
			<p>after</p>
			""",
			"its closing brace is not alone on its line");

	[Test]
	public Task An_unclosed_block_is_refused() =>
		Rejects(
			"""
			@code {
			int   x;
			""",
			"no closing brace");

	[Test]
	public Task A_template_in_a_block_is_refused() =>
		Rejects(
			"""
			@code {
			private RenderFragment   Hello => @<p>Hello</p>;
			}
			""",
			"it does not parse as C# class members");

	[Test]
	public Task A_text_tag_in_a_block_is_refused() =>
		Rejects(
			"""
			@functions {
			void   Write()
			{
			<text>hello</text>
			}
			}
			""",
			"it does not parse as C# class members");

	[Test]
	public Task A_line_transition_in_a_block_is_refused() =>
		Rejects(
			"""
			@functions {
			void   Write()
			{
			@:hello
			}
			}
			""",
			"it does not parse as C# class members");

	[Test]
	public Task A_razor_comment_in_a_block_is_refused() =>
		Rejects(
			"""
			@code {
			@* don't { *@
			int   x;
			}
			""",
			"it does not parse as C# class members");

	[Test]
	public Task Statements_are_not_members_and_are_refused() =>
		Rejects(
			"""
			@code {
			x   = 1;
			}
			""",
			"it does not parse as C# class members");

	[Test]
	public Task One_refused_block_leaves_the_other_formatted() =>
		Formats(
			"""
			@code {
			RenderFragment   F => @<p>x</p>;
			}
			<p>between</p>
			@code {
			int x=1;
			}
			""",
			"""
			@code {
			RenderFragment   F => @<p>x</p>;
			}
			<p>between</p>
			@code {
			    int x = 1;
			}
			""",
			declined: 1);

	[Test]
	public Task Two_blocks_are_both_formatted() =>
		Formats(
			"""
			@code {
			int x=1;
			}
			<p>between</p>
			@functions {
			int y=2;
			}
			""",
			"""
			@code {
			    int x = 1;
			}
			<p>between</p>
			@functions {
			    int y = 2;
			}
			""");

	[Test]
	public Task An_empty_block_is_unchanged() =>
		Unchanged(
			"""
			<p>x</p>
			@code {
			}
			@code {

			}
			""");

	[Test]
	public Task A_file_without_blocks_is_unchanged() =>
		Unchanged(
			"""
			@page "/"
			<p>   x   </p>
			@{
			    var   y = 1;
			}
			@if (y > 0) {
			<p>y</p>
			}
			""");

	[Test]
	public Task Csharp_options_come_from_the_razor_files_settings() =>
		Formats(
			"""
			@code {
			void M() {
			if (true) { M(); }
			}
			}
			""",
			"""
			@code {
			  void M() {
			    if (true) { M(); }
			  }
			}
			""",
			"indent_size = 2\ncsharp_new_line_before_open_brace = none");

	[Test]
	public Task A_width_is_measured_from_the_blocks_column() =>
		Formats(
			"""
			<div>
			        @code {
			        void M(int first, int second, int third) { }
			        }
			</div>
			""",
			"""
			<div>
			        @code {
			            void M(
			                int first,
			                int second,
			                int third
			            )
			            { }
			        }
			</div>
			""",
			"max_line_length = 50");

	[Test]
	public Task A_file_header_template_is_never_inserted_into_a_block() =>
		Formats(
			"""
			@code {
			int x=1;
			}
			""",
			"""
			@code {
			    int x = 1;
			}
			""",
			"file_header_template = Copyright");

	[Test]
	public Task Preprocessor_directives_inside_a_block_are_kept() =>
		Formats(
			"""
			@code {
			#if DEBUG
			int x=1;
			#else
			int x=2;
			#endif
			#region Fields
			int y;
			#endregion
			}
			""",
			"""
			@code {
			#if DEBUG
			int x=1;
			#else
			    int x = 2;
			#endif

			    #region Fields

			    int y;

			    #endregion
			}
			""");
}
