namespace Nullean.Kerf.Tests.Formatting.Trivia;

/// <summary>
/// Comments are where formatters lose content. Every bug the corpus has found so far lives here:
/// doc-comment markers stripped, comments above a closing brace dropped or mis-indented, comments
/// welded onto the end of the previous line.
/// </summary>
/// <remarks>
/// Members that are incidental to a test are fields rather than methods, so that block layout — a
/// separate subject with its own tests — cannot make a comment test fail for an unrelated reason.
/// </remarks>
public class CommentTests : FormattingTest
{
	[Test]
	public Task Leading_comment_keeps_its_own_line() => Unchanged(
		"""
		public class C
		{
		    // a comment
		    public int Value;
		}
		""");

	[Test]
	public Task Leading_comment_is_indented_with_the_code_it_precedes() => Formats(
		"""
		public class C
		{
		// a comment
		    public int Value;
		}
		""",
		"""
		public class C
		{
		    // a comment
		    public int Value;
		}
		""");

	[Test]
	public Task Trailing_comment_stays_on_the_line_it_follows() => Unchanged(
		"""
		public class C
		{
		    public void M()
		    {
		        Call(); // why we call
		    }
		}
		""");

	[Test]
	public Task Trailing_comment_gets_exactly_one_space_before_it() => Formats(
		"""
		public class C
		{
		    public void M()
		    {
		        Call();     // why we call
		    }
		}
		""",
		"""
		public class C
		{
		    public void M()
		    {
		        Call(); // why we call
		    }
		}
		""");

	[Test]
	public Task Comment_above_a_closing_brace_stays_with_the_block() => Unchanged(
		"""
		public class C
		{
		    public void M()
		    {
		        Call();
		        // nothing more to do
		    }
		}
		""");

	[Test]
	public Task Comment_above_a_closing_brace_is_indented_with_the_block() => Formats(
		"""
		public class C
		{
		    public void M()
		    {
		        Call();
		// nothing more to do
		    }
		}
		""",
		"""
		public class C
		{
		    public void M()
		    {
		        Call();
		        // nothing more to do
		    }
		}
		""");

	[Test]
	public Task Comment_as_the_only_content_of_a_block() => Unchanged(
		"""
		public class C
		{
		    public void M()
		    {
		        // deliberately empty
		    }
		}
		""");

	[Test]
	public Task Comment_as_the_only_content_of_a_type() => Unchanged(
		"""
		public class C
		{
		    // nothing here yet
		}
		""");

	[Test]
	public Task Consecutive_comments_each_keep_their_line() => Unchanged(
		"""
		public class C
		{
		    // first
		    // second
		    // third
		    public int Value;
		}
		""");

	[Test]
	public Task Comment_at_the_start_of_a_file() => Unchanged(
		"""
		// a licence header
		using System;
		""");

	[Test]
	public Task Comment_at_the_end_of_a_file() => Unchanged(
		"""
		using System;

		// trailing thought
		""");

	[Test]
	public Task Comment_at_the_end_of_a_file_without_a_blank_line() => Unchanged(
		"""
		using System;
		// trailing thought
		""");

	[Test]
	public Task File_containing_only_a_comment() => Unchanged(
		"""
		// this file is a placeholder
		""");

	[Test]
	public Task Block_comment_on_its_own_line() => Unchanged(
		"""
		public class C
		{
		    /* a block comment */
		    public int Value;
		}
		""");

	[Test]
	public Task Block_comment_keeps_its_internal_alignment() => Unchanged(
		"""
		public class C
		{
		    /*
		     * aligned
		     * continuation
		     */
		    public int Value;
		}
		""");

	[Test]
	public Task Block_comment_between_code_on_one_line_keeps_its_spacing() => Unchanged(
		"""
		public class C
		{
		    public void M()
		    {
		        Call(/* inline */ argument);
		    }
		}
		""");

	[Test]
	public Task Commented_out_code_is_not_reformatted() => Unchanged(
		"""
		public class C
		{
		    // public void Old(){Call( );}
		    public int Value;
		}
		""");

	[Test]
	public Task Comment_between_members() => Unchanged(
		"""
		public class C
		{
		    public int First;

		    // separates the two
		    public int Second;
		}
		""");

	[Test]
	public Task Comment_between_using_directives() => Unchanged(
		"""
		using System;

		// grouping note
		using System.Linq;
		""");

	[Test]
	public Task Comment_after_an_opening_brace() => Unchanged(
		"""
		public class C
		{
		    public void M()
		    {
		        // set up
		        Call();
		    }
		}
		""");

	[Test]
	public Task Comment_indentation_follows_a_change_of_nesting() => Formats(
		"""
		public class C
		{
		public void M()
		{
		// nested twice
		Call();
		}
		}
		""",
		"""
		public class C
		{
		    public void M()
		    {
		        // nested twice
		        Call();
		    }
		}
		""");

	[Test]
	public Task Comment_that_starts_further_left_than_its_code_is_moved_with_it() => Formats(
		"""
		public class C
		{
		    public void M()
		    {
		// left-flushed
		        Call();
		    }
		}
		""",
		"""
		public class C
		{
		    public void M()
		    {
		        // left-flushed
		        Call();
		    }
		}
		""");

	[Test]
	public Task Trailing_comment_on_a_member_does_not_disturb_the_next_one() => Unchanged(
		"""
		public class C
		{
		    public int First; // done

		    public int Second;
		}
		""");

	[Test]
	public Task Comment_containing_a_quote_or_brace_is_left_alone() => Unchanged(
		"""
		public class C
		{
		    // a " quote and a { brace and a }
		    public int Value;
		}
		""");

	[Test]
	public Task Empty_comment() => Unchanged(
		"""
		public class C
		{
		    //
		    public int Value;
		}
		""");

	[Test]
	public Task Comment_inside_an_argument_list_that_breaks() => Unchanged(
		"""
		public class C
		{
		    public void M()
		    {
		        Call(
		            first,
		            // explains the second
		            second
		        );
		    }
		}
		""",
		editorConfig: "max_line_length = 40");

	[Test]
	public Task Comment_between_a_type_and_its_members() => Unchanged(
		"""
		public class C
		{
		    // about the members below
		    public int First;
		    public int Second;
		}
		""");

	[Test]
	public Task A_comment_above_a_single_line_block_leaves_it_on_one_line() => Unchanged(
		"""
		public class C
		{
		    // keeps its shape
		    public void M() { }
		}
		""");
}
