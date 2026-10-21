namespace Nullean.Curb.Tests.Formatting.Expressions;

/// <summary>
/// Continuation lines of an argument or a ternary branch indent once from the line they start on, as
/// <c>dotnet format whitespace</c> leaves them. See
/// <see href="https://github.com/nullean/curb/issues/106">issue #106</see> and
/// <see href="https://github.com/nullean/curb/issues/108">issue #108</see>.
/// </summary>
public class ContinuationIndentTests : FormattingTest
{
	// Issue #108: an argument on the `(` line that continues below indents once, not twice.

	[Test]
	public Task A_nested_call_argument_continues_one_level_in() => Unchanged(
		"""
		public class C
		{
		    public void M()
		    {
		        Outer(Inner(
		            value));
		    }
		}
		""");

	[Test]
	public Task A_continuation_written_two_levels_in_stays_there() => Unchanged(
		"""
		public class C
		{
		    public void M()
		    {
		        Outer(Inner(
		                value));
		        var flagged = items.Where(x =>
		                x.IsEnabled &&
		                x.IsVisible);
		        var r = items.Select(x => x.Flag
		                ? x.A
		                : x.B);
		        Foo(x =>
		                x.Bar(
		                    y));
		    }
		}
		""");

	[Test]
	public Task A_continuation_aligned_to_a_column_stays_there() => Unchanged(
		"""
		public class C
		{
		    public void M()
		    {
		        Outer(Inner(
		              aligned));
		        Foo(x => x
		                  .Where(y => y)
		                  .ToList());
		    }
		}
		""");

	[Test]
	public Task A_reindented_statement_takes_its_two_level_continuation_along() => Formats(
		"""
		public class C
		{
		    public void M()
		    {
		            Outer(Inner(
		                    value));
		    var flagged = items.Where(x => x.IsEnabled &&
		            x.IsVisible);
		    }
		}
		""",
		"""
		public class C
		{
		    public void M()
		    {
		        Outer(Inner(
		                value));
		        var flagged = items.Where(x => x.IsEnabled &&
		                x.IsVisible);
		    }
		}
		""");

	[Test]
	public Task A_continuation_off_the_tab_stops_keeps_its_column() => Unchanged(
		"public class C\n{\n\tpublic void M()\n\t{\n\t\tOuter(Inner(\n\t\t\t  offstop));\n\t\tOuter(Inner(\n\t\t\t\ttwo));\n\t}\n}",
		editorConfig: "indent_style = tab");

	[Test]
	public Task A_reindented_statement_off_the_tab_stops_moves_its_continuation_with_it() => Formats(
		"public class C\n{\n\tpublic void M()\n\t{\n\t\t\tOuter(Inner(\n\t\t\t      reindented));\n\t}\n}",
		"public class C\n{\n\tpublic void M()\n\t{\n\t\tOuter(Inner(\n\t\t\t  reindented));\n\t}\n}",
		editorConfig: "indent_style = tab");

	[Test]
	public Task An_argument_starting_after_another_argument_s_continuation_keeps_its_offset() => Unchanged(
		"""
		public class C
		{
		    public void M()
		    {
		        Foo(Bar(
		            a), Baz(
		            b));
		        Outer(
		            first, Inner(
		                    value));
		    }
		}
		""");

	[Test]
	public Task A_brace_or_a_multi_line_string_does_not_stand_for_the_continuation() => Unchanged(
		""""
		public class C
		{
		    public void M()
		    {
		        Foo(Bar(x =>
		        {
		            Run();
		        }, a));
		        Foo(Bar(new C
		        {
		            A = 1,
		        }, a));
		        Foo(Bar(
		            """
		            raw
		            """,
		            b));
		    }
		}
		"""");

	[Test]
	public Task A_nested_call_with_several_arguments_continues_one_level_in() => Unchanged(
		"""
		public class C
		{
		    public void M()
		    {
		        Foo(Bar(a,
		            b));
		        Foo(Bar(Baz(
		            a)));
		    }
		}
		""");

	[Test]
	public Task A_later_argument_on_the_open_paren_line_continues_one_level_in() => Unchanged(
		"""
		public class C
		{
		    public void M()
		    {
		        Outer(first, Inner(
		            value));
		    }
		}
		""");

	[Test]
	public Task An_argument_on_a_line_of_its_own_continues_one_level_in_from_that_line() => Unchanged(
		"""
		public class C
		{
		    public void M()
		    {
		        Outer(
		            first, Inner(
		                value));
		    }
		}
		""");

	[Test]
	public Task A_binary_argument_continues_at_the_list_level() => Unchanged(
		"""
		public class C
		{
		    public void M()
		    {
		        Foo(a &&
		            b);
		        Foo(a +
		            b, c);
		    }
		}
		""");

	[Test]
	public Task An_awaited_negated_or_cast_call_argument_continues_one_level_in() => Unchanged(
		"""
		public class C
		{
		    public async Task M()
		    {
		        Foo(await Bar(
		            a));
		        Foo(!Bar(
		            a));
		        Foo((int)Bar(
		            a));
		        Foo(new Bar(
		            a));
		    }
		}
		""");

	[Test]
	public Task A_lambda_body_on_the_arrow_line_continues_one_level_in() => Unchanged(
		"""
		public class C
		{
		    public void M()
		    {
		        var flagged = items.Where(x => x.IsEnabled &&
		            x.IsVisible);
		        Foo(x => Bar(
		            a));
		    }
		}
		""");

	[Test]
	public Task A_lambda_body_below_the_arrow_is_one_level_in() => Unchanged(
		"""
		public class C
		{
		    public void M()
		    {
		        Foo(x =>
		            x.Bar);
		        var q = items.Select(x =>
		            x.Bar &&
		            x.Baz);
		        Foo(x =>
		            x.Bar(
		                y));
		    }
		}
		""");

	[Test]
	public Task A_broken_ternary_lambda_body_in_an_argument_is_one_level_in() => Unchanged(
		"""
		public class C
		{
		    public void M()
		    {
		        var r = items.Select(x => x.Flag
		            ? x.A
		            : x.B);
		        Foo(x => x.Flag
		            ? Bar(
		                a)
		            : b);
		    }
		}
		""");

	[Test]
	public Task A_member_chain_lambda_body_in_an_argument_is_one_level_in() => Unchanged(
		"""
		public class C
		{
		    public void M()
		    {
		        Foo(x => x
		            .Where(y => y)
		            .ToList());
		        Foo(x
		            .Where(y => y));
		    }
		}
		""");

	[Test]
	public Task A_lambda_body_bringing_its_own_braces_anchors_to_the_statement() => Unchanged(
		"""
		public class C
		{
		    public void M()
		    {
		        Foo(x => new C
		        {
		            A = 1,
		        });
		        Foo(x => x switch
		        {
		            _ => 1,
		        });
		        Foo(a, x => new C
		        {
		            A = 1,
		        });
		    }
		}
		""");

	[Test]
	public Task A_block_lambda_argument_is_unchanged() => Unchanged(
		"""
		public class C
		{
		    public void M()
		    {
		        Foo(x =>
		        {
		            Run();
		        });
		        Foo(a, x =>
		        {
		            Run();
		        });
		        Foo(Bar(x =>
		        {
		            Run();
		        }));
		    }
		}
		""");

	[Test]
	public Task A_nested_call_in_an_arrow_body_or_attribute_continues_one_level_in() => Unchanged(
		"""
		public class C
		{
		    public int P => Foo(Bar(
		        a));

		    [Attr(Foo(
		        a))]
		    public void N() => Foo(Bar(
		        a));

		    [Attr(new[]
		    {
		        "a",
		    })]
		    [Attr(x, new[]
		    {
		        "a",
		    })]
		    public void O()
		    {
		    }
		}
		""");

	[Test]
	public Task A_nested_call_argument_continues_one_tab_in() => Formats(
		"""
		public class C
		{
		    public void M()
		    {
		        Outer(Inner(
		            value));
		        var flagged = items.Where(x => x.IsEnabled &&
		            x.IsVisible);
		    }
		}
		""",
		"public class C\n{\n\tpublic void M()\n\t{\n\t\tOuter(Inner(\n\t\t\tvalue));\n\t\tvar flagged = items.Where(x => x.IsEnabled &&\n\t\t\tx.IsVisible);\n\t}\n}",
		editorConfig: "indent_style = tab");

	[Test]
	public Task A_reindented_statement_keeps_its_argument_one_level_in() => Formats(
		"""
		public class C
		{
		    public void M()
		    {
		            Outer(Inner(
		                value));
		    }
		}
		""",
		"""
		public class C
		{
		    public void M()
		    {
		        Outer(Inner(
		            value));
		    }
		}
		""");

	// Issue #106: a block-bodied lambda in a ternary keeps its shape, and the `:` after its closing
	// brace starts a line of its own at the statement's indent.

	[Test]
	public Task A_colon_after_a_block_lambda_in_a_ternary_moves_to_its_own_line() => Formats(
		"""
		public class C
		{
		    public void M(bool enabled)
		    {
		        Action handler = enabled ? () =>
		        {
		            Run();
		        } : null;
		    }
		}
		""",
		"""
		public class C
		{
		    public void M(bool enabled)
		    {
		        Action handler = enabled ? () =>
		        {
		            Run();
		        }
		        : null;
		    }
		}
		""");

	[Test]
	public Task A_colon_indented_after_a_block_lambda_comes_back_to_the_statement() => Formats(
		"""
		public class C
		{
		    public void M(bool enabled)
		    {
		        Action handler = enabled ? () =>
		        {
		            Run();
		        }
		            : null;
		    }
		}
		""",
		"""
		public class C
		{
		    public void M(bool enabled)
		    {
		        Action handler = enabled ? () =>
		        {
		            Run();
		        }
		        : null;
		    }
		}
		""");

	[Test]
	public Task A_ternary_broken_before_a_block_lambda_keeps_the_lambda_on_its_line() => Formats(
		"""
		public class C
		{
		    public void M(bool enabled)
		    {
		        Action handler = enabled
		            ? () =>
		            {
		                Run();
		            }
		            : null;
		    }
		}
		""",
		"""
		public class C
		{
		    public void M(bool enabled)
		    {
		        Action handler = enabled
		            ? () =>
		            {
		                Run();
		            }
		        : null;
		    }
		}
		""");

	[Test]
	public Task Block_lambdas_and_delegates_in_either_branch_split_before_the_colon() => Formats(
		"""
		public class C
		{
		    public void M(bool enabled)
		    {
		        Foo(enabled ? () =>
		        {
		            Run();
		        } : null);
		        Action a = enabled ? () =>
		        {
		            Run();
		        } : () =>
		        {
		            Stop();
		        };
		        Action b = enabled ? delegate
		        {
		            Run();
		        } : null;
		        Action c = enabled ? async x =>
		        {
		            await Run(x);
		        } : null;
		        return enabled ? () =>
		        {
		        } : null;
		    }
		}
		""",
		"""
		public class C
		{
		    public void M(bool enabled)
		    {
		        Foo(enabled ? () =>
		        {
		            Run();
		        }
		        : null);
		        Action a = enabled ? () =>
		        {
		            Run();
		        }
		        : () =>
		        {
		            Stop();
		        };
		        Action b = enabled ? delegate
		        {
		            Run();
		        }
		        : null;
		        Action c = enabled ? async x =>
		        {
		            await Run(x);
		        }
		        : null;
		        return enabled ? () =>
		        {
		        }
		        : null;
		    }
		}
		""");

	[Test]
	public Task A_colon_after_a_block_lambda_with_a_trailing_comment_is_kept() => Unchanged(
		"""
		public class C
		{
		    public void M(bool enabled)
		    {
		        Action handler = enabled ? () =>
		        {
		            Run();
		        } // note
		        : null;
		    }

		    Action P => enabled ? () =>
		    {
		        Run();
		    }
		    : null;
		}
		""");

	[Test]
	public Task A_single_line_block_lambda_or_an_initializer_keeps_the_colon_inline() => Unchanged(
		"""
		public class C
		{
		    public void M(bool enabled)
		    {
		        Action a = enabled ? () => { Run(); } : null;
		        Action b = enabled ? null : () =>
		        {
		            Run();
		        };
		        object c = enabled ? new C
		        {
		            X = 1,
		        } : null;
		    }
		}
		""");

	[Test]
	public Task A_colon_after_a_block_lambda_moves_to_its_own_line_with_tabs() => Formats(
		"""
		public class C
		{
		    public void M(bool enabled)
		    {
		        Action handler = enabled ? () =>
		        {
		            Run();
		        } : null;
		    }
		}
		""",
		"public class C\n{\n\tpublic void M(bool enabled)\n\t{\n\t\tAction handler = enabled ? () =>\n\t\t{\n\t\t\tRun();\n\t\t}\n\t\t: null;\n\t}\n}",
		editorConfig: "indent_style = tab");

	[Test]
	public Task A_reindented_ternary_with_a_block_lambda_moves_with_its_statement() => Formats(
		"""
		public class C
		{
		    public void M(bool enabled)
		    {
		            Action handler = enabled ? () =>
		            {
		                Run();
		            } : null;
		    }
		}
		""",
		"""
		public class C
		{
		    public void M(bool enabled)
		    {
		        Action handler = enabled ? () =>
		        {
		            Run();
		        }
		        : null;
		    }
		}
		""");
}
