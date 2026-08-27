namespace Nullean.Curb.Tests.Formatting.Expressions;

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
	public Task A_reassigned_collection_expression_breaks_the_same_way() => Formats(
		"""
		public class C
		{
		    public void M()
		    {
		        int[] values;
		        values = [alpha, beta, gamma];
		    }
		}
		""",
		"""
		public class C
		{
		    public void M()
		    {
		        int[] values;
		        values =
		        [
		            alpha,
		            beta,
		            gamma
		        ];
		    }
		}
		""",
		editorConfig: "max_line_length = 30");

	/// <summary>
	/// A ternary breaks at its own <c>?</c> and <c>:</c>, one indent in from wherever it starts. Curb
	/// used to break after the <c>=</c> as well, nesting those a level deeper than the assignment
	/// they belong to. See <see href="https://github.com/nullean/curb/issues/34">issue #34</see>.
	/// </summary>
	[Test]
	public Task A_ternary_initializer_indents_once_not_twice() => Formats(
		"""
		public class C
		{
		    public void M(bool condition)
		    {
		        var value = condition ? alpha : beta;
		    }
		}
		""",
		"""
		public class C
		{
		    public void M(bool condition)
		    {
		        var value = condition
		            ? alpha
		            : beta;
		    }
		}
		""",
		editorConfig: "max_line_length = 40");

	[Test]
	public Task A_ternary_reassignment_indents_once_not_twice() => Formats(
		"""
		public class C
		{
		    private object value;

		    public void M(bool condition)
		    {
		        value = condition ? alpha : beta;
		    }
		}
		""",
		"""
		public class C
		{
		    private object value;

		    public void M(bool condition)
		    {
		        value = condition
		            ? alpha
		            : beta;
		    }
		}
		""",
		editorConfig: "max_line_length = 40");

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

	// ---- an operand with its own layout stays attached to the operator that precedes it -----------

	[Test]
	public Task An_assignments_chain_rhs_stays_attached_and_breaks_at_its_own_dots() => Formats(
		// Without BreaksWithoutHelp, the RHS hung on its own indented line — which then measured as
		// fitting flat at that shallower indent, so the chain never broke at its own dots either: the
		// assignment ate the only break the line needed.
		"""
		public class C
		{
		    public void M()
		    {
		        result = result.WithArgs(alpha).WaitForCompletion(beta).WithParentRelationship(gamma);
		    }
		}
		""",
		"""
		public class C
		{
		    public void M()
		    {
		        result = result
		            .WithArgs(alpha)
		            .WaitForCompletion(beta)
		            .WithParentRelationship(gamma);
		    }
		}
		""",
		editorConfig: "max_line_length = 60");

	[Test]
	public Task A_discard_assignments_awaited_call_stays_attached() => Formats(
		// BreaksWithoutHelp looks through the await to the call it wraps — the keyword adds no break
		// opportunity of its own and hides none of the call's.
		"""
		public class C
		{
		    public async Task M()
		    {
		        _ = await repository.PutItemAsync(alphaArgument, betaArgument, gammaArgument, deltaArgument);
		    }
		}
		""",
		"""
		public class C
		{
		    public async Task M()
		    {
		        _ = await repository.PutItemAsync(
		            alphaArgument,
		            betaArgument,
		            gammaArgument,
		            deltaArgument
		        );
		    }
		}
		""",
		editorConfig: "max_line_length = 60");

	[Test]
	public Task A_lambda_body_that_is_a_chain_stays_attached_to_its_arrow() => Formats(
		// Same reasoning as OperandOnRight, one step further out: a lambda arrow is an operator like
		// any other, and a body that is itself a chain has somewhere to break already.
		"""
		public class C
		{
		    public void M()
		    {
		        Configure(s => s.Indices(alphaIndex).Query(betaQuery).Size(oneResult));
		    }
		}
		""",
		"""
		public class C
		{
		    public void M()
		    {
		        Configure(
		            s => s
		                .Indices(alphaIndex)
		                .Query(betaQuery)
		                .Size(oneResult)
		        );
		    }
		}
		""",
		editorConfig: "max_line_length = 45");

	[Test]
	public Task A_lambda_body_that_is_an_assignment_stays_attached_to_its_arrow() => Formats(
		// An assignment prints its own left side, operator and OperandOnRight-driven right side —
		// `context.EnvironmentVariables[k] = v` needs the arrow attached to it the same way a chain
		// body does, not hung on a line of its own only for the assignment to then decide its own
		// break from a level it does not belong at.
		"""
		public class C
		{
		    public void M()
		    {
		        Configure(context => context.EnvironmentVariables[alphaKey] = betaValue);
		    }
		}
		""",
		"""
		public class C
		{
		    public void M()
		    {
		        Configure(
		            context => context.EnvironmentVariables[alphaKey] =
		                betaValue
		        );
		    }
		}
		""",
		editorConfig: "max_line_length = 40");
}
