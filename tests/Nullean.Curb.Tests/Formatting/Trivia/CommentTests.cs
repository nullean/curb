namespace Nullean.Curb.Tests.Formatting.Trivia;

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
	public Task A_missing_blank_line_after_the_using_list_is_added_even_before_a_comment() => Formats(
		// csharp_blank_lines_after_using_list defaults to one and is forced rather than merely floored
		// (see BlankLineTests), so this reaches a trailing comment the same as it would real code.
		"""
		using System;
		// trailing thought
		""",
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

	// ---- the line under a trailing comment ---------------------------------------------------------
	//
	// Every expectation below was checked against dotnet format whitespace.

	[Test]
	public Task A_comment_under_a_trailing_comment_stays_at_the_member_column() => Unchanged(
		// dotnet format only re-lays trivia that is out of shape. Each comment here already sits where
		// the member after it does, so it is left there rather than pulled under the trailing comment.
		"""
		public class C
		{
		    enum E
		    {
		        A, // one
		        // before b
		        B,
		    }

		    object M(int x)
		    {
		        var o = new Version
		        {
		            Major = 1, // one
		            // before minor
		            Minor = 2,
		        };
		        var s = x switch
		        {
		            1 => 1, // one
		            // before the discard
		            _ => 2,
		        };
		        int[] l =
		        [
		            1, // one
		            // before two
		            2,
		        ];
		        Call(); // one
		        // before the next call
		        // and a second line
		        Call();
		        return o;
		    }
		}
		""");

	[Test]
	public Task A_comment_under_a_trailing_comment_that_has_to_move_aligns_under_it() => Formats(
		// Once the run is out of shape, dotnet format moves all of it under the trailing comment, even
		// a line that was already at the statement column.
		"""
		public class C
		{
		    void M()
		    {
		        Call(); // one
		   // too far left
		        Call();
		        Call(); // one
		        // at the statement column
		                // under the trailing comment
		        Call();
		    }
		}
		""",
		"""
		public class C
		{
		    void M()
		    {
		        Call(); // one
		                // too far left
		        Call();
		        Call(); // one
		                // at the statement column
		                // under the trailing comment
		        Call();
		    }
		}
		""");

	[Test]
	public Task Only_a_line_comment_aligns_under_a_trailing_line_comment() => Formats(
		// A block or doc comment under a trailing `//`, or a `//` under a trailing `/* */`, goes to the
		// statement column. Curb used to align all of them.
		"""
		public class C
		{
		    void M()
		    {
		        Call(); // one
		                /* block */
		        Call();
		        Call(); /* one */
		                // line
		        Call();
		        Call(); // one
		                // line
		   /* block after a line */
		        Call();
		    }
		}
		""",
		"""
		public class C
		{
		    void M()
		    {
		        Call(); // one
		        /* block */
		        Call();
		        Call(); /* one */
		        // line
		        Call();
		        Call(); // one
		                // line
		        /* block after a line */
		        Call();
		    }
		}
		""");

	[Test]
	public Task A_trailing_line_comment_does_not_open_a_blank_line_after_it() => Unchanged(
		// The comment ends its own line. The break the list printer emits after it used to open a
		// second one, which left a blank line after every commented attribute, member and argument.
		"""
		public class C
		{
		    enum E
		    {
		        A, // one
		        B // two
		    }

		    [A] // one
		    [B] // two
		    public void M(int y)
		    {
		        var o = new Version
		        {
		            Major = 1, // one
		            Minor = 2 // two
		        };
		        var x = y switch
		        {
		            1 => 1, // one
		            _ => 2, // two
		        };
		        int[] l =
		        [
		            1, // one
		            2 // two
		        ];
		        Call(
		            1, // one
		            2); // two
		    }

		    [A] // one
		    public int P { get; set; }

		    private static readonly int[,] Patterns = {
		        { 2, 1, 1, 2, 3, 2, 0, 0 }, // 1
		        { 2, 3, 3, 1, 1, 1, 2, 0 } // 2
		    };
		}
		""");

	[Test]
	public Task A_trailing_line_comment_does_not_open_a_blank_line_after_it_with_a_width() => Unchanged(
		"""
		public class C
		{
		    [TestCase(2024, 2, 31)] // first
		    [TestCase(2024, 4, 31)] // second
		    public void SomeTest(int year, int month, int expected)
		    {
		        var o = new Version
		        {
		            Major = 1, // one
		            // before minor
		            Minor = 2,
		        };
		    }
		}
		""",
		editorConfig: "max_line_length = 120");

	[Test]
	public Task A_trailing_line_comment_before_an_opening_brace_leaves_it_on_the_next_line() => Unchanged(
		// The brace's own break merges into the comment's, so no blank line opens between them.
		"""
		public class C // c
		{
		    public int M(int value) // m
		    {
		        if (value > 0) // i
		        {
		            return 1;
		        }
		        return 0;
		    }
		}
		""");

	[Test]
	public Task A_trailing_line_comment_before_an_opening_brace_keeps_it_on_the_next_line_under_none() => Unchanged(
		// The comment has already ended the line the brace would have joined, so it stays on the next
		// line at the header's indent. The space csharp_new_line_before_open_brace = none puts in front
		// of it used to land at the start of that line.
		"""
		public class C // c
		{
		    public int M(int value) // m
		    {
		        if (value > 0) // i
		        {
		            return 1;
		        }
		        return 0;
		    }
		}
		""",
		editorConfig: "csharp_new_line_before_open_brace = none");

	[Test]
	public Task An_inline_block_comment_before_a_value_keeps_one_space_after_it() => Unchanged(
		// The comment's own trailing space and the space the printer puts in front of the value are the
		// same space, so only one is written.
		"""
		public class C
		{
		    private int _field = /* lead */ 1;

		    public int Body() => /* lead */ 1;

		    public int M(int a)
		    {
		        var x = /* lead */ a;
		        x = /* lead */ a;
		        Use(/* lead */ a);
		        Use(a, /* lead */ a);
		        Use(a + /* lead */ a);
		        return /* lead */ a;
		    }

		    private void Use(int a, int b = 0) { }
		}
		""");

	[Test]
	public Task An_inline_block_comment_before_a_value_keeps_one_space_after_it_with_a_width() => Unchanged(
		"""
		public class C
		{
		    public int M(int a)
		    {
		        var x = /* lead */ a;
		        Use(a, /* lead */ a);
		        return /* lead */ a;
		    }
		}
		""",
		editorConfig: "max_line_length = 120");

	// ---- a trailing comment at the end of a group ---------------------------------------------------

	// Every expectation below was checked against dotnet format whitespace, which leaves each input
	// unchanged.

	[Test]
	public Task A_trailing_comment_on_the_last_switch_arm_keeps_it_on_one_line() => Unchanged(
		// With no comma, the comment trails the arm's value rather than the separator, so it sits
		// inside the arm's group. Its line break used to force that group open after `=>` (issue #127).
		"""
		public class C
		{
		    public int M(int y)
		    {
		        return y switch
		        {
		            1 => 1, // one
		            _ => 2 // two
		        };
		    }

		    public int N(int y)
		    {
		        return y switch
		        {
		            1 => 1,
		            > 1 when y < 10 => Compute(y, 1), // guarded
		            _ => Compute(y, 2) // last
		        };
		    }

		    private static int Compute(int a, int b) => a;
		}
		""");

	[Test]
	public Task A_trailing_comment_on_the_last_switch_arm_keeps_it_on_one_line_with_tabs() => Unchanged(
		"""
		public class C
		{
			public int M(int y)
			{
				return y switch
				{
					1 => 1, // one
					_ => 2 // two
				};
			}
		}
		""",
		editorConfig: "indent_style = tab");

	[Test]
	public Task A_trailing_comment_on_the_last_switch_arm_keeps_it_on_one_line_with_a_width() => Unchanged(
		"""
		public class C
		{
		    public int M(int y)
		    {
		        return y switch
		        {
		            1 => 1, // one
		            _ => 2 // two
		        };
		    }

		    public int N(int y)
		    {
		        return y switch
		        {
		            1 => 1,
		            > 1 when y < 10 => Compute(y, 1), // guarded
		            _ => Compute(y, 2) // last
		        };
		    }

		    private static int Compute(int a, int b) => a;
		}
		""",
		editorConfig: "max_line_length = 120");

	[Test]
	public Task A_trailing_comment_on_the_last_nested_initializer_leaves_it_on_one_line_with_a_width() => Unchanged(
		// The same cause under a width: the comment trailed the last initializer's `}` inside its group
		// and opened it out one element per line (issue #98).
		"""
		public class C
		{
		    private static readonly int[,] Patterns = {
		        { 2, 1, 1, 2, 3, 2, 0, 0 }, // 1
		        { 2, 3, 3, 1, 1, 1, 2, 0 } // 2
		    };

		    public void M()
		    {
		        Call(
		            1, // one
		            2 // two
		        );
		        var o = new Version
		        {
		            Major = 1, // one
		            Minor = Compute(1, 2) // two
		        };
		    }

		    private static void Call(int a, int b)
		    { }

		    private static int Compute(int a, int b) => a;
		}
		""",
		editorConfig: "max_line_length = 120");

	[Test]
	public Task A_last_switch_arm_broken_after_the_arrow_stays_broken_with_a_trailing_comment() => Unchanged(
		// The comment no longer holds the arm open, so the author's break has to.
		"""
		public class C
		{
		    public int M(int y)
		    {
		        return y switch
		        {
		            1 => 1,
		            _ =>
		                2 // two
		        };
		    }
		}
		""");

	[Test]
	public Task An_inline_block_comment_against_a_closing_brace_stays_against_it() => Unchanged(
		// The comment always wrote a space after itself, so `{ /* ignore */}` gained one (issue #128).
		// One the source has is still kept.
		"""
		public class C
		{
		    public C() { /* serialization */}

		    public void M()
		    {
		        try { Run(); } catch { /* ignore */}
		        try { Run(); } catch { /* spaced */ }
		        var a = new[] { 1, 2 /* last */};
		        var o = new O { X = 1 /* x */};
		    }

		    public void N() {/* tight */}

		    private void Run() { }
		}

		public class O { public int X; }
		""");

	[Test]
	public Task An_inline_block_comment_against_the_next_token_stays_against_it() => Unchanged(
		"""
		public class C
		{
		    private int _field = /* lead */1;

		    public int M(int a)
		    {
		        var x = /* lead */a;
		        Use(/* lead */a);
		        Use(a /* tail */);
		        Use(a, /* lead */a);
		        Use(a /* one *//* two */, a);
		        return a /* tail */;
		    }

		    private void Use(int a, int b = 0) { }
		}
		""");

	[Test]
	public Task An_inline_block_comment_against_the_next_token_stays_against_it_with_tabs() => Unchanged(
		"""
		public class C
		{
			public void M()
			{
				try { Run(); } catch { /* ignore */}
				Use(/* lead */1);
			}

			private void Run() { }
			private void Use(int a) { }
		}
		""",
		editorConfig: "indent_style = tab");

	[Test]
	public Task An_inline_block_comment_against_the_next_token_stays_against_it_with_a_width() => Unchanged(
		"""
		public class C
		{
		    public C()
		    { /* serialization */}

		    public int M(int a)
		    {
		        var x = new[] { 1, 2 /* last */};
		        Use(/* lead */a);
		        Use(a /* tail */);
		        return a /* tail */;
		    }
		}
		""",
		editorConfig: "max_line_length = 120");
}
