namespace Nullean.Curb.Tests.Formatting.Options;

/// <summary>
/// <c>csharp_prefer_braces</c>, code style rule IDE0011.
/// </summary>
/// <remarks>
/// <para>
/// A real .NET key that <c>dotnet format style</c> implements only with a workspace behind it. Curb
/// reaches the same answer from syntax alone, and the expectations here were checked against the real
/// thing: on the combined case, Curb's output is byte-identical to <c>dotnet format style</c> followed
/// by <c>dotnet format whitespace</c>.
/// </para>
/// <para>
/// Off unless the key says otherwise, and <c>false</c> also does nothing. Roslyn reads <c>false</c> as
/// "take the braces off", but removing a brace pair can change what a name means — a declaration
/// inside the block stops being scoped to it — while adding braces cannot. Curb only adds.
/// </para>
/// <para>
/// This is the first rule that <em>inserts</em> tokens rather than moving or permuting them, so the
/// declared delta is a counted one: both verifiers step over a <c>{</c> or <c>}</c> the source lacks
/// and require the pair to balance. Which brace is the added one cannot be decided locally, since
/// braces are indistinguishable and an added <c>}</c> sits directly in front of the enclosing block's
/// own — so the pairing is settled by count at the end of the file.
/// </para>
/// </remarks>
public class PreferBracesTests : FormattingTest
{
	private const string Always = "csharp_prefer_braces = true";

	// ---- the default ------------------------------------------------------------------------------

	[Test]
	public Task Bodies_are_left_as_written_without_the_key() => Unchanged(
		"""
		public class C
		{
		    public void M(int x)
		    {
		        if (x > 0)
		            Call();
		    }
		}
		""");

	[Test]
	public Task False_does_not_take_braces_off() => Unchanged(
		// Roslyn would remove them. Curb does not: a declaration inside the block stops being scoped
		// to it, and that is a change in meaning rather than in layout.
		"""
		public class C
		{
		    public void M(int x)
		    {
		        if (x > 0)
		        {
		            Call();
		        }
		    }
		}
		""",
		editorConfig: "csharp_prefer_braces = false");

	// ---- adding them ------------------------------------------------------------------------------

	[Test]
	public Task An_unbraced_body_gains_them() => WithAndWithout(
		"""
		public class C
		{
		    public void M(int x)
		    {
		        if (x > 0)
		            Call();
		    }
		}
		""",
		"""
		public class C
		{
		    public void M(int x)
		    {
		        if (x > 0)
		            Call();
		    }
		}
		""",
		"""
		public class C
		{
		    public void M(int x)
		    {
		        if (x > 0)
		        {
		            Call();
		        }
		    }
		}
		""",
		Always);

	[Test]
	public Task A_body_on_the_headers_line_is_expanded() => Formats(
		// csharp_preserve_single_line_statements would keep this inline, and the brace rule beats it.
		// Leaving `if (a) Call();` alone would satisfy nobody: IDE0011 would still fire on the next
		// build, and not firing is the entire point of doing this before the compiler runs.
		"""
		public class C
		{
		    public void M(int x)
		    {
		        if (x > 0) Call();
		    }
		}
		""",
		"""
		public class C
		{
		    public void M(int x)
		    {
		        if (x > 0)
		        {
		            Call();
		        }
		    }
		}
		""",
		editorConfig: Always);

	[Test]
	public Task Else_gets_them_too_and_follows_a_closing_brace() => Formats(
		"""
		public class C
		{
		    public void M(int x)
		    {
		        if (x > 0)
		            Call();
		        else
		            Other();
		    }
		}
		""",
		"""
		public class C
		{
		    public void M(int x)
		    {
		        if (x > 0)
		        {
		            Call();
		        }
		        else
		        {
		            Other();
		        }
		    }
		}
		""",
		editorConfig: Always);

	[Test]
	public Task An_else_if_chain_braces_its_bodies_not_the_chain() => Formats(
		"""
		public class C
		{
		    public void M(int x)
		    {
		        if (x > 0)
		            Call();
		        else if (x > 1)
		            Other();
		    }
		}
		""",
		"""
		public class C
		{
		    public void M(int x)
		    {
		        if (x > 0)
		        {
		            Call();
		        }
		        else if (x > 1)
		        {
		            Other();
		        }
		    }
		}
		""",
		editorConfig: Always);

	[Test]
	public Task Every_control_flow_construct_is_covered() => Formats(
		"""
		public class C
		{
		    public void M(int x)
		    {
		        for (var i = 0; i < 2; i++)
		            Call();
		        while (x > 0)
		            Call();
		        foreach (var i in items)
		            Call();
		        lock (this)
		            Call();
		    }
		}
		""",
		"""
		public class C
		{
		    public void M(int x)
		    {
		        for (var i = 0; i < 2; i++)
		        {
		            Call();
		        }

		        while (x > 0)
		        {
		            Call();
		        }

		        foreach (var i in items)
		        {
		            Call();
		        }

		        lock (this)
		        {
		            Call();
		        }
		    }
		}
		""",
		editorConfig: Always);

	[Test]
	public Task Nested_unbraced_bodies_each_gain_their_own() => Formats(
		"""
		public class C
		{
		    public void M(int x)
		    {
		        if (x > 2)
		            if (x > 3)
		                Call();
		    }
		}
		""",
		"""
		public class C
		{
		    public void M(int x)
		    {
		        if (x > 2)
		        {
		            if (x > 3)
		            {
		                Call();
		            }
		        }
		    }
		}
		""",
		editorConfig: Always);

	[Test]
	public Task A_body_that_already_has_braces_is_untouched() => Unchanged(
		"""
		public class C
		{
		    public void M(int x)
		    {
		        if (x > 0)
		        {
		            Call();
		        }
		    }
		}
		""",
		editorConfig: Always);

	[Test]
	public Task A_braced_body_on_one_line_keeps_its_line() => Unchanged(
		// IDE0011 is satisfied — the braces are there — so the preservation option decides, and it
		// says leave it. Only an *unbraced* body is expanded.
		"""
		public class C
		{
		    public void M(int x)
		    {
		        if (x > 0) { Call(); }
		    }
		}
		""",
		editorConfig: Always);

	// ---- when_multiline ---------------------------------------------------------------------------

	[Test]
	public Task When_multiline_leaves_a_body_that_shares_the_header_line() => Unchanged(
		"""
		public class C
		{
		    public void M(int x)
		    {
		        if (x > 0) Call();
		    }
		}
		""",
		editorConfig: "csharp_prefer_braces = when_multiline");

	[Test]
	public Task When_multiline_leaves_a_one_line_body_alone() => Unchanged(
		// The body is what has to span lines, not the statement. This test used to assert the
		// opposite and so encoded the bug: Curb asked whether the body sat on the header's line, which
		// is true of almost every unbraced statement ever written, and braced the lot.
		//
		// Measured against `dotnet format style` with the option at warning severity — the way the
		// roslyn repository sets it, and where this was thousands of files of churn.
		"""
		public class C
		{
		    public void M(int x)
		    {
		        if (x > 0)
		            Call();
		    }
		}
		""",
		editorConfig: "csharp_prefer_braces = when_multiline");

	[Test]
	public Task When_multiline_braces_a_body_that_spans_lines() => Formats(
		// Keyed on where the author put the body, not on where reflow ends up putting it: asking the
		// printed layout would let one run's width decide the next run's tokens.
		"""
		public class C
		{
		    public bool M(int x)
		    {
		        if (x > 0)
		            return
		                false;
		        return true;
		    }
		}
		""",
		"""
		public class C
		{
		    public bool M(int x)
		    {
		        if (x > 0)
		        {
		            return false;
		        }

		        return true;
		    }
		}
		""",
		editorConfig: "csharp_prefer_braces = when_multiline");

	// ---- the option it has to cooperate with ------------------------------------------------------

	[Test]
	public Task The_brace_style_option_still_governs_where_the_brace_goes() => Formats(
		"""
		public class C
		{
		    public void M(int x)
		    {
		        if (x > 0)
		            Call();
		    }
		}
		""",
		// The brace Curb synthesises is placed by the same option as every brace the source wrote, so
		// K&R reaches it as well.
		"""
		public class C {
		    public void M(int x) {
		        if (x > 0) {
		            Call();
		        }
		    }
		}
		""",
		editorConfig: Always + "\ncsharp_new_line_before_open_brace = none");
}
