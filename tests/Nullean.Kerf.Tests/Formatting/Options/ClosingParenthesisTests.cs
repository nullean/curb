namespace Nullean.Kerf.Tests.Formatting.Options;

/// <summary>
/// <c>csharp_wrap_before_declaration_rpar</c> and <c>csharp_wrap_before_invocation_rpar</c>.
/// </summary>
/// <remarks>
/// <para>
/// ReSharper's keys for where a broken list's closing parenthesis goes. Kerf has always had the
/// behaviour without the control: when reflow decides the list must break, the parenthesis takes a
/// line of its own, and when the author broke the list themselves, whatever they did is reproduced.
/// These take the decision away from both.
/// </para>
/// <para>
/// Safe either way — <c>dotnet format</c> leaves both arrangements alone, so neither setting costs
/// the fixed-point property. Which is why this is a preference Kerf can offer at all.
/// </para>
/// </remarks>
public class ClosingParenthesisTests : FormattingTest
{
	private const string Narrow = "max_line_length = 60";

	// ---- the default ------------------------------------------------------------------------------

	[Test]
	public Task Reflow_puts_it_on_its_own_line() => Formats(
		"""
		public class C
		{
		    public void M(int alphaParameter, int betaParameter, int gammaParameter)
		    {
		    }
		}
		""",
		"""
		public class C
		{
		    public void M(
		        int alphaParameter,
		        int betaParameter,
		        int gammaParameter
		    )
		    {
		    }
		}
		""",
		editorConfig: Narrow);

	[Test]
	public Task An_author_who_hugged_it_keeps_it_hugged() => Unchanged(
		"""
		public class C
		{
		    public void M(
		        int alphaParameter,
		        int betaParameter)
		    {
		    }
		}
		""",
		editorConfig: Narrow);

	// ---- taking the decision -------------------------------------------------------------------------

	[Test]
	public Task True_gives_it_a_line_even_where_the_author_hugged() => WithAndWithout(
		"""
		public class C
		{
		    public void M(
		        int alphaParameter,
		        int betaParameter)
		    {
		    }
		}
		""",
		"""
		public class C
		{
		    public void M(
		        int alphaParameter,
		        int betaParameter)
		    {
		    }
		}
		""",
		"""
		public class C
		{
		    public void M(
		        int alphaParameter,
		        int betaParameter
		    )
		    {
		    }
		}
		""",
		"csharp_wrap_before_declaration_rpar = true",
		editorConfig: Narrow);

	[Test]
	public Task False_keeps_it_beside_the_last_parameter_even_when_reflow_breaks() => Formats(
		"""
		public class C
		{
		    public void M(int alphaParameter, int betaParameter, int gammaParameter)
		    {
		    }
		}
		""",
		"""
		public class C
		{
		    public void M(
		        int alphaParameter,
		        int betaParameter,
		        int gammaParameter)
		    {
		    }
		}
		""",
		editorConfig: Narrow + "\ncsharp_wrap_before_declaration_rpar = false");

	[Test]
	public Task The_invocation_key_governs_argument_lists() => Formats(
		"""
		public class C
		{
		    public void M()
		    {
		        Call(alphaArgument, betaArgument, gammaArgument, deltaArgument);
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
		            gammaArgument,
		            deltaArgument);
		    }
		}
		""",
		editorConfig: Narrow + "\ncsharp_wrap_before_invocation_rpar = false");

	[Test]
	public Task The_two_keys_are_independent() => Formats(
		// A declaration and a call in the same file, told opposite things.
		"""
		public class C
		{
		    public void M(int alphaParameter, int betaParameter, int gammaParam)
		    {
		        Call(alphaArgument, betaArgument, gammaArgument, deltaArgument);
		    }
		}
		""",
		"""
		public class C
		{
		    public void M(
		        int alphaParameter,
		        int betaParameter,
		        int gammaParam)
		    {
		        Call(
		            alphaArgument,
		            betaArgument,
		            gammaArgument,
		            deltaArgument
		        );
		    }
		}
		""",
		editorConfig: Narrow
			+ "\ncsharp_wrap_before_declaration_rpar = false"
			+ "\ncsharp_wrap_before_invocation_rpar = true");

	// ---- what it moves with it -----------------------------------------------------------------------

	[Test]
	public Task An_expression_body_follows_the_parenthesis_down() => Formats(
		// dotnet format anchors an expression body to the line its arrow lands on, and the arrow lands
		// where the closing parenthesis left it. Forcing the parenthesis to hug therefore puts the
		// switch a level deeper, and the option has to carry that with it or the two disagree.
		"""
		public class C
		{
		    public int M(int alphaParameter, int betaParameter, int gammaParam) => alphaParameter switch
		    {
		        1 => 1,
		        _ => 0
		    };
		}
		""",
		"""
		public class C
		{
		    public int M(
		        int alphaParameter,
		        int betaParameter,
		        int gammaParam) => alphaParameter switch
		        {
		            1 => 1,
		            _ => 0
		        };
		}
		""",
		editorConfig: Narrow + "\ncsharp_wrap_before_declaration_rpar = false");

	// ---- the numeric limits ---------------------------------------------------------------------------

	[Test]
	public Task A_parameter_count_over_the_limit_chops_the_list() => WithAndWithout(
		// A count, not a column. This fits comfortably on one line and is chopped anyway, because the
		// question the key asks is how many things are on the line rather than how long it is.
		"""
		public class C
		{
		    public void M(int a, int b, int c)
		    {
		    }
		}
		""",
		"""
		public class C
		{
		    public void M(int a, int b, int c)
		    {
		    }
		}
		""",
		"""
		public class C
		{
		    public void M(
		        int a,
		        int b,
		        int c
		    )
		    {
		    }
		}
		""",
		"csharp_max_formal_parameters_on_line = 2");

	[Test]
	public Task A_list_at_the_limit_is_left_alone() => Unchanged(
		"""
		public class C
		{
		    public void M(int a, int b)
		    {
		    }
		}
		""",
		editorConfig: "csharp_max_formal_parameters_on_line = 2");

	[Test]
	public Task The_invocation_limit_is_separate() => Formats(
		"""
		public class C
		{
		    public void M(int a, int b, int c)
		    {
		        Call(x, y, z);
		    }
		}
		""",
		"""
		public class C
		{
		    public void M(int a, int b, int c)
		    {
		        Call(
		            x,
		            y,
		            z
		        );
		    }
		}
		""",
		editorConfig: "csharp_max_invocation_arguments_on_line = 2");

	[Test]
	public Task The_count_and_the_parenthesis_keys_compose() => Formats(
		// The count decides whether to chop; the parenthesis key decides where the `)` lands.
		"""
		public class C
		{
		    public void M(int a, int b, int c)
		    {
		    }
		}
		""",
		"""
		public class C
		{
		    public void M(
		        int a,
		        int b,
		        int c)
		    {
		    }
		}
		""",
		editorConfig: "csharp_max_formal_parameters_on_line = 2\ncsharp_wrap_before_declaration_rpar = false");

	[Test]
	public Task A_body_kept_on_one_line_still_moves_down_when_the_header_wraps() => Formats(
		// csharp_preserve_single_line_blocks keeps `{ }` collapsed, but dotnet format will not let it
		// share a line with a `)` that ended a wrapped header. Whether the header wrapped is this
		// run's decision when a count forced it, so the body asks the list's group rather than the
		// source — the case that made this option violate the fixed point until it did.
		"""
		public class C
		{
		    public C(int a, int b, int c) { }
		}
		""",
		"""
		public class C
		{
		    public C(
		        int a,
		        int b,
		        int c
		    )
		    { }
		}
		""",
		editorConfig: "csharp_max_formal_parameters_on_line = 2");

	// ---- the chain head -------------------------------------------------------------------------------

	[Test]
	public Task A_plain_receiver_keeps_its_first_call_by_default() => Unchanged(
		// `source.Where(…)` reads as one thing, so the receiver is not left stranded.
		"""
		public class C
		{
		    public void M()
		    {
		        var r = source.Where(x => x.Active)
		            .Select(x => x.Name)
		            .ToList();
		    }
		}
		""",
		editorConfig: Narrow);

	[Test]
	public Task Wrapping_before_the_first_call_strands_the_receiver() => WithAndWithout(
		"""
		public class C
		{
		    public void M()
		    {
		        var r = source.Where(x => x.Active).Select(x => x.Name).ToList();
		    }
		}
		""",
		"""
		public class C
		{
		    public void M()
		    {
		        var r = source.Where(x => x.Active)
		            .Select(x => x.Name)
		            .ToList();
		    }
		}
		""",
		"""
		public class C
		{
		    public void M()
		    {
		        var r = source
		            .Where(x => x.Active)
		            .Select(x => x.Name)
		            .ToList();
		    }
		}
		""",
		"csharp_wrap_before_first_method_call = true",
		editorConfig: Narrow);

	[Test]
	public Task A_chain_that_fits_is_not_broken_by_the_key() => Unchanged(
		// It decides where a break goes, not whether there is one.
		"""
		public class C
		{
		    public void M()
		    {
		        var r = source.Where(x).ToList();
		    }
		}
		""",
		editorConfig: Narrow + "\ncsharp_wrap_before_first_method_call = true");
}
