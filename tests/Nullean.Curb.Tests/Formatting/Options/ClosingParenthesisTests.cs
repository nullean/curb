namespace Nullean.Curb.Tests.Formatting.Options;

/// <summary>
/// <c>csharp_wrap_before_declaration_rpar</c> and <c>csharp_wrap_before_invocation_rpar</c>.
/// </summary>
/// <remarks>
/// <para>
/// ReSharper's keys for where a broken list's closing parenthesis goes. Curb has always had the
/// behaviour without the control: when reflow decides the list must break, the parenthesis takes a
/// line of its own, and when the author broke the list themselves, whatever they did is reproduced.
/// These take the decision away from both.
/// </para>
/// <para>
/// Safe either way — <c>dotnet format</c> leaves both arrangements alone, so neither setting costs
/// the fixed-point property. Which is why this is a preference Curb can offer at all.
/// </para>
/// </remarks>
public class ClosingParenthesisTests : FormattingTest
{
	private const string Narrow = "max_line_length = 60";

	// A width now selects deterministic layout as well as the column, so a test whose subject is the
	// author's own arrangement has to ask for preservation rather than assume it. That is the opt-out this
	// file documents, and spelling it out is what stops these tests quietly changing subject.
	private const string NarrowPreserving = Narrow + "\ncsharp_keep_existing_linebreaks = true";

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
		    { }
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
		editorConfig: NarrowPreserving);

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
		editorConfig: NarrowPreserving);

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
		    { }
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

	// ---- binary chains ---------------------------------------------------------------------------------

	[Test]
	public Task A_long_condition_overflows_the_line_in_preservation_mode() => Unchanged(
		// Curb offers no break opportunity inside a binary chain it is reproducing, so a condition too long
		// for the line simply overflows it: the parentheses move, the operands do not. That is what
		// csharp_wrap_chained_binary_expressions exists to fix, below.
		"""
		public class C
		{
		    public void M()
		    {
		        if (
		            firstCondition && secondCondition && thirdCondition && fourth
		        )
		        {
		        }
		    }
		}
		""",
		editorConfig: NarrowPreserving);

	/// <summary>Deterministic layout breaks it instead, without needing the chain key at all.</summary>
	/// <remarks>
	/// The operands are reached through <c>OperandOnRight</c>, which offers a break wherever it is not
	/// reproducing an author's arrangement — so in this mode the condition never overflows. The stepped
	/// indent is the chain's own nesting showing through, which is why the chain key still earns its place:
	/// it flattens the whole chain to one level.
	/// </remarks>
	[Test]
	public Task A_long_condition_breaks_under_deterministic_layout() => Formats(
		"""
		public class C
		{
		    public void M()
		    {
		        if (firstCondition && secondCondition && thirdCondition && fourth)
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
		        if (
		            firstCondition && secondCondition &&
		                thirdCondition && fourth
		        )
		        { }
		    }
		}
		""",
		editorConfig: Narrow);

	[Test]
	public Task Chopping_gives_every_operand_a_line() => Formats(
		"""
		public class C
		{
		    public void M()
		    {
		        var ok = alphaCondition && betaCondition && gammaCond;
		    }
		}
		""",
		"""
		public class C
		{
		    public void M()
		    {
		        var ok = alphaCondition
		            && betaCondition
		            && gammaCond;
		    }
		}
		""",
		editorConfig: Narrow + "\ncsharp_wrap_chained_binary_expressions = chop_if_long");

	[Test]
	public Task The_operator_can_end_the_line_instead_of_starting_it() => Formats(
		"""
		public class C
		{
		    public void M()
		    {
		        var ok = alphaCondition && betaCondition && gammaCond;
		    }
		}
		""",
		"""
		public class C
		{
		    public void M()
		    {
		        var ok = alphaCondition &&
		            betaCondition &&
		            gammaCond;
		    }
		}
		""",
		editorConfig: Narrow
			+ "\ncsharp_wrap_chained_binary_expressions = chop_if_long"
			+ "\ncsharp_wrap_before_binary_opsign = false");

	[Test]
	public Task A_two_operand_chain_is_left_alone() => Unchanged(
		// Two reads fine on one line, and the group around it already offers a break.
		"""
		public class C
		{
		    public void M()
		    {
		        var ok = alphaCondition && betaCondition;
		    }
		}
		""",
		editorConfig: Narrow + "\ncsharp_wrap_chained_binary_expressions = chop_if_long");

	[Test]
	public Task A_right_associative_chain_keeps_every_operator() => Formats(
		// `??` associates the other way, so its operands are left-hand children. Looking each operator
		// up from its operand afterwards found nothing for them and dropped the lot — caught by the
		// content check on a corpus file, not by any test that existed.
		"""
		public class C
		{
		    public void M()
		    {
		        var value = alphaCandidate ?? betaCandidate ?? gammaCandidate;
		    }
		}
		""",
		"""
		public class C
		{
		    public void M()
		    {
		        var value = alphaCandidate
		            ?? betaCandidate
		            ?? gammaCandidate;
		    }
		}
		""",
		editorConfig: Narrow + "\ncsharp_wrap_chained_binary_expressions = chop_if_long");

	[Test]
	public Task A_chain_inside_a_call_is_left_to_the_call() => Unchanged(
		// A chain there is measured by whatever encloses it, and the break opportunities this would
		// add change that measurement without necessarily being taken — which left one corpus file
		// long on the first run and broken on the second.
		"""
		public class C
		{
		    public void M()
		    {
		        Assert(d => d.A == 1 && d.B == 2 && d.C == 3);
		    }
		}
		""",
		editorConfig: Narrow + "\ncsharp_wrap_chained_binary_expressions = chop_if_long");

	// ---- initializer element counts ---------------------------------------------------------------------

	[Test]
	public Task An_initializer_over_the_element_limit_chops() => WithAndWithout(
		"""
		public class C
		{
		    public void M()
		    {
		        var b = new Options { X = 1, Y = 2, Z = 3 };
		    }
		}
		""",
		"""
		public class C
		{
		    public void M()
		    {
		        var b = new Options { X = 1, Y = 2, Z = 3 };
		    }
		}
		""",
		"""
		public class C
		{
		    public void M()
		    {
		        var b = new Options
		        {
		            X = 1,
		            Y = 2,
		            Z = 3
		        };
		    }
		}
		""",
		"csharp_max_initializer_elements_on_line = 2");

	[Test]
	public Task An_initializer_at_the_limit_is_left_alone() => Unchanged(
		// Breaking only. This never closes up an initializer that fits — that direction is the one
		// that cannot be made to settle.
		"""
		public class C
		{
		    public void M()
		    {
		        var a = new Point { X = 1, Y = 2 };
		    }
		}
		""",
		editorConfig: "csharp_max_initializer_elements_on_line = 2");

	[Test]
	public Task Arrays_have_a_limit_of_their_own() => Formats(
		"""
		public class C
		{
		    public void M()
		    {
		        var c = new[] { 1, 2, 3 };
		    }
		}
		""",
		"""
		public class C
		{
		    public void M()
		    {
		        var c = new[]
		        {
		            1,
		            2,
		            3
		        };
		    }
		}
		""",
		editorConfig: "csharp_max_array_initializer_elements_on_line = 2");

	[Test]
	public Task An_initializer_in_an_argument_is_left_to_the_call() => Unchanged(
		// Where a construct with its own braces anchors is read from the source, so forcing a break
		// the source does not have puts it a level out and the next run corrects it.
		"""
		public class C
		{
		    public void M()
		    {
		        Send(new Options { X = 1, Y = 2, Z = 3 });
		    }
		}
		""",
		editorConfig: "csharp_max_initializer_elements_on_line = 2");

	// ---- chop always -----------------------------------------------------------------------------------

	[Test]
	public Task Parameters_can_be_chopped_whatever_the_width() => WithAndWithout(
		"""
		public class C
		{
		    public void M(int a, int b)
		    {
		    }
		}
		""",
		"""
		public class C
		{
		    public void M(int a, int b)
		    {
		    }
		}
		""",
		"""
		public class C
		{
		    public void M(
		        int a,
		        int b
		    )
		    {
		    }
		}
		""",
		"csharp_wrap_parameters_style = chop_always");

	[Test]
	public Task Chopping_always_still_leaves_an_empty_list_alone() => Unchanged(
		"""
		public class C
		{
		    public void M()
		    {
		    }
		}
		""",
		editorConfig: "csharp_wrap_parameters_style = chop_always");

	// ---- chop always, the two keys that need deterministic layout --------------------------------------

	/// <summary>
	/// <c>csharp_wrap_arguments_style</c> and <c>csharp_wrap_object_and_collection_initializer_style</c>,
	/// which are honoured only under deterministic layout — that is, only once a width is set.
	/// </summary>
	/// <remarks>
	/// The asymmetry with the parameter key above is measured, not chosen. Forcing every argument list to
	/// break moves what is nested inside it, and preservation mode has rules that read that nesting's
	/// indentation from the source — 140 corpus files stopped settling. Deterministic layout has no such
	/// rule, and the corpus comes back idempotent and 100% conformant with both keys on.
	/// </remarks>
	// A width is all it takes: max_line_length selects deterministic layout as well as the column.
	private const string Deterministic = "max_line_length = 120\n";

	[Test]
	public Task Arguments_can_be_chopped_under_deterministic_layout() => Formats(
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
		        Call(
		            a,
		            b
		        );
		    }
		}
		""",
		Deterministic + "csharp_wrap_arguments_style = chop_always");

	[Test]
	public Task Chopping_arguments_is_ignored_without_it() => Unchanged(
		"""
		public class C
		{
		    public void M()
		    {
		        Call(a, b);
		    }
		}
		""",
		editorConfig: "csharp_wrap_arguments_style = chop_always");

	[Test]
	public Task Initializers_can_be_chopped_under_deterministic_layout() => Formats(
		"""
		public class C
		{
		    private C _c = new C { A = 1, B = 2 };
		}
		""",
		"""
		public class C
		{
		    private C _c = new C
		    {
		        A = 1,
		        B = 2
		    };
		}
		""",
		Deterministic + "csharp_wrap_object_and_collection_initializer_style = chop_always");

	[Test]
	public Task Chopping_initializers_is_ignored_without_it() => Unchanged(
		"""
		public class C
		{
		    private C _c = new C { A = 1, B = 2 };
		}
		""",
		editorConfig: "csharp_wrap_object_and_collection_initializer_style = chop_always");

	// ---- wrap if long: the fill layout --------------------------------------------------------------

	/// <summary>
	/// <c>wrap_if_long</c> on <c>csharp_wrap_parameters_style</c>, <c>csharp_wrap_arguments_style</c>,
	/// <c>csharp_wrap_object_and_collection_initializer_style</c> and
	/// <c>csharp_wrap_chained_binary_expressions</c>: pack elements onto a line until the next one would
	/// not fit, rather than <c>chop_if_long</c>'s all-or-nothing group or <c>chop_always</c>'s
	/// unconditional one-per-line.
	/// </summary>
	/// <remarks>
	/// Deterministic mode only, the same restriction <c>chop_always</c> carries for arguments and
	/// initializers — packing decides a break by measuring width, and preservation mode has rules
	/// elsewhere that read that break's indentation from the source. Unlike <c>chop_always</c>, which is
	/// admissible in either mode for parameters, <c>wrap_if_long</c> carries the restriction there too:
	/// see <c>OptionsBindingTests</c>.
	/// </remarks>
	private const string Narrower = "max_line_length = 50\n";

	[Test]
	public Task Parameters_pack_onto_a_line_with_wrap_if_long() => Formats(
		"""
		public class C
		{
		    public void M(int alpha, int beta, int gamma, int delta, int epsilon)
		    {
		    }
		}
		""",
		"""
		public class C
		{
		    public void M(
		        int alpha, int beta, int gamma, int delta,
		        int epsilon
		    )
		    { }
		}
		""",
		Narrower + "csharp_wrap_parameters_style = wrap_if_long");

	[Test]
	public Task Arguments_pack_onto_a_line_with_wrap_if_long() => Formats(
		"""
		public class C
		{
		    public void M()
		    {
		        Call(alpha, beta, gamma, delta, epsilon, zeta);
		    }
		}
		""",
		"""
		public class C
		{
		    public void M()
		    {
		        Call(
		            alpha, beta, gamma, delta, epsilon,
		            zeta
		        );
		    }
		}
		""",
		Narrower + "csharp_wrap_arguments_style = wrap_if_long");

	[Test]
	public Task Initializer_elements_pack_onto_a_line_with_wrap_if_long() => Formats(
		"""
		public class C
		{
		    public void M()
		    {
		        var arr = new[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13 };
		    }
		}
		""",
		"""
		public class C
		{
		    public void M()
		    {
		        var arr = new[]
		        {
		            1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12,
		            13
		        };
		    }
		}
		""",
		Narrower + "csharp_wrap_object_and_collection_initializer_style = wrap_if_long");

	[Test]
	public Task Binary_operands_pack_onto_a_line_with_wrap_if_long() => Formats(
		"""
		public class C
		{
		    public void M()
		    {
		        var ok = alpha && beta && gamma && delta && epsilon && zeta;
		    }
		}
		""",
		"""
		public class C
		{
		    public void M()
		    {
		        var ok = alpha && beta && gamma && delta
		            && epsilon && zeta;
		    }
		}
		""",
		Narrower + "csharp_wrap_chained_binary_expressions = wrap_if_long");

	[Test]
	public Task Wrap_if_long_stays_flat_when_everything_fits() => Formats(
		// Deterministic mode collapses an empty body to `{ }` regardless of this key — the fixture
		// is here to show the parameter list itself stays joined, not to test that collapse.
		"""
		public class C
		{
		    public void M(int a, int b)
		    {
		    }
		}
		""",
		"""
		public class C
		{
		    public void M(int a, int b)
		    { }
		}
		""",
		editorConfig: Deterministic + "csharp_wrap_parameters_style = wrap_if_long");
}
