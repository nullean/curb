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
