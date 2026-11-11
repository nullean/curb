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

	/// <summary>
	/// The spaces in front of a trailing <c>//</c> comment stay as written, so hand-aligned comment
	/// columns survive, as <c>dotnet format</c> leaves them. See
	/// <see href="https://github.com/nullean/curb/issues/236">issue #236</see>.
	/// </summary>
	[Test]
	public Task Several_spaces_before_a_trailing_line_comment_are_kept() => Unchanged(
		"""
		public class C
		{
		    private int _a;     // a
		    private int _bb;    // bb

		    public bool M(int x)
		    {
		        Run("a");      // first
		        Run("long");   // second
		        return x switch
		        {
		            0 => true,      // zero
		            100 => true,    // hundred
		            _ => false,
		        };
		    }

		    void Run(string s) { }
		}
		""");

	[Test]
	public Task Several_spaces_before_a_trailing_line_comment_are_kept_with_a_width_when_linebreaks_are_kept() => Unchanged(
		"""
		public class C
		{
		    public void M()
		    {
		        Run("a");      // first
		        Run("long");   // second
		    }

		    void Run(string s) { }
		}
		""",
		editorConfig: "max_line_length = 120\ncsharp_keep_existing_linebreaks = true");

	[Test]
	public Task The_gap_before_a_trailing_line_comment_is_kept_when_the_code_in_front_of_it_moves() => Formats(
		// dotnet format re-spaces the code and re-indents the line, but writes the gap as it stands, and
		// aligns the comment below under the trailing one where it now starts.
		"""
		public class C
		{
		    public void M(int x)
		    {
		          var y = Foo( x );      // call
		                                   // under
		    }

		    int Foo(int q) => q;
		}
		""",
		"""
		public class C
		{
		    public void M(int x)
		    {
		        var y = Foo(x);      // call
		                             // under
		    }

		    int Foo(int q) => q;
		}
		""");

	[Test]
	public Task A_tab_before_a_trailing_line_comment_becomes_one_space() => Formats(
		// dotnet format keeps a tab there only while it leaves the line alone, and otherwise rewrites it into
		// spaces counted from where the comment lands. One space is a fixed point of both.
		"public class C\n{\n    public void M()\n    {\n        Call();\t\t// tabs\n        Call(); \t // mixed\n    }\n\n    void Call() { }\n}",
		"""
		public class C
		{
		    public void M()
		    {
		        Call(); // tabs
		        Call(); // mixed
		    }

		    void Call() { }
		}
		""");

	[Test]
	public Task Deterministic_layout_puts_one_space_before_a_trailing_line_comment() => Formats(
		// With a width, a line's text is a function of its tokens and the width, not of the source's spacing.
		"""
		public class C
		{
		    public bool M(int x)
		    {
		        Call();     // why we call
		        return x switch
		        {
		            0 => true,      // zero
		            _ => false,
		        };
		    }

		    void Call() { }
		}
		""",
		"""
		public class C
		{
		    public bool M(int x)
		    {
		        Call(); // why we call
		        return x switch
		        {
		            0 => true, // zero
		            _ => false,
		        };
		    }

		    void Call()
		    { }
		}
		""",
		editorConfig: "max_line_length = 120");

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

	/// <summary>
	/// A blank line between the comments above a closing brace stays, as dotnet format leaves it. Curb dropped
	/// it above a block's, a type's and, once its comments moved in with the statements, a switch's brace.
	/// See <see href="https://github.com/nullean/curb/issues/230">issue #230</see>.
	/// </summary>
	[Test]
	public Task A_blank_line_between_comments_above_a_closing_brace_stays() => Unchanged(
		"""
		public class C
		{
		    public void M(int x)
		    {
		        {
		            Call();
		            // one

		            // two
		        }
		        {
		            Call();
		            /* one */

		            /* two */
		        }
		        switch (x)
		        {
		            case 1:
		                break;
		                // one

		                /* two */
		        }
		    }

		    // one

		    // two
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
	public Task Under_tabs_a_comment_under_a_trailing_comment_aligns_under_it() => Formats(
		// dotnet format re-lays any trivia indented with a tab, so with indent_style = tab the comment
		// at the member column is never in shape: it goes under the trailing comment, as tabs then
		// spaces. A blank line still parts it from the trailing comment. The source has no tab, so it
		// is Curb's own output that dotnet format would re-lay.
		"""
		public class C
		{
		    enum E
		    {
		        A, // one
		        // before b
		        B,
		    }

		    void M()
		    {
		        Call(); // one
		        // first
		        // second
		        Call(); // one

		        // after a blank line
		        Call();
		    }
		}
		""",
		"public class C\n{\n\tenum E\n\t{\n\t\tA, // one\n\t\t   // before b\n\t\tB,\n\t}\n\n\tvoid M()\n\t{\n\t\tCall(); // one\n\t\t\t\t// first\n\t\t\t\t// second\n\t\tCall(); // one\n\n\t\t// after a blank line\n\t\tCall();\n\t}\n}",
		editorConfig: "indent_style = tab\ntab_width = 4\nindent_size = 4");

	[Test]
	public Task Under_tabs_a_comment_between_collection_elements_stays_at_the_element_column() => Formats(
		// dotnet format leaves the trivia between collection elements alone, tabs or not, but re-lays
		// it between the members of an object initializer. The width drops the blank lines, which brings
		// each comment directly under the trailing one.
		"public class C\n{\n\tvoid M()\n\t{\n\t\tvar l = new List<int>\n\t\t{\n\t\t\t1, // one\n\n\t\t\t// collection\n\t\t\t2,\n\t\t};\n\t\tvar d = new Dictionary<int, int>\n\t\t{\n\t\t\t{ 1, 2 }, // one\n\n\t\t\t// entry\n\t\t\t{ 3, 4 },\n\t\t};\n\t\tint[] c =\n\t\t[\n\t\t\t1, // one\n\n\t\t\t// expression\n\t\t\t2,\n\t\t];\n\t\tint[] a =\n\t\t{\n\t\t\t1, // one\n\n\t\t\t// array\n\t\t\t2,\n\t\t};\n\t\tvar o = new V\n\t\t{\n\t\t\tA = 1, // one\n\t\t\t// member\n\t\t\tB = 2,\n\t\t};\n\t}\n}",
		"public class C\n{\n\tvoid M()\n\t{\n\t\tvar l = new List<int>\n\t\t{\n\t\t\t1, // one\n\t\t\t// collection\n\t\t\t2,\n\t\t};\n\t\tvar d = new Dictionary<int, int>\n\t\t{\n\t\t\t{ 1, 2 }, // one\n\t\t\t// entry\n\t\t\t{\n\t\t\t\t3,\n\t\t\t\t4\n\t\t\t},\n\t\t};\n\t\tint[] c =\n\t\t[\n\t\t\t1, // one\n\t\t\t// expression\n\t\t\t2,\n\t\t];\n\t\tint[] a = {\n\t\t\t1, // one\n\t\t\t// array\n\t\t\t2,\n\t\t};\n\t\tvar o = new V\n\t\t{\n\t\t\tA = 1, // one\n\t\t\t\t   // member\n\t\t\tB = 2,\n\t\t};\n\t}\n}",
		editorConfig: "indent_style = tab\ntab_width = 4\nindent_size = 4\nmax_line_length = 120");

	[Test]
	public Task Under_tabs_a_comment_a_dropped_blank_line_brings_under_a_trailing_comment_aligns() => Formats(
		// Curb drops the blank line after an opening brace, so the comment ends up directly under the
		// brace's trailing comment, where dotnet format aligns it: two columns in, which takes no tab.
		"""
		public class C
		{ // opener

		    // legacy
		    public int First;
		}
		""",
		"public class C\n{ // opener\n  // legacy\n\tpublic int First;\n}",
		editorConfig: "indent_style = tab\ntab_width = 4\nindent_size = 4");

	[Test]
	public Task A_tab_indented_comment_under_a_trailing_comment_aligns_under_it() => Formats(
		// The source's tabs put the trivia out of shape even when Curb writes spaces, though the comment
		// starts its line the way the statement does.
		"public class C\n{\n\tvoid M()\n\t{\n\t\tCall(); // one\n\t\t// tabbed\n\t\tCall();\n\t}\n}",
		"""
		public class C
		{
		    void M()
		    {
		        Call(); // one
		                // tabbed
		        Call();
		    }
		}
		""");

	[Test]
	public Task A_comment_under_a_statement_printed_verbatim_aligns_under_its_trailing_comment() => Formats(
		// goto has no printer of its own, and the verbatim path did not record its last token, so the
		// comment below never saw the trailing comment and went to the statement column.
		"""
		public class C
		{
		    int M()
		    {
		        goto done; // order
		   // out of shape
		        return 1;
		    done:
		        return 0;
		    }
		}
		""",
		"""
		public class C
		{
		    int M()
		    {
		        goto done; // order
		                   // out of shape
		        return 1;
		    done:
		        return 0;
		    }
		}
		""");

	[Test]
	public Task A_comment_under_a_declaration_kept_as_written_aligns_under_its_trailing_comment() => Formats(
		// The same, through the path the = ignore settings take.
		"""
		public class C
		{
		    void M()
		    {
		        var x  =  1; // one
		   // out of shape
		        Use(x);
		    }
		}
		""",
		"""
		public class C
		{
		    void M()
		    {
		        var x  =  1; // one
		                     // out of shape
		        Use(x);
		    }
		}
		""",
		editorConfig: "csharp_space_around_declaration_statements = ignore");

	[Test]
	public Task A_comment_under_a_multi_line_string_aligns_under_its_trailing_comment() => Formats(
		// The column is the one on the string's last line, not the string's whole length.
		"""
		public class C
		{
		    void M()
		    {
		        var s = @"first
		second"; // verbatim
		   // out of shape
		        Use(s);
		    }
		}
		""",
		"""
		public class C
		{
		    void M()
		    {
		        var s = @"first
		second"; // verbatim
		         // out of shape
		        Use(s);
		    }
		}
		""");

	// Issue #16: the comment above a section's first statement sits where that statement does. The
	// label's comment broke the line at the label's column, the comment stayed there, and the next
	// run read it as out of shape and moved it under the label's comment.
	[Test]
	public Task A_comment_under_a_case_labels_trailing_comment_stays_at_the_statement_column() => Unchanged(
		"""
		public class C
		{
		    int M(int k)
		    {
		        switch (k)
		        {
		            case 1: // one
		            case 2:     // two
		                // shared
		                return 1;
		            case 3: // three
		                    // under three
		                return 3;
		        }
		        return 0;
		    }
		}
		""");

	[Test]
	public Task A_comment_under_a_case_labels_trailing_comment_that_has_to_move_aligns_under_it() => Formats(
		// Out of shape, at the label's column, so dotnet format moves it under the label's comment.
		"""
		public class C
		{
		    int M(int k)
		    {
		        switch (k)
		        {
		            case 2: // two
		            // shared
		                return 1;
		        }
		        return 0;
		    }
		}
		""",
		"""
		public class C
		{
		    int M(int k)
		    {
		        switch (k)
		        {
		            case 2: // two
		                    // shared
		                return 1;
		        }
		        return 0;
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

	// ---- the line a trailing comment breaks ----------------------------------------------------------

	// dotnet format leaves every one of these continuations where the author put it, so each output
	// below is a fixed point of it.

	[Test]
	public Task A_continuation_after_a_trailing_comment_stays_one_level_in() => Unchanged(
		"""
		public class C : Base, // base
		    IFoo, // foo
		    IBar
		{
		    public IEnumerable<int> M(int a)
		    {
		        var y = // eq
		            a;
		        y = // assign
		            a;
		        yield return // y
		            a;
		        throw // t
		            new E();
		    }

		    public int N(int a)
		    {
		        return // r
		            a;
		    }
		}
		""");

	[Test]
	public Task A_continuation_after_a_trailing_comment_stays_one_level_in_with_a_width() => Unchanged(
		// The break is the comment's, not the author's, so a width does not read it away. It used to put
		// the continuation at the statement's column: the comment's break was the only one there, and a
		// break after it that printed flat left the line where the comment's indent had put it.
		"""
		public class C : Base, // base
		    IFoo, // foo
		    IBar
		{
		    public IEnumerable<int> M(int a)
		    {
		        var y = // eq
		            a;
		        y = // assign
		            a;
		        yield return // y
		            a;
		        throw // t
		            new E();
		    }

		    public int N(int a)
		    {
		        return // r
		            a;
		    }
		}
		""",
		editorConfig: "max_line_length = 120");

	[Test]
	public Task A_continuation_after_a_trailing_comment_moves_one_level_in() => Formats(
		"""
		public class C : Base, // base
		IFoo
		{
		    public int M(int a)
		    {
		        var y = // eq
		        a;
		        return // r
		        y;
		    }
		}
		""",
		// The value below `=` stays at the author's column, as dotnet format leaves it: #130 keeps a
		// value the author put below `=` where they put it, comment or not. A base list's next type does
		// too, since #231 keeps every line the author broke a base list onto.
		"""
		public class C : Base, // base
		IFoo
		{
		    public int M(int a)
		    {
		        var y = // eq
		        a;
		        return // r
		            y;
		    }
		}
		""");

	// ---- a trailing comment at the end of a group ---------------------------------------------------

	// Every expectation below was checked against dotnet format whitespace, which leaves each input
	// unchanged.
	//
	// With no width, a plain arm keeps its line through AuthorJoined on the arrow anyway (#104), so the
	// first two cases pass with or without the BreakPropagation exemption; they guard the shape, not
	// the mechanism. The `_with_a_width` variants, and the ternary case with no width, fail without it.

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
	public Task A_trailing_comment_after_a_last_ternary_leaves_it_on_one_line() => Unchanged(
		// No author break is read here: the ternary's own group held the comment as its last content, so
		// its line break opened the ternary at `?` and `:`.
		"""
		public class C
		{
		    public int M(int y)
		    {
		        var o = new O
		        {
		            A = 1,
		            B = y > 1 ? 1 : 2 // b
		        };
		        return y switch
		        {
		            1 => 1,
		            _ => y > 1 ? 1 : 2 // two
		        };
		    }
		}

		public class O { public int A; public int B; }
		""");

	[Test]
	public Task A_switch_arm_broken_before_the_arrow_stays_broken() => Unchanged(
		// The arrow used to join the pattern's line whether or not a comment followed the arm.
		"""
		public class C
		{
		    public int M(int y)
		    {
		        return y switch
		        {
		            1
		                => 1,
		            > 1 when y < 10 // guarded
		                => 2,
		            > 10 when y < 100
		                => 3,
		            _ when y > 1000
		                => 4 // four
		        };
		    }
		}
		""");

	[Test]
	public Task A_switch_arm_broken_before_the_arrow_stays_broken_with_tabs() => Unchanged(
		"""
		public class C
		{
			public int M(int y)
			{
				return y switch
				{
					> 10 when y < 100
						=> 3,
					_ when y > 1000
						=> 4 // four
				};
			}
		}
		""",
		editorConfig: "indent_style = tab");

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

	/// <summary>
	/// The spaces the author put on either side of an inline <c>/* */</c> comment stay as written, as
	/// <c>dotnet format</c> leaves them. See <see href="https://github.com/nullean/curb/issues/148">issue #148</see>.
	/// </summary>
	[Test]
	public Task Several_spaces_beside_an_inline_block_comment_are_kept() => Unchanged(
		"""
		public class C
		{
		    public int M(int a)
		    {
		        var x =   /* c */   1;
		        Use(a  /* c */  , a);
		        Use(/* c */   a,   /* d */a);
		        Use(a /* c */  /* d */  , a);
		        if (a > 0) { Use(a, a); /* after */   }
		        return x   /* r */;
		    }

		    private void Use(int a, int b) { }
		}
		""");

	[Test]
	public Task Deterministic_layout_puts_one_space_beside_an_inline_block_comment() => Formats(
		"""
		public class C
		{
		    public int M(int a)
		    {
		        var x =   /* c */   1;
		        Use(a  /* c */  , a);
		        return x   /* r */;
		    }

		    private void Use(int a, int b) { }
		}
		""",
		"""
		public class C
		{
		    public int M(int a)
		    {
		        var x = /* c */ 1;
		        Use(a /* c */ , a);
		        return x /* r */;
		    }

		    private void Use(int a, int b)
		    { }
		}
		""",
		editorConfig: "max_line_length = 120");

	[Test]
	public Task Whitespace_after_a_line_comment_does_not_count_toward_the_width() => Formats(
		// Roslyn counts the whitespace after a `//` comment as part of it, and the width decision measured
		// it although trim_trailing_whitespace strips it: the first line broke on the first run and, with
		// the whitespace gone, joined on the second (issue #188). The second line is too long either way.
		"public class C\n{\n    public int Total(int a, int b) => a + b; // note  \t\n    public int Longer(int alpha, int beta) => alpha + beta;\n}",
		"""
		public class C
		{
		    public int Total(int a, int b) => a + b; // note
		    public int Longer(int alpha, int beta) =>
		        alpha + beta;
		}
		""",
		editorConfig: "max_line_length = 52");

	[Test]
	public Task A_comment_under_a_trailing_comment_leaves_its_column_once_a_blank_line_parts_them() => Formats(
		// The blank line csharp_blank_lines_around_type adds between the types parts the comment from the
		// trailing comment above it. Aligned under that comment anyway, it kept the column on the first run,
		// and the second, which no longer found it directly under, moved it to the type's (issue #188).
		"""
		public class A
		{
		} // trailing
		   // leading
		public class B
		{
		}
		""",
		"""
		public class A
		{
		} // trailing

		// leading
		public class B
		{
		}
		""");
}
