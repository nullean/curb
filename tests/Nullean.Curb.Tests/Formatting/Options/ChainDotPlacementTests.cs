namespace Nullean.Curb.Tests.Formatting.Options;

/// <summary>
/// <c>csharp_wrap_after_dot_in_method_calls</c>, and the break predicate it needed first.
/// </summary>
/// <remarks>
/// <para>
/// Free ground: <c>dotnet format</c> leaves a chain alone whichever side of the break its dots sit
/// on, so both renderings are fixed points.
/// </para>
/// <para>
/// The option could not be added until the chain printer stopped deciding <em>whether</em> a link was
/// broken by looking at where the dot was. Those are two separate questions, and conflating them
/// meant a trailing-dot chain read as unbroken and was joined back up.
/// </para>
/// </remarks>
public class ChainDotPlacementTests : FormattingTest
{
	private const string DotTrails = "csharp_wrap_after_dot_in_method_calls = true";

	// ---- the predicate ---------------------------------------------------------------------------------

	[Test]
	public Task A_break_is_kept_whichever_side_the_dot_was_on() => Formats(
		// Both chains are broken; they differ only in where the author left the dot. Curb used to keep
		// the first and join the second, because it asked about the dot's line rather than the link's
		// — against its own rule that nothing joins lines the author broke. The dot normalises to the
		// head of the line, which is Curb's rendering; the break itself is the author's and stays.
		"""
		public class C
		{
		    void Leading()
		    {
		        foo
		            .Bar()
		            .Baz()
		            .Qux();
		    }

		    void Trailing()
		    {
		        foo.
		            Bar().
		            Baz().
		            Qux();
		    }
		}
		""",
		"""
		public class C
		{
		    void Leading()
		    {
		        foo
		            .Bar()
		            .Baz()
		            .Qux();
		    }

		    void Trailing()
		    {
		        foo
		            .Bar()
		            .Baz()
		            .Qux();
		    }
		}
		""");

	[Test]
	public Task A_chain_the_author_kept_on_one_line_still_stays_there() => Unchanged(
		// The predicate decides where breaks are, not whether to introduce them. A short chain has
		// none either way.
		"""
		public class C
		{
		    void M() => foo.Bar().Baz().Qux();
		}
		""");

	// ---- the option ------------------------------------------------------------------------------------

	[Test]
	public Task The_key_moves_the_dot_to_the_end_of_the_line() => WithAndWithout(
		"""
		public class C
		{
		    void M()
		    {
		        foo
		            .Bar()
		            .Baz()
		            .Qux();
		    }
		}
		""",
		"""
		public class C
		{
		    void M()
		    {
		        foo
		            .Bar()
		            .Baz()
		            .Qux();
		    }
		}
		""",
		"""
		public class C
		{
		    void M()
		    {
		        foo.
		            Bar().
		            Baz().
		            Qux();
		    }
		}
		""",
		DotTrails);

	[Test]
	public Task A_chain_that_does_not_break_keeps_its_dots_where_they_were() => Unchanged(
		// Only the dots at a break move, so a chain printed flat is untouched by the option.
		"""
		public class C
		{
		    void M() => foo.Bar().Baz().Qux();
		}
		""",
		editorConfig: DotTrails);

	[Test]
	public Task A_receiver_that_keeps_its_first_call_keeps_that_dot_glued() => Formats(
		// csharp_wrap_before_first_method_call = false is the one way left to attach a first call —
		// and that link has no break for its dot to sit on either side of, so the dot stays glued.
		"""
		public class C
		{
		    void M()
		    {
		        builder.AddProject(aaaa).AddService(bbbb).Build();
		    }
		}
		""",
		"""
		public class C
		{
		    void M()
		    {
		        builder.AddProject(aaaa).
		            AddService(bbbb).
		            Build();
		    }
		}
		""",
		editorConfig: DotTrails + "\ncsharp_wrap_before_first_method_call = false\nmax_line_length = 45");
}
