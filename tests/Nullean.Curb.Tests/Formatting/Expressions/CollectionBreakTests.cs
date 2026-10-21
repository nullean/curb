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
	/// A bracket alone on its line below the <c>=</c> is the brace placement
	/// <c>csharp_new_line_before_open_brace</c> asks for, so it sits at the declaration's level rather
	/// than one in. <c>dotnet format</c> accepts either indent; Curb has one answer for each shape.
	/// </summary>
	[Test]
	public Task An_opened_out_collection_below_the_equals_takes_the_brace_position() => Formats(
		"""
		public class C
		{
		    private int[] _numbers =
		        [
		            1,
		            2,
		        ];
		}
		""",
		"""
		public class C
		{
		    private int[] _numbers =
		    [
		        1,
		        2,
		    ];
		}
		""");

	[Test]
	public Task Without_a_brace_line_an_opened_out_collection_stays_one_indent_in() => Unchanged(
		"""
		public class C {
		    private int[] _numbers =
		        [
		            1,
		            2,
		        ];

		    private int[] _array =
		        {
		            1,
		            2,
		        };
		}
		""",
		editorConfig: "csharp_new_line_before_open_brace = none");

	[Test]
	public Task Values_below_the_equals_indent_with_tabs() => Unchanged(
		"public class C\n{\n\tprivate int[] _numbers =\n\t\t[1, 2, 3];\n\n\tprivate Action _callback =\n\t\t() =>\n\t\t{\n\t\t\tRun();\n\t\t};\n}",
		editorConfig: "indent_style = tab");

	[Test]
	public Task A_collection_the_author_broke_partway_keeps_its_brackets_on_the_equals_line() => Formats(
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
		""",
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
	public Task An_array_initializer_broken_inside_an_element_is_not_opened_out() => Formats(
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
		""",
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
}
