namespace Nullean.Curb.Tests.Formatting.Trivia;

/// <summary>
/// The IDE2000 series, which Curb satisfies by default rather than by key.
/// </summary>
/// <remarks>
/// <para>
/// Seven experimental rules about blank lines in awkward places. Measured: <c>dotnet format</c>
/// applies none of them, in either the whitespace or the style pass — so they are free ground, and
/// anything Curb does here it may keep doing.
/// </para>
/// <para>
/// All seven are already what Curb writes, which is why none of them were onboarded as an option
/// with its own default tied to this behaviour. The seventh, IDE2003 — a blank line between a block
/// statement and whatever follows it — briefly was: <c>csharp_blank_lines_after_block_statements</c>
/// defaulted to one on the strength of jb's own default and "closer to how most C# actually gets
/// written." Reverted deliberately: kept symmetric with
/// <c>csharp_blank_lines_before_block_statements</c>, which stayed at zero throughout, and left to
/// the author on both sides like every other member/statement spacing key in the family bar
/// <c>around_type</c> and <c>around_local_method</c> — both narrower, more clearly-conventional
/// cases. The key itself is unaffected and still forces air when asked; see
/// <c>BlankLineOptionTests.Block_statements_have_their_own_setting</c>.
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
		// A blank line, which is what IDE2006 is about — not a line break, which is the author's and
		// is now kept. This test used to feed a plain break and assert Curb closed it up, conflating
		// the two and quietly asserting that Curb joined lines the author had broken.
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
		    public int M() =>
		        1;
		}
		""");

	[Test]
	public Task IDE2003_no_blank_line_between_a_block_statement_and_what_follows() => Unchanged(
		// Back to free ground, alongside the other six — see this class's own remarks for why
		// csharp_blank_lines_after_block_statements's default reverted rather than staying the one
		// rule here that added a blank line by default.
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
