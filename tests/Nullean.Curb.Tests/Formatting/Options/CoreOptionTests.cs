namespace Nullean.Curb.Tests.Formatting.Options;

/// <summary>
/// The eight core EditorConfig keys.
/// </summary>
/// <remarks>
/// The IDE0055 options are onboarded one at a time and each gains a file of its own alongside this
/// one — see <c>NewLineBeforeOpenBraceTests</c>. Those not yet reached are catalogued and reported
/// as unimplemented rather than silently ignored; see <c>OptionsBindingTests</c>.
/// </remarks>
public class CoreOptionTests : FormattingTest
{
	// ---- indent_style / indent_size ------------------------------------------------------------

	[Test]
	public Task Spaces_by_default_at_four_columns() => Formats(
		"""
		public class C
		{
		public int Value;
		}
		""",
		"""
		public class C
		{
		    public int Value;
		}
		""");

	[Test]
	public Task Indent_size_of_two() => Formats(
		"""
		public class C
		{
		public int Value;
		}
		""",
		"""
		public class C
		{
		  public int Value;
		}
		""",
		editorConfig: "indent_size = 2");

	[Test]
	public Task Indent_size_of_eight() => Formats(
		"""
		public class C
		{
		public int Value;
		}
		""",
		"""
		public class C
		{
		        public int Value;
		}
		""",
		editorConfig: "indent_size = 8");

	[Test]
	public Task Tabs() => Formats(
		"""
		public class C
		{
		public int Value;
		}
		""",
		"public class C\n{\n\tpublic int Value;\n}",
		editorConfig: "indent_style = tab");

	[Test]
	public Task Tabs_nest_one_per_level() => Formats(
		"""
		public class C
		{
		public void M()
		{
		Call();
		}
		}
		""",
		"public class C\n{\n\tpublic void M()\n\t{\n\t\tCall();\n\t}\n}",
		editorConfig: "indent_style = tab");

	[Test]
	public Task Spaces_replace_tabs_in_the_source() => Formats(
		"public class C\n{\n\tpublic int Value;\n}",
		"""
		public class C
		{
		    public int Value;
		}
		""",
		editorConfig: "indent_style = space");

	// ---- end_of_line ---------------------------------------------------------------------------

	[Test]
	public Task Line_feed_endings() => FormatsExactly(
		"public class C\n{\n}",
		"public class C\n{\n}\n",
		editorConfig: "end_of_line = lf");

	[Test]
	public Task Carriage_return_line_feed_endings() => FormatsExactly(
		"public class C\n{\n}",
		"public class C\r\n{\r\n}\r\n",
		editorConfig: "end_of_line = crlf");

	[Test]
	public Task Crlf_source_is_converted_to_lf() => FormatsExactly(
		"public class C\r\n{\r\n}",
		"public class C\n{\n}\n",
		editorConfig: "end_of_line = lf");

	[Test]
	public Task Unset_end_of_line_matches_the_platform() => FormatsExactly(
		"public class C\n{\n}",
		$"public class C{Environment.NewLine}{{{Environment.NewLine}}}{Environment.NewLine}",
		editorConfig: "end_of_line = auto");

	// ---- insert_final_newline ------------------------------------------------------------------

	[Test]
	public Task A_final_newline_is_added_by_default() => FormatsExactly(
		"public class C\n{\n}",
		"public class C\n{\n}\n");

	[Test]
	public Task A_final_newline_can_be_suppressed() => FormatsExactly(
		"public class C\n{\n}\n",
		"public class C\n{\n}",
		editorConfig: "insert_final_newline = false");

	[Test]
	public Task Several_trailing_newlines_collapse_to_one() => FormatsExactly(
		"public class C\n{\n}\n\n\n",
		"public class C\n{\n}\n");

	// ---- trim_trailing_whitespace ---------------------------------------------------------------

	[Test]
	public Task Trailing_whitespace_is_removed() => FormatsExactly(
		"public class C   \n{   \n}   \n",
		"public class C\n{\n}\n");

	[Test]
	public Task Trailing_whitespace_on_a_blank_line_is_removed() => FormatsExactly(
		"public class C\n{\n    \n    public int Value;\n}\n",
		"public class C\n{\n\n    public int Value;\n}\n");

	// ---- max_line_length -------------------------------------------------------------------------

	[Test]
	public Task Reflow_is_off_by_default() => Unchanged(
		"""
		public class C
		{
		    public void M()
		    {
		        Call(alphaArgument, betaArgument, gammaArgument, deltaArgument, epsilon);
		    }
		}
		""");

	[Test]
	public Task Max_line_length_off_is_explicit_too() => Unchanged(
		"""
		public class C
		{
		    public void M()
		    {
		        Call(alphaArgument, betaArgument, gammaArgument, deltaArgument, epsilon);
		    }
		}
		""",
		editorConfig: "max_line_length = off");

	[Test]
	public Task A_narrow_width_breaks_an_argument_list() => Formats(
		"""
		public class C
		{
		    public void M()
		    {
		        Call(alpha, beta, gamma);
		    }
		}
		""",
		"""
		public class C
		{
		    public void M()
		    {
		        Call(
		            alpha,
		            beta,
		            gamma
		        );
		    }
		}
		""",
		editorConfig: "max_line_length = 30");

	[Test]
	public Task A_generous_width_leaves_it_alone() => Unchanged(
		"""
		public class C
		{
		    public void M()
		    {
		        Call(alpha, beta, gamma);
		    }
		}
		""",
		editorConfig: "max_line_length = 200");

	[Test]
	public Task Width_counts_the_indent() => Formats(
		"""
		public class C
		{
		    public void M()
		    {
		        Call(alpha, beta);
		    }
		}
		""",
		"""
		public class C
		{
		    public void M()
		    {
		        Call(
		            alpha,
		            beta
		        );
		    }
		}
		""",
		editorConfig: "max_line_length = 24");

	[Test]
	public Task Several_options_together() => Formats(
		"""
		public class C
		{
		public void M()
		{
		Call(alpha, beta, gamma);
		}
		}
		""",
		"public class C\n{\n\tpublic void M()\n\t{\n\t\tCall(\n\t\t\talpha,\n\t\t\tbeta,\n\t\t\tgamma\n\t\t);\n\t}\n}",
		editorConfig: """
		indent_style = tab
		max_line_length = 24
		insert_final_newline = false
		""");
}
