namespace Nullean.Curb.Tests.Formatting.Options;

/// <summary>
/// <c>csharp_preserve_single_line_blocks</c> and <c>csharp_preserve_single_line_statements</c>.
/// </summary>
/// <remarks>
/// <para>
/// These are the options that make Curb's output depend on its input, and both are on by default
/// because that is what dotnet format does. They beat every other layout option: a preserved body
/// keeps its brace on the header line whatever <c>csharp_new_line_before_open_brace</c> says, and
/// is not reflowed whatever <c>max_line_length</c> says.
/// </para>
/// <para>
/// The split between them is dotnet format's rather than one Curb would have chosen. Blocks covers
/// member, type, namespace and enum bodies; statements covers a control-flow body sharing its
/// header's line, two statements separated by a semicolon, and a statement on its case label's
/// line. A switch needs both, since a one-line switch necessarily has its statement on the label's
/// line.
/// </para>
/// <para>
/// Curb remains idempotent: running it twice changes nothing, which every test here asserts for
/// free. It is deliberately not canonicalising, which is correct IDE0055 behaviour.
/// </para>
/// </remarks>
public class PreserveSingleLineTests : FormattingTest
{
	// ---- what preserving keeps --------------------------------------------------------------------

	[Test]
	public Task An_empty_body_written_on_one_line_stays_there() => Unchanged(
		"""
		public class C
		{
		    public void Empty() { }
		}
		""");

	[Test]
	public Task A_one_line_body_with_a_statement_stays_there() => Unchanged(
		"""
		public class C
		{
		    public int Q { get { return 1; } }
		}
		""");

	[Test]
	public Task Control_blocks_written_on_one_line_stay_there() => Unchanged(
		"""
		public class C
		{
		    public void M()
		    {
		        if (a) { return; }
		        while (a) { Call(); }
		        lock (g) { Call(); }
		    }
		}
		""");

	[Test]
	public Task A_type_a_namespace_and_an_enum_can_all_be_preserved() => Unchanged(
		"""
		namespace N { }
		public class C { }
		public enum E { A }
		""");

	[Test]
	public Task A_switch_written_on_one_line_stays_there() => Unchanged(
		"""
		public class C
		{
		    public void M()
		    {
		        switch (a) { case 1: break; }
		    }
		}
		""");

	[Test]
	public Task A_local_function_and_a_lambda_can_be_preserved() => Unchanged(
		"""
		public class C
		{
		    public void M()
		    {
		        void Inner() { }
		        Action a = () => { };
		    }
		}
		""");

	// ---- what preserving does not reach -----------------------------------------------------------

	[Test]
	public Task A_body_the_author_broke_stays_broken() => Unchanged(
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
	public Task Only_the_body_written_on_one_line_is_kept() => Unchanged(
		"""
		public class C
		{
		    public void Empty() { }

		    public void M()
		    {
		        Call();
		    }
		}
		""");

	// ---- preserving beats the other layout options ------------------------------------------------

	[Test]
	public Task Preserving_beats_the_brace_placement_option() => Unchanged(
		"""
		public class C
		{
		    public void Empty() { }
		}
		""",
		editorConfig: "csharp_new_line_before_open_brace = all");

	/// <summary>
	/// A preserved body beats the width — but only in preservation mode, which a width no longer selects.
	/// </summary>
	/// <remarks>
	/// <c>max_line_length</c> now chooses deterministic layout as well as the column, so this test has to
	/// ask for preservation explicitly. That is not a workaround: the two settings together are precisely
	/// the configuration this option is about, and the pairing is what a reader needs to see.
	/// </remarks>
	[Test]
	public Task Preserving_beats_the_line_width() => Unchanged(
		"""
		public class C
		{
		    public void M() { CallSomethingWithAVeryLongNameIndeed(); }
		}
		""",
		editorConfig: "max_line_length = 40\ncsharp_keep_existing_linebreaks = true");

	/// <summary>Without that opt-out, the width wins and the body opens out.</summary>
	[Test]
	public Task Deterministic_layout_expands_it_instead() => Formats(
		"""
		public class C
		{
		    public void M() { CallSomethingWithAVeryLongNameIndeed(); }
		}
		""",
		"""
		public class C
		{
		    public void M()
		    {
		        CallSomethingWithAVeryLongNameIndeed();
		    }
		}
		""",
		editorConfig: "max_line_length = 40");

	// ---- turning it off ---------------------------------------------------------------------------

	[Test]
	public Task Disabling_it_expands_every_body() => Formats(
		"""
		public class C
		{
		    public void Empty() { }

		    public int Q { get { return 1; } }
		}
		""",
		"""
		public class C
		{
		    public void Empty()
		    {
		    }

		    public int Q
		    {
		        get
		        {
		            return 1;
		        }
		    }
		}
		""",
		editorConfig: "csharp_preserve_single_line_blocks = false");

	[Test]
	public Task Disabling_it_expands_a_type_body() => Formats(
		"""
		public class C { }
		public enum E { A }
		""",
		"""
		public class C
		{
		}
		public enum E
		{
		    A
		}
		""",
		editorConfig: "csharp_preserve_single_line_blocks = false");

	[Test]
	public Task With_it_off_the_brace_option_applies_again() => Formats(
		"""
		public class C
		{
		    public void Empty() { }
		}
		""",
		"""
		public class C {
		    public void Empty() {
		    }
		}
		""",
		editorConfig: """
		csharp_preserve_single_line_blocks = false
		csharp_new_line_before_open_brace = none
		""");

	// ---- csharp_preserve_single_line_statements ----------------------------------------------------

	[Test]
	public Task A_statement_on_its_header_line_stays_there() => Unchanged(
		"""
		public class C
		{
		    public void M()
		    {
		        if (a) return;
		        for (var i = 0; i < 1; i++) Call();
		    }
		}
		""");

	[Test]
	public Task Two_statements_on_one_line_stay_on_one_line() => Unchanged(
		"""
		public class C
		{
		    public void M()
		    {
		        int y = 1; int z = 2;
		    }
		}
		""");

	[Test]
	public Task A_case_statement_on_its_label_line_stays_there() => Unchanged(
		"""
		public class C
		{
		    public void M()
		    {
		        switch (a) { case 1: break; }
		    }
		}
		""");

	[Test]
	public Task A_whole_try_written_on_one_line_stays_there() => Unchanged(
		"""
		public class C
		{
		    public void M()
		    {
		        try { Call(); } catch { }
		    }
		}
		""");

	[Test]
	public Task A_catch_after_a_broken_try_still_takes_its_own_line() => Formats(
		"""
		public class C
		{
		    public void M()
		    {
		        try
		        {
		        } catch (Exception e)
		        {
		        }
		    }
		}
		""",
		// An empty try always collapses regardless of this option — Curb's one unconditional opinion,
		// see StatementTests' "an empty try, catch or finally is always { }". What this case actually
		// asserts is that catch still takes its own line rather than joining try's now-collapsed brace.
		"""
		public class C
		{
		    public void M()
		    {
		        try { }
		        catch (Exception e) { }
		    }
		}
		""");

	[Test]
	public Task Disabling_it_moves_a_body_off_its_header_line() => Formats(
		"""
		public class C
		{
		    public void M()
		    {
		        if (a) return;
		        int y = 1; int z = 2;
		    }
		}
		""",
		"""
		public class C
		{
		    public void M()
		    {
		        if (a)
		            return;
		        int y = 1;
		        int z = 2;
		    }
		}
		""",
		editorConfig: "csharp_preserve_single_line_statements = false");

	[Test]
	public Task A_braced_body_moves_off_the_header_but_keeps_its_braces_collapsed() => Formats(
		"""
		public class C
		{
		    public void M()
		    {
		        if (a) { return; }
		    }
		}
		""",
		// Where the body goes is this option's decision; whether the braces collapse is the block
		// option's, and that is still on.
		"""
		public class C
		{
		    public void M()
		    {
		        if (a)
		        { return; }
		    }
		}
		""",
		editorConfig: "csharp_preserve_single_line_statements = false");

	[Test]
	public Task A_one_line_switch_needs_both_options() => Formats(
		"""
		public class C
		{
		    public void M()
		    {
		        switch (a) { case 1: break; }
		    }
		}
		""",
		// A switch on one line necessarily has its statement on the label's line, which is this
		// option's business, so turning it off expands the whole thing.
		"""
		public class C
		{
		    public void M()
		    {
		        switch (a)
		        {
		            case 1:
		                break;
		        }
		    }
		}
		""",
		editorConfig: "csharp_preserve_single_line_statements = false");

	/// <summary>
	/// An empty catch is Curb's one unconditional opinion in this family: <c>{ }</c> whatever these
	/// options say. See <see href="https://github.com/nullean/curb/issues/25">issue #25</see> and
	/// <c>Printers.Statements.JoinsClauseToBlock</c> for why.
	/// </summary>
	[Test]
	public Task An_empty_catch_stays_collapsed_when_everything_else_expands() => Formats(
		"""
		public class C
		{
		    public void M()
		    {
		        try { Call(); } catch { }
		    }
		}
		""",
		"""
		public class C
		{
		    public void M()
		    {
		        try
		        {
		            Call();
		        }
		        catch { }
		    }
		}
		""",
		editorConfig: """
		csharp_preserve_single_line_blocks = false
		csharp_preserve_single_line_statements = false
		""");

	// ---- the options are what make output input-dependent -------------------------------------------

	[Test]
	public Task Two_bodies_written_differently_stay_different() => Unchanged(
		"""
		public class C
		{
		    public void A() { }

		    public void B()
		    {
		    }
		}
		""");
}
