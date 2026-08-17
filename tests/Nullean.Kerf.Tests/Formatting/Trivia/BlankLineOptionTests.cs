namespace Nullean.Kerf.Tests.Formatting.Trivia;

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
/// The defaults reproduce exactly what Kerf did before they existed — a cap of one and no minimum —
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
		// gap before the closing brace is not offered, because Kerf removes one there by default and
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
	public Task Namespaces_have_their_own_setting() => Formats(
		"""
		using System;
		namespace N
		{
		}
		""",
		"""
		using System;

		namespace N
		{
		}
		""",
		editorConfig: "csharp_blank_lines_around_namespace = 1");

	// ---- what is deliberately not offered ------------------------------------------------------------

	[Test]
	public Task A_wrapped_member_does_not_take_a_single_line_setting_because_there_is_none() => Formats(
		// ReSharper's blank_lines_around_single_line_* family is the one part of this category Kerf
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
