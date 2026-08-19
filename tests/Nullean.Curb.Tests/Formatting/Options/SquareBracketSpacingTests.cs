namespace Nullean.Curb.Tests.Formatting.Options;

/// <summary>
/// <c>csharp_space_before_open_square_brackets</c>,
/// <c>csharp_space_between_empty_square_brackets</c> and
/// <c>csharp_space_between_square_brackets</c>.
/// </summary>
/// <remarks>
/// <para>
/// The three are independent and reach different sets of brackets. <c>before_open</c> covers
/// brackets that attach to something — array types and creations, element access, indexer
/// declarations — and skips those that start an expression: an attribute list, a collection
/// expression, a list pattern. <c>between_square</c> is the reverse on the last two: a collection
/// expression and a list pattern do take inner spacing.
/// </para>
/// <para>
/// <c>between_empty</c> covers brackets holding nothing, and a multi-dimensional rank counts as
/// empty even though it holds commas — <c>int[,]</c> becomes <c>int[ , ]</c>. Those commas are not
/// reached by the comma options, which was confirmed by setting them the other way round.
/// </para>
/// </remarks>
public class SquareBracketSpacingTests : FormattingTest
{
	// ---- defaults --------------------------------------------------------------------------------

	[Test]
	public Task Brackets_hug_everything_by_default() => Formats(
		"""
		public class C
		{
		    private int [ ] _a = new int [ 1 ];

		    public void M()
		    {
		        var v = _a [ 0 ];
		    }
		}
		""",
		"""
		public class C
		{
		    private int[] _a = new int[1];

		    public void M()
		    {
		        var v = _a[0];
		    }
		}
		""");

	// ---- csharp_space_before_open_square_brackets ------------------------------------------------

	[Test]
	public Task A_space_can_precede_the_bracket_of_a_type_or_access() => Formats(
		"""
		public class C
		{
		    private int[] _a = new int[] { 1, 2 };
		    private int[,] _b = new int[2, 3];
		    private int[][] _c;

		    public void M(params object[] rest)
		    {
		        var v = _a[0];
		        var q = new[] { 1, 2 };
		    }

		    public int this[int i] => 0;
		}
		""",
		"""
		public class C
		{
		    private int [] _a = new int [] { 1, 2 };
		    private int [,] _b = new int [2, 3];
		    private int [] [] _c;

		    public void M(params object [] rest)
		    {
		        var v = _a [0];
		        var q = new [] { 1, 2 };
		    }

		    public int this [int i] => 0;
		}
		""",
		editorConfig: "csharp_space_before_open_square_brackets = true");

	[Test]
	public Task Brackets_that_start_an_expression_keep_their_position() => Formats(
		"""
		public class C
		{
		    [System.Obsolete]
		    public void M()
		    {
		        int[] e = [1, 2];
		        if (arr is [1, 2])
		        {
		        }
		    }
		}
		""",
		// The attribute list, the collection expression and the list pattern all keep their
		// bracket; only `int[]`, which is an array type, takes the space.
		"""
		public class C
		{
		    [System.Obsolete]
		    public void M()
		    {
		        int [] e = [1, 2];
		        if (arr is [1, 2])
		        {
		        }
		    }
		}
		""",
		editorConfig: "csharp_space_before_open_square_brackets = true");

	// ---- csharp_space_between_empty_square_brackets ----------------------------------------------

	[Test]
	public Task Empty_brackets_can_be_spaced_open() => Formats(
		"""
		public class C
		{
		    private int[] _a = new int[] { 1, 2 };
		    private int[][] _c;

		    public void M(params object[] rest)
		    {
		        var q = new[] { 1, 2 };
		    }
		}
		""",
		"""
		public class C
		{
		    private int[ ] _a = new int[ ] { 1, 2 };
		    private int[ ][ ] _c;

		    public void M(params object[ ] rest)
		    {
		        var q = new[ ] { 1, 2 };
		    }
		}
		""",
		editorConfig: "csharp_space_between_empty_square_brackets = true");

	[Test]
	public Task A_multidimensional_rank_counts_as_empty() => Formats(
		"""
		public class C
		{
		    private int[,] _b;
		    private int[,,] _d;
		}
		""",
		"""
		public class C
		{
		    private int[ , ] _b;
		    private int[ , , ] _d;
		}
		""",
		editorConfig: "csharp_space_between_empty_square_brackets = true");

	[Test]
	public Task The_comma_options_do_not_reach_an_empty_rank() => Formats(
		"""
		public class C
		{
		    private int[,] _b;
		}
		""",
		"""
		public class C
		{
		    private int[ , ] _b;
		}
		""",
		editorConfig: """
		csharp_space_between_empty_square_brackets = true
		csharp_space_after_comma = false
		csharp_space_before_comma = true
		""");

	[Test]
	public Task A_sized_rank_is_not_empty() => Formats(
		"""
		public class C
		{
		    private int[] _a = new int[2, 3];
		}
		""",
		// `new int[2, 3]` holds sizes so it is left alone; the declaration's own `int[]` does not.
		"""
		public class C
		{
		    private int[ ] _a = new int[2, 3];
		}
		""",
		editorConfig: "csharp_space_between_empty_square_brackets = true");

	// ---- csharp_space_between_square_brackets ----------------------------------------------------

	[Test]
	public Task Occupied_brackets_can_be_spaced_open() => Formats(
		"""
		public class C
		{
		    private int[] _a = new int[2, 3];

		    public void M()
		    {
		        var v = _a[0];
		        var w = _a[1, 2];
		    }

		    public int this[int i] => 0;
		}
		""",
		"""
		public class C
		{
		    private int[] _a = new int[ 2, 3 ];

		    public void M()
		    {
		        var v = _a[ 0 ];
		        var w = _a[ 1, 2 ];
		    }

		    public int this[ int i ] => 0;
		}
		""",
		editorConfig: "csharp_space_between_square_brackets = true");

	[Test]
	public Task Collection_expressions_and_list_patterns_take_inner_spacing() => Formats(
		"""
		public class C
		{
		    public void M()
		    {
		        int[] e = [1, 2];
		        if (arr is [1, 2])
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
		        int[] e = [ 1, 2 ];
		        if (arr is [ 1, 2 ])
		        {
		        }
		    }
		}
		""",
		editorConfig: "csharp_space_between_square_brackets = true");

	[Test]
	public Task Empty_brackets_are_not_touched_by_the_occupied_option() => Unchanged(
		"""
		public class C
		{
		    private int[] _a;
		    private int[,] _b;

		    public void M()
		    {
		        var q = new[] { 1 };
		    }
		}
		""",
		editorConfig: "csharp_space_between_square_brackets = true");

	// ---- interaction -----------------------------------------------------------------------------

	[Test]
	public Task All_three_together() => Formats(
		"""
		public class C
		{
		    private int[] _a = new int[2, 3];
		    private int[,] _b;

		    public void M()
		    {
		        var v = _a[0];
		    }
		}
		""",
		"""
		public class C
		{
		    private int [ ] _a = new int [ 2, 3 ];
		    private int [ , ] _b;

		    public void M()
		    {
		        var v = _a [ 0 ];
		    }
		}
		""",
		editorConfig: """
		csharp_space_before_open_square_brackets = true
		csharp_space_between_empty_square_brackets = true
		csharp_space_between_square_brackets = true
		""");

	[Test]
	public Task Inner_spacing_does_not_stop_a_collection_from_wrapping() => Formats(
		"""
		public class C
		{
		    public void M()
		    {
		        int[] e = [firstElement, secondElement, thirdElement];
		    }
		}
		""",
		"""
		public class C
		{
		    public void M()
		    {
		        int[] e = [
		            firstElement,
		            secondElement,
		            thirdElement
		        ];
		    }
		}
		""",
		editorConfig: """
		max_line_length = 40
		csharp_space_between_square_brackets = true
		""");
}
