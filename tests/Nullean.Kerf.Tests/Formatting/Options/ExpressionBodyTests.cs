namespace Nullean.Kerf.Tests.Formatting.Options;

/// <summary>
/// The <c>csharp_style_expression_bodied_*</c> family, code style rules IDE0021 to IDE0027 and
/// IDE0061.
/// </summary>
/// <remarks>
/// <para>
/// Real .NET keys that <c>dotnet format style</c> can only apply with a workspace and a build behind
/// it. Checked against the real thing: on the combined case Kerf's output is byte-identical to
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

	// ---- accessors and whole properties -------------------------------------------------------------

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
		"""
		public class C
		{
		    public int Value { get => _x; set => _x = value; }
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
}
