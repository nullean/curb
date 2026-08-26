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
/// Six of the seven are already what Curb writes, which is why they were never onboarded as options.
/// Offering them as keys would mean an option whose <c>false</c> is already the behaviour, and whose
/// <c>true</c> would have to make Curb <em>preserve</em> a blank line it currently removes — a worse
/// default in exchange for a key almost nobody sets. They are asserted here instead, so they are a
/// decision rather than an accident.
/// </para>
/// <para>
/// The seventh, IDE2003, is the only one that would <em>add</em> a blank line — between a block
/// statement and whatever follows it. Reconsidered since: jb's own default does exactly this, and on
/// its own merits a block statement reads as a distinct unit of control flow worth setting off from
/// what comes next — closer to how most C# actually gets written than leaving the two flush. It is a
/// real option now, <c>csharp_blank_lines_after_block_statements</c> (default on), covered in
/// <c>BlankLineOptionTests</c> rather than here, since it is no longer free-ground default behaviour
/// but a documented, controllable one.
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
	public Task IDE2003_a_statement_after_a_block_is_now_a_real_option_not_free_ground() => Formats(
		// Once the only rule of the seven Curb did not hold; now it does, by default, via
		// csharp_blank_lines_after_block_statements — see BlankLineOptionTests for the key itself,
		// including proving it can be turned back off. Kept here too, unchanged in shape from before,
		// so this file's own claim (nothing here needs configuration) stays checked.
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

		        Next();
		    }
		}
		""");
}
