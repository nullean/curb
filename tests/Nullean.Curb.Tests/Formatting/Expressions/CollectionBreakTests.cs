namespace Nullean.Curb.Tests.Formatting.Expressions;

/// <summary>
/// Breaks the author placed around collection expressions, initializers and block lambdas stay where
/// they are with no width set, as <c>dotnet format whitespace</c> leaves them. See
/// <see href="https://github.com/nullean/curb/issues/107">issue #107</see> for a value below the
/// <c>=</c> and <see href="https://github.com/nullean/curb/issues/113">issue #113</see> for a
/// collection broken partway through.
/// </summary>
public class CollectionBreakTests : FormattingTest
{
	[Test]
	public Task Values_that_bring_their_own_brackets_stay_below_a_field_equals() => Unchanged(
		"""
		public class C
		{
		    private int[] _numbers =
		        [1, 2, 3];

		    private Options _options =
		        new Options { Retries = 3 };

		    private Action _callback =
		        () =>
		        {
		            Run();
		        };
		}
		""");

	[Test]
	public Task Values_that_bring_their_own_brackets_stay_below_a_local_equals() => Unchanged(
		"""
		public class C
		{
		    public void M()
		    {
		        int[] numbers =
		            [1, 2, 3];
		        var options =
		            new Options { Retries = 3 };
		        var flags =
		            new[] { true, false };
		        int[] array =
		            { 1, 2, 3 };
		        var anonymous =
		            new { A = 1 };
		        Action callback =
		            () =>
		            {
		                Run();
		            };
		    }
		}
		""");

	[Test]
	public Task Values_that_bring_their_own_brackets_stay_below_an_assignment() => Unchanged(
		"""
		public class C
		{
		    public void M()
		    {
		        numbers =
		            [4, 5];
		        options =
		            new Options { Retries = 3 };
		        callback =
		            () =>
		            {
		                Run();
		            };
		    }
		}
		""");

	[Test]
	public Task An_opened_out_creation_below_the_equals_keeps_its_braces_under_new() => Unchanged(
		"""
		public class C
		{
		    private Options _options =
		        new Options
		        {
		            Retries = 3,
		        };

		    private List<int> _list =
		        new List<int>
		        {
		            1,
		            2,
		        };
		}
		""");

	[Test]
	public Task A_trailing_comment_after_the_equals_keeps_the_value_below() => Unchanged(
		"""
		public class C
		{
		    private int[] _numbers = // the defaults
		        [1, 2, 3];
		}
		""");

	[Test]
	public Task An_array_initializer_brace_below_the_equals_keeps_its_own_line() => Unchanged(
		"""
		public class C
		{
		    private static readonly int[] Numbers =
		    {
		        1,
		        2,
		    };
		}
		""");

	/// <summary>
	/// A bracket below the <c>=</c> stays at whichever level the author put it, as <c>dotnet format</c>
	/// leaves it: the declaration's own, one level in, or deeper.
	/// </summary>
	[Test]
	public Task An_opened_out_collection_below_the_equals_keeps_the_level_the_author_gave_it() => Unchanged(
		"""
		public class C
		{
		    private int[] _first =
		    [
		        1,
		        2,
		    ];

		    private int[] _second =
		        [
		            1,
		            2,
		        ];

		    private int[] _third =
		            [
		                1,
		            ];
		}
		""");

	[Test]
	public Task Values_below_the_equals_keep_a_deeper_indent() => Unchanged(
		"""
		public class C
		{
		    private int[] _numbers =
		            [1, 2, 3];

		    private Options _options =
		            new Options
		            {
		                Retries = 3,
		            };

		    private int _count =
		            Compute();
		}
		""");

	/// <summary>
	/// <c>dotnet format</c> does not apply <c>csharp_new_line_before_open_brace</c> to a collection
	/// expression or to an array or collection initializer, so each bracket stays where the author put it.
	/// </summary>
	[Test]
	public Task Without_a_brace_line_collection_brackets_stay_where_the_author_put_them() => Unchanged(
		"""
		public class C {
		    private int[] _numbers =
		        [
		            1,
		            2,
		        ];

		    private int[] _first =
		    [
		        1,
		    ];

		    private int[] _array =
		        {
		            1,
		            2,
		        };

		    private List<int> _list = new List<int>
		    {
		        1,
		    };
		}
		""",
		editorConfig: "csharp_new_line_before_open_brace = none");

	[Test]
	public Task Values_below_the_equals_indent_with_tabs() => Unchanged(
		"public class C\n{\n\tprivate int[] _numbers =\n\t\t[1, 2, 3];\n\n\tprivate Action _callback =\n\t\t() =>\n\t\t{\n\t\t\tRun();\n\t\t};\n}",
		editorConfig: "indent_style = tab");

	[Test]
	public Task A_collection_the_author_broke_partway_keeps_its_brackets_on_the_equals_line() => Unchanged(
		"""
		public class C
		{
		    public int[] M()
		    {
		        int[] a = [1,
		                2];
		        return a;
		    }
		}
		""");

	[Test]
	public Task Collection_continuation_lines_keep_the_column_the_author_gave_them() => Unchanged(
		"""
		public class C
		{
		    public void M()
		    {
		        int[] a = [1,
		            2];
		        int[] b = [1,
		                   2,
		                   3];
		        int[] c = [
		                1,
		              2,
		            ];
		    }
		}
		""");

	[Test]
	public Task A_reindented_statement_takes_its_collection_lines_along() => Formats(
		"""
		public class C
		{
		    public void M()
		    {
		          int[] a = [1,
		                     2];
		    int[] b =
		        [
		            1,
		        ];
		    }
		}
		""",
		"""
		public class C
		{
		    public void M()
		    {
		        int[] a = [1,
		                   2];
		        int[] b =
		            [
		                1,
		            ];
		    }
		}
		""");

	[Test]
	public Task Aligned_collection_lines_keep_their_column_with_tabs() => Unchanged(
		"public class C\n{\n\tpublic void M()\n\t{\n\t\tint[] a = [1,\n\t\t\t\t   2];\n\t\tint[] b =\n\t\t\t[\n\t\t\t\t1,\n\t\t\t];\n\t}\n}",
		editorConfig: "indent_style = tab");

	[Test]
	public Task A_nested_aligned_collection_keeps_its_columns() => Unchanged(
		"""
		public class C
		{
		    public void M()
		    {
		        int[][] a = [[1,
		                      2],
		                     [3]];
		        Use([1,
		             2]);
		    }
		}
		""");

	[Test]
	public Task A_collection_keeps_each_break_where_the_author_put_it() => Unchanged(
		"""
		public class C
		{
		    public void M()
		    {
		        int[] a = [
		            1, 2];
		        int[] b = [1, 2
		        ];
		        int[] c = [1,
		            2,];
		        int[] d = [
		            1,
		            2
		        ];
		        a = [1,
		            2];
		    }
		}
		""");

	[Test]
	public Task A_nested_collection_keeps_its_partial_break() => Unchanged(
		"""
		public class C
		{
		    public void M()
		    {
		        int[][] a = [[1,
		                2], [3]];
		    }
		}
		""");

	[Test]
	public Task Comments_between_collection_elements_add_no_lines() => Unchanged(
		"""
		public class C
		{
		    public void M()
		    {
		        int[] a = [1, // one
		            2];
		        int[] b = [1,
		            // two
		            2];
		        int[] c = [1, /* one */
		            2];
		    }
		}
		""");

	[Test]
	public Task A_partially_broken_collection_indents_with_tabs() => Unchanged(
		"public class C\n{\n\tpublic void M()\n\t{\n\t\tint[] a = [1,\n\t\t\t2];\n\t}\n}",
		editorConfig: "indent_style = tab");

	[Test]
	public Task An_array_initializer_broken_inside_an_element_is_not_opened_out() => Unchanged(
		"""
		public class C
		{
		    public bool[] Flags(bool a, bool b)
		    {
		        var flags = new[] { a ||
		                            b };
		        return flags;
		    }
		}
		""");

	[Test]
	public Task Initializer_continuation_lines_keep_the_column_the_author_gave_them() => Unchanged(
		"""
		public class C
		{
		    public void M()
		    {
		        var a = new[] { 1,
		                        2 };
		        var b = new List<int>
		          {
		               1,
		          };
		        var c = new[] {
		                1,
		           };
		    }

		    private static readonly int[] F =
		        {
		            1,
		          2,
		        };
		}
		""");

	[Test]
	public Task Array_and_collection_initializers_keep_each_break_where_the_author_put_it() => Unchanged(
		"""
		public class C
		{
		    public void M()
		    {
		        var a = new[] { 1,
		            2 };
		        var b = new List<int> { 1,
		            2 };
		        int[] c = { 1,
		            2 };
		        var d = new[] { 1, // one
		            2 };
		        var e = new[] { new[] { 1,
		                2 }, new[] { 3 } };
		    }
		}
		""");

	/// <summary>
	/// <c>dotnet format</c> opens out an object initializer that spans lines, so this one still does.
	/// </summary>
	[Test]
	public Task An_object_initializer_broken_inside_a_member_is_still_opened_out() => Formats(
		"""
		public class C
		{
		    public void M(bool a, bool b)
		    {
		        var options = new Options { Flag = a ||
		            b };
		    }
		}
		""",
		"""
		public class C
		{
		    public void M(bool a, bool b)
		    {
		        var options = new Options
		        {
		            Flag = a ||
		            b
		        };
		    }
		}
		""");

	[Test]
	public Task Deterministic_layout_still_joins_what_fits() => Formats(
		"""
		public class C
		{
		    private int[] _numbers =
		        [1, 2, 3];

		    private Options _options =
		        new Options { Retries = 3 };

		    public void M()
		    {
		        int[] a = [1,
		            2];
		        var b = new[] { 1,
		            2 };
		    }
		}
		""",
		"""
		public class C
		{
		    private int[] _numbers = [1, 2, 3];

		    private Options _options = new Options { Retries = 3 };

		    public void M()
		    {
		        int[] a = [1, 2];
		        var b = new[] { 1, 2 };
		    }
		}
		""",
		editorConfig: "max_line_length = 120");

	/// <summary>
	/// A collection that opens on the <c>=&gt;</c> line stays there, with its elements one level in and its
	/// closing bracket at the member's level. See
	/// <see href="https://github.com/nullean/curb/issues/126">issue #126</see>.
	/// </summary>
	[Test]
	public Task A_collection_on_an_expression_body_arrow_line_stays_there() => Unchanged(
		"""
		public class C
		{
		    public int[] L() => [
		        1,
		        2,
		    ];

		    public int[] Short() => [1, 2];

		    public int[] Values => [
		        1,
		        2,
		    ];
		}
		""");

	[Test]
	public Task A_collection_on_a_lambda_or_switch_arm_arrow_line_stays_there() => Unchanged(
		"""
		public class C
		{
		    public void M(int k)
		    {
		        Func<int[]> f = () => [
		            1,
		            2,
		        ];
		        var x = k switch
		        {
		            0 => [
		                1,
		            ],
		            1 => [1, 2],
		            _ => new[] {
		                3,
		            },
		        };
		        Use(() => [
		            1,
		        ]);
		    }
		}
		""");

	[Test]
	public Task Collections_and_initializers_on_an_arrow_line_keep_their_breaks() => Unchanged(
		"""
		public class C
		{
		    public int[] Partial() => [1,
		        2];

		    public int[] Below() =>
		        [
		            1,
		        ];

		    public int[] Array() => new[] {
		        1,
		    };

		    public List<int> List() => new List<int>
		    {
		        1,
		    };
		}
		""");

	[Test]
	public Task A_collection_on_an_arrow_line_indents_with_tabs() => Unchanged(
		"public class C\n{\n\tpublic int[] L() => [\n\t\t1,\n\t\t2,\n\t];\n\n\tpublic void M()\n\t{\n\t\tFunc<int[]> f = () => [\n\t\t\t1,\n\t\t];\n\t}\n}",
		editorConfig: "indent_style = tab");

	/// <summary>
	/// <c>dotnet format</c> aligns a comment under a trailing comment above it in a statement or an
	/// argument list, but leaves one between collection elements where it is.
	/// </summary>
	[Test]
	public Task A_comment_under_a_trailing_comment_between_elements_stays_where_it_is() => Unchanged(
		"""
		public class C
		{
		    public int[] Values => [
		        1, // one
		        // two
		        2,
		    ];

		    public void M()
		    {
		        int[] a = [1, // one
		                   // two
		                   2];
		        var b = new[] {
		            1, // one
		            // two
		            2,
		        };
		        var c = new List<int>
		        {
		            1, // one
		            // two
		            2,
		        };
		    }
		}
		""");

	[Test]
	public Task A_trailing_comment_after_the_arrow_adds_no_blank_line() => Unchanged(
		"""
		public class C
		{
		    public int[] Values() => // the defaults
		        [1, 2];

		    public void M()
		    {
		        Func<int[]> f = () => // the defaults
		            [1, 2];
		    }
		}
		""");

	[Test]
	public Task A_switch_arm_value_below_the_arrow_stays_there() => Unchanged(
		"""
		public class C
		{
		    public int[] M(int k) => k switch
		    {
		        0 => // zero
		            [1],
		        1 =>
		            [2, 3],
		        _ => [],
		    };
		}
		""");
}
