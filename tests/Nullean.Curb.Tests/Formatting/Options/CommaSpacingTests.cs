namespace Nullean.Curb.Tests.Formatting.Options;

/// <summary>
/// <c>csharp_space_after_comma</c> and <c>csharp_space_before_comma</c>.
/// </summary>
/// <remarks>
/// <para>
/// The interesting part is which commas these reach. dotnet format governs argument and parameter
/// lists, type <em>argument</em> lists, initializers, anonymous types, tuples, array ranks, list
/// patterns and deconstruction designations — and leaves alone type <em>parameter</em> lists,
/// constraint clauses, attribute lists, base lists, declarator lists, enum bodies, switch
/// expression arms, <c>for</c> headers, <c>orderby</c> clauses and pattern subpatterns.
/// </para>
/// <para>
/// Both halves are tested here, because a formatter that applied the option everywhere would look
/// more consistent and would be wrong. Every expectation came from running dotnet format.
/// </para>
/// </remarks>
public class CommaSpacingTests : FormattingTest
{
	private const string Swapped = """
		csharp_space_after_comma = false
		csharp_space_before_comma = true
		""";

	// ---- defaults --------------------------------------------------------------------------------

	[Test]
	public Task A_comma_takes_a_trailing_space_by_default() => Formats(
		"""
		public class C
		{
		    public void M(int x , string y)
		    {
		        Call(x , y);
		    }
		}
		""",
		"""
		public class C
		{
		    public void M(int x, string y)
		    {
		        Call(x, y);
		    }
		}
		""");

	// ---- the governed positions ------------------------------------------------------------------

	[Test]
	public Task Parameter_and_argument_lists_are_governed() => Formats(
		"""
		public class C
		{
		    public void M(int x, string y)
		    {
		        Call(x, y);
		        Named(a: 1, b: 2);
		    }
		}
		""",
		"""
		public class C
		{
		    public void M(int x ,string y)
		    {
		        Call(x ,y);
		        Named(a: 1 ,b: 2);
		    }
		}
		""",
		editorConfig: Swapped);

	[Test]
	public Task Type_argument_lists_are_governed() => Formats(
		"""
		public class C
		{
		    public void M()
		    {
		        var d = new Dictionary<string, int>();
		        Convert<int, string>(1);
		    }
		}
		""",
		"""
		public class C
		{
		    public void M()
		    {
		        var d = new Dictionary<string ,int>();
		        Convert<int ,string>(1);
		    }
		}
		""",
		editorConfig: Swapped);

	[Test]
	public Task Initializers_and_anonymous_types_are_governed() => Formats(
		"""
		public class C
		{
		    public void M()
		    {
		        var list = new List<int> { 1, 2 };
		        var anon = new { A = 1, B = 2 };
		    }
		}
		""",
		"""
		public class C
		{
		    public void M()
		    {
		        var list = new List<int> { 1 ,2 };
		        var anon = new { A = 1 ,B = 2 };
		    }
		}
		""",
		editorConfig: Swapped);

	[Test]
	public Task Tuples_are_governed_in_both_expression_and_type_position() => Formats(
		"""
		public class C
		{
		    public (int a, string b) M((int x, int y) p)
		    {
		        return (1, "a");
		    }
		}
		""",
		"""
		public class C
		{
		    public (int a ,string b) M((int x ,int y) p)
		    {
		        return (1 ,"a");
		    }
		}
		""",
		editorConfig: Swapped);

	[Test]
	public Task Array_ranks_and_element_access_are_governed() => Formats(
		"""
		public class C
		{
		    public void M()
		    {
		        var r = new int[1, 2];
		        var w = r[0, 1];
		    }
		}
		""",
		"""
		public class C
		{
		    public void M()
		    {
		        var r = new int[1 ,2];
		        var w = r[0 ,1];
		    }
		}
		""",
		editorConfig: Swapped);

	[Test]
	public Task Deconstruction_and_list_patterns_are_governed() => Formats(
		"""
		public class C
		{
		    public void M()
		    {
		        var (p1, p2) = pair;
		        if (arr is [1, 2, 3])
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
		        var (p1 ,p2) = pair;
		        if (arr is [1 ,2 ,3])
		        {
		        }
		    }
		}
		""",
		editorConfig: Swapped);

	[Test]
	public Task Attribute_arguments_are_governed_even_though_the_list_is_not() => Formats(
		"""
		public class C
		{
		    [Obsolete("x", true), Serializable]
		    public void M()
		    {
		    }
		}
		""",
		"""
		public class C
		{
		    [Obsolete("x" ,true), Serializable]
		    public void M()
		    {
		    }
		}
		""",
		editorConfig: Swapped);

	// ---- the positions these options do not reach -------------------------------------------------

	[Test]
	public Task Type_parameter_lists_and_constraints_are_left_alone() => Unchanged(
		"""
		public class C<T, U> where T : IThing, IOther
		{
		}
		""",
		editorConfig: Swapped);

	[Test]
	public Task Base_lists_are_left_alone() => Unchanged(
		"""
		public class C : Base, IThing
		{
		}
		""",
		editorConfig: Swapped);

	[Test]
	public Task Declarator_lists_and_enum_bodies_are_left_alone() => Unchanged(
		"""
		public class C
		{
		    private int a = 1, b = 2;

		    public enum E
		    {
		        One,
		        Two,
		    }
		}
		""",
		editorConfig: Swapped);

	[Test]
	public Task Switch_expression_arms_are_left_alone() => Unchanged(
		"""
		public class C
		{
		    public int M(int x) => x switch
		    {
		        1 => 1,
		        _ => 0,
		    };
		}
		""",
		editorConfig: Swapped);

	[Test]
	public Task For_headers_are_left_alone() => Unchanged(
		"""
		public class C
		{
		    public void M()
		    {
		        for (int i = 0, j = 1; ; i++, j++)
		        {
		        }
		    }
		}
		""",
		editorConfig: Swapped);

	[Test]
	public Task Orderby_is_left_alone() => Unchanged(
		"""
		public class C
		{
		    public void M()
		    {
		        var q = from p in items orderby p.A, p.B select p;
		    }
		}
		""",
		editorConfig: Swapped);

	[Test]
	public Task Pattern_subpatterns_are_left_alone() => Unchanged(
		"""
		public class C
		{
		    public void M()
		    {
		        if (o is Point { X: 1, Y: 2 })
		        {
		        }

		        if (o is (1, 2))
		        {
		        }
		    }
		}
		""",
		editorConfig: Swapped);

	// ---- interaction with reflow -----------------------------------------------------------------

	[Test]
	public Task Dropping_the_space_does_not_stop_a_list_from_wrapping() => Formats(
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
		csharp_space_after_comma = false
		""");

	[Test]
	public Task A_list_that_fits_keeps_its_commas_tight() => Formats(
		"""
		public class C
		{
		    public void M()
		    {
		        Call(a, b, c);
		    }
		}
		""",
		"""
		public class C
		{
		    public void M()
		    {
		        Call(a,b,c);
		    }
		}
		""",
		editorConfig: """
		max_line_length = 40
		csharp_space_after_comma = false
		""");
}
