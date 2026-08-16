namespace Nullean.Kerf.Tests.Formatting.Trivia;

/// <summary>
/// The IDE2000 series, which Kerf satisfies by default rather than by key.
/// </summary>
/// <remarks>
/// <para>
/// Seven experimental rules about blank lines in awkward places. Measured: <c>dotnet format</c>
/// applies none of them, in either the whitespace or the style pass — so they are free ground, and
/// anything Kerf does here it may keep doing.
/// </para>
/// <para>
/// Six of the seven are already what Kerf writes, which is why they were never onboarded as options.
/// Offering them as keys would mean an option whose <c>false</c> is already the behaviour, and whose
/// <c>true</c> would have to make Kerf <em>preserve</em> a blank line it currently removes — a worse
/// default in exchange for a key almost nobody sets. They are asserted here instead, so they are a
/// decision rather than an accident.
/// </para>
/// <para>
/// The seventh, IDE2003, is the only one that would <em>add</em> a blank line — between a block and
/// the statement after it. Not done: it is the one rule in the series that writes something the
/// author did not, and it is the least wanted of them.
/// </para>
/// </remarks>
public class ExperimentalBlankLineTests : FormattingTest
{
	[Test]
	public Task IDE2000_a_run_of_blank_lines_collapses_to_one() => Formats(
		"""
		public class C
		{
		    public void M()
		    {
		        Call();


		        Call();
		    }
		}
		""",
		"""
		public class C
		{
		    public void M()
		    {
		        Call();

		        Call();
		    }
		}
		""");

	[Test]
	public Task IDE2002_no_blank_line_between_consecutive_braces() => Formats(
		"""
		public class C
		{
		    public void M()
		    {
		        if (true)
		        {
		            Call();
		        }

		    }
		}
		""",
		"""
		public class C
		{
		    public void M()
		    {
		        if (true)
		        {
		            Call();
		        }
		    }
		}
		""");

	[Test]
	public Task IDE2004_no_blank_line_after_a_constructor_initializer_colon() => Formats(
		"""
		public class C
		{
		    public C() :
		        base()
		    {
		    }
		}
		""",
		"""
		public class C
		{
		    public C() : base()
		    {
		    }
		}
		""");

	[Test]
	public Task IDE2005_no_blank_line_after_a_conditional_token() => Formats(
		"""
		public class C
		{
		    public int M(bool b) => b ?
		        1 :
		        2;
		}
		""",
		"""
		public class C
		{
		    public int M(bool b) => b ? 1 : 2;
		}
		""");

	[Test]
	public Task IDE2006_no_blank_line_after_an_arrow() => Formats(
		"""
		public class C
		{
		    public int M() =>
		        1;
		}
		""",
		"""
		public class C
		{
		    public int M() => 1;
		}
		""");

	[Test]
	public Task IDE2003_a_statement_after_a_block_is_not_given_one() => Unchanged(
		// The one rule in the series Kerf does not hold, and the only one that would write a blank
		// line the author did not. Left alone deliberately.
		"""
		public class C
		{
		    public void M()
		    {
		        if (true)
		        {
		            Call();
		        }
		        Next();
		    }
		}
		""");
}
