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
		editorConfig: "csharp_indent_labels = no_indent");

	[Test]
	public Task Flip_when_block_matches_no_indent_everywhere_tested() => Formats(
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
		editorConfig: "csharp_indent_labels = flip_when_block");

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
