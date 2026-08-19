namespace Nullean.Curb.Tests.Formatting.Options;

/// <summary>
/// The two <c>= ignore</c> settings: <c>csharp_space_around_binary_operators</c> and
/// <c>csharp_space_around_declaration_statements</c>.
/// </summary>
/// <remarks>
/// <para>
/// The only settings that ask Curb not to format. The construct is emitted from the source
/// verbatim, so alignment the author put in survives and nothing inside it is reflowed however
/// narrow <c>max_line_length</c> is.
/// </para>
/// <para>
/// Note that <c>csharp_space_around_declaration_statements</c>' other value is <c>false</c>, not
/// <c>true</c>: the choice is between normalising the spacing and reproducing it, never between
/// adding and removing spaces.
/// </para>
/// </remarks>
public class IgnoreSpacingTests : FormattingTest
{
	private const string Ragged = """
		public class C
		{
		    public void M()
		    {
		        int      x     =     1;
		        x = x    +     2;
		        var b = x   >   1   &&   x   <   9;
		        int y=1;
		        y=y+1;
		    }

		    private int      _field     =     3;
		}
		""";

	// ---- csharp_space_around_binary_operators = ignore --------------------------------------------

	[Test]
	public Task Ignore_leaves_operator_spacing_exactly_as_written() => Formats(
		Ragged,
		// The declarations normalise — that is the other option's business, and it is at its
		// default — while everything around an operator is reproduced.
		"""
		public class C
		{
		    public void M()
		    {
		        int x = 1;
		        x = x    +     2;
		        var b = x   >   1   &&   x   <   9;
		        int y = 1;
		        y=y+1;
		    }

		    private int _field = 3;
		}
		""",
		editorConfig: "csharp_space_around_binary_operators = ignore");

	[Test]
	public Task Ignore_covers_assignment_as_well_as_binary_operators() => Unchanged(
		"""
		public class C
		{
		    public void M()
		    {
		        y=y+1;
		        z    =    z    *    2;
		    }
		}
		""",
		editorConfig: "csharp_space_around_binary_operators = ignore");

	[Test]
	public Task Ignore_beats_the_line_width() => Unchanged(
		"""
		public class C
		{
		    public void M()
		    {
		        result = firstOperand    +    secondOperand    +    thirdOperand;
		    }
		}
		""",
		editorConfig: """
		max_line_length = 40
		csharp_space_around_binary_operators = ignore
		""");

	// ---- csharp_space_around_declaration_statements = ignore --------------------------------------

	[Test]
	public Task Ignore_leaves_a_declaration_exactly_as_written() => Formats(
		Ragged,
		// The whole declaration is reproduced, initializer included — so the operators inside
		// `var b = x   >   1` survive too, while the expression statements normalise.
		"""
		public class C
		{
		    public void M()
		    {
		        int      x     =     1;
		        x = x + 2;
		        var b = x   >   1   &&   x   <   9;
		        int y=1;
		        y = y + 1;
		    }

		    private int      _field     =     3;
		}
		""",
		editorConfig: "csharp_space_around_declaration_statements = ignore");

	[Test]
	public Task An_aligned_column_of_fields_survives() => Unchanged(
		"""
		public class C
		{
		    private readonly int    _first  = 1;
		    private readonly string _second = "two";
		    private readonly bool   _third  = true;
		}
		""",
		editorConfig: "csharp_space_around_declaration_statements = ignore");

	// ---- the default is to normalise ---------------------------------------------------------------

	[Test]
	public Task False_normalises_everything() => Formats(
		Ragged,
		"""
		public class C
		{
		    public void M()
		    {
		        int x = 1;
		        x = x + 2;
		        var b = x > 1 && x < 9;
		        int y = 1;
		        y = y + 1;
		    }

		    private int _field = 3;
		}
		""",
		editorConfig: "csharp_space_around_declaration_statements = false");

	[Test]
	public Task The_default_matches_false() => Formats(
		Ragged,
		"""
		public class C
		{
		    public void M()
		    {
		        int x = 1;
		        x = x + 2;
		        var b = x > 1 && x < 9;
		        int y = 1;
		        y = y + 1;
		    }

		    private int _field = 3;
		}
		""");

	[Test]
	public Task True_is_not_a_value_this_option_takes() => Formats(
		Ragged,
		// Reported and the default kept, rather than guessed at.
		"""
		public class C
		{
		    public void M()
		    {
		        int x = 1;
		        x = x + 2;
		        var b = x > 1 && x < 9;
		        int y = 1;
		        y = y + 1;
		    }

		    private int _field = 3;
		}
		""",
		editorConfig: "csharp_space_around_declaration_statements = true");

	// ---- the two together ---------------------------------------------------------------------------

	[Test]
	public Task Both_ignored_leaves_the_whole_method_alone() => Unchanged(
		"""
		public class C
		{
		    public void M()
		    {
		        int      x     =     1;
		        x = x    +     2;
		        y=y+1;
		    }
		}
		""",
		editorConfig: """
		csharp_space_around_binary_operators = ignore
		csharp_space_around_declaration_statements = ignore
		""");

	[Test]
	public Task Ignoring_does_not_stop_the_surrounding_indent_being_fixed() => Formats(
		"""
		public class C
		{
		public void M()
		{
		int      x     =     1;
		}
		}
		""",
		"""
		public class C
		{
		    public void M()
		    {
		        int      x     =     1;
		    }
		}
		""",
		editorConfig: "csharp_space_around_declaration_statements = ignore");
}
