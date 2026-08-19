namespace Nullean.Curb.Tests.Formatting.Options;

/// <summary>
/// <c>csharp_new_line_before_open_brace</c> — which of twelve constructs put their opening brace on
/// a line of its own.
/// </summary>
/// <remarks>
/// <para>
/// The default is <c>all</c> (Allman), matching Roslyn, so the tests that pass no editorConfig are
/// asserting the default. <c>none</c> is K&amp;R, and any comma-separated subset mixes the two.
/// </para>
/// <para>
/// Six of the twelve constructs — accessors, properties, anonymous types and
/// object/collection/array initializers — brace on a <em>soft</em> break: the brace only moves to
/// its own line once the construct no longer fits. With reflow off, which is the default, those
/// stay on one line whatever this option says, so their tests set a <c>max_line_length</c> narrow
/// enough to force the break. That is a real difference from <c>dotnet format</c>, which has no
/// width concept and always breaks them.
/// </para>
/// </remarks>
public class NewLineBeforeOpenBraceTests : FormattingTest
{
	// ---- all (the default) ---------------------------------------------------------------------

	[Test]
	public Task Allman_is_the_default() => Formats(
		"""
		public class C {
		    public void M() {
		        if (true) {
		        }
		    }
		}
		""",
		"""
		public class C
		{
		    public void M()
		    {
		        if (true)
		        {
		        }
		    }
		}
		""");

	[Test]
	public Task All_is_the_same_as_the_default() => Unchanged(
		"""
		public class C
		{
		    public void M()
		    {
		    }
		}
		""",
		editorConfig: "csharp_new_line_before_open_brace = all");

	// ---- none ----------------------------------------------------------------------------------

	[Test]
	public Task None_puts_every_brace_on_the_line_that_introduces_it() => Formats(
		"""
		public class C
		{
		    public void M()
		    {
		        if (true)
		        {
		        }
		    }
		}
		""",
		"""
		public class C {
		    public void M() {
		        if (true) {
		        }
		    }
		}
		""",
		editorConfig: "csharp_new_line_before_open_brace = none");

	[Test]
	public Task None_leaves_closing_braces_where_they_are() => Formats(
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
		public class C {
		    public void M() {
		        Call();
		    }
		}
		""",
		editorConfig: "csharp_new_line_before_open_brace = none");

	// ---- types -----------------------------------------------------------------------------------

	[Test]
	public Task Types_alone_braces_only_the_type() => Formats(
		"""
		public class C {
		    public void M() {
		    }
		}
		""",
		"""
		public class C
		{
		    public void M() {
		    }
		}
		""",
		editorConfig: "csharp_new_line_before_open_brace = types");

	[Test]
	public Task Types_covers_struct_interface_record_and_enum() => Formats(
		"""
		public struct S {
		}
		public interface I {
		}
		public record R {
		}
		public enum E {
		    A,
		}
		""",
		"""
		public struct S
		{
		}
		public interface I
		{
		}
		public record R
		{
		}
		public enum E
		{
		    A,
		}
		""",
		editorConfig: "csharp_new_line_before_open_brace = types");

	[Test]
	public Task Types_covers_a_block_namespace() => Formats(
		"""
		namespace N {
		    public class C {
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
		editorConfig: "csharp_new_line_before_open_brace = types");

	[Test]
	public Task Types_excluded_keeps_the_type_brace_up() => Formats(
		"""
		public class C
		{
		    public void M()
		    {
		    }
		}
		""",
		"""
		public class C {
		    public void M()
		    {
		    }
		}
		""",
		editorConfig: "csharp_new_line_before_open_brace = methods");

	// ---- methods ---------------------------------------------------------------------------------

	[Test]
	public Task Methods_covers_constructors_and_operators() => Formats(
		"""
		public class C
		{
		    public C()
		    {
		    }

		    public void M()
		    {
		    }

		    public static C operator +(C left, C right)
		    {
		        return left;
		    }
		}
		""",
		"""
		public class C {
		    public C() {
		    }

		    public void M() {
		    }

		    public static C operator +(C left, C right) {
		        return left;
		    }
		}
		""",
		editorConfig: "csharp_new_line_before_open_brace = none");

	[Test]
	public Task Methods_does_not_touch_an_expression_body() => Unchanged(
		"""
		public class C {
		    public int M() => 1;
		}
		""",
		editorConfig: "csharp_new_line_before_open_brace = none");

	// ---- control_blocks --------------------------------------------------------------------------

	[Test]
	public Task Control_blocks_excluded_pulls_if_else_and_loop_braces_up() => Formats(
		"""
		public class C
		{
		    public void M()
		    {
		        if (a)
		        {
		        }
		        else
		        {
		        }

		        while (a)
		        {
		        }

		        foreach (var x in items)
		        {
		        }

		        for (var i = 0; i < 1; i++)
		        {
		        }
		    }
		}
		""",
		"""
		public class C
		{
		    public void M()
		    {
		        if (a) {
		        }
		        else {
		        }

		        while (a) {
		        }

		        foreach (var x in items) {
		        }

		        for (var i = 0; i < 1; i++) {
		        }
		    }
		}
		""",
		editorConfig: "csharp_new_line_before_open_brace = types,methods");

	[Test]
	public Task Control_blocks_excluded_pulls_try_catch_finally_braces_up() => Formats(
		"""
		public class C
		{
		    public void M()
		    {
		        try
		        {
		        }
		        catch (Exception e)
		        {
		        }
		        finally
		        {
		        }
		    }
		}
		""",
		"""
		public class C
		{
		    public void M()
		    {
		        try {
		        }
		        catch (Exception e) {
		        }
		        finally {
		        }
		    }
		}
		""",
		editorConfig: "csharp_new_line_before_open_brace = types,methods");

	[Test]
	public Task Control_blocks_excluded_pulls_switch_using_and_lock_braces_up() => Formats(
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

		        using (var s = Open())
		        {
		        }

		        lock (gate)
		        {
		        }
		    }
		}
		""",
		"""
		public class C
		{
		    public void M()
		    {
		        switch (a) {
		            case 1:
		                break;
		        }

		        using (var s = Open()) {
		        }

		        lock (gate) {
		        }
		    }
		}
		""",
		editorConfig: "csharp_new_line_before_open_brace = types,methods");

	[Test]
	public Task Control_blocks_leaves_a_bare_embedded_statement_alone() => Unchanged(
		"""
		public class C
		{
		    public void M()
		    {
		        if (a)
		            Call();
		    }
		}
		""",
		editorConfig: "csharp_new_line_before_open_brace = types,methods");

	// ---- local_functions -------------------------------------------------------------------------

	[Test]
	public Task Local_functions_follow_the_methods_flag() => Formats(
		// Not the `local_functions` flag, despite its name. Measured against dotnet format, which the
		// fixed-point property makes authoritative: `methods` alone moves a local function's brace and
		// `local_functions` alone moves nothing. This asserted the documented behaviour and cost a
		// fixed point for anyone setting `methods` — the same resolution as indexers and events.
		"""
		public class C
		{
		    public void M() {
		        void Inner() {
		        }
		    }
		}
		""",
		"""
		public class C
		{
		    public void M()
		    {
		        void Inner()
		        {
		        }
		    }
		}
		""",
		editorConfig: "csharp_new_line_before_open_brace = types,methods");

	// ---- lambdas ---------------------------------------------------------------------------------

	[Test]
	public Task Lambdas_brace_on_their_own_line_by_default() => Formats(
		"""
		public class C
		{
		    public void M()
		    {
		        Action a = () => {
		        };
		    }
		}
		""",
		"""
		public class C
		{
		    public void M()
		    {
		        Action a = () =>
		        {
		        };
		    }
		}
		""");

	[Test]
	public Task Lambdas_excluded_keeps_the_brace_after_the_arrow() => Formats(
		"""
		public class C
		{
		    public void M()
		    {
		        Action a = () =>
		        {
		        };
		        Action<int> b = x =>
		        {
		        };
		    }
		}
		""",
		"""
		public class C
		{
		    public void M()
		    {
		        Action a = () => {
		        };
		        Action<int> b = x => {
		        };
		    }
		}
		""",
		editorConfig: "csharp_new_line_before_open_brace = types,methods,control_blocks");

	// ---- anonymous_methods -----------------------------------------------------------------------

	[Test]
	public Task Anonymous_methods_brace_on_their_own_line_by_default() => Formats(
		"""
		public class C
		{
		    public void M()
		    {
		        Action a = delegate {
		        };
		    }
		}
		""",
		"""
		public class C
		{
		    public void M()
		    {
		        Action a = delegate
		        {
		        };
		    }
		}
		""");

	[Test]
	public Task Anonymous_methods_excluded_keeps_the_brace_after_delegate() => Formats(
		"""
		public class C
		{
		    public void M()
		    {
		        Action<int> a = delegate (int x)
		        {
		        };
		    }
		}
		""",
		"""
		public class C
		{
		    public void M()
		    {
		        Action<int> a = delegate (int x) {
		        };
		    }
		}
		""",
		editorConfig: "csharp_new_line_before_open_brace = types,methods,control_blocks");

	// ---- accessors and properties ----------------------------------------------------------------
	//
	// These brace on a soft break, so max_line_length is what makes the option observable.

	[Test]
	public Task Auto_properties_stay_on_one_line_when_they_fit() => Unchanged(
		"""
		public class C
		{
		    public int Value { get; set; }
		}
		""");

	[Test]
	public Task Properties_break_their_brace_onto_its_own_line() => Formats(
		"""
		public class C
		{
		    public int LongPropertyName { get; set; }
		}
		""",
		"""
		public class C
		{
		    public int LongPropertyName
		    {
		        get;
		        set;
		    }
		}
		""",
		editorConfig: "max_line_length = 30");

	[Test]
	public Task Properties_excluded_keeps_the_brace_on_the_declaration() => Formats(
		"""
		public class C
		{
		    public int LongPropertyName { get; set; }
		}
		""",
		"""
		public class C
		{
		    public int LongPropertyName {
		        get;
		        set;
		    }
		}
		""",
		editorConfig: """
		max_line_length = 30
		csharp_new_line_before_open_brace = types,methods,control_blocks,accessors
		""");

	[Test]
	public Task Accessors_with_bodies_brace_on_their_own_line() => Formats(
		"""
		public class C
		{
		    public int Value
		    {
		        get {
		            return 1;
		        }
		    }
		}
		""",
		"""
		public class C
		{
		    public int Value
		    {
		        get
		        {
		            return 1;
		        }
		    }
		}
		""",
		editorConfig: "max_line_length = 30");

	[Test]
	public Task Accessors_excluded_keeps_the_body_brace_up() => Formats(
		"""
		public class C
		{
		    public int Value
		    {
		        get
		        {
		            return 1;
		        }
		    }
		}
		""",
		"""
		public class C
		{
		    public int Value
		    {
		        get {
		            return 1;
		        }
		    }
		}
		""",
		editorConfig: """
		max_line_length = 30
		csharp_new_line_before_open_brace = types,methods,control_blocks,properties
		""");

	// ---- indexers --------------------------------------------------------------------------------

	[Test]
	public Task An_indexer_answers_to_the_properties_flag() => Formats(
		// Not what the option's documentation implies, and not what this test used to assert. Measured
		// against dotnet format, which the fixed-point property makes authoritative: with `properties`
		// set and `indexers` absent it moves the brace, and with `indexers` set and `properties`
		// absent it does not. So `indexers` governs nothing, and reading it made Curb's output stop
		// being a fixed point for anyone who set `properties` alone.
		"""
		public class C
		{
		    public int this[int index] { get; set; }
		}
		""",
		"""
		public class C
		{
		    public int this[int index]
		    {
		        get;
		        set;
		    }
		}
		""",
		editorConfig: """
		max_line_length = 30
		csharp_new_line_before_open_brace = types,properties
		""");

	// ---- events ----------------------------------------------------------------------------------

	[Test]
	public Task An_event_accessor_list_answers_to_the_properties_flag_too() => Formats(
		"""
		public class C
		{
		    public event EventHandler Changed { add { } remove { } }
		}
		""",
		// The accessor bodies were written on one line, so csharp_preserve_single_line_blocks
		// keeps them there; only the list itself breaks.
		"""
		public class C
		{
		    public event EventHandler Changed
		    {
		        add { }
		        remove { }
		    }
		}
		""",
		editorConfig: """
		max_line_length = 40
		csharp_new_line_before_open_brace = types,properties
		""");

	[Test]
	public Task Field_like_events_have_no_braces_to_place() => Formats(
		"""
		public class C
		{
		    public event EventHandler Changed;
		}
		""",
		"""
		public class C {
		    public event EventHandler Changed;
		}
		""",
		editorConfig: "csharp_new_line_before_open_brace = none");

	// ---- object, collection and array initializers -----------------------------------------------

	[Test]
	public Task Initializers_break_their_brace_onto_its_own_line() => Formats(
		"""
		public class C
		{
		    private readonly Point _p = new Point { X = 1, Y = 2 };
		}
		""",
		"""
		public class C
		{
		    private readonly Point _p = new Point
		    {
		        X = 1,
		        Y = 2
		    };
		}
		""",
		editorConfig: "max_line_length = 40");

	[Test]
	public Task Initializers_excluded_keeps_the_brace_on_the_constructor() => Formats(
		"""
		public class C
		{
		    private readonly Point _p = new Point { X = 1, Y = 2 };
		}
		""",
		"""
		public class C
		{
		    private readonly Point _p = new Point {
		        X = 1,
		        Y = 2
		    };
		}
		""",
		editorConfig: """
		max_line_length = 40
		csharp_new_line_before_open_brace = types
		""");

	// ---- anonymous types -------------------------------------------------------------------------

	[Test]
	public Task Anonymous_types_excluded_keeps_the_brace_after_new() => Formats(
		"""
		public class C
		{
		    public void M()
		    {
		        var value = new { First = 1, Second = 2 };
		    }
		}
		""",
		"""
		public class C
		{
		    public void M()
		    {
		        var value = new {
		            First = 1,
		            Second = 2
		        };
		    }
		}
		""",
		editorConfig: """
		max_line_length = 40
		csharp_new_line_before_open_brace = types,methods
		""");

	// ---- parsing ---------------------------------------------------------------------------------

	[Test]
	public Task Values_are_order_and_whitespace_insensitive() => Formats(
		"""
		public class C
		{
		    public void M()
		    {
		    }
		}
		""",
		"""
		public class C {
		    public void M()
		    {
		    }
		}
		""",
		editorConfig: "csharp_new_line_before_open_brace =   methods ,  control_blocks  ");

	[Test]
	public Task An_unrecognised_value_falls_back_to_the_default() => Unchanged(
		"""
		public class C
		{
		    public void M()
		    {
		    }
		}
		""",
		editorConfig: "csharp_new_line_before_open_brace = allman");
}
