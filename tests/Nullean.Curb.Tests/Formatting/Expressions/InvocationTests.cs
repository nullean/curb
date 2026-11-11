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

	// Issue #218: an open generic's omitted arguments have nothing between their commas to space, and dotnet
	// format leaves them together whatever the comma spacing options say.
	[Test]
	public Task An_open_generic_keeps_its_commas_together() => Unchanged(
		"""
		public class C
		{
		    public static readonly System.Type Two = typeof(System.Collections.Generic.Dictionary<,>);
		    public static readonly System.Type Three = typeof(System.Func<,,>);
		    public static readonly System.Type One = typeof(System.Collections.Generic.List<>);
		}
		""");

	[Test]
	public Task An_open_generic_keeps_its_commas_together_with_comma_spacing_on() => Unchanged(
		"""
		public class C
		{
		    public static readonly System.Type Two = typeof(System.Collections.Generic.Dictionary<,>);
		    public static readonly System.Collections.Generic.Dictionary<int , string> Closed;
		}
		""",
		editorConfig: "csharp_space_before_comma = true\ncsharp_space_after_comma = true");

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

	// ---- a trailing block-bodied callback hugs the call, even with earlier arguments ---------------

	[Test]
	public Task A_leading_argument_before_a_trailing_callback_stays_inline() => Unchanged(
		// The single-argument hug only fired for one argument total — `On("click", () => { … })` has
		// two, so it fell through to the ordinary "wrap every argument" path and exploded the leading
		// string argument onto its own line for no reason.
		"""
		public class C
		{
		    public void M()
		    {
		        app.Add("", async Task (Cancel _) =>
		        {
		            Call();
		        });
		    }
		}
		""");

	[Test]
	public Task Several_leading_arguments_before_a_trailing_callback_all_stay_inline() => Unchanged(
		"""
		public class C
		{
		    public void M()
		    {
		        On("click", target, true, () =>
		        {
		            Call();
		        });
		    }
		}
		""");

	[Test]
	public Task A_trailing_expression_bodied_lambda_does_not_hug_even_with_a_leading_argument() => Formats(
		// EndsWithBlockBodiedCallback, not the broader EndsWithOwnBlock: a trailing argument that
		// could still print flat has to leave the list its own break opportunity — hugging it the
		// same way a genuine block does would strand the list without one.
		"""
		public class C
		{
		    public void M()
		    {
		        Configure(alphaOption, s => s.Indices(betaIndex).Query(gammaQuery));
		    }
		}
		""",
		"""
		public class C
		{
		    public void M()
		    {
		        Configure(
		            alphaOption,
		            s => s.Indices(betaIndex).Query(gammaQuery)
		        );
		    }
		}
		""",
		editorConfig: "max_line_length = 55");

	// Issue #269: dotnet format leaves a comma the author put first on its line where it is, at the column
	// they gave it, and moves it only as far as the line the list starts on moved.
	[Test]
	public Task A_leading_comma_stays_first_on_its_line() => Unchanged(
		"""
		using System;

		public class C
		{
		    void M(int[] a)
		    {
		        Foo(1
		          , 2
		          , 3);
		        var c = new C(1
		                    , 2);
		        var e = a[0
		                 , 1];
		        Foo(
		            1
		          , 2
		          , 3
		        );
		        Foo(1 // one
		          , 2
		          , 3);
		        Foo(1
		          // two
		          , 2

		          , 3);
		        Bar(1
		          , () =>
		          {
		              return;
		          });
		    }

		    public C(int a, int b) { }
		    void Foo(int a, int b, int c) { }
		    void Bar(int a, Action b) { }
		}
		""");

	[Test]
	public Task A_leading_comma_moves_with_its_line() => Formats(
		"""
		public class C
		{
		  void M()
		  {
		      Foo(1
		        , 2
		        , 3);
		  }

		  void Foo(int a, int b, int c) { }
		}
		""",
		"""
		public class C
		{
		    void M()
		    {
		        Foo(1
		          , 2
		          , 3);
		    }

		    void Foo(int a, int b, int c) { }
		}
		""");

	[Test]
	public Task A_width_still_joins_a_comma_first_argument_list() => Formats(
		"""
		public class C
		{
		    void M()
		    {
		        Foo(1
		          , 2
		          , 3);
		    }

		    void Foo(int a, int b, int c) => M();
		}
		""",
		"""
		public class C
		{
		    void M()
		    {
		        Foo(1, 2, 3);
		    }

		    void Foo(int a, int b, int c) => M();
		}
		""",
		editorConfig: "max_line_length = 120");
}
