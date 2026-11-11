namespace Nullean.Curb.Tests.Formatting.Declarations;

/// <summary>Methods, their parameter lists and their bodies.</summary>
public class MethodTests : FormattingTest
{
	[Test]
	public Task Method_with_a_body() => Unchanged(
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
	public Task Empty_body() => Unchanged(
		"""
		public class C
		{
		    public void M()
		    {
		    }
		}
		""");

	[Test]
	public Task Body_brace_moves_to_its_own_line() => Formats(
		"""
		public class C
		{
		    public void M() {
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
	public Task Parameters_are_comma_separated() => Formats(
		"""
		public class C
		{
		    public void M(int first,string second)
		    {
		    }
		}
		""",
		"""
		public class C
		{
		    public void M(int first, string second)
		    {
		    }
		}
		""");

	[Test]
	public Task Empty_parameter_list_has_no_space() => Formats(
		"""
		public class C
		{
		    public void M( )
		    {
		    }
		}
		""",
		"""
		public class C
		{
		    public void M()
		    {
		    }
		}
		""");

	[Test]
	public Task No_space_between_the_name_and_the_parenthesis() => Formats(
		"""
		public class C
		{
		    public void M ()
		    {
		    }
		}
		""",
		"""
		public class C
		{
		    public void M()
		    {
		    }
		}
		""");

	[Test]
	public Task Expression_bodied_method() => Unchanged(
		"""
		public class C
		{
		    public int Value() => 1;
		}
		""");

	[Test]
	public Task Async_method() => Unchanged(
		"""
		public class C
		{
		    public async Task M()
		    {
		        await Other();
		    }
		}
		""");

	[Test]
	public Task Generic_method() => Unchanged(
		"""
		public class C
		{
		    public T Get<T>()
		    {
		        return default;
		    }
		}
		""");

	[Test]
	public Task Generic_method_with_a_constraint() => Unchanged(
		"""
		public class C
		{
		    public T Get<T>() where T : new()
		    {
		        return new T();
		    }
		}
		""");

	[Test]
	public Task Default_parameter_value_has_spaces_around_the_equals() => Formats(
		"""
		public class C
		{
		    public void M(int value=1)
		    {
		    }
		}
		""",
		"""
		public class C
		{
		    public void M(int value = 1)
		    {
		    }
		}
		""");

	[Test]
	public Task Ref_and_out_parameters() => Unchanged(
		"""
		public class C
		{
		    public void M(ref int first, out int second)
		    {
		        second = first;
		    }
		}
		""");

	[Test]
	public Task Params_parameter() => Unchanged(
		"""
		public class C
		{
		    public void M(params int[] values)
		    {
		    }
		}
		""");

	[Test]
	public Task This_parameter_on_an_extension_method() => Unchanged(
		"""
		public static class Extensions
		{
		    public static int Twice(this int value)
		    {
		        return value * 2;
		    }
		}
		""");

	[Test]
	public Task Attribute_on_a_parameter() => Unchanged(
		"""
		public class C
		{
		    public void M([Required] string value)
		    {
		    }
		}
		""");

	[Test]
	public Task Attribute_above_a_method() => Unchanged(
		"""
		public class C
		{
		    [Obsolete]
		    public void M()
		    {
		    }
		}
		""");

	[Test]
	public Task Several_attributes_each_on_their_own_line() => Unchanged(
		"""
		public class C
		{
		    [Obsolete]
		    [Conditional("DEBUG")]
		    public void M()
		    {
		    }
		}
		""");

	[Test]
	public Task Nullable_return_type() => Unchanged(
		"""
		public class C
		{
		    public string? M()
		    {
		        return null;
		    }
		}
		""");

	[Test]
	public Task Array_return_type() => Unchanged(
		"""
		public class C
		{
		    public int[] M()
		    {
		        return [];
		    }
		}
		""");

	[Test]
	public Task Explicit_interface_implementation() => Unchanged(
		"""
		public class C : IThing
		{
		    void IThing.Do()
		    {
		    }
		}
		""");

	[Test]
	public Task Abstract_method_has_no_body() => Unchanged(
		"""
		public abstract class C
		{
		    public abstract void M();
		}
		""");

	[Test]
	public Task Local_function() => Unchanged(
		"""
		public class C
		{
		    public void M()
		    {
		        Inner();

		        void Inner()
		        {
		            Call();
		        }
		    }
		}
		""");

	[Test]
	public Task Static_local_function() => Unchanged(
		"""
		public class C
		{
		    public void M()
		    {
		        static int Double(int value)
		        {
		            return value * 2;
		        }
		    }
		}
		""");

	[Test]
	public Task Parameter_list_breaks_one_per_line_when_it_does_not_fit() => Formats(
		"""
		public class C
		{
		    public void Method(int alpha, int beta, int gamma, int delta)
		    {
		    }
		}
		""",
		"""
		public class C
		{
		    public void Method(
		        int alpha,
		        int beta,
		        int gamma,
		        int delta
		    )
		    { }
		}
		""",
		editorConfig: "max_line_length = 40");

	[Test]
	public Task Parameter_list_stays_on_one_line_when_it_fits() => Formats(
		"""
		public class C
		{
		    public void M(int alpha, int beta)
		    {
		    }
		}
		""",
		// An empty pair collapses under deterministic layout, which a width now selects. It does not move
		// onto the header line — that stays csharp_new_line_before_open_brace's call.
		"""
		public class C
		{
		    public void M(int alpha, int beta)
		    { }
		}
		""",
		editorConfig: "max_line_length = 120");

	// Issue #256: dotnet format leaves a parameter line the author broke at the column they gave it. Moved
	// a level in, `int y` also dragged the `where` clause below it six columns left, to column 2.
	[Test]
	public Task A_wrapped_parameter_line_keeps_its_column() => Unchanged(
		"""
		public class C
		{
		    void M(int x,
		                    int y)
		    {
		    }

		    void N<T>(int x,
		              int y)
		        where T : class
		    {
		    }

		    int O(int x,
		          int y) =>
		        x + y;

		    public C(int x,
		             int y)
		        : base()
		    {
		    }
		}
		""");

	[Test]
	public Task A_parenthesis_on_its_own_line_keeps_its_column() => Unchanged(
		"""
		public class C
		{
		    void M(
		            int x,
		            int y
		        )
		    {
		    }

		    void N(
		    int x,
		    int y)
		    {
		    }
		}
		""");

	[Test]
	public Task A_leading_comma_stays_first_on_its_line() => Unchanged(
		"""
		public class C
		{
		    void M(int x
		         , int y
		         , int z)
		    {
		    }

		    void N(
		        int x
		      , int y
		    )
		    {
		    }
		}
		""");

	[Test]
	public Task Every_kind_of_parameter_list_keeps_its_columns() => Unchanged(
		"""
		using System;

		public delegate void D(int x,
		                       int y);

		public record R(int X,
		                int Y);

		public class P(int x,
		               int y)
		{
		    public static P operator +(P a,
		                               P b) => a;

		    void M()
		    {
		        int Add(int a,
		                int b) => a + b;

		        Func<int, int, int> f = (int a,
		                                 int b) => a + b;
		        Run((int a,
		             int b) =>
		        {
		            return a + b;
		        });
		    }

		    void Run(Func<int, int, int> f) { }
		}
		""");

	[Test]
	public Task A_wrapped_parameter_line_moves_with_its_declaration() => Formats(
		"""
		public class C
		{
		  void M(int x,
		                    int y)
		  {
		  }
		}
		""",
		"""
		public class C
		{
		    void M(int x,
		                      int y)
		    {
		    }
		}
		""");

	[Test]
	public Task A_comment_blank_line_or_directive_in_a_parameter_list_keeps_the_column() => Unchanged(
		"""
		public class C
		{
		    void M( // note
		        int x,
		        int y)
		    {
		    }

		    void N(int x,

		           int y)
		    {
		    }

		    void O(int x,
		#if DEBUG
		           int y,
		#endif
		           int z)
		    {
		    }

		    void P(int x,
		           // second
		           int y)
		    {
		    }
		}
		""");

	// Issue #257: dotnet format keeps a parameter's attribute on a line of its own.
	[Test]
	public Task An_attribute_on_its_own_line_stays_above_its_parameter() => Unchanged(
		"""
		using System;

		public class C
		{
		    void M(int x,
		           [Obsolete]
		           int y)
		    {
		    }

		    void N(
		        [Obsolete]
		            int y)
		    {
		    }
		}
		""");

	[Test]
	public Task A_width_still_joins_a_wrapped_parameter_list() => Formats(
		"""
		using System;

		public class C
		{
		    void M(int x
		         , [Obsolete]
		           int y)
		    {
		    }
		}
		""",
		"""
		using System;

		public class C
		{
		    void M(int x, [Obsolete] int y)
		    { }
		}
		""",
		editorConfig: "max_line_length = 120");
}
