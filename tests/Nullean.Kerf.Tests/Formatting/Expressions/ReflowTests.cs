namespace Nullean.Kerf.Tests.Formatting.Expressions;

/// <summary>
/// Width-driven line breaking — the capability neither <c>dotnet format</c> nor a whitespace
/// rewriter has.
/// </summary>
/// <remarks>
/// Varying <c>max_line_length</c> per test is deliberate. CSharpier holds width constant at 100 for
/// its entire suite and forces breaks by padding identifiers with underscores; making the width an
/// argument tests the boundary conditions directly instead.
/// </remarks>
public class ReflowTests : FormattingTest
{
	[Test]
	public Task Fits_exactly_at_the_width() => Unchanged(
		"""
		public class C
		{
		    public void M()
		    {
		        Call(alpha);
		    }
		}
		""",
		editorConfig: "max_line_length = 20");

	[Test]
	public Task One_column_over_the_width_breaks() => Formats(
		"""
		public class C
		{
		    public void M()
		    {
		        Call(alphaLong);
		    }
		}
		""",
		"""
		public class C
		{
		    public void M()
		    {
		        Call(
		            alphaLong
		        );
		    }
		}
		""",
		editorConfig: "max_line_length = 20");

	[Test]
	public Task Parameter_list_breaks_one_per_line() => Formats(
		"""
		public class C
		{
		    public void Method(int alpha, int beta, int gamma)
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
		        int gamma
		    )
		    { }
		}
		""",
		editorConfig: "max_line_length = 30");

	[Test]
	[Skip("the value breaks after `=` and the bracket double-indents; same gap as query clauses")]
	public Task Collection_expression_breaks_one_per_line() => Formats(
		"""
		public class C
		{
		    public void M()
		    {
		        int[] values = [alpha, beta, gamma];
		    }
		}
		""",
		"""
		public class C
		{
		    public void M()
		    {
		        int[] values =
		        [
		            alpha,
		            beta,
		            gamma
		        ];
		    }
		}
		""",
		editorConfig: "max_line_length = 30");

	[Test]
	public Task Nested_calls_break_from_the_outside_in() => Formats(
		"""
		public class C
		{
		    public void M()
		    {
		        Outer(Inner(alpha, beta), gamma);
		    }
		}
		""",
		"""
		public class C
		{
		    public void M()
		    {
		        Outer(
		            Inner(alpha, beta),
		            gamma
		        );
		    }
		}
		""",
		editorConfig: "max_line_length = 34");

	[Test]
	public Task A_wide_width_leaves_everything_alone() => Unchanged(
		"""
		public class C
		{
		    public void M()
		    {
		        Call(alpha, beta, gamma, delta, epsilon, zeta, eta, theta);
		    }
		}
		""",
		editorConfig: "max_line_length = 200");

	[Test]
	public Task Indentation_counts_toward_the_width() => Formats(
		"""
		public class C
		{
		    public void M()
		    {
		        if (condition)
		        {
		            Call(alpha, beta);
		        }
		    }
		}
		""",
		"""
		public class C
		{
		    public void M()
		    {
		        if (condition)
		        {
		            Call(
		                alpha,
		                beta
		            );
		        }
		    }
		}
		""",
		editorConfig: "max_line_length = 28");

	[Test]
	public Task An_argument_list_that_still_does_not_fit_when_broken() => Formats(
		"""
		public class C
		{
		    public void M()
		    {
		        Call(aVeryLongArgumentNameIndeed);
		    }
		}
		""",
		"""
		public class C
		{
		    public void M()
		    {
		        Call(
		            aVeryLongArgumentNameIndeed
		        );
		    }
		}
		""",
		editorConfig: "max_line_length = 16");

	[Test]
	public Task Empty_argument_list_never_breaks() => Unchanged(
		"""
		public class C
		{
		    public void M()
		    {
		        Call();
		    }
		}
		""",
		editorConfig: "max_line_length = 8");

	[Test]
	public Task A_single_long_token_cannot_be_broken() => Unchanged(
		"""
		public class C
		{
		    public void M()
		    {
		        aVeryLongIdentifierThatExceedsTheWidth();
		    }
		}
		""",
		editorConfig: "max_line_length = 10");

	[Test]
	public Task Reflow_is_idempotent_at_a_narrow_width() => Unchanged(
		"""
		public class C
		{
		    public void M()
		    {
		        Call(
		            alpha,
		            beta,
		            gamma
		        );
		    }
		}
		""",
		editorConfig: "max_line_length = 30");

	[Test]
	public Task Width_applies_inside_a_nested_type() => Formats(
		"""
		public class Outer
		{
		    public class Inner
		    {
		        public void M()
		        {
		            Call(alpha, beta);
		        }
		    }
		}
		""",
		"""
		public class Outer
		{
		    public class Inner
		    {
		        public void M()
		        {
		            Call(
		                alpha,
		                beta
		            );
		        }
		    }
		}
		""",
		editorConfig: "max_line_length = 28");
}
