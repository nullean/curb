namespace Nullean.Curb.Tests.Formatting.Options;

/// <summary>
/// <c>csharp_indent_case_contents</c>, <c>_case_contents_when_block</c>, <c>_switch_labels</c>,
/// <c>_block_contents</c> and <c>_labels</c>.
/// </summary>
/// <remarks>
/// The three switch options compose: <c>_switch_labels</c> decides where the labels sit inside the
/// braces, and the contents are then placed relative to the label by whichever of the other two
/// applies — <c>_case_contents_when_block</c> for a section that opens with a braced body,
/// <c>_case_contents</c> for anything else. The first statement decides for the whole section.
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

	// dotnet format picks a section's indent from its first statement and holds it for the rest: a `break`
	// after a leading block goes where the block went, and a block after a leading statement goes with the
	// statement. So does a comment above the switch's brace, from the last section (issue #326).
	[Test]
	public Task A_section_opening_with_a_block_takes_the_block_indent() => Formats(
		"""
		public class C
		{
		    public void M()
		    {
		        switch (a)
		        {
		            case 1:
		                {
		                    Call();
		                }
		                break;
		            case 2:
		                Call();
		                {
		                    Call();
		                }
		                // tail
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
		            {
		                Call();
		            }
		            break;
		            case 2:
		                Call();
		                {
		                    Call();
		                }
		                // tail
		        }
		    }
		}
		""",
		editorConfig: "csharp_indent_case_contents_when_block = false");

	[Test]
	public Task A_section_opening_with_a_statement_takes_the_contents_indent() => Formats(
		"""
		public class C
		{
		    public void M()
		    {
		        switch (a)
		        {
		            case 1:
		                {
		                    Call();
		                }
		                break;
		            case 2:
		                Call();
		                {
		                    Call();
		                }
		                // tail
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
		                {
		                    Call();
		                }
		                break;
		            case 2:
		            Call();
		            {
		                Call();
		            }
		            // tail
		        }
		    }
		}
		""",
		editorConfig: "csharp_indent_case_contents = false");

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

	// A section's block goes a level in like any other: its `{` sits level with what it holds and its `}`,
	// not with the label's statements. A comment above the brace stays at the statements' indent.
	[Test]
	public Task Indented_braces_move_a_case_block_brace_in() => WithAndWithout(
		"""
		public class C
		{
		    public void M()
		    {
		        switch (a)
		        {
		            case 1:
		                // first
		                {
		                    Call();
		                    break;
		                }
		            default:
		            {
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
		                // first
		                    {
		                    Call();
		                    break;
		                    }
		            default:
		                    {
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
		            // first
		                {
		                Call();
		                break;
		                }
		            default:
		                {
		                break;
		                }
		            }
		        }
		    }
		""",
		opinionConfig: "csharp_indent_case_contents_when_block = false",
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

	// dotnet format indents a switch expression's arms from the line its `{` sits on, and closes it a
	// level in from there. Once the brace has a line of its own, arms and closing brace move twice.
	[Test]
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

	[Test]
	public Task A_nested_switch_expression_shifts_from_its_own_brace() => Formats(
		"""
		public class C
		{
		    public int M(int x, int y)
		    {
		        return x switch
		        {
		            1 => 1,
		            _ => y switch
		            {
		                2 => 2,
		                _ => 0,
		            },
		        };
		    }
		}
		""",
		"""
		public class C
		    {
		    public int M(int x, int y)
		        {
		        return x switch
		            {
		                1 => 1,
		                _ => y switch
		                    {
		                        2 => 2,
		                        _ => 0,
		                        },
		                };
		        }
		    }
		""",
		editorConfig: "csharp_indent_braces = true");

	[Test]
	public Task A_switch_expression_brace_on_switch_leaves_its_arms_alone() => Formats(
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
		public class C {
		    public int M(int x) {
		        return x switch {
		            1 => 1,
		            _ => 0,
		            };
		        }
		    }
		""",
		editorConfig: """
		csharp_indent_braces = true
		csharp_new_line_before_open_brace = none
		""");

	// dotnet format does not apply csharp_indent_braces to a lambda's block: the brace stays where the
	// author put it and what it holds goes a level in from it, as without the option.
	[Test]
	public Task A_lambda_block_indents_its_statements_from_its_brace() => Formats(
		"""
		class C
		    {
		    void M()
		        {
		        Action<int> a = x =>
		            {
		            switch (x)
		                {
		                case 1:
		                    break;
		                }
		            };
		        Run(x =>
		        {
		        Foo(x);
		        });
		        }
		    }
		""",
		"""
		class C
		    {
		    void M()
		        {
		        Action<int> a = x =>
		            {
		                switch (x)
		                    {
		                    case 1:
		                        break;
		                    }
		            };
		        Run(x =>
		        {
		            Foo(x);
		        });
		        }
		    }
		""",
		editorConfig: "csharp_indent_braces = true");

	// Under a width the brace's column is Curb's to choose: a lambda's goes to the statement's indent and an
	// anonymous method's a level in, where dotnet format puts one it moves down.
	[Test]
	public Task A_lambda_block_takes_a_fixed_shape_under_a_width() => Formats(
		"""
		class C
		    {
		    void M()
		        {
		        Run(x =>
		            {
		            Foo(x);
		            });
		        Run(delegate
		        {
		        Foo();
		        });
		        }
		    }
		""",
		"""
		class C
		    {
		    void M()
		        {
		        Run(x =>
		        {
		            Foo(x);
		        });
		        Run(delegate
		            {
		                Foo();
		                });
		        }
		    }
		""",
		editorConfig: """
		csharp_indent_braces = true
		max_line_length = 100
		""");

	// A brace moved down from the arrow lands at the statement's indent, not a level in.
	[Test]
	public Task A_lambda_brace_moved_off_the_arrow_is_not_indented() => Formats(
		"""
		class C
		    {
		    void M()
		        {
		        Run(() => {
		            Foo();
		            });
		        }
		    }
		""",
		"""
		class C
		    {
		    void M()
		        {
		        Run(() =>
		        {
		            Foo();
		        });
		        }
		    }
		""",
		editorConfig: "csharp_indent_braces = true");

	// An anonymous method's brace keeps its column too, but its closing brace sits a level in from it, with
	// the statements. One moved down from `delegate` takes the option's level, and the rest follows it.
	[Test]
	public Task An_anonymous_method_closes_level_with_its_statements() => Formats(
		"""
		class C
		    {
		    void M()
		        {
		        Run(delegate
		        {
		        Foo();
		        });
		        Run(delegate {
		            Foo();
		            });
		        }
		    }
		""",
		"""
		class C
		    {
		    void M()
		        {
		        Run(delegate
		        {
		            Foo();
		            });
		        Run(delegate
		            {
		                Foo();
		                });
		        }
		    }
		""",
		editorConfig: "csharp_indent_braces = true");

	// With the brace beside the arrow the option still leaves a lambda's closing brace at the statement's
	// indent, and still moves an anonymous method's a level in.
	[Test]
	public Task A_lambda_brace_on_the_arrow_closes_at_the_statement() => Formats(
		"""
		class C {
		    void M() {
		        Run(() => {
		            Foo();
		            });
		        Run(delegate {
		            Foo();
		        });
		        }
		    }
		""",
		"""
		class C {
		    void M() {
		        Run(() => {
		            Foo();
		        });
		        Run(delegate {
		            Foo();
		            });
		        }
		    }
		""",
		editorConfig: """
		csharp_indent_braces = true
		csharp_new_line_before_open_brace = none
		""");

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
		// Curb used to accept `no_indent` and `flip_when_block` — the names of Roslyn's internal enum
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
	public Task A_comment_above_a_label_sits_where_the_statements_do() => Formats(
		// dotnet format leaves the label one level out but its comments where the statements are,
		// a comment under a trailing comment included; Curb took them out with the label.
		"""
		public class C
		{
		    public int M()
		    {
		        var x = 0;
		    // at the label
		    retry:
		        x++;
		        Call(); // trailing
		        // under the trailing comment
		    done:
		        return x;
		    }
		}
		""",
		"""
		public class C
		{
		    public int M()
		    {
		        var x = 0;
		        // at the label
		    retry:
		        x++;
		        Call(); // trailing
		        // under the trailing comment
		    done:
		        return x;
		    }
		}
		""");

	[Test]
	public Task Flush_left_takes_the_comments_above_a_label_to_column_zero() => Formats(
		"""
		public class C
		{
		    public void M()
		    {
		        // above
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
		// above
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
