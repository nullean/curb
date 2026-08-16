namespace Nullean.Kerf.Tests.Formatting.Options;

/// <summary>
/// <c>csharp_preserve_single_line_blocks</c>.
/// </summary>
/// <remarks>
/// <para>
/// This is the option that makes Kerf's output depend on its input, and it is on by default because
/// that is what dotnet format does. It beats both <c>csharp_new_line_before_open_brace</c> — a
/// preserved body keeps its brace on the header line — and <c>max_line_length</c>, since a
/// preserved block is not reflowed.
/// </para>
/// <para>
/// Kerf remains idempotent: running it twice changes nothing, which every test here asserts for
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

	[Test]
	public Task Preserving_beats_the_line_width() => Unchanged(
		"""
		public class C
		{
		    public void M() { CallSomethingWithAVeryLongNameIndeed(); }
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

	// ---- the option is what makes output input-dependent -------------------------------------------

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
