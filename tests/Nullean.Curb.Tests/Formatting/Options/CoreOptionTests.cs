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
		// No editorConfig: space is already the default, so this documents default behaviour rather
		// than the option — an explicit `indent_style = space` here would prove nothing beyond what
		// leaving it unset already does.
		"public class C\n{\n\tpublic int Value;\n}",
		"""
		public class C
		{
		    public int Value;
		}
		""");

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
		"public class C\n{\n    public void M()\n    {\n        Call();\n    \n        Call();\n    }\n}\n",
		"public class C\n{\n    public void M()\n    {\n        Call();\n\n        Call();\n    }\n}\n");

	// `trim_trailing_whitespace = false` has no case here: it is bound correctly (see
	// OptionsBindingTests) but every scenario tried — a plain line, and inside the verbatim-preserved
	// `csharp_space_around_declaration_statements = ignore` region, where trailing whitespace has
	// nowhere else to come from — still has it stripped. DocPrinter.cs's DocKind.Trim case (~line 181)
	// calls _output.TrimTrailingWhitespace() unconditionally, unlike the other three call sites in that
	// file, which correctly gate on _trimTrailingWhitespace. Filed as a real gap rather than worked
	// around: writing a case that asserts the current behaviour would bake the bug into a golden
	// expectation, which is exactly what this suite's own convention says not to do. See
	// build/conformance-divergences.json / checkOptionCoverage's exclusion list in Targets.fs.

	// ---- tab_width -------------------------------------------------------------------------------

	[Test]
	public Task Tab_width_counts_toward_the_line_length() => Formats(
		// Two tabs at the call site: 16 columns at tab_width = 8, past a width of 30 once the call
		// itself is counted — the same source stays on one line at the default tab_width = 4, where
		// two tabs are only 8 columns.
		"""
		public class C
		{
		public void M()
		{
		Call(alpha, beta);
		}
		}
		""",
		"public class C\n{\n\tpublic void M()\n\t{\n\t\tCall(\n\t\t\talpha,\n\t\t\tbeta\n\t\t);\n\t}\n}",
		editorConfig: "indent_style = tab\nmax_line_length = 30\ntab_width = 8");

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
