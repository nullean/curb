namespace Nullean.Curb.Tests.Formatting.Options;

/// <summary>
/// The <c>csharp_space_*</c> options that control a single well-defined position.
/// </summary>
/// <remarks>
/// Expectations here were cross-checked against <c>dotnet format whitespace</c> rather than against
/// Curb's own output, which is the only way a conformance claim means anything. The comma, square
/// bracket and parenthesis keys are onboarded separately.
/// </remarks>
public class SpacingTests : FormattingTest
{
	// ---- csharp_space_after_cast -----------------------------------------------------------------

	[Test]
	public Task A_cast_hugs_its_operand_by_default() => Formats(
		"""
		public class C
		{
		    public void M()
		    {
		        var x = (int) value;
		    }
		}
		""",
		"""
		public class C
		{
		    public void M()
		    {
		        var x = (int)value;
		    }
		}
		""");

	[Test]
	public Task A_cast_takes_a_space_when_asked() => Formats(
		"""
		public class C
		{
		    public void M()
		    {
		        var x = (int)value;
		        var y = (List<string>)other;
		    }
		}
		""",
		"""
		public class C
		{
		    public void M()
		    {
		        var x = (int) value;
		        var y = (List<string>) other;
		    }
		}
		""",
		editorConfig: "csharp_space_after_cast = true");

	// ---- csharp_space_after_keywords_in_control_flow_statements ----------------------------------

	[Test]
	public Task A_control_flow_keyword_takes_a_space_by_default() => Unchanged(
		"""
		public class C
		{
		    public void M()
		    {
		        if (a)
		        {
		        }
		    }
		}
		""");

	[Test]
	public Task Control_flow_keywords_hug_their_parenthesis_when_disabled() => Formats(
		"""
		public class C
		{
		    public void M()
		    {
		        if (a)
		        {
		        }
		        while (a)
		        {
		        }
		        foreach (var i in items)
		        {
		        }
		        for (var i = 0; i < 10; i++)
		        {
		        }
		        switch (a)
		        {
		        }
		        lock (gate)
		        {
		        }
		        using (var s = Open())
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
		        if(a)
		        {
		        }
		        while(a)
		        {
		        }
		        foreach(var i in items)
		        {
		        }
		        for(var i = 0; i < 10; i++)
		        {
		        }
		        switch(a)
		        {
		        }
		        lock(gate)
		        {
		        }
		        using(var s = Open())
		        {
		        }
		    }
		}
		""",
		editorConfig: "csharp_space_after_keywords_in_control_flow_statements = false");

	[Test]
	public Task The_while_of_a_do_loop_counts_as_a_control_flow_keyword() => Formats(
		"""
		public class C
		{
		    public void M()
		    {
		        do
		        {
		        }
		        while (a);
		    }
		}
		""",
		"""
		public class C
		{
		    public void M()
		    {
		        do
		        {
		        }
		        while(a);
		    }
		}
		""",
		editorConfig: "csharp_space_after_keywords_in_control_flow_statements = false");

	[Test]
	public Task Catch_and_its_when_filter_also_count() => Formats(
		"""
		public class C
		{
		    public void M()
		    {
		        try
		        {
		        }
		        catch (Exception e) when (a)
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
		        try { }
		        catch(Exception e) when(a) { }
		    }
		}
		""",
		editorConfig: "csharp_space_after_keywords_in_control_flow_statements = false");

	[Test]
	public Task Fixed_counts_too() => Formats(
		"""
		public class C
		{
		    public unsafe void M()
		    {
		        fixed (int* p = &x)
		        {
		        }
		    }
		}
		""",
		"""
		public class C
		{
		    public unsafe void M()
		    {
		        fixed(int* p = &x)
		        {
		        }
		    }
		}
		""",
		editorConfig: "csharp_space_after_keywords_in_control_flow_statements = false");

	// ---- inheritance clause colon ----------------------------------------------------------------

	[Test]
	public Task An_inheritance_colon_takes_spaces_on_both_sides_by_default() => Unchanged(
		"""
		public class C : Base, IThing
		{
		}
		""");

	[Test]
	public Task The_inheritance_colon_can_lose_both_its_spaces() => Formats(
		"""
		public class C : Base, IThing
		{
		}
		""",
		"""
		public class C:Base, IThing
		{
		}
		""",
		editorConfig: """
		csharp_space_before_colon_in_inheritance_clause = false
		csharp_space_after_colon_in_inheritance_clause = false
		""");

	[Test]
	public Task The_two_inheritance_colon_options_are_independent() => Formats(
		"""
		public class C : Base
		{
		}
		""",
		"""
		public class C: Base
		{
		}
		""",
		editorConfig: "csharp_space_before_colon_in_inheritance_clause = false");

	[Test]
	public Task A_struct_and_an_interface_use_the_same_colon_options() => Formats(
		"""
		public struct S : IThing
		{
		}
		public interface I : IOther
		{
		}
		""",
		"""
		public struct S:IThing
		{
		}

		public interface I:IOther
		{
		}
		""",
		editorConfig: """
		csharp_space_before_colon_in_inheritance_clause = false
		csharp_space_after_colon_in_inheritance_clause = false
		""");

	// ---- csharp_space_around_binary_operators -----------------------------------------------------

	[Test]
	public Task Binary_operators_take_spaces_by_default() => Unchanged(
		"""
		public class C
		{
		    public void M()
		    {
		        var y = 1 + 2 * 3;
		        var z = a && b || c;
		    }
		}
		""");

	[Test]
	public Task Binary_operators_lose_their_spaces_under_none() => Formats(
		"""
		public class C
		{
		    public void M()
		    {
		        var y = 1 + 2 * 3;
		        var z = a && b || c;
		        var r = a ?? b;
		    }
		}
		""",
		"""
		public class C
		{
		    public void M()
		    {
		        var y = 1+2*3;
		        var z = a&&b||c;
		        var r = a??b;
		    }
		}
		""",
		editorConfig: "csharp_space_around_binary_operators = none");

	[Test]
	public Task None_reaches_assignment_but_not_a_declarator() => Formats(
		"""
		public class C
		{
		    public void M()
		    {
		        var y = 1;
		        y = y + 1;
		        y += 1;
		    }
		}
		""",
		"""
		public class C
		{
		    public void M()
		    {
		        var y = 1;
		        y=y+1;
		        y+=1;
		    }
		}
		""",
		editorConfig: "csharp_space_around_binary_operators = none");

	[Test]
	public Task None_leaves_the_conditional_operator_and_is_alone() => Formats(
		"""
		public class C
		{
		    public void M()
		    {
		        var q = a is int n ? n : 0;
		    }
		}
		""",
		"""
		public class C
		{
		    public void M()
		    {
		        var q = a is int n ? n : 0;
		    }
		}
		""",
		editorConfig: "csharp_space_around_binary_operators = none");

	[Test]
	public Task Ignore_is_reported_rather_than_guessed_at() => Unchanged(
		"""
		public class C
		{
		    public void M()
		    {
		        var y = 1 + 2;
		    }
		}
		""",
		editorConfig: "csharp_space_around_binary_operators = ignore");

	// ---- csharp_space_before_dot / _after_dot -----------------------------------------------------

	[Test]
	public Task A_dot_hugs_both_sides_by_default() => Unchanged(
		"""
		public class C
		{
		    public void M()
		    {
		        Console.WriteLine(x);
		    }
		}
		""");

	[Test]
	public Task A_dot_can_take_a_space_on_either_side() => Formats(
		"""
		public class C
		{
		    public void M()
		    {
		        Console.WriteLine(x);
		    }
		}
		""",
		"""
		public class C
		{
		    public void M()
		    {
		        Console . WriteLine(x);
		    }
		}
		""",
		editorConfig: """
		csharp_space_before_dot = true
		csharp_space_after_dot = true
		""");

	[Test]
	public Task The_two_dot_options_are_independent() => Formats(
		"""
		public class C
		{
		    public void M()
		    {
		        Console.WriteLine(x);
		    }
		}
		""",
		"""
		public class C
		{
		    public void M()
		    {
		        Console. WriteLine(x);
		    }
		}
		""",
		editorConfig: "csharp_space_after_dot = true");

	// ---- for statement semicolons ----------------------------------------------------------------

	[Test]
	public Task For_semicolons_take_a_trailing_space_by_default() => Unchanged(
		"""
		public class C
		{
		    public void M()
		    {
		        for (var i = 0; i < 10; i++)
		        {
		        }
		    }
		}
		""");

	[Test]
	public Task For_semicolons_can_swap_which_side_the_space_is_on() => Formats(
		"""
		public class C
		{
		    public void M()
		    {
		        for (var i = 0; i < 10; i++)
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
		        for (var i = 0 ;i < 10 ;i++)
		        {
		        }
		    }
		}
		""",
		editorConfig: """
		csharp_space_before_semicolon_in_for_statement = true
		csharp_space_after_semicolon_in_for_statement = false
		""");

	[Test]
	public Task An_empty_for_header_still_gets_its_semicolon_spacing() => Formats(
		"""
		public class C
		{
		    public void M()
		    {
		        for (; ; )
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
		        for (;;)
		        {
		        }
		    }
		}
		""",
		editorConfig: "csharp_space_after_semicolon_in_for_statement = false");

	/// <summary>
	/// A space before a semicolon belongs to the clause in front of it, so an empty clause neither
	/// takes one nor doubles the space after the previous semicolon. See
	/// <see href="https://github.com/nullean/curb/issues/133">issue #133</see>.
	/// </summary>
	[Test]
	public Task An_empty_for_clause_takes_no_space_before_its_semicolon() => Formats(
		"""
		public class C
		{
		    public void M(bool a, int i)
		    {
		        for (;;) { }
		        for (; a;) { }
		        for (var j = 0; ; j++) { }
		        for (var j = 0; j < 3;) { }
		        for (; ; i++) { }
		        for (var j = 0;;) { }
		    }
		}
		""",
		"""
		public class C
		{
		    public void M(bool a, int i)
		    {
		        for ( ; ; ) { }
		        for (; a ;) { }
		        for (var j = 0 ; ; j++) { }
		        for (var j = 0 ; j < 3 ;) { }
		        for (; ; i++) { }
		        for (var j = 0 ; ;) { }
		    }
		}
		""",
		editorConfig: "csharp_space_before_semicolon_in_for_statement = true");

	[Test]
	public Task An_empty_for_clause_with_the_space_on_the_other_side() => Formats(
		"""
		public class C
		{
		    public void M(bool a, int i)
		    {
		        for (; a;) { }
		        for (var j = 0; ; j++) { }
		        for (; ; i++) { }
		        for (; i < 3; i++) { }
		        for (var j = 0;;) { }
		    }
		}
		""",
		"""
		public class C
		{
		    public void M(bool a, int i)
		    {
		        for (;a ;) { }
		        for (var j = 0 ;;j++) { }
		        for (;;i++) { }
		        for (;i < 3 ;i++) { }
		        for (var j = 0 ;;) { }
		    }
		}
		""",
		editorConfig: """
		csharp_space_before_semicolon_in_for_statement = true
		csharp_space_after_semicolon_in_for_statement = false
		""");

	/// <summary>
	/// Roslyn's rule for a header with no clauses spaces whatever follows its <c>)</c> as though it
	/// came after one of the semicolons, so without that space <c>{ }</c> on the header's line is
	/// glued to it. Matched rather than recorded as a divergence: it is what IDE0055 asks for, and
	/// <c>for (;;) { }</c> would be reported as a violation in the editor.
	/// </summary>
	[Test]
	public Task An_empty_for_header_hugs_a_body_on_its_line_without_the_space() => Formats(
		"""
		public class C
		{
		    public void M(int i)
		    {
		        for (; ; ) { }
		        for (; ; ) i++;
		        for (; ; ) ;
		        for (; ; )
		        { }
		    }
		}
		""",
		"""
		public class C
		{
		    public void M(int i)
		    {
		        for (;;){ }
		        for (;;)i++;
		        for (;;);
		        for (;;)
		        { }
		    }
		}
		""",
		editorConfig: "csharp_space_after_semicolon_in_for_statement = false");

	[Test]
	public Task An_empty_for_header_with_both_spacing_options_flipped() => Formats(
		"""
		public class C
		{
		    public void M()
		    {
		        for (; ; ) { }
		        for (; ; ) ;
		    }
		}
		""",
		"""
		public class C
		{
		    public void M()
		    {
		        for ( ; ;){ }
		        for ( ; ;) ;
		    }
		}
		""",
		editorConfig: """
		csharp_space_before_semicolon_in_for_statement = true
		csharp_space_after_semicolon_in_for_statement = false
		""");

	[Test]
	public Task An_empty_for_header_hugs_an_end_of_line_brace_without_the_space() => Formats(
		"""
		public class C {
		    public void M() {
		        for (; ; ) {
		            Call();
		        }
		    }
		}
		""",
		"""
		public class C {
		    public void M() {
		        for (;;){
		            Call();
		        }
		    }
		}
		""",
		editorConfig: """
		csharp_new_line_before_open_brace = none
		csharp_space_after_semicolon_in_for_statement = false
		""");

	// ---- csharp_space_after_unary_operator --------------------------------------------------------

	[Test]
	public Task Unary_minus_plus_and_not_take_a_space_when_asked() => Formats(
		"""
		public class C
		{
		    public void M(int x)
		    {
		        int a = -x;
		        int b = +x;
		        bool c = !true;
		    }
		}
		""",
		"""
		public class C
		{
		    public void M(int x)
		    {
		        int a = - x;
		        int b = + x;
		        bool c = ! true;
		    }
		}
		""",
		editorConfig: "csharp_space_after_unary_operator = true");

	[Test]
	public Task Bitwise_complement_and_prefix_increment_decrement_are_not_reached() => Unchanged(
		// Measured directly against jb: ~x, ++x and --x stay glued to their operand even with the key
		// on — only unary minus/plus, logical not, and the unsafe pointer prefix operators respond.
		"""
		public class C
		{
		    public void M(int x)
		    {
		        int a = ~x;
		        ++x;
		        --x;
		    }
		}
		""",
		editorConfig: "csharp_space_after_unary_operator = true");

	[Test]
	public Task The_unsafe_pointer_prefix_operators_take_a_space_too() => Formats(
		"""
		public class C
		{
		    public unsafe void M(int x, int* p)
		    {
		        int q = *p;
		        int* r = &x;
		    }
		}
		""",
		"""
		public class C
		{
		    public unsafe void M(int x, int* p)
		    {
		        int q = * p;
		        int* r = & x;
		    }
		}
		""",
		editorConfig: "csharp_space_after_unary_operator = true");

	// ---- csharp_space_around_ternary_operator -----------------------------------------------------

	[Test]
	public Task The_ternary_operator_can_lose_its_surrounding_space() => Formats(
		"""
		public class C
		{
		    public int M(int x)
		    {
		        return x > 0 ? x : 0;
		    }
		}
		""",
		"""
		public class C
		{
		    public int M(int x)
		    {
		        return x > 0?x:0;
		    }
		}
		""",
		editorConfig: "csharp_space_around_ternary_operator = false");

	[Test]
	public Task A_wrapped_ternary_still_breaks_without_the_space() => Formats(
		// The gap in front of ? and : is a wrap point (arena.SoftLine), not a plain space — turning
		// the option off changes what the flat form reads as, not whether a long ternary can still
		// break under max_line_length.
		"""
		public class C
		{
		    public int M(int aVeryLongParameterName, int anotherVeryLongParameterOne)
		    {
		        return aVeryLongParameterName > 0 ? aVeryLongParameterName : anotherVeryLongParameterOne;
		    }
		}
		""",
		"""
		public class C
		{
		    public int M(
		        int aVeryLongParameterName,
		        int anotherVeryLongParameterOne
		    )
		    {
		        return aVeryLongParameterName > 0
		            ?aVeryLongParameterName
		            :anotherVeryLongParameterOne;
		    }
		}
		""",
		editorConfig: "csharp_space_around_ternary_operator = false\nmax_line_length = 60");

	// ---- the batch together ----------------------------------------------------------------------

	[Test]
	public Task Every_option_in_this_batch_at_once() => Formats(
		"""
		public class C : Base
		{
		    public void M()
		    {
		        var x = (int)value;
		        for (var i = 0; i < 10; i++)
		        {
		            Console.WriteLine(x + i);
		        }
		    }
		}
		""",
		"""
		public class C:Base
		{
		    public void M()
		    {
		        var x = (int) value;
		        for(var i = 0 ;i<10 ;i++)
		        {
		            Console . WriteLine(x+i);
		        }
		    }
		}
		""",
		editorConfig: """
		csharp_space_after_cast = true
		csharp_space_after_keywords_in_control_flow_statements = false
		csharp_space_before_colon_in_inheritance_clause = false
		csharp_space_after_colon_in_inheritance_clause = false
		csharp_space_around_binary_operators = none
		csharp_space_after_dot = true
		csharp_space_before_dot = true
		csharp_space_after_semicolon_in_for_statement = false
		csharp_space_before_semicolon_in_for_statement = true
		""");
}
