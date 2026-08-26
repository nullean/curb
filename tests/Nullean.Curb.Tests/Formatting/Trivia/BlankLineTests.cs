namespace Nullean.Curb.Tests.Formatting.Trivia;

/// <summary>
/// Blank lines are the one piece of the author's layout Curb deliberately keeps. The rule is: at
/// most one is preserved, none is ever invented, and none survives at the start or end of a block.
/// </summary>
/// <remarks>
/// <para>
/// Getting this wrong is how output stops being idempotent — a blank line added on one run is
/// preserved by the next, and grows. Two such bugs have already been found that way.
/// </para>
/// <para>
/// Expectations here follow <c>dotnet format</c>, established by running it rather than assumed:
/// it <b>keeps</b> blank lines after an opening brace, before a closing brace, and at the start of
/// a file. Curb currently drops the latter two, so those tests are skipped and record the gap.
/// </para>
/// </remarks>
public class BlankLineTests : FormattingTest
{
	[Test]
	public Task A_single_blank_line_between_members_is_kept() => Unchanged(
		"""
		public class C
		{
		    public int First;

		    public int Second;
		}
		""");

	[Test]
	public Task Runs_of_blank_lines_collapse_to_one() => Formats(
		"""
		public class C
		{
		    public int First;




		    public int Second;
		}
		""",
		"""
		public class C
		{
		    public int First;

		    public int Second;
		}
		""");

	[Test]
	public Task Members_with_no_blank_line_stay_adjacent() => Unchanged(
		"""
		public class C
		{
		    public int First;
		    public int Second;
		}
		""");

	[Test]
	public Task A_blank_line_after_an_opening_brace_is_dropped() => Formats(
		// csharp_blank_lines_inside_type defaults to zero and is forced rather than merely floored
		// (see BlankLineOptionTests), so — unlike most of this family — the author's blank line here
		// does not survive by default; a blank line directly under an opening brace reads as
		// accidental rather than as deliberate spacing.
		"""
		public class C
		{

		    public int Value;
		}
		""",
		"""
		public class C
		{
		    public int Value;
		}
		""");

	[Test]
	public Task A_blank_line_before_a_closing_brace_is_dropped() => Formats(
		"""
		public class C
		{
		    public int Value;

		}
		""",
		// An opinion, not a divergence. dotnet format keeps this blank line but never adds one, so
		// dropping it leaves output dotnet format is content with — the admission test for anything
		// Curb decides that dotnet format declines to.
		"""
		public class C
		{
		    public int Value;
		}
		""");

	[Test]
	public Task A_blank_line_between_statements_is_kept() => Unchanged(
		"""
		public class C
		{
		    public void M()
		    {
		        First();

		        Second();
		    }
		}
		""");

	[Test]
	public Task Blank_lines_between_statements_collapse() => Formats(
		"""
		public class C
		{
		    public void M()
		    {
		        First();



		        Second();
		    }
		}
		""",
		"""
		public class C
		{
		    public void M()
		    {
		        First();

		        Second();
		    }
		}
		""");

	[Test]
	public Task A_blank_line_between_using_directives_is_kept() => Unchanged(
		"""
		using System;

		using System.Linq;
		""");

	[Test]
	public Task A_blank_line_between_usings_and_the_first_type_is_kept() => Unchanged(
		"""
		using System;

		public class C
		{
		    public int Value;
		}
		""");

	[Test]
	public Task Leading_blank_lines_in_a_file_are_dropped() => Formats(
		"""


		using System;
		""",
		// Same shape: dotnet format keeps them, never adds them, and leaves the trimmed file alone.
		"""
		using System;
		""");

	[Test]
	public Task Trailing_blank_lines_collapse_to_the_final_newline() => Formats(
		"""
		using System;


		""",
		"""
		using System;
		""");

	[Test]
	public Task A_blank_line_before_a_comment_is_kept() => Unchanged(
		"""
		public class C
		{
		    public int First;

		    // about the second
		    public int Second;
		}
		""");

	[Test]
	public Task A_blank_line_between_a_comment_and_its_code_is_kept() => Unchanged(
		"""
		public class C
		{
		    // a note

		    public int Value;
		}
		""");

	[Test]
	public Task Blank_lines_are_not_invented_between_adjacent_statements() => Unchanged(
		"""
		public class C
		{
		    public void M()
		    {
		        First();
		        Second();
		        Third();
		    }
		}
		""");

	[Test]
	public Task A_blank_line_between_nested_types_is_kept() => Unchanged(
		"""
		public class Outer
		{
		    public class First
		    {
		    }

		    public class Second
		    {
		    }
		}
		""");
}
