namespace Nullean.Curb.Tests.Formatting.Options;

/// <summary>
/// <c>csharp_space_between_parentheses</c> and the six method parenthesis options.
/// </summary>
/// <remarks>
/// <para>
/// <c>space_between_parentheses</c> is a list of <c>control_flow_statements</c>,
/// <c>expressions</c> and <c>type_casts</c>, defaulting to none of them. It deliberately does not
/// reach a method's parameter or argument list — those have six options of their own, splitting
/// empty from occupied and the inside of the parentheses from the space before them — nor a tuple,
/// which no option reaches.
/// </para>
/// <para>
/// A lambda takes the occupied and empty parameter-list options but not the name option, since it
/// has no name for the space to follow. A constructor initialiser, an object creation and an
/// attribute's argument list all count as calls.
/// </para>
/// </remarks>
public class ParenthesisSpacingTests : FormattingTest
{
	// ---- csharp_space_between_parentheses: control_flow_statements -------------------------------

	[Test]
	public Task Control_flow_parentheses_hug_their_condition_by_default() => Unchanged(
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
	public Task Control_flow_parentheses_can_be_spaced_open() => Formats(
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
		        for (var i = 0; i < 1; i++)
		        {
		        }
		        foreach (var i in items)
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
		        if ( a )
		        {
		        }
		        while ( a )
		        {
		        }
		        for ( var i = 0; i < 1; i++ )
		        {
		        }
		        foreach ( var i in items )
		        {
		        }
		        switch ( a )
		        {
		        }
		        lock ( gate )
		        {
		        }
		        using ( var s = Open() )
		        {
		        }
		    }
		}
		""",
		editorConfig: "csharp_space_between_parentheses = control_flow_statements");

	[Test]
	public Task Catch_its_filter_and_do_while_count_as_control_flow() => Formats(
		"""
		public class C
		{
		    public void M()
		    {
		        do
		        {
		        }
		        while (a);

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
		        do
		        {
		        }
		        while ( a );

		        try { }
		        catch ( Exception e ) when ( a ) { }
		    }
		}
		""",
		editorConfig: "csharp_space_between_parentheses = control_flow_statements");

	[Test]
	public Task Control_flow_spacing_leaves_calls_and_casts_alone() => Formats(
		"""
		public class C
		{
		    public void M(int x)
		    {
		        Call(x);
		        var c = (int)value;
		        var p = (a + b) * c;
		    }
		}
		""",
		"""
		public class C
		{
		    public void M(int x)
		    {
		        Call(x);
		        var c = (int)value;
		        var p = (a + b) * c;
		    }
		}
		""",
		editorConfig: "csharp_space_between_parentheses = control_flow_statements");

	// ---- csharp_space_between_parentheses: expressions and type_casts ----------------------------

	[Test]
	public Task A_parenthesised_expression_can_be_spaced_open() => Formats(
		"""
		public class C
		{
		    public void M()
		    {
		        var p = (a + b) * c;
		        var t = (1, 2);
		    }
		}
		""",
		"""
		public class C
		{
		    public void M()
		    {
		        var p = ( a + b ) * c;
		        var t = (1, 2);
		    }
		}
		""",
		editorConfig: "csharp_space_between_parentheses = expressions");

	[Test]
	public Task A_cast_can_be_spaced_open_without_moving_its_operand() => Formats(
		"""
		public class C
		{
		    public void M()
		    {
		        var c = (int)value;
		    }
		}
		""",
		"""
		public class C
		{
		    public void M()
		    {
		        var c = ( int )value;
		    }
		}
		""",
		editorConfig: "csharp_space_between_parentheses = type_casts");

	[Test]
	public Task All_three_can_be_listed_together() => Formats(
		"""
		public class C
		{
		    public void M()
		    {
		        if (a)
		        {
		        }
		        var c = (int)value;
		        var p = (a + b) * c;
		    }
		}
		""",
		"""
		public class C
		{
		    public void M()
		    {
		        if ( a )
		        {
		        }
		        var c = ( int )value;
		        var p = ( a + b ) * c;
		    }
		}
		""",
		editorConfig: "csharp_space_between_parentheses = control_flow_statements,expressions,type_casts");

	[Test]
	public Task An_unrecognised_value_falls_back_to_no_spacing() => Unchanged(
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
		""",
		editorConfig: "csharp_space_between_parentheses = statements");

	// ---- method declaration parentheses -----------------------------------------------------------

	[Test]
	public Task An_occupied_parameter_list_can_be_spaced_open() => Formats(
		"""
		public class C
		{
		    public void M(int x, string y)
		    {
		        var lam = (int a) => a;
		    }

		    public C(int x)
		    {
		    }
		}
		""",
		"""
		public class C
		{
		    public void M( int x, string y )
		    {
		        var lam = ( int a ) => a;
		    }

		    public C( int x )
		    {
		    }
		}
		""",
		editorConfig: "csharp_space_between_method_declaration_parameter_list_parentheses = true");

	[Test]
	public Task An_empty_parameter_list_has_its_own_option() => Formats(
		"""
		public class C
		{
		    public void Empty()
		    {
		        var l2 = () => 1;
		    }
		}
		""",
		"""
		public class C
		{
		    public void Empty( )
		    {
		        var l2 = ( ) => 1;
		    }
		}
		""",
		editorConfig: "csharp_space_between_method_declaration_empty_parameter_list_parentheses = true");

	[Test]
	public Task The_space_before_a_parameter_list_skips_lambdas() => Formats(
		"""
		public class C
		{
		    public void M(int x)
		    {
		        var lam = (int a) => a;
		    }

		    public void Empty()
		    {
		    }
		}
		""",
		"""
		public class C
		{
		    public void M (int x)
		    {
		        var lam = (int a) => a;
		    }

		    public void Empty ()
		    {
		    }
		}
		""",
		editorConfig: "csharp_space_between_method_declaration_name_and_open_parenthesis = true");

	[Test]
	public Task Declaration_options_leave_calls_alone() => Formats(
		"""
		public class C
		{
		    public void M(int x)
		    {
		        Call(x);
		        Empty();
		    }
		}
		""",
		"""
		public class C
		{
		    public void M( int x )
		    {
		        Call(x);
		        Empty();
		    }
		}
		""",
		editorConfig: "csharp_space_between_method_declaration_parameter_list_parentheses = true");

	// ---- method call parentheses -----------------------------------------------------------------

	[Test]
	public Task An_occupied_argument_list_can_be_spaced_open() => Formats(
		"""
		public class C
		{
		    public void M(int x)
		    {
		        Call(x, 1);
		        var o = new C(1);
		    }

		    public C(int i) : this(1)
		    {
		    }
		}
		""",
		"""
		public class C
		{
		    public void M(int x)
		    {
		        Call( x, 1 );
		        var o = new C( 1 );
		    }

		    public C(int i) : this( 1 )
		    {
		    }
		}
		""",
		editorConfig: "csharp_space_between_method_call_parameter_list_parentheses = true");

	[Test]
	public Task An_empty_argument_list_has_its_own_option() => Formats(
		"""
		public class C
		{
		    public void M()
		    {
		        Empty();
		        var e = new C();
		    }
		}
		""",
		"""
		public class C
		{
		    public void M()
		    {
		        Empty( );
		        var e = new C( );
		    }
		}
		""",
		editorConfig: "csharp_space_between_method_call_empty_parameter_list_parentheses = true");

	[Test]
	public Task The_space_before_an_argument_list_covers_creations_and_attributes() => Formats(
		"""
		public class C
		{
		    [Obsolete("x")]
		    public void M()
		    {
		        Call(x);
		        Empty();
		        var o = new C(1);
		        var g = M2<int>(1);
		    }
		}
		""",
		"""
		public class C
		{
		    [Obsolete ("x")]
		    public void M()
		    {
		        Call (x);
		        Empty ();
		        var o = new C (1);
		        var g = M2<int> (1);
		    }
		}
		""",
		editorConfig: "csharp_space_between_method_call_name_and_opening_parenthesis = true");

	[Test]
	public Task An_attribute_argument_list_is_a_call() => Formats(
		"""
		public class C
		{
		    [Obsolete("x", true)]
		    public void M()
		    {
		    }
		}
		""",
		"""
		public class C
		{
		    [Obsolete( "x", true )]
		    public void M()
		    {
		    }
		}
		""",
		editorConfig: "csharp_space_between_method_call_parameter_list_parentheses = true");

	// ---- interaction with reflow -----------------------------------------------------------------

	[Test]
	public Task Inner_spacing_does_not_stop_an_argument_list_from_wrapping() => Formats(
		"""
		public class C
		{
		    public void M()
		    {
		        Call(firstArgument, secondArgument, thirdArgument);
		    }
		}
		""",
		"""
		public class C
		{
		    public void M()
		    {
		        Call(
		            firstArgument,
		            secondArgument,
		            thirdArgument
		        );
		    }
		}
		""",
		editorConfig: """
		max_line_length = 40
		csharp_space_between_method_call_parameter_list_parentheses = true
		""");

	[Test]
	public Task An_argument_list_that_fits_keeps_its_spacing() => Formats(
		"""
		public class C
		{
		    public void M()
		    {
		        Call(a, b);
		    }
		}
		""",
		"""
		public class C
		{
		    public void M()
		    {
		        Call( a, b );
		    }
		}
		""",
		editorConfig: """
		max_line_length = 60
		csharp_space_between_method_call_parameter_list_parentheses = true
		""");
}
