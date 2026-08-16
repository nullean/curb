namespace Nullean.Kerf.Tests.Formatting.Options;

/// <summary>
/// <c>csharp_indent_case_contents</c>, <c>_case_contents_when_block</c>, <c>_switch_labels</c>,
/// <c>_block_contents</c> and <c>_labels</c>.
/// </summary>
/// <remarks>
/// The three switch options compose: <c>_switch_labels</c> decides where the labels sit inside the
/// braces, and the contents are then placed relative to the label by whichever of the other two
/// applies — <c>_case_contents_when_block</c> for a braced body, <c>_case_contents</c> for anything
/// else. A section holding both takes both decisions.
/// </remarks>
public class IndentationTests : FormattingTest
{
	// ---- csharp_indent_case_contents -------------------------------------------------------------

	[Test]
	public Task Case_contents_are_indented_by_default() => Unchanged(
		"""
		public class C
		{
		    public void M()
		    {
		        switch (a)
		        {
		            case 1:
		                Call();
		                break;
		        }
		    }
		}
		""");

	[Test]
	public Task Case_contents_can_sit_level_with_their_label() => Formats(
		"""
		public class C
		{
		    public void M()
		    {
		        switch (a)
		        {
		            case 1:
		                Call();
		                break;
		            default:
		                break;
		        }
		    }
		}
		""",
		"""
		public class C
		{
		    public void M()
		    {
		        switch (a)
		        {
		            case 1:
		            Call();
		            break;
		            default:
		            break;
		        }
		    }
		}
		""",
		editorConfig: "csharp_indent_case_contents = false");

	[Test]
	public Task A_braced_case_body_is_not_governed_by_the_contents_option() => Formats(
		"""
		public class C
		{
		    public void M()
		    {
		        switch (a)
		        {
		            case 1:
		                Call();
		                break;
		            case 2:
		                {
		                    Call();
		                    break;
		                }
		        }
		    }
		}
		""",
		"""
		public class C
		{
		    public void M()
		    {
		        switch (a)
		        {
		            case 1:
		            Call();
		            break;
		            case 2:
		                {
		                    Call();
		                    break;
		                }
		        }
		    }
		}
		""",
		editorConfig: "csharp_indent_case_contents = false");

	// ---- csharp_indent_case_contents_when_block ---------------------------------------------------

	[Test]
	public Task A_braced_case_body_can_sit_level_with_its_label() => Formats(
		"""
		public class C
		{
		    public void M()
		    {
		        switch (a)
		        {
		            case 2:
		                {
		                    Call();
		                    break;
		                }
		        }
		    }
		}
		""",
		"""
		public class C
		{
		    public void M()
		    {
		        switch (a)
		        {
		            case 2:
		            {
		                Call();
		                break;
		            }
		        }
		    }
		}
		""",
		editorConfig: "csharp_indent_case_contents_when_block = false");

	// ---- csharp_indent_switch_labels --------------------------------------------------------------

	[Test]
	public Task Switch_labels_can_sit_level_with_the_brace() => Formats(
		"""
		public class C
		{
		    public void M()
		    {
		        switch (a)
		        {
		            case 1:
		                Call();
		                break;
		            default:
		                break;
		        }
		    }
		}
		""",
		"""
		public class C
		{
		    public void M()
		    {
		        switch (a)
		        {
		        case 1:
		            Call();
		            break;
		        default:
		            break;
		        }
		    }
		}
		""",
		editorConfig: "csharp_indent_switch_labels = false");

	[Test]
	public Task The_contents_follow_the_label_wherever_it_goes() => Formats(
		"""
		public class C
		{
		    public void M()
		    {
		        switch (a)
		        {
		            case 1:
		                Call();
		                break;
		        }
		    }
		}
		""",
		"""
		public class C
		{
		    public void M()
		    {
		        switch (a)
		        {
		        case 1:
		        Call();
		        break;
		        }
		    }
		}
		""",
		editorConfig: """
		csharp_indent_switch_labels = false
		csharp_indent_case_contents = false
		""");

	// ---- csharp_indent_block_contents -------------------------------------------------------------

	[Test]
	public Task Block_contents_are_indented_by_default() => Unchanged(
		"""
		public class C
		{
		    public void M()
		    {
		        Call();
		    }
		}
		""");

	[Test]
	public Task Block_contents_can_sit_level_with_their_braces() => Formats(
		"""
		public class C
		{
		    public void M()
		    {
		        Call();
		        {
		            Nested();
		        }
		        if (a)
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
		    Call();
		    {
		    Nested();
		    }
		    if (a)
		    {
		    Call();
		    }
		    }
		}
		""",
		editorConfig: "csharp_indent_block_contents = false");

	[Test]
	public Task Type_members_are_not_block_contents() => Formats(
		"""
		public class C
		{
		    private int _a;

		    public void M()
		    {
		        Call();
		    }
		}
		""",
		"""
		public class C
		{
		    private int _a;

		    public void M()
		    {
		    Call();
		    }
		}
		""",
		editorConfig: "csharp_indent_block_contents = false");

	// ---- csharp_indent_braces --------------------------------------------------------------------

	[Test]
	public Task Braces_sit_with_their_construct_by_default() => Unchanged(
		"""
		public class C
		{
		    public void M()
		    {
		        Call();
		    }
		}
		""");

	[Test]
	public Task Braces_can_be_indented_to_meet_their_contents() => Formats(
		"""
		namespace N
		{
		    public enum E
		    {
		        One,
		    }

		    public class C
		    {
		        public void M()
		        {
		            Call();
		        }
		    }
		}
		""",
		// The contents do not move; the braces come to meet them.
		"""
		namespace N
		    {
		    public enum E
		        {
		        One,
		        }

		    public class C
		        {
		        public void M()
		            {
		            Call();
		            }
		        }
		    }
		""",
		editorConfig: "csharp_indent_braces = true");

	[Test]
	public Task Indented_braces_cover_accessors_switches_and_control_blocks() => Formats(
		"""
		public class C
		{
		    public int Q
		    {
		        get
		        {
		            return 1;
		        }
		    }

		    public void M()
		    {
		        switch (a)
		        {
		            case 1:
		                break;
		        }
		        if (a)
		        {
		            Call();
		        }
		    }
		}
		""",
		"""
		public class C
		    {
		    public int Q
		        {
		        get
		            {
		            return 1;
		            }
		        }

		    public void M()
		        {
		        switch (a)
		            {
		            case 1:
		                break;
		            }
		        if (a)
		            {
		            Call();
		            }
		        }
		    }
		""",
		editorConfig: "csharp_indent_braces = true");

	[Test]
	public Task Indented_braces_leave_brackets_alone() => Formats(
		"""
		public class C
		{
		    public void M()
		    {
		        int[] c = [1];
		    }
		}
		""",
		"""
		public class C
		    {
		    public void M()
		        {
		        int[] c = [1];
		        }
		    }
		""",
		editorConfig: "csharp_indent_braces = true");

	[Test]
	[Skip("dotnet format also shifts a switch expression's arms and closes it two levels in; Kerf moves only the braces, as it does everywhere else")]
	public Task A_switch_expression_shifts_its_arms_too() => Formats(
		"""
		public class C
		{
		    public int M(int x)
		    {
		        return x switch
		        {
		            1 => 1,
		            _ => 0,
		        };
		    }
		}
		""",
		"""
		public class C
		    {
		    public int M(int x)
		        {
		        return x switch
		            {
		                1 => 1,
		                _ => 0,
		                };
		        }
		    }
		""",
		editorConfig: "csharp_indent_braces = true");

	// ---- csharp_indent_labels ---------------------------------------------------------------------

	[Test]
	public Task A_label_sits_one_level_left_by_default() => Unchanged(
		"""
		public class C
		{
		    public void M()
		    {
		    outer:
		        Call();
		        {
		        inner:
		            Call();
		        }
		    }
		}
		""");

	[Test]
	public Task A_label_can_sit_level_with_its_statements() => Formats(
		"""
		public class C
		{
		    public void M()
		    {
		    outer:
		        Call();
		        {
		        inner:
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
		        outer:
		        Call();
		        {
		            inner:
		            Call();
		        }
		    }
		}
		""",
		editorConfig: "csharp_indent_labels = no_change");

	[Test]
	public Task Flush_left_puts_a_label_at_column_zero() => Formats(
		// Measured from dotnet format, which puts it at column zero whatever the surrounding indent.
		// Kerf used to accept `no_indent` and `flip_when_block` — the names of Roslyn's internal enum
		// rather than of its EditorConfig values — so the documented `flush_left` fell through to the
		// default and column zero was never reachable at all.
		"""
		public class C
		{
		    public void M()
		    {
		        outer:
		        Call();
		    }
		}
		""",
		"""
		public class C
		{
		    public void M()
		    {
		outer:
		        Call();
		    }
		}
		""",
		editorConfig: "csharp_indent_labels = flush_left");

	[Test]
	public Task A_label_inside_a_switch_section_follows_the_same_rule() => Unchanged(
		"""
		public class C
		{
		    public void M()
		    {
		        switch (a)
		        {
		            case 1:
		            inSection:
		                Call();
		                break;
		        }
		    }
		}
		""");

	[Test]
	public Task An_unrecognised_label_value_falls_back_to_the_default() => Unchanged(
		"""
		public class C
		{
		    public void M()
		    {
		    outer:
		        Call();
		    }
		}
		""",
		editorConfig: "csharp_indent_labels = left");
}
