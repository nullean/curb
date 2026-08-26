namespace Nullean.Curb.Tests.Formatting.Trivia;

/// <summary>
/// ReSharper's blank-line family.
/// </summary>
/// <remarks>
/// <para>
/// Free ground in a way almost nothing else is: <c>dotnet format</c> adds no blank line, removes
/// none and collapses none — measured in both directions, on a file carrying three consecutive
/// blanks and one carrying none. So every setting here is a fixed point whatever it is set to, which
/// is why the whole family could be taken at once where the wrapping keys had to be argued one at a
/// time.
/// </para>
/// <para>
/// The defaults reproduce exactly what Curb did before they existed — a cap of one and no minimum —
/// so no repository moves by adopting a version that has them.
/// </para>
/// </remarks>
public class BlankLineOptionTests : FormattingTest
{
	private const string Source = """
		public class C
		{
		    private int _a;
		    private int _b;



		    public void M()
		    {
		        Call();


		        Call();
		    }
		    public void N()
		    {
		    }
		}
		""";

	// ---- the defaults -----------------------------------------------------------------------------

	[Test]
	public Task A_run_of_blank_lines_collapses_to_one_by_default() => Formats(
		Source,
		"""
		public class C
		{
		    private int _a;
		    private int _b;

		    public void M()
		    {
		        Call();

		        Call();
		    }
		    public void N()
		    {
		    }
		}
		""");

	// ---- keeping more, or fewer -------------------------------------------------------------------

	[Test]
	public Task Declarations_can_keep_two() => Formats(
		Source,
		"""
		public class C
		{
		    private int _a;
		    private int _b;


		    public void M()
		    {
		        Call();

		        Call();
		    }
		    public void N()
		    {
		    }
		}
		""",
		editorConfig: "csharp_keep_blank_lines_in_declarations = 2");

	[Test]
	public Task Code_can_keep_none() => Formats(
		Source,
		"""
		public class C
		{
		    private int _a;
		    private int _b;

		    public void M()
		    {
		        Call();
		        Call();
		    }
		    public void N()
		    {
		    }
		}
		""",
		editorConfig: "csharp_keep_blank_lines_in_code = 0");

	// ---- asking for lines the author did not write --------------------------------------------------

	[Test]
	public Task Methods_can_be_given_a_line_of_air() => Formats(
		// `M()` and `N()` sit against each other in the source; this is the setting that parts them.
		Source,
		"""
		public class C
		{
		    private int _a;
		    private int _b;

		    public void M()
		    {
		        Call();

		        Call();
		    }

		    public void N()
		    {
		    }
		}
		""",
		editorConfig: "csharp_blank_lines_around_invocable = 1");

	[Test]
	public Task A_minimum_never_exceeds_the_cap() => Formats(
		// The two compose in the obvious order: the author's count is raised to the minimum and then
		// capped, so asking for two around methods while keeping only one gets one.
		Source,
		"""
		public class C
		{
		    private int _a;
		    private int _b;

		    public void M()
		    {
		        Call();

		        Call();
		    }

		    public void N()
		    {
		    }
		}
		""",
		editorConfig: "csharp_blank_lines_around_invocable = 2\ncsharp_keep_blank_lines_in_declarations = 1");

	[Test]
	public Task Fields_types_and_properties_have_their_own_settings() => Formats(
		"""
		public class C
		{
		    private int _a;
		    private int _b;
		    public int P { get; set; }
		}
		""",
		"""
		public class C
		{
		    private int _a;

		    private int _b;

		    public int P { get; set; }
		}
		""",
		editorConfig: "csharp_blank_lines_around_field = 1\ncsharp_blank_lines_around_property = 1");

	[Test]
	public Task A_type_can_be_given_air_under_its_opening_brace() => Formats(
		// `blank_lines_inside_type` governs only the gap between the brace and the first member; the
		// gap before the closing brace is not offered, because Curb removes one there by default and
		// dotnet format leaves that alone.
		"""
		public class C
		{
		    private int _a;
		}
		""",
		"""
		public class C
		{

		    private int _a;
		}
		""",
		editorConfig: "csharp_blank_lines_inside_type = 1");

	[Test]
	public Task Types_have_their_own_setting() => Unchanged(
		// The default is already one, so proving the key is bound means proving it can be turned off
		// rather than on: without the key, a bare 0-gap source gets the default's forced blank line
		// (see A_type_can_be_given_air_under_its_opening_brace's sibling tests for that default), but
		// with it set to 0 there is no minimum to raise the author's own (also zero) count to.
		"""
		public class A
		{
		}
		public class B
		{
		}
		""",
		editorConfig: "csharp_blank_lines_around_type = 0");

	[Test]
	public Task Using_lists_have_their_own_setting() => Unchanged(
		// The default is already one (see BlankLineTests), so proving the key is bound means proving
		// it can be turned off — the same shape as Types_have_their_own_setting above.
		"""
		using System;
		namespace N
		{
		}
		""",
		editorConfig: "csharp_blank_lines_after_using_list = 0");

	[Test]
	public Task A_block_scoped_namespace_can_be_given_air_under_its_opening_brace() => Formats(
		// csharp_blank_lines_inside_namespace is BlankLinesInsideType's analogue for a block-scoped
		// namespace body — see PrintNamespaceBody.
		"""
		namespace N
		{
		    public class C
		    {
		    }
		}
		""",
		"""
		namespace N
		{

		    public class C
		    {
		    }
		}
		""",
		editorConfig: "csharp_blank_lines_inside_namespace = 1");

	[Test]
	public Task Regions_can_be_given_less_air_than_the_default() => Formats(
		// csharp_blank_lines_around_region/inside_region both default to one and are forced exactly
		// (see RegionTests); this proves both are bound by turning them off together.
		"""
		public class C
		{
		    #region Values

		    public int Value;

		    #endregion
		}
		public class D
		{
		}
		""",
		"""
		public class C
		{
		    #region Values
		    public int Value;
		    #endregion
		}

		public class D
		{
		}
		""",
		editorConfig: """
		csharp_blank_lines_around_region = 0
		csharp_blank_lines_inside_region = 0
		""");

	[Test]
	public Task Block_statements_can_be_given_air_in_front_too() => Formats(
		// csharp_blank_lines_before_block_statements defaults to zero; this proves it is bound by
		// asking for air before a plain if with nothing else demanding one.
		"""
		public class C
		{
		    public void M()
		    {
		        Call();
		        if (a)
		        {
		            Call2();
		        }
		    }
		}
		""",
		"""
		public class C
		{
		    public void M()
		    {
		        Call();

		        if (a)
		        {
		            Call2();
		        }
		    }
		}
		""",
		editorConfig: "csharp_blank_lines_before_block_statements = 1");

	[Test]
	public Task Block_statements_have_their_own_setting() => Unchanged(
		// The default is already one (see ExperimentalBlankLineTests' IDE2003 case), so proving the
		// key is bound means proving it can be turned off — the same shape as Types_have_their_own_
		// setting above.
		"""
		public class C
		{
		    public void M()
		    {
		        if (a)
		        {
		            Call2();
		        }
		        Call3();
		    }
		}
		""",
		editorConfig: "csharp_blank_lines_after_block_statements = 0");

	[Test]
	public Task A_comment_aligned_under_a_trailing_one_is_not_pushed_apart_by_the_air_after_it() => Unchanged(
		// csharp_blank_lines_after_block_statements defaults to one, and forced a blank line directly
		// in front of the aligned comment before this test existed — invisible to
		// TokenPrinter.AlignsUnderTrailingComment's own trivia walk on the run that forced it (the
		// walk only starts counting once it begins, after the blank line already went out through a
		// separate call), but real literal source text by the very next run, which then read the two
		// comments as belonging to separate runs and dropped the alignment — an idempotency bug the
		// corpus caught, not a hand-written case, since it needs a real author-aligned comment.
		"""
		public class C
		{
		    public void M(bool branch)
		    {
		        if (branch)
		            Call1(); // detached
		                     // ref file
		        if (branch)
		            Call2();
		    }
		}
		""");

	[Test]
	public Task Control_transfer_statements_have_their_own_setting() => Formats(
		// Both default to zero, so proving they are bound means proving they can add air rather than
		// take it away. Unreachable() is never actually reached — that is exactly why the "after" key
		// defaults to zero — but the option does not know that, and correctness here is about the
		// option being wired up, not about the code making sense.
		"""
		public class C
		{
		    public void M()
		    {
		        Call();
		        return;
		        Unreachable();
		    }
		}
		""",
		"""
		public class C
		{
		    public void M()
		    {
		        Call();

		        return;

		        Unreachable();
		    }
		}
		""",
		editorConfig: """
		csharp_blank_lines_before_control_transfer_statements = 1
		csharp_blank_lines_after_control_transfer_statements = 1
		""");

	[Test]
	public Task Switch_sections_have_their_own_setting() => Formats(
		// csharp_blank_lines_before_case is the gap between sections (never between stacked labels of
		// the same one); csharp_blank_lines_after_case is the gap from a section's labels to its first
		// statement only, not statement-to-statement separation within the section (Second() staying
		// flush against First() below proves that half).
		"""
		public class C
		{
		    public void M(int x)
		    {
		        switch (x)
		        {
		            case 1:
		                First();
		                Second();
		                break;
		            case 2:
		                Third();
		                break;
		        }
		    }
		}
		""",
		"""
		public class C
		{
		    public void M(int x)
		    {
		        switch (x)
		        {
		            case 1:

		                First();
		                Second();
		                break;

		            case 2:

		                Third();
		                break;
		        }
		    }
		}
		""",
		editorConfig: """
		csharp_blank_lines_before_case = 1
		csharp_blank_lines_after_case = 1
		""");

	[Test]
	public Task A_single_line_comment_can_be_given_air_in_front() => Formats(
		// Defaults to zero, matching jb. `//` specifically — a doc comment or /* */ block is not what
		// the key is documented to reach.
		"""
		public class C
		{
		    public void M()
		    {
		        Call1();
		        // a comment
		        Call2();
		    }
		}
		""",
		"""
		public class C
		{
		    public void M()
		    {
		        Call1();

		        // a comment
		        Call2();
		    }
		}
		""",
		editorConfig: "csharp_blank_lines_before_single_line_comment = 1");

	[Test]
	public Task Namespaces_have_their_own_setting() => Formats(
		// Two adjacent namespaces rather than a using directive above one: csharp_blank_lines_after_
		// using_list now defaults to one of its own (see BlankLineTests), which would otherwise force
		// the same blank line and leave this case proving nothing about the option under test.
		"""
		namespace N
		{
		}
		namespace M
		{
		}
		""",
		"""
		namespace N
		{
		}

		namespace M
		{
		}
		""",
		editorConfig: "csharp_blank_lines_around_namespace = 1");

	// ---- removing blank lines directly under an opening brace ---------------------------------------

	[Test]
	public Task A_blank_line_under_a_block_s_opening_brace_is_removed_by_default() => Formats(
		// csharp_remove_blank_lines_near_braces_in_code defaults to true, matching jb: a blank line an
		// author left directly under `{` reads as accidental rather than deliberate.
		"""
		public class C
		{
		    public void M()
		    {

		        Call();
		    }
		}
		""",
		"""
		public class C
		{
		    public void M()
		    {
		        Call();
		    }
		}
		""");

	[Test]
	public Task A_block_can_keep_its_blank_line_under_the_opening_brace() => Unchanged(
		"""
		public class C
		{
		    public void M()
		    {

		        Call();
		    }
		}
		""",
		editorConfig: "csharp_remove_blank_lines_near_braces_in_code = false");

	[Test]
	public Task A_type_can_keep_its_blank_line_under_the_opening_brace() => Unchanged(
		"""
		public class C
		{

		    public void M()
		    {
		        Call();
		    }
		}
		""",
		editorConfig: "csharp_remove_blank_lines_near_braces_in_declarations = false");

	// ---- what is deliberately not offered ------------------------------------------------------------

	[Test]
	public Task A_wrapped_member_does_not_take_a_single_line_setting_because_there_is_none() => Formats(
		// ReSharper's blank_lines_around_single_line_* family is the one part of this category Curb
		// does not implement, and it is worth an assertion rather than only a comment.
		//
		// "Single line" would have to be read from the source, and reflow moves it: `M` below is one
		// line in the source and two in the output, so a second run would see a different member than
		// the first and give it a different number of blank lines. Two corpus files grew a line per
		// run. Every member therefore takes the ordinary setting, whatever the source looked like.
		"""
		public class C
		{
		    public int M() => Something(aaaaaaaaaaaaaaaaaaaaaa, bbbbbbbbbbbbbbbbbbbbbb, cccccccccccccccccccc);
		    public int N() => 1;
		}
		""",
		"""
		public class C
		{
		    public int M() =>
		        Something(aaaaaaaaaaaaaaaaaaaaaa, bbbbbbbbbbbbbbbbbbbbbb, cccccccccccccccccccc);

		    public int N() => 1;
		}
		""",
		editorConfig: "csharp_blank_lines_around_invocable = 1\nmax_line_length = 100");

	// ---- the file-scoped namespace ------------------------------------------------------------------

	[Test]
	public Task The_line_under_a_file_scoped_namespace_is_configurable() => Formats(
		// One by default, because that is what dotnet format writes there. This is the same setting,
		// asked for explicitly.
		"""
		namespace N;
		public class C
		{
		}
		""",
		"""
		namespace N;


		public class C
		{
		}
		""",
		editorConfig: "csharp_blank_lines_after_file_scoped_namespace_directive = 2\ncsharp_keep_blank_lines_in_declarations = 2");

	// ---- what it refuses ----------------------------------------------------------------------------

	[Test]
	public Task An_absurd_count_is_refused_rather_than_honoured() => Unchanged(
		// A file is not improved by two hundred blank lines, and a typo in a config should not be able
		// to ask for them. Out of range is a diagnostic and the default, like any other bad value.
		"""
		public class C
		{
		    private int _a;
		    private int _b;
		}
		""",
		editorConfig: "csharp_blank_lines_around_field = 200");
}
