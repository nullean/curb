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

	// Issue #139: a body below `=>` stays as far in from the line it belongs to as the author put it.

	[Test]
	public Task A_body_below_the_arrow_keeps_the_offset_the_author_gave_it() => Unchanged(
		"""
		public class C
		{
		    public int[] Values() =>
		            [1, 2];

		    public int Count() =>
		            Compute(1);

		    public int Aligned() =>
		          Compute(3);

		    public void M()
		    {
		        System.Func<int> f = () =>
		                Compute(2);
		        Foo(x =>
		                x + 1);
		        var s = 1 switch
		        {
		            1 =>
		                    2,
		            _ => 3
		        };
		    }
		}
		""");

	[Test]
	public Task A_body_below_the_arrow_after_a_trailing_comment_keeps_its_offset() => Unchanged(
		"""
		public class C
		{
		    public int Q => // note
		            2;

		    public void M()
		    {
		        var s = 1 switch
		        {
		            1 => // why
		                    2,
		            _ => 3
		        };
		    }
		}
		""");

	[Test]
	public Task A_reindented_member_takes_the_body_below_its_arrow_along() => Formats(
		"""
		public class C
		{
		  public int Shifted() =>
		          4;

		    public void M()
		    {
		            System.Func<int> f = () =>
		                    Compute(2);
		    }
		}
		""",
		"""
		public class C
		{
		    public int Shifted() =>
		            4;

		    public void M()
		    {
		        System.Func<int> f = () =>
		                Compute(2);
		    }
		}
		""");

	[Test]
	public Task A_body_below_the_arrow_keeps_its_offset_with_tabs() => Unchanged(
		"public class C\n{\n\tpublic int Count() =>\n\t\t\tCompute(1);\n\n\tpublic int Aligned() =>\n\t\t  Compute(3);\n\n\tpublic void M()\n\t{\n\t\tSystem.Func<int> f = () =>\n\t\t\t\tCompute(2);\n\t}\n}",
		editorConfig: "indent_style = tab");

	[Test]
	public Task Deterministic_layout_still_decides_a_body_below_the_arrow() => Formats(
		"""
		public class C
		{
		    public int Count() =>
		            Compute(1);

		    public void M()
		    {
		        System.Func<int> f = () =>
		                Compute(2);
		    }
		}
		""",
		"""
		public class C
		{
		    public int Count() => Compute(1);

		    public void M()
		    {
		        System.Func<int> f = () => Compute(2);
		    }
		}
		""",
		editorConfig: "max_line_length = 120");

	// Issue #143: an argument the author broke to keeps its offset from the line the list starts on.

	[Test]
	public Task An_argument_line_keeps_the_offset_the_author_gave_it() => Unchanged(
		"""
		public class C
		{
		    public void M(int[] arr)
		    {
		        Foo(
		                first,
		                second);
		        Foo(first,
		                second);
		        Foo(
		              aligned);
		        var x = arr[
		                0];
		        var y = new C(
		                    1,
		                      2);
		    }
		}
		""");

	[Test]
	public Task An_argument_line_after_a_comment_or_a_blank_line_keeps_its_offset() => Unchanged(
		"""
		public class C
		{
		    public void M()
		    {
		        Foo( // why
		                first,
		                second);
		        Foo(
		                // lead
		                first,

		                second);
		    }
		}
		""");

	[Test]
	public Task An_argument_line_s_own_continuation_and_block_follow_it() => Unchanged(
		"""
		public class C
		{
		    public void M()
		    {
		        Foo(first,
		                Bar(1,
		                    2),
		                  third);
		        Foo(
		                new C
		                {
		                    X = 1
		                },
		                second);
		        Foo(
		                a, new C
		                {
		                    X = 1
		                });
		        Foo(
		            a, new C
		            {
		                X = 1
		            });
		        Foo(
		                a,
		                x =>
		                {
		                    return x;
		                });
		    }
		}
		""");

	[Test]
	public Task An_argument_line_s_lambda_body_keeps_its_own_continuation_where_it_is() => Unchanged(
		"""
		public class C
		{
		    public string M(string[] constants)
		    {
		        return constants.FirstOrDefault(
		            name => constants.All(c => c.Has(name)) && constants
		                    .Select(c => c[name])
		                    .Count() == 2
		        );
		    }
		}
		""");

	// The line a list or an arrow ends up on can be the closing line of a raw string, which is the
	// string's own text and so has not moved; reading its indent as a shift drifted a level per run.

	[Test]
	public Task An_argument_or_body_below_the_end_of_a_raw_string_keeps_its_offset() => Unchanged(
		""""
		public class C : B
		{
		    public void M()
		    {
		        var s = """
		            text
		            """.Replace(
		            "a",
		            b);
		        var t = """
		            text
		            """.Replace(
		                "a",
		                b);
		    }

		    public C() : base("""
		        text
		        """) =>
		        Run();
		}
		"""");

	[Test]
	public Task An_argument_below_the_end_of_a_raw_string_keeps_its_offset_with_tabs() => Unchanged(
		"public class C\n{\n\tpublic void M()\n\t{\n\t\tvar t = \"\"\"\n\t\t\ttext\n\t\t\t\"\"\".Replace(\n\t\t\t\t\"a\",\n\t\t\t\tb);\n\t\tvar u = \"\"\"\n\t\t\ttext\n\t\t\t\"\"\".Replace(\n\t\t\t\"a\",\n\t\t\tb);\n\t}\n}",
		editorConfig: "indent_style = tab");

	[Test]
	public Task A_reindented_statement_takes_its_argument_lines_along() => Formats(
		"""
		public class C
		{
		    public void M()
		    {
		  Shift(
		          a,
		              b);
		    }
		}
		""",
		"""
		public class C
		{
		    public void M()
		    {
		        Shift(
		                a,
		                    b);
		    }
		}
		""");

	[Test]
	public Task An_argument_line_keeps_its_offset_with_tabs() => Unchanged(
		"public class C\n{\n\tpublic void M(int[] arr)\n\t{\n\t\tFoo(\n\t\t\t\tfirst,\n\t\t\t\tsecond);\n\t\tFoo(\n\t\t\t  aligned);\n\t\tvar x = arr[\n\t\t\t\t0];\n\t}\n}",
		editorConfig: "indent_style = tab");

	// Issue #16: a lambda whose body hangs off its arrow indents that body a level before the body
	// indents its own lines, so an argument line inside it is two levels into the lambda, not one.
	// Read as one, `.Kind()` moved a level right on every run.
	[Test]
	public Task A_chain_line_in_a_lambda_argument_with_an_operator_keeps_its_offset() => Unchanged(
		"""
		public class C
		{
		    object M()
		    {
		        return all
		            .Where(label => label.First().Get(x)
		                .Kind() is 1 or 2);
		    }

		    void N()
		    {
		        Where(label => label.First()
		            .Kind() == 1);
		        Where(label => label
		                .Kind() is 1);
		        Where((a, b) => a.First()
		            .Kind() != b);
		    }
		}
		""");

	[Test]
	public Task A_chain_line_in_a_lambda_argument_with_an_operator_keeps_its_offset_with_tabs() => Unchanged(
		"public class C\n{\n\tvoid N()\n\t{\n\t\tWhere(label => label.First()\n\t\t\t.Kind() == 1);\n\t}\n}",
		editorConfig: "indent_style = tab");

	// Issue #16: a tuple keeps the lines the author broke it onto, the one after `(` and the `)`'s
	// included, at their offset from the line it starts on. It joined `(` / `Name: m,` and put the rest
	// at the statement's column, which the next run moved again.
	[Test]
	public Task A_tuple_keeps_its_lines_at_their_offset() => Formats(
		"""
		public class C
		{
		    object M(int[] xs)
		    {
		          return xs.Select(m => (
		              Name: m,
		              Path: m
		          ));
		    }

		    object N()
		    {
		        var t = (1,
		                 2);
		        return (
		            t,
		            2);
		    }
		}
		""",
		"""
		public class C
		{
		    object M(int[] xs)
		    {
		        return xs.Select(m => (
		            Name: m,
		            Path: m
		        ));
		    }

		    object N()
		    {
		        var t = (1,
		                 2);
		        return (
		            t,
		            2);
		    }
		}
		""");

	// Issue #16: an initializer Curb opens out puts its brace on a line of its own, and that brace, not
	// the element below it, is the argument's first continuation line. Read from the element, the
	// braces landed under it on the first run and back at the indent on the second.
	[Test]
	public Task An_initializer_opened_out_in_a_lambda_argument_settles_in_one_run() => Formats(
		"""
		public class C
		{
		    void M()
		    {
		        Assert(() => Create(new[] { Parse(a),
		                                    Parse(b) },
		                            options: x));
		    }
		}
		""",
		"""
		public class C
		{
		    void M()
		    {
		        Assert(() => Create(new[]
		        {
		            Parse(a),
		            Parse(b)
		        },
		                            options: x));
		    }
		}
		""",
		editorConfig: "csharp_new_line_before_members_in_object_initializers = true");

	[Test]
	public Task Deterministic_layout_still_decides_an_argument_line() => Formats(
		"""
		public class C
		{
		    public void M()
		    {
		        Foo(
		                first,
		                second);
		    }
		}
		""",
		"""
		public class C
		{
		    public void M()
		    {
		        Foo(first, second);
		    }
		}
		""",
		editorConfig: "max_line_length = 120");
}
