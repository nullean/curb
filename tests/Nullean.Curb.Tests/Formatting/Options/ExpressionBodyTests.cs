namespace Nullean.Curb.Tests.Formatting.Options;

/// <summary>
/// The <c>csharp_style_expression_bodied_*</c> family, code style rules IDE0021 to IDE0027 and
/// IDE0061.
/// </summary>
/// <remarks>
/// <para>
/// Real .NET keys that <c>dotnet format style</c> can only apply with a workspace and a build behind
/// it. Checked against the real thing: on the combined case Curb's output is byte-identical to
/// <c>dotnet format style</c> followed by <c>dotnet format whitespace</c>.
/// </para>
/// <para>
/// One direction only, as with braces. <c>false</c> does nothing rather than turning expression
/// bodies back into blocks.
/// </para>
/// <para>
/// This is the only rewrite that drops source rather than adding to it or permuting it — a block's
/// braces and its <c>return</c> go, and one <c>=&gt;</c> arrives. The printer names the exact spans
/// it dropped and the exact number of arrows it added, and both verifiers hold it to them.
/// </para>
/// </remarks>
public class ExpressionBodyTests : FormattingTest
{
	private const string Methods = "csharp_style_expression_bodied_methods = true";

	// ---- the default ------------------------------------------------------------------------------

	[Test]
	public Task Bodies_are_left_as_written_without_the_key() => Unchanged(
		"""
		public class C
		{
		    public int Get()
		    {
		        return _x;
		    }
		}
		""");

	[Test]
	public Task False_does_not_turn_expression_bodies_back_into_blocks() => Unchanged(
		"""
		public class C
		{
		    public int Get() => _x;
		}
		""",
		editorConfig: "csharp_style_expression_bodied_methods = false");

	// ---- what converts ----------------------------------------------------------------------------

	[Test]
	public Task A_single_return_becomes_an_expression_body() => WithAndWithout(
		"""
		public class C
		{
		    public int Get()
		    {
		        return _x;
		    }
		}
		""",
		"""
		public class C
		{
		    public int Get()
		    {
		        return _x;
		    }
		}
		""",
		"""
		public class C
		{
		    public int Get() => _x;
		}
		""",
		Methods);

	[Test]
	public Task A_void_body_keeps_its_call() => Formats(
		"""
		public class C
		{
		    public void Run()
		    {
		        Call();
		    }
		}
		""",
		"""
		public class C
		{
		    public void Run() => Call();
		}
		""",
		editorConfig: Methods);

	[Test]
	public Task A_throw_keeps_its_keyword() => Formats(
		"""
		public class C
		{
		    public int Get()
		    {
		        throw new NotSupportedException();
		    }
		}
		""",
		"""
		public class C
		{
		    public int Get() => throw new NotSupportedException();
		}
		""",
		editorConfig: Methods);

	[Test]
	public Task A_constructor_converts_on_its_own_key() => Formats(
		"""
		public class C
		{
		    public C(int x)
		    {
		        _x = x;
		    }
		}
		""",
		"""
		public class C
		{
		    public C(int x) => _x = x;
		}
		""",
		editorConfig: "csharp_style_expression_bodied_constructors = true");

	[Test]
	public Task A_local_function_converts_on_its_own_key() => Formats(
		"""
		public class C
		{
		    public void M()
		    {
		        int Inner()
		        {
		            return 1;
		        }
		    }
		}
		""",
		"""
		public class C
		{
		    public void M()
		    {
		        int Inner() => 1;
		    }
		}
		""",
		editorConfig: "csharp_style_expression_bodied_local_functions = true");

	[Test]
	public Task An_operator_converts_on_its_own_key() => Formats(
		"""
		public struct Point
		{
		    public static Point operator +(Point a, Point b)
		    {
		        return new Point(a.X + b.X, a.Y + b.Y);
		    }
		}
		""",
		"""
		public struct Point
		{
		    public static Point operator +(Point a, Point b) => new Point(a.X + b.X, a.Y + b.Y);
		}
		""",
		editorConfig: "csharp_style_expression_bodied_operators = true");

	// ---- indexers ---------------------------------------------------------------------------------

	// Issue #260: the key was bound and never applied. Each getter shape converts as `dotnet format style`
	// (IDE0026) converts it, and an indexer with a setter keeps its accessors.
	[Test]
	public Task A_get_only_indexer_converts_whatever_its_getter_looks_like() => Formats(
		"""
		public class C
		{
		    private int[] _data = new int[10];

		    public int this[int index]
		    {
		        get { return _data[index]; }
		    }

		    public int this[string key]
		    {
		        get
		        {
		            return _data[key.Length];
		        }
		    }

		    public int this[long i]
		    {
		        get { throw new System.NotSupportedException(); }
		    }

		    public int this[short i]
		    {
		        get => _data[i];
		    }
		}
		""",
		"""
		public class C
		{
		    private int[] _data = new int[10];

		    public int this[int index] => _data[index];

		    public int this[string key] => _data[key.Length];

		    public int this[long i] => throw new System.NotSupportedException();

		    public int this[short i] => _data[i];
		}
		""",
		editorConfig: "csharp_style_expression_bodied_indexers = true");

	[Test]
	public Task An_indexer_with_a_setter_keeps_its_accessors() => Unchanged(
		"""
		public class C
		{
		    private int[] _data = new int[10];

		    public int this[int i]
		    {
		        get { return _data[i]; }
		        set { _data[i] = value; }
		    }
		}
		""",
		editorConfig: "csharp_style_expression_bodied_indexers = true");

	[Test]
	public Task The_property_key_does_not_reach_an_indexer() => Unchanged(
		"""
		public class C
		{
		    private int[] _data = new int[10];

		    public int this[int index]
		    {
		        get { return _data[index]; }
		    }
		}
		""",
		editorConfig: "csharp_style_expression_bodied_properties = true");

	// ---- accessors and whole properties -------------------------------------------------------------

	// Issue #16: after a trailing comment the converted arrow starts the next line one level in. The
	// comment's break left it at the member's column, and the next run moved it a level in.
	[Test]
	public Task A_converted_property_after_a_trailing_comment_puts_its_arrow_one_level_in() => Formats(
		"""
		public class C
		{
		    internal object Compilation // not for correctness
		    {
		        get { return null; }
		    }
		}
		""",
		"""
		public class C
		{
		    internal object Compilation // not for correctness
		        => null;
		}
		""",
		editorConfig: "csharp_style_expression_bodied_properties = true");

	[Test]
	public Task A_converted_method_after_a_trailing_comment_puts_its_arrow_one_level_in() => Formats(
		"""
		public class C
		{
		    int M() // note
		    {
		        return 1;
		    }
		}
		""",
		"""
		public class C
		{
		    int M() // note
		        => 1;
		}
		""",
		editorConfig: "csharp_style_expression_bodied_methods = true");

	// The braces of a switch or an initializer in the converted body sit at the indent of the arrow's
	// line, where dotnet format puts them. That line starts with the arrow the conversion wrote, and the
	// braces took the member's line instead, a level to the left (found by the corpus while fixing #229).
	[Test]
	public Task Braces_in_a_method_converted_after_a_trailing_comment_follow_the_arrow_line() => Formats(
		"""
		public class C
		{
		    void M() // note
		    {
		        AssertUsed(comp2, ordinal switch
		        {
		            1 => references,
		            _ => expected,
		        }, references);
		    }

		    void N() // note
		    {
		        AssertUsed(comp2, new X
		        {
		            A = 1,
		        }, references);
		    }
		}
		""",
		"""
		public class C
		{
		    void M() // note
		        => AssertUsed(comp2, ordinal switch
		        {
		            1 => references,
		            _ => expected,
		        }, references);

		    void N() // note
		        => AssertUsed(comp2, new X
		        {
		            A = 1,
		        }, references);
		}
		""",
		editorConfig: "csharp_style_expression_bodied_methods = true");

	[Test]
	public Task Accessors_convert_without_the_property_collapsing() => Formats(
		// Two accessors, so there is a setter to keep and the accessor list has to stay.
		"""
		public class C
		{
		    public int Value
		    {
		        get
		        {
		            return _x;
		        }
		        set
		        {
		            _x = value;
		        }
		    }
		}
		""",
		// The accessor list stays opened out, as the author wrote it: with no width Curb never joins
		// lines (issue #24), and converting the bodies is no reason to start.
		"""
		public class C
		{
		    public int Value
		    {
		        get => _x;
		        set => _x = value;
		    }
		}
		""",
		editorConfig: "csharp_style_expression_bodied_accessors = true");

	[Test]
	public Task A_getter_only_property_collapses_entirely() => Formats(
		// The wider rewrite: the accessor list's own braces and the `get` keyword go too.
		"""
		public class C
		{
		    public int Value
		    {
		        get
		        {
		            return _x;
		        }
		    }
		}
		""",
		"""
		public class C
		{
		    public int Value => _x;
		}
		""",
		editorConfig: "csharp_style_expression_bodied_properties = true");

	[Test]
	public Task A_getter_that_already_used_an_arrow_collapses_too() => Formats(
		// And adds no arrow of its own — the source's carries across, which is why the printer counts
		// arrows rather than inferring them from the braces it dropped.
		"""
		public class C
		{
		    public int Value
		    {
		        get => _x;
		    }
		}
		""",
		"""
		public class C
		{
		    public int Value => _x;
		}
		""",
		editorConfig: "csharp_style_expression_bodied_properties = true");

	[Test]
	public Task An_auto_property_is_not_a_candidate() => Unchanged(
		"""
		public class C
		{
		    public int Value { get; set; }
		}
		""",
		editorConfig: "csharp_style_expression_bodied_properties = true");

	// ---- what it refuses ----------------------------------------------------------------------------

	[Test]
	public Task Two_statements_are_left_alone() => Unchanged(
		"""
		public class C
		{
		    public int Get()
		    {
		        var y = 1;
		        return y;
		    }
		}
		""",
		editorConfig: Methods);

	[Test]
	public Task An_empty_body_is_left_alone() => Unchanged(
		"""
		public class C
		{
		    public void Run()
		    {
		    }
		}
		""",
		editorConfig: Methods);

	[Test]
	public Task A_bare_return_has_no_expression_to_move() => Unchanged(
		"""
		public class C
		{
		    public void Run()
		    {
		        return;
		    }
		}
		""",
		editorConfig: Methods);

	[Test]
	public Task A_comment_inside_the_body_stops_it() => Unchanged(
		// The braces are what the comment hangs off. Losing it is not a trade worth making for a
		// shorter member, so the block stays.
		"""
		public class C
		{
		    public int Get()
		    {
		        // the reason
		        return _x;
		    }
		}
		""",
		editorConfig: Methods);

	[Test]
	public Task A_directive_inside_the_body_stops_it() => Unchanged(
		"""
		public class C
		{
		    public int Get()
		    {
		#if DEBUG
		        return _x;
		#endif
		    }
		}
		""",
		editorConfig: Methods);

	// ---- when_on_single_line ------------------------------------------------------------------------

	[Test]
	public Task When_on_single_line_takes_a_block_the_author_kept_on_one_line() => Formats(
		"""
		public class C
		{
		    public int Get() { return _x; }
		}
		""",
		"""
		public class C
		{
		    public int Get() => _x;
		}
		""",
		editorConfig: "csharp_style_expression_bodied_methods = when_on_single_line");

	[Test]
	public Task When_on_single_line_leaves_a_block_spread_over_several() => Unchanged(
		// Keyed on the source, which reflow cannot move. Asking whether the *result* would fit would
		// let one run's width decide the next run's tokens.
		"""
		public class C
		{
		    public int Get()
		    {
		        return _x;
		    }
		}
		""",
		editorConfig: "csharp_style_expression_bodied_methods = when_on_single_line");

	// ---- the severity suffix -------------------------------------------------------------------------

	[Test]
	public Task A_severity_suffix_is_not_part_of_the_value() => Formats(
		// `true:suggestion` is how these are written, and how the corpus writes them.
		"""
		public class C
		{
		    public int Get()
		    {
		        return _x;
		    }
		}
		""",
		"""
		public class C
		{
		    public int Get() => _x;
		}
		""",
		editorConfig: "csharp_style_expression_bodied_methods = true:suggestion");
	[Test]
	public Task A_throwing_getter_collapses_in_one_pass() => Formats(
		// Two rewrites that have to compose on the same run: the accessor-level one turns the block
		// into `get => throw …`, and the property-level one collapses `{ get => … }` to `=> …`. The
		// property printer recognised a returning block and an arrow but not a throwing block, so the
		// first run did half the job and the second did the rest — a file that never settled, on 120
		// roslyn files' worth of shapes like this.
		"""
		public class C
		{
		    public int Count
		    {
		        get
		        {
		            throw new System.NotImplementedException();
		        }
		    }
		}
		""",
		"""
		public class C
		{
		    public int Count => throw new System.NotImplementedException();
		}
		""",
		editorConfig: "csharp_style_expression_bodied_properties = true\ncsharp_style_expression_bodied_accessors = true");

	// ---- settling in one pass (issue #186) --------------------------------------------------------
	//
	// The run after a conversion reads back the arrow it wrote, so the conversion has to write the layout
	// that run gives an arrow. It laid its body out in a group of its own, which put a switch below the
	// arrow, and the next run hugged it to the arrow: 216 files of the generated corpus that changed on a
	// second pass. Every expected output below is what `dotnet format style` (IDE0021 to IDE0027, IDE0061)
	// followed by `dotnet format whitespace` wrote, except where a case names a width.

	private const string Switch =
		"""
		public class C
		{
		    public string Name(int x)
		    {
		        return x switch
		        {
		            1 => "one",
		            _ => "other",
		        };
		    }
		}
		""";

	private const string SwitchOnTheArrow =
		"""
		public class C
		{
		    public string Name(int x) => x switch
		    {
		        1 => "one",
		        _ => "other",
		    };
		}
		""";

	[Test]
	public Task A_converted_switch_hugs_the_arrow_on_the_run_that_converts_it() => Formats(Switch, SwitchOnTheArrow, editorConfig: Methods);

	[Test]
	public Task A_converted_switch_hugs_the_arrow_at_a_width_too() =>
		Formats(Switch, SwitchOnTheArrow, editorConfig: Methods + "\nmax_line_length = 140");

	[Test]
	public Task A_converted_switch_after_a_hugging_paren_follows_the_arrow_a_level_in() => Formats(
		"""
		public class C
		{
		    public string Name(
		        int a,
		        int b)
		    {
		        return a switch
		        {
		            1 => "one",
		            _ => "other",
		        };
		    }
		}
		""",
		"""
		public class C
		{
		    public string Name(
		        int a,
		        int b) => a switch
		        {
		            1 => "one",
		            _ => "other",
		        };
		}
		""",
		editorConfig: Methods);

	[Test]
	public Task A_converted_getter_switch_hugs_the_arrow() => Formats(
		"""
		public class C
		{
		    public string Kind
		    {
		        get
		        {
		            return _x switch
		            {
		                1 => "one",
		                _ => "other",
		            };
		        }
		    }
		}
		""",
		"""
		public class C
		{
		    public string Kind => _x switch
		    {
		        1 => "one",
		        _ => "other",
		    };
		}
		""",
		editorConfig: "csharp_style_expression_bodied_properties = true\ncsharp_style_expression_bodied_accessors = true\nmax_line_length = 140");

	[Test]
	public Task A_converted_local_function_switch_hugs_the_arrow() => Formats(
		"""
		public class C
		{
		    public int M()
		    {
		        int Inner(int y)
		        {
		            return y switch
		            {
		                1 => 1,
		                _ => 0,
		            };
		        }

		        return Inner(1);
		    }
		}
		""",
		"""
		public class C
		{
		    public int M()
		    {
		        int Inner(int y) => y switch
		        {
		            1 => 1,
		            _ => 0,
		        };

		        return Inner(1);
		    }
		}
		""",
		editorConfig: "csharp_style_expression_bodied_local_functions = true");

	[Test]
	public Task A_converted_call_the_author_opened_out_starts_on_the_arrow_line() => Formats(
		// The arrow takes over the line `return` was on, so the value stays where the author started it,
		// as it does after an arrow the author wrote.
		"""
		public class C
		{
		    public string Call(int a, int b)
		    {
		        return string.Join(
		            ",",
		            a,
		            b);
		    }

		    public int Fail(int x)
		    {
		        throw new System.ArgumentOutOfRangeException(
		            nameof(x));
		    }
		}
		""",
		"""
		public class C
		{
		    public string Call(int a, int b) => string.Join(
		            ",",
		            a,
		            b);

		    public int Fail(int x) => throw new System.ArgumentOutOfRangeException(
		            nameof(x));
		}
		""",
		editorConfig: Methods);

	[Test]
	public Task A_converted_property_pattern_keeps_its_braces_at_the_member_column() => Formats(
		// On the arrow's line, a body indents its own continuation lines a level, but a property pattern's
		// braces stay at the member's column, as they stay at the statement's after `return`.
		"""
		public class C
		{
		    public bool M(object value)
		    {
		        return value is System.Version
		        {
		            Major: 1,
		            Minor: 2
		        };
		    }
		}
		""",
		"""
		public class C
		{
		    public bool M(object value) => value is System.Version
		    {
		        Major: 1,
		        Minor: 2
		    };
		}
		""",
		editorConfig: Methods);

	[Test]
	public Task A_converted_body_measures_its_semicolon_as_the_next_run_does() => Formats(
		// Curb's own layout, at a width dotnet format has no opinion on. The conversion printed the `;`
		// inside the body's group, where the next run prints it after the clause, so at this width the
		// first run broke the ternary and the second joined it back.
		"""
		public class C
		{
		    private object value;

		    public void M(bool condition) {
		        value = condition ? alpha : beta;
		    }
		}
		""",
		"""
		public class C
		{
		    private object value;

		    public void M(bool condition) =>
		        value = condition ? alpha : beta;
		}
		""",
		editorConfig: Methods + "\nmax_line_length = 40");
}
