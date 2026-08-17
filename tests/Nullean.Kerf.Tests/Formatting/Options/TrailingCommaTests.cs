namespace Nullean.Kerf.Tests.Formatting.Options;

/// <summary>
/// <c>csharp_trailing_comma_in_multiline_lists</c> and
/// <c>csharp_trailing_comma_in_singleline_lists</c>.
/// </summary>
/// <remarks>
/// <para>
/// ReSharper's keys rather than invented ones. Rider has offered this for years and reads these
/// exact spellings, so a repository that has configured Rider gets the same answer from Kerf without
/// having to say it twice. Both are off by default: this is the only thing Kerf does that adds or
/// removes a token rather than moving whitespace, so it stays an explicit ask.
/// </para>
/// <para>
/// <c>dotnet format</c> neither adds a trailing comma nor removes one, which is what makes the rule
/// safe to hold — output carrying them is still a fixed point of it, checked directly rather than
/// assumed.
/// </para>
/// <para>
/// The hazard is the grammar, not the layout. C# permits a trailing comma in initializers, enum
/// bodies, anonymous types, switch expressions, collection expressions and list patterns, and
/// forbids one in argument lists, parameter lists, type argument and type parameter lists and
/// tuples. Emitting it in a forbidden position produces source that does not compile, so the
/// forbidden constructs are tested as explicitly as the permitted ones.
/// </para>
/// </remarks>
public class TrailingCommaTests : FormattingTest
{
	private const string Multiline = "csharp_trailing_comma_in_multiline_lists = true";
	private const string Singleline = "csharp_trailing_comma_in_singleline_lists = true";

	// ---- the default ------------------------------------------------------------------------------

	[Test]
	public Task Nothing_happens_unless_asked() => Unchanged(
		"""
		public class C
		{
		    private static readonly int[] Values = new[]
		    {
		        1,
		        2
		    };
		}
		""");

	[Test]
	public Task An_existing_trailing_comma_is_kept_by_default() => Unchanged(
		// The default preserves what the author wrote in both directions, so a repository that already
		// uses trailing commas keeps them without setting anything.
		"""
		public class C
		{
		    private static readonly int[] Values = new[]
		    {
		        1,
		        2,
		    };
		}
		""");

	// ---- multiline --------------------------------------------------------------------------------

	[Test]
	public Task A_broken_initializer_gains_one() => WithAndWithout(
		"""
		public class C
		{
		    private static readonly int[] Values = new[]
		    {
		        1,
		        2
		    };
		}
		""",
		"""
		public class C
		{
		    private static readonly int[] Values = new[]
		    {
		        1,
		        2
		    };
		}
		""",
		"""
		public class C
		{
		    private static readonly int[] Values = new[]
		    {
		        1,
		        2,
		    };
		}
		""",
		Multiline);

	[Test]
	public Task An_enum_body_gains_one() => WithAndWithout(
		"""
		public enum E
		{
		    Alpha,
		    Beta
		}
		""",
		"""
		public enum E
		{
		    Alpha,
		    Beta
		}
		""",
		"""
		public enum E
		{
		    Alpha,
		    Beta,
		}
		""",
		Multiline);

	[Test]
	public Task A_switch_expression_gains_one() => WithAndWithout(
		"""
		public class C
		{
		    public int M(int x) => x switch
		    {
		        1 => 1,
		        _ => 0
		    };
		}
		""",
		"""
		public class C
		{
		    public int M(int x) => x switch
		    {
		        1 => 1,
		        _ => 0
		    };
		}
		""",
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
		Multiline);

	[Test]
	public Task An_anonymous_type_gains_one() => WithAndWithout(
		"""
		public class C
		{
		    public void M()
		    {
		        var value = new
		        {
		            Alpha = 1,
		            Beta = 2
		        };
		    }
		}
		""",
		"""
		public class C
		{
		    public void M()
		    {
		        var value = new
		        {
		            Alpha = 1,
		            Beta = 2
		        };
		    }
		}
		""",
		"""
		public class C
		{
		    public void M()
		    {
		        var value = new
		        {
		            Alpha = 1,
		            Beta = 2,
		        };
		    }
		}
		""",
		Multiline);

	[Test]
	public Task A_list_that_stays_on_one_line_does_not() => Unchanged(
		// `multiline` means what it says. The flat rendering is the other option's business.
		"""
		public class C
		{
		    private static readonly int[] Values = new[] { 1, 2 };
		}
		""",
		editorConfig: Multiline);

	[Test]
	public Task The_decision_follows_the_printed_layout_not_the_source() => Formats(
		// This list arrives on one line and leaves on several, and the comma follows the layout it ends
		// up with. Whether it breaks is reflow's answer, taken on this same run, which is why the comma
		// goes in as an IfBreak against the list's own group rather than being chosen from the source.
		"""
		public class C
		{
		    private static readonly string[] Values = new[] { "alpha", "beta", "gamma", "delta" };
		}
		""",
		"""
		public class C
		{
		    private static readonly string[] Values = new[]
		    {
		        "alpha",
		        "beta",
		        "gamma",
		        "delta",
		    };
		}
		""",
		editorConfig: "max_line_length = 60\n" + Multiline);

	// ---- singleline -------------------------------------------------------------------------------

	[Test]
	public Task The_singleline_option_covers_the_flat_rendering() => WithAndWithout(
		"""
		public class C
		{
		    private static readonly int[] Values = new[] { 1, 2 };
		}
		""",
		"""
		public class C
		{
		    private static readonly int[] Values = new[] { 1, 2 };
		}
		""",
		"""
		public class C
		{
		    private static readonly int[] Values = new[] { 1, 2, };
		}
		""",
		Singleline);

	[Test]
	public Task The_two_options_are_independent() => Formats(
		// Singleline alone: flat lists get one, broken ones lose theirs. An odd state to want, but it
		// is one ReSharper allows, and it is the reason these are two keys rather than one.
		"""
		public class C
		{
		    private static readonly int[] Flat = new[] { 1, 2 };
		    private static readonly int[] Broken = new[]
		    {
		        1,
		        2,
		    };
		}
		""",
		"""
		public class C
		{
		    private static readonly int[] Flat = new[] { 1, 2, };
		    private static readonly int[] Broken = new[]
		    {
		        1,
		        2
		    };
		}
		""",
		editorConfig: Singleline);

	[Test]
	public Task Both_together_mean_always() => Formats(
		"""
		public class C
		{
		    private static readonly int[] Flat = new[] { 1, 2 };
		    private static readonly int[] Broken = new[]
		    {
		        1,
		        2
		    };
		}
		""",
		"""
		public class C
		{
		    private static readonly int[] Flat = new[] { 1, 2, };
		    private static readonly int[] Broken = new[]
		    {
		        1,
		        2,
		    };
		}
		""",
		editorConfig: Multiline + "\n" + Singleline);

	[Test]
	public Task Setting_it_false_removes_one_the_author_wrote() => Formats(
		// Explicitly false is an instruction, not an absence. This is the direction that makes the rule
		// canonicalising rather than merely permissive: with the key set either way, the two spellings
		// of the same list converge on one.
		"""
		public class C
		{
		    private static readonly int[] Values = new[]
		    {
		        1,
		        2,
		    };
		}
		""",
		"""
		public class C
		{
		    private static readonly int[] Values = new[]
		    {
		        1,
		        2
		    };
		}
		""",
		editorConfig: "csharp_trailing_comma_in_multiline_lists = false\n" + Singleline);

	// ---- the constructs that forbid one -----------------------------------------------------------

	[Test]
	public Task An_argument_list_never_gets_one() => Formats(
		// `Call(a, b,)` does not compile. Both keys are on and the list is broken, which is exactly the
		// condition that produces a comma everywhere it is legal.
		"""
		public class C
		{
		    public void M()
		    {
		        Call(argumentOne, argumentTwo, argumentThree, argumentFour);
		    }
		}
		""",
		"""
		public class C
		{
		    public void M()
		    {
		        Call(
		            argumentOne,
		            argumentTwo,
		            argumentThree,
		            argumentFour
		        );
		    }
		}
		""",
		editorConfig: "max_line_length = 40\n" + Multiline + "\n" + Singleline);

	[Test]
	public Task A_parameter_list_never_gets_one() => Formats(
		"""
		public class C
		{
		    public void Method(int parameterOne, string parameterTwo, bool three)
		    {
		    }
		}
		""",
		"""
		public class C
		{
		    public void Method(
		        int parameterOne,
		        string parameterTwo,
		        bool three
		    )
		    { }
		}
		""",
		editorConfig: "max_line_length = 40\n" + Multiline + "\n" + Singleline);

	[Test]
	public Task A_tuple_never_gets_one() => Unchanged(
		"""
		public class C
		{
		    public (int First, int Second) M() => (1, 2);
		}
		""",
		editorConfig: Multiline + "\n" + Singleline);

	[Test]
	public Task A_type_argument_list_never_gets_one() => Unchanged(
		"""
		public class C
		{
		    private Dictionary<string, int> Map = null;
		}
		""",
		editorConfig: Multiline + "\n" + Singleline);

	// ---- comments ---------------------------------------------------------------------------------

	[Test]
	public Task A_trailing_comma_carrying_a_comment_is_left_as_written() => Unchanged(
		// Rewriting the comma means dropping the token, and dropping it takes its trivia along. A comma
		// is not worth losing a comment over, so such a list is printed as it arrived.
		"""
		public class C
		{
		    private static readonly int[] Values = new[]
		    {
		        1,
		        2, // last for now
		    };
		}
		""",
		editorConfig: "csharp_trailing_comma_in_multiline_lists = false");
}
