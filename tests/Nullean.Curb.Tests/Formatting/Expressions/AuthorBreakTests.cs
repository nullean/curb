namespace Nullean.Curb.Tests.Formatting.Expressions;

/// <summary>
/// Breaks the author placed inside an expression stay where they are with no width set, as
/// <c>dotnet format whitespace</c> leaves them. See
/// <see href="https://github.com/nullean/curb/issues/102">issue #102</see>.
/// </summary>
public class AuthorBreakTests : FormattingTest
{
	[Test]
	public Task A_break_after_a_field_initializer_equals_is_kept() => Unchanged(
		"""
		public class C
		{
		    private readonly ConcurrentDictionary<string, StringComparer> _comparers =
		        new(StringComparer.Ordinal);
		}
		""");

	[Test]
	public Task A_break_after_a_property_initializer_equals_is_kept() => Unchanged(
		"""
		public class C
		{
		    public IReadOnlyList<string> Names { get; } =
		        new List<string>();
		}
		""");

	[Test]
	public Task A_break_after_a_local_initializer_equals_is_kept() => Unchanged(
		"""
		public class C
		{
		    public void M()
		    {
		        var comparer =
		            StringComparer.Ordinal;
		    }
		}
		""");

	[Test]
	public Task A_break_after_an_assignment_equals_is_kept() => Unchanged(
		"""
		public class C
		{
		    public void M()
		    {
		        _comparer =
		            StringComparer.Ordinal;
		    }
		}
		""");

	[Test]
	public Task A_joined_initializer_stays_joined() => Unchanged(
		"""
		public class C
		{
		    private readonly ConcurrentDictionary<string, StringComparer> _comparers = new(StringComparer.Ordinal);
		}
		""");

	[Test]
	public Task An_attribute_argument_list_broken_one_per_line_is_kept() => Unchanged(
		"""
		public class C
		{
		    [SuppressMessage(
		        "Category",
		        "CA0000",
		        Justification = "A justification that is long enough that an author chose to wrap it.")]
		    public void M()
		    {
		    }
		}
		""");

	[Test]
	public Task An_attribute_argument_list_broken_after_its_first_argument_is_kept() => Unchanged(
		"""
		public class C
		{
		    [SuppressMessage("Category", "CA0000",
		        Justification = "A justification that is long enough that an author chose to wrap it.")]
		    public void M()
		    {
		    }
		}
		""");

	[Test]
	public Task A_joined_attribute_argument_list_stays_joined() => Unchanged(
		"""
		public class C
		{
		    [SuppressMessage("Category", "CA0000", Justification = "Short.")]
		    public void M()
		    {
		    }
		}
		""");

	[Test]
	public Task A_ternary_broken_before_its_operators_is_kept() => Unchanged(
		"""
		public class C
		{
		    public static string Ternary(bool condition, string path)
		    {
		        var result = condition
		            ? path.Trim()
		            : path.ToUpperInvariant();
		        return result;
		    }
		}
		""");

	[Test]
	public Task A_ternary_broken_after_its_operators_is_kept() => Unchanged(
		"""
		public class C
		{
		    public static string Ternary(bool condition, string path)
		    {
		        var result = condition ?
		            path.Trim() :
		            path.ToUpperInvariant();
		        return result;
		    }
		}
		""");

	[Test]
	public Task A_ternary_broken_only_before_its_colon_is_kept() => Unchanged(
		"""
		public class C
		{
		    public static string Ternary(bool condition, string path)
		    {
		        return condition ? path.Trim()
		            : path.ToUpperInvariant();
		    }
		}
		""");

	[Test]
	public Task A_nested_ternary_keeps_every_break() => Unchanged(
		"""
		public class C
		{
		    public static string Ternary(int value)
		    {
		        return value < 0
		            ? "negative"
		            : value == 0
		                ? "zero"
		                : "positive";
		    }
		}
		""");

	[Test]
	public Task A_flat_ternary_chain_stays_flat() => Unchanged(
		// The `else if` style of chain: dotnet format leaves the last `:` under the others, where one
		// level in from the inner ternary made a staircase of it — issue #237.
		"""
		public class C
		{
		    public string M(int x)
		    {
		        return x == 0
		            ? "zero"
		            : x == 1 ? "one"
		            : "many";
		    }

		    public string N(int x) => x == 0
		        ? "zero"
		        : x == 1
		        ? "one"
		        : "many";
		}
		""");

	[Test]
	public Task A_ternary_keeps_the_indent_the_author_gave_its_continuation_lines() => Unchanged(
		// Past a broken condition, under an argument, behind `??`: dotnet format leaves each where it
		// was, and the ambient indent plus one level pulled them back — issue #237.
		"""
		public class C
		{
		    public string N(bool a, bool b, string s)
		    {
		        var v = a
		                && b
		                    ? "x"
		                    : "y";
		        Call(a
		                ? "a"
		              : "b", 1);
		        var w = s
		            ?? (a
		                    ? "a"
		                    : "b");
		        var z = a ?
		                    "a" :
		                        "b";
		        return v;
		    }
		}
		""");

	[Test]
	public Task A_ternary_branch_keeps_its_own_lines_in_from_its_continuation_line() => Unchanged(
		// A branch's own lines move with the `?` or `:` line it starts on: an initializer's braces, a
		// lambda's block, a call's arguments, a member chain's links and a switch expression's braces,
		// rather than falling back to the ambient indent. The `:` after the block still goes to the
		// statement's indent, where dotnet format puts it — issue #106.
		"""
		public class E
		{
		    public object A(bool a, bool b)
		    {
		        var v = a
		                && b
		                    ? new E
		                    {
		                        X = 1,
		                    }
		                    : null;
		        Func<int, int> f = a
		                ? y =>
		                {
		                    return y;
		                }
		        : y => y;
		        var w = a
		                    ? Call(
		                        1,
		                        2)
		                    : null;
		        var c = a
		                    ? items
		                        .Where(x => x)
		                        .Select(x => x)
		                    : x switch
		                    {
		                        1 => 2,
		                        _ => 3,
		                    };
		        return v;
		    }
		}
		""");

	[Test]
	public Task A_reindented_ternary_takes_its_continuation_lines_along() => Formats(
		"""
		public class C
		{
		    public string M(int x)
		    {
		          return x == 0
		              ? "zero"
		              : x == 1 ? "one"
		              : "many";
		    }
		}
		""",
		"""
		public class C
		{
		    public string M(int x)
		    {
		        return x == 0
		            ? "zero"
		            : x == 1 ? "one"
		            : "many";
		    }
		}
		""");

	[Test]
	public Task A_flat_ternary_chain_is_laid_out_by_width_when_one_is_set() => WithAndWithout(
		"""
		public class C
		{
		    public string M(int x)
		    {
		        return x == 0
		            ? "zero"
		            : x == 1 ? "one"
		            : "many";
		    }
		}
		""",
		"""
		public class C
		{
		    public string M(int x)
		    {
		        return x == 0
		            ? "zero"
		            : x == 1 ? "one"
		            : "many";
		    }
		}
		""",
		"""
		public class C
		{
		    public string M(int x)
		    {
		        return x == 0 ? "zero" : x == 1 ? "one" : "many";
		    }
		}
		""",
		"max_line_length = 80");

	[Test]
	public Task A_joined_ternary_stays_joined() => Unchanged(
		"""
		public class C
		{
		    public static string Ternary(bool condition, string path)
		    {
		        return condition ? path.Trim() : path.ToUpperInvariant();
		    }
		}
		""");

	[Test]
	public Task A_comment_after_the_equals_keeps_the_value_indented() => Unchanged(
		"""
		public class C
		{
		    private readonly int _x = // why
		        Compute();

		    public void M()
		    {
		        _y = // why
		            Compute();
		    }
		}
		""");

	[Test]
	public Task An_attribute_argument_list_keeps_a_comment_after_a_comma_without_a_blank_line() => Unchanged(
		"""
		public class C
		{
		    [SuppressMessage(
		        "Category", // the category
		        "CA0000")]
		    public void M()
		    {
		    }
		}
		""");

	[Test]
	public Task An_attribute_argument_list_keeps_its_closing_parenthesis_on_its_own_line() => Unchanged(
		"""
		public class C
		{
		    [SuppressMessage(
		        "Category",
		        "CA0000"
		    )]
		    public void M()
		    {
		    }
		}
		""");

	[Test]
	public Task A_broken_ternary_keeps_the_expression_body_on_the_arrow_line() => Unchanged(
		"""
		public class C
		{
		    public string Name => _flag
		        ? "a"
		        : "b";

		    public int M(bool b) => b
		        ? 1 // one
		        : 2;
		}
		""");

	[Test]
	public Task A_broken_ternary_indents_once_wherever_it_starts() => Unchanged(
		"""
		public class C
		{
		    public int M(bool b)
		    {
		        Call(b
		            ? 1
		            : 2);
		        Call(x, b
		            ? 1
		            : 2);
		        Call(
		            b
		                ? 1
		                : 2);
		        _x = b
		            ? 1
		            : 2;
		        return b
		            ? 1
		            : 2;
		    }
		}
		""");

	[Test]
	public Task A_broken_ternary_after_another_argument_on_a_line_of_its_own_indents_from_that_line() => Unchanged(
		"""
		public class C
		{
		    public int M(bool b)
		    {
		        Call(
		            a, b ? 1
		                : 2);
		        Call(x,
		            a, b
		                ? 1
		                : 2);
		    }
		}
		""");

	[Test]
	public Task A_broken_ternary_as_a_condition_indents_once() => Unchanged(
		// The condition's parentheses already indent it, so the ternary adds no level of its own, as a
		// broken && chain there adds none — issue #109.
		"""
		public class C
		{
		    public void M(bool b)
		    {
		        if (b
		            ? x
		            : y)
		        {
		        }
		        while (b ?
		            x :
		            y)
		        {
		        }
		        do
		        {
		        }
		        while (b
		            ? x
		            : y);
		        lock (b
		            ? x
		            : y)
		        {
		        }
		    }
		}
		""");

	[Test]
	public Task A_broken_ternary_in_parentheses_inside_a_condition_indents_once() => Unchanged(
		// Still on the `(` line, so the condition's own indent is the one level it needs. One starting on
		// a later line of the condition indents from that line. The `&&` chain at the end is the
		// control: it already printed this way.
		"""
		public class C
		{
		    public void M(bool b)
		    {
		        if (a && (b
		            ? x
		            : y))
		        {
		        }
		        if (a || (b ?
		            x :
		            y))
		        {
		        }
		        if (!(b
		            ? x
		            : y) && a)
		        {
		        }
		        while (a && (b
		            ? x
		            : y))
		        {
		        }
		        do
		        {
		        }
		        while (a && (b
		            ? x
		            : y));
		        if (a &&
		            (b
		                ? x
		                : y))
		        {
		        }
		        if (a && (b
		            && c))
		        {
		        }
		    }
		}
		""");

	[Test]
	public Task An_expression_body_on_the_arrow_line_keeps_its_own_continuation_indent() => Unchanged(
		"""
		public class C
		{
		    public int A => Call(
		        1,
		        2);
		    public int B => a +
		        b;
		    public Foo F => new Foo
		    {
		        A = 1
		    };
		    public int G => a
		        .B()
		        .C();
		    public int J => Call(() =>
		    {
		        Run();
		    });
		}
		""");

	[Test]
	public Task A_broken_lambda_or_switch_arm_body_stays_on_the_arrow_line() => Unchanged(
		"""
		public class C
		{
		    public void M(bool b)
		    {
		        Func<bool> g = () => x.A &&
		            x.B;
		        Func<int> h = () => Call(
		            x);
		        Func<Foo> i = () => new Foo
		        {
		            A = x
		        };
		        Func<int> k = () => b
		            ? 1
		            : 2;
		    }

		    public int N(int x, bool b) => x switch
		    {
		        1 => a &&
		            b,
		        2 => Call(
		            b),
		        3 => b
		            ? 1
		            : 2,
		        _ => 0,
		    };
		}
		""");

	// Issue #117: a value the author put on the line after `return` was joined onto it.
	[Test]
	public Task A_value_below_return_keeps_its_line_and_column() => Unchanged(
		"""
		public class C
		{
		    public int Simple(int value)
		    {
		        return
		            value * 2;
		    }

		    public int Deeper(int value)
		    {
		        return
		                value * 2;
		    }

		    public int Level(int value)
		    {
		        return
		        value * 2;
		    }
		}
		""");

	[Test]
	public Task A_chain_below_return_keeps_its_lines() => Unchanged(
		"""
		public class C
		{
		    public bool Leading(bool a, bool b)
		    {
		        return
		            a
		            && b;
		    }

		    public bool Trailing(bool a, bool b)
		    {
		        return
		            a &&
		            b;
		    }
		}
		""");

	[Test]
	public Task A_value_below_yield_return_or_throw_keeps_its_line() => Unchanged(
		"""
		public class C
		{
		    public IEnumerable<int> Yield(int value)
		    {
		        yield return
		            value;
		    }

		    public void Throw()
		    {
		        throw
		            new InvalidOperationException();
		    }
		}
		""");

	[Test]
	public Task A_call_below_return_keeps_its_argument_indent() => Unchanged(
		"""
		public class C
		{
		    public int Call(int value)
		    {
		        return
		            Math.Max(
		                value,
		                1);
		    }

		    public int Brackets(int value)
		    {
		        return
		            new[]
		            {
		                value,
		            }.Length;
		    }
		}
		""");

	// The comment before the first operand is the shape issue #115 left open: it and the operand
	// printed at the statement's column.
	[Test]
	public Task A_comment_before_a_value_below_return_keeps_its_column() => Unchanged(
		"""
		public class C
		{
		    public int Value(int value)
		    {
		        return
		            // why
		            value;
		    }

		    public bool Chain(bool a, bool b)
		    {
		        return
		            // why
		            a
		            && b;
		    }
		}
		""");

	[Test]
	public Task A_trailing_comment_after_return_keeps_the_value_at_its_column() => Unchanged(
		"""
		public class C
		{
		    public int Value(int value)
		    {
		        return // why
		            value;
		    }

		    public bool Chain(bool a, bool b)
		    {
		        return // why
		            a
		            && b;
		    }
		}
		""");

	[Test]
	public Task A_value_below_return_moves_with_a_reindented_statement() => Formats(
		"""
		public class C
		{
		    public int M(int value)
		    {
		            return
		                value * 2;
		    }

		    public bool N(bool a, bool b)
		    {
		      return
		          a
		          && b;
		    }
		}
		""",
		"""
		public class C
		{
		    public int M(int value)
		    {
		        return
		            value * 2;
		    }

		    public bool N(bool a, bool b)
		    {
		        return
		            a
		            && b;
		    }
		}
		""");

	// Curb breaks before a `return` that sat mid-line. dotnet format leaves the value at its source
	// column rather than moving it by however far `return` moved.
	[Test]
	public Task A_value_below_a_mid_line_return_keeps_its_source_column() => Formats(
		"""
		public class C
		{
		    public int M(int n, int value)
		    {
		        switch (n)
		        {
		            case 1: return
		                        value;
		            default:
		                return 0;
		        }
		    }

		    public int P
		    {
		        get { return
		                  Value; }
		    }
		}
		""",
		"""
		public class C
		{
		    public int M(int n, int value)
		    {
		        switch (n)
		        {
		            case 1:
		                return
		                        value;
		            default:
		                return 0;
		        }
		    }

		    public int P
		    {
		        get
		        {
		            return
		                  Value;
		        }
		    }
		}
		""");

	[Test]
	public Task A_value_below_return_keeps_its_line_under_tabs() => Formats(
		"public class C\n{\n\tpublic bool M(bool a, bool b)\n\t{\n\t\t\treturn\n\t\t\t\t// why\n\t\t\t\ta\n\t\t\t\t&& b;\n\t}\n\n\tpublic int N(int value)\n\t{\n\t\treturn // why\n\t\t\tvalue;\n\t}\n}",
		"public class C\n{\n\tpublic bool M(bool a, bool b)\n\t{\n\t\treturn\n\t\t\t// why\n\t\t\ta\n\t\t\t&& b;\n\t}\n\n\tpublic int N(int value)\n\t{\n\t\treturn // why\n\t\t\tvalue;\n\t}\n}",
		editorConfig: "indent_style = tab");

	// A value the author indented deeper than one level keeps its own lines under it, rather than
	// letting a call's arguments or an initializer's braces fall back to the one-level position.
	[Test]
	public Task A_value_indented_deeper_below_return_keeps_its_own_lines_under_it() => Unchanged(
		"""
		public class C
		{
		    public int Call(int value)
		    {
		        return
		                Math.Max(
		                    value,
		                    1);
		    }

		    public Foo Init(int value)
		    {
		        return
		                new Foo
		                {
		                    X = value,
		                };
		    }

		    public void Throw()
		    {
		        throw
		                new ArgumentException(
		                    "bad");
		    }

		    public Func<int> Lambda(int value)
		    {
		        return
		                () =>
		                {
		                    return value;
		                };
		    }
		}
		""");

	[Test]
	public Task A_value_indented_deeper_below_return_moves_with_a_reindented_statement() => Formats(
		"""
		public class C
		{
		    public int M(int value)
		    {
		            return
		                    Math.Max(
		                        value,
		                        1);
		    }

		    public int N(int n, int value)
		    {
		        switch (n)
		        {
		            case 1: return
		                        Math.Max(
		                            value,
		                            1);
		            default:
		                return 0;
		        }
		    }
		}
		""",
		"""
		public class C
		{
		    public int M(int value)
		    {
		        return
		                Math.Max(
		                    value,
		                    1);
		    }

		    public int N(int n, int value)
		    {
		        switch (n)
		        {
		            case 1:
		                return
		                        Math.Max(
		                            value,
		                            1);
		            default:
		                return 0;
		        }
		    }
		}
		""");

	[Test]
	public Task A_value_indented_deeper_below_return_keeps_its_own_lines_under_tabs() => Formats(
		"public class C\n{\n\tpublic int M(int value)\n\t{\n\t\t\treturn\n\t\t\t\t\tMath.Max(\n\t\t\t\t\t\tvalue,\n\t\t\t\t\t\t1);\n\t}\n\n\tpublic Foo N(int value)\n\t{\n\t\treturn\n\t\t\t\tnew Foo\n\t\t\t\t{\n\t\t\t\t\tX = value,\n\t\t\t\t};\n\t}\n}",
		"public class C\n{\n\tpublic int M(int value)\n\t{\n\t\treturn\n\t\t\t\tMath.Max(\n\t\t\t\t\tvalue,\n\t\t\t\t\t1);\n\t}\n\n\tpublic Foo N(int value)\n\t{\n\t\treturn\n\t\t\t\tnew Foo\n\t\t\t\t{\n\t\t\t\t\tX = value,\n\t\t\t\t};\n\t}\n}",
		editorConfig: "indent_style = tab");

	[Test]
	public Task Deterministic_layout_joins_a_value_below_return_that_fits() => Formats(
		"""
		public class C
		{
		    public int M(int value)
		    {
		        return
		            value * 2;
		    }
		}
		""",
		"""
		public class C
		{
		    public int M(int value)
		    {
		        return value * 2;
		    }
		}
		""",
		editorConfig: "max_line_length = 120");

	[Test]
	public Task Deterministic_layout_still_joins_what_fits() => Formats(
		"""
		public class C
		{
		    [SuppressMessage(
		        "Category",
		        "CA0000")]
		    public string Name => _flag
		        ? "a"
		        : "b";

		    private readonly int _x =
		        Compute();
		}
		""",
		"""
		public class C
		{
		    [SuppressMessage("Category", "CA0000")]
		    public string Name => _flag ? "a" : "b";

		    private readonly int _x = Compute();
		}
		""",
		editorConfig: "max_line_length = 120");

	// Issue #147: a switch or query the author put below `=>` is not joined onto the arrow's line.

	[Test]
	public Task A_switch_expression_body_below_the_arrow_stays_there() => Unchanged(
		"""
		public class C
		{
		    public int N(int y) =>
		        y switch
		        {
		            1 => 3,
		            _ => 2
		        };

		    public int Deeper(int y) =>
		            y switch
		            {
		                _ => 2
		            };

		    public int Flush(int y) =>
		    y switch
		    {
		        _ => 2
		    };

		    public IEnumerable<int> Q(int[] xs) =>
		            from x in xs
		            where x > 1
		            select x;
		}
		""");

	[Test]
	public Task A_reindented_switch_expression_body_below_the_arrow_moves_with_its_member() => Formats(
		"""
		public class C
		{
		        public int N(int y) =>
		                y switch
		                {
		                    _ => 2
		                };
		}
		""",
		"""
		public class C
		{
		    public int N(int y) =>
		            y switch
		            {
		                _ => 2
		            };
		}
		""");

	[Test]
	public Task A_switch_expression_body_below_the_arrow_stays_there_with_tabs() => Unchanged(
		"public class C\n{\n\tpublic int N(int y) =>\n\t\ty switch\n\t\t{\n\t\t\t1 => 3,\n\t\t\t_ => 2\n\t\t};\n}",
		editorConfig: "indent_style = tab");

	[Test]
	public Task Deterministic_layout_still_hugs_a_switch_expression_body() => Formats(
		"""
		public class C
		{
		    public int N(int y) =>
		        y switch
		        {
		            1 => 3,
		            _ => 2
		        };
		}
		""",
		"""
		public class C
		{
		    public int N(int y) => y switch
		    {
		        1 => 3,
		        _ => 2
		    };
		}
		""",
		editorConfig: "max_line_length = 120");
	// Issue #232: with no width, a type argument list the author broke stays broken where they broke it.
	[Test]
	public Task A_broken_type_argument_list_is_kept() => Unchanged(
		"""
		public class C
		{
		    public void M()
		    {
		        Add<List<int>,
		            Dictionary<string, int>>();
		        Add<
		            List<int>,
		            int>();
		        Add<int
		            >();
		        var map = new Dictionary<string,
		                      int>();
		        var names = items.Where(i => i > 0)
		            .Select<int,
		                string>(i => i.ToString())
		            .ToList();
		    }
		}
		""");

	[Test]
	public Task A_broken_type_argument_list_moves_only_as_far_as_its_line() => Formats(
		"""
		public class C
		{
		    public void M()
		    {
		          Add<List<int>,
		                  Dictionary<string, int>>();
		    }
		}
		""",
		"""
		public class C
		{
		    public void M()
		    {
		        Add<List<int>,
		                Dictionary<string, int>>();
		    }
		}
		""");

	[Test]
	public Task A_broken_type_argument_list_is_joined_at_a_width() => Formats(
		"""
		public class C
		{
		    public void M()
		    {
		        Add<List<int>,
		            Dictionary<string, int>>();
		    }
		}
		""",
		"""
		public class C
		{
		    public void M()
		    {
		        Add<List<int>, Dictionary<string, int>>();
		    }
		}
		""",
		editorConfig: "max_line_length = 120");

	// Issue #234: with no width, a break after a parenthesis, or before its close, stays.
	[Test]
	public Task A_query_on_the_line_after_its_parenthesis_is_kept() => Unchanged(
		"""
		public class C
		{
		    public int M(int[] items)
		    {
		        var count = (
		            from i in items
		            select i
		            ).Count();
		        return (
		                from i in items
		                where i > 0
		                select i)
		            .Count();
		    }
		}
		""");

	[Test]
	public Task An_expression_on_the_line_after_its_parenthesis_is_kept() => Unchanged(
		"""
		public class C
		{
		    public void M()
		    {
		        var sum = (
		            a + b
		        );
		        var total = (
		              a +
		                  b);
		        var f = (
		            x =>
		            {
		                return x;
		            });
		    }
		}
		""");

	[Test]
	public Task A_parenthesized_query_moves_only_as_far_as_its_line() => Formats(
		"""
		public class C
		{
		    public int M(int[] items)
		    {
		          return (
		                  from i in items
		                  select i)
		              .Count();
		    }
		}
		""",
		"""
		public class C
		{
		    public int M(int[] items)
		    {
		        return (
		                from i in items
		                select i)
		            .Count();
		    }
		}
		""");

	[Test]
	public Task A_query_broken_after_its_parenthesis_is_joined_at_a_width() => Formats(
		"""
		public class C
		{
		    public int M(int[] items)
		    {
		        return (
		            from i in items
		            select i).Count();
		    }
		}
		""",
		"""
		public class C
		{
		    public int M(int[] items)
		    {
		        return (from i in items select i).Count();
		    }
		}
		""",
		editorConfig: "max_line_length = 120");
}
