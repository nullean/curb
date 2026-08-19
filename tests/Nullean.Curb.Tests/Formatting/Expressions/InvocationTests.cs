namespace Nullean.Curb.Tests.Formatting.Expressions;

/// <summary>Calls, argument lists, member access and element access.</summary>
public class InvocationTests : FormattingTest
{
	[Test]
	public Task Call_with_no_arguments() => Unchanged(
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
	public Task Empty_argument_list_has_no_space() => Formats(
		"""
		public class C
		{
		    public void M()
		    {
		        Call( );
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
	public Task Arguments_are_comma_separated() => Formats(
		"""
		public class C
		{
		    public void M()
		    {
		        Call(first,second,third);
		    }
		}
		""",
		"""
		public class C
		{
		    public void M()
		    {
		        Call(first, second, third);
		    }
		}
		""");

	[Test]
	public Task No_space_between_the_name_and_the_parenthesis() => Formats(
		"""
		public class C
		{
		    public void M()
		    {
		        Call (first);
		    }
		}
		""",
		"""
		public class C
		{
		    public void M()
		    {
		        Call(first);
		    }
		}
		""");

	[Test]
	public Task Named_arguments() => Unchanged(
		"""
		public class C
		{
		    public void M()
		    {
		        Call(first: 1, second: 2);
		    }
		}
		""");

	[Test]
	public Task Ref_out_and_in_arguments_keep_their_space() => Unchanged(
		"""
		public class C
		{
		    public void M()
		    {
		        Call(ref first, out var second, in third);
		    }
		}
		""");

	[Test]
	public Task Out_var_keeps_its_space() => Unchanged(
		"""
		public class C
		{
		    public void M()
		    {
		        if (map.TryGetValue("key", out var value))
		        {
		            Call(value);
		        }
		    }
		}
		""");

	[Test]
	public Task Member_access() => Unchanged(
		"""
		public class C
		{
		    public void M()
		    {
		        instance.Method();
		    }
		}
		""");

	[Test]
	public Task Qualified_member_access() => Unchanged(
		"""
		public class C
		{
		    public void M()
		    {
		        System.Console.WriteLine("hello");
		    }
		}
		""");

	[Test]
	public Task Null_conditional_access() => Unchanged(
		"""
		public class C
		{
		    public void M()
		    {
		        instance?.Method();
		    }
		}
		""");

	[Test]
	public Task Null_forgiving_operator() => Unchanged(
		"""
		public class C
		{
		    public void M()
		    {
		        instance!.Method();
		    }
		}
		""");

	[Test]
	public Task Element_access() => Unchanged(
		"""
		public class C
		{
		    public void M()
		    {
		        Call(values[0]);
		    }
		}
		""");

	[Test]
	public Task Element_access_with_an_index_from_the_end() => Unchanged(
		"""
		public class C
		{
		    public void M()
		    {
		        Call(values[^1]);
		    }
		}
		""");

	[Test]
	public Task Range_expression() => Unchanged(
		"""
		public class C
		{
		    public void M()
		    {
		        Call(values[1..3]);
		    }
		}
		""");

	[Test]
	public Task Generic_call() => Unchanged(
		"""
		public class C
		{
		    public void M()
		    {
		        Call<string>(value);
		    }
		}
		""");

	[Test]
	public Task Generic_call_with_several_type_arguments() => Formats(
		"""
		public class C
		{
		    public void M()
		    {
		        Call<string,int>(value);
		    }
		}
		""",
		"""
		public class C
		{
		    public void M()
		    {
		        Call<string, int>(value);
		    }
		}
		""");

	[Test]
	public Task Nested_calls() => Unchanged(
		"""
		public class C
		{
		    public void M()
		    {
		        Outer(Inner(value));
		    }
		}
		""");

	[Test]
	public Task Await_expression() => Unchanged(
		"""
		public class C
		{
		    public async Task M()
		    {
		        await Call();
		    }
		}
		""");

	[Test]
	public Task Awaited_call_with_configure_await() => Unchanged(
		"""
		public class C
		{
		    public async Task M()
		    {
		        await Call().ConfigureAwait(false);
		    }
		}
		""");

	[Test]
	public Task Nameof_and_typeof() => Unchanged(
		"""
		public class C
		{
		    public void M()
		    {
		        Call(nameof(Value), typeof(string));
		    }
		}
		""");

	[Test]
	public Task Argument_list_breaks_one_per_line_when_it_does_not_fit() => Formats(
		"""
		public class C
		{
		    public void M()
		    {
		        Call(alphaArgument, betaArgument, gammaArgument);
		    }
		}
		""",
		"""
		public class C
		{
		    public void M()
		    {
		        Call(
		            alphaArgument,
		            betaArgument,
		            gammaArgument
		        );
		    }
		}
		""",
		editorConfig: "max_line_length = 40");

	[Test]
	public Task Argument_list_stays_on_one_line_when_it_fits() => Unchanged(
		"""
		public class C
		{
		    public void M()
		    {
		        Call(alpha, beta);
		    }
		}
		""",
		editorConfig: "max_line_length = 120");

	[Test]
	public Task Reflow_is_off_by_default_so_long_calls_stay_put() => Unchanged(
		"""
		public class C
		{
		    public void M()
		    {
		        Call(alphaArgument, betaArgument, gammaArgument, deltaArgument, epsilonArgument);
		    }
		}
		""");

	[Test]
	[Skip("member-chain breaking is not implemented — a long chain is not broken per call")]
	public Task Long_member_chain_breaks_one_call_per_line() => Formats(
		"""
		public class C
		{
		    public void M()
		    {
		        instance.First().Second().Third().Fourth();
		    }
		}
		""",
		"""
		public class C
		{
		    public void M()
		    {
		        instance
		            .First()
		            .Second()
		            .Third()
		            .Fourth();
		    }
		}
		""",
		editorConfig: "max_line_length = 40");
}
