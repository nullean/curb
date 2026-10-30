namespace Nullean.Curb.Tests.Formatting.Expressions;

/// <summary>
/// Breaks in a bracketed argument list, the <c>:</c> after a block lambda in a ternary, and a lambda
/// block's offset stay where <c>dotnet format whitespace</c> leaves them. See
/// <see href="https://github.com/nullean/curb/issues/124">issue #124</see>,
/// <see href="https://github.com/nullean/curb/issues/134">issue #134</see> and
/// <see href="https://github.com/nullean/curb/issues/135">issue #135</see>.
/// </summary>
public class BracketAndLambdaBlockTests : FormattingTest
{
	// Issue #124: a bracketed argument list the author broke keeps its breaks.

	[Test]
	public Task A_broken_element_access_is_kept() => Unchanged(
		"""
		public class C
		{
		    public void M(int[] arr, int[][] grid, int[]? maybe, int[,] two)
		    {
		        Foo(arr[
		            0]);
		        var x = arr[
		            0];
		        var y = arr[
		            0
		        ];
		        var w = maybe?[
		            0];
		        var v = two[0,
		            1];
		        var u = two[
		            0,
		            1];
		    }
		}
		""");

	[Test]
	public Task Nested_and_chained_brackets_keep_their_breaks() => Unchanged(
		"""
		public class C
		{
		    public void M(int[] arr, int[][] grid)
		    {
		        var y = grid[
		            0][
		            1];
		        var z = grid[arr[
		            0]];
		        var t = arr[Foo(
		            a)];
		        var d = new Dictionary<int, int>
		        {
		            [
		                1] = 2,
		        };
		    }
		}
		""");

	[Test]
	public Task A_joined_element_access_stays_joined() => Unchanged(
		"""
		public class C
		{
		    public void M(int[] arr, int[,] two)
		    {
		        var x = arr[0];
		        var v = two[0, 1];
		    }
		}
		""");

	[Test]
	public Task A_reindented_element_access_moves_with_its_statement() => Formats(
		"""
		public class C
		{
		    public void M(int[] arr)
		    {
		            var x = arr[
		                0];
		    }
		}
		""",
		"""
		public class C
		{
		    public void M(int[] arr)
		    {
		        var x = arr[
		            0];
		    }
		}
		""");

	[Test]
	public Task A_broken_element_access_with_tabs_is_kept() => Unchanged(
		"public class C\n{\n\tpublic void M(int[] arr)\n\t{\n\t\tvar x = arr[\n\t\t\t0];\n\t\tFoo(arr[\n\t\t\t  0]);\n\t}\n}",
		editorConfig: "indent_style = tab");

	// Issue #134: the `:` after a block lambda goes to the indent of the line its statement, lambda,
	// member, switch arm or initializer element starts on.

	[Test]
	public Task A_colon_after_a_block_lambda_on_a_later_argument_line_goes_to_the_statement() => Formats(
		"""
		public class C
		{
		    public void M(bool enabled)
		    {
		        Register(
		            enabled ? () =>
		            {
		                Run();
		            }
		            : null);
		        Register(a,
		            enabled ? () =>
		            {
		                Run();
		            } : null);
		        var r = Outer(
		            Inner(
		                enabled ? () =>
		                {
		                    Run();
		                } : null));
		    }

		    Action P => Wrap(
		        enabled ? () =>
		        {
		            Run();
		        } : null);
		}
		""",
		"""
		public class C
		{
		    public void M(bool enabled)
		    {
		        Register(
		            enabled ? () =>
		            {
		                Run();
		            }
		        : null);
		        Register(a,
		            enabled ? () =>
		            {
		                Run();
		            }
		        : null);
		        var r = Outer(
		            Inner(
		                enabled ? () =>
		                {
		                    Run();
		                }
		        : null));
		    }

		    Action P => Wrap(
		        enabled ? () =>
		        {
		            Run();
		        }
		    : null);
		}
		""");

	[Test]
	public Task A_colon_after_a_block_lambda_goes_to_its_lambda_arm_or_initializer_line() => Formats(
		"""
		public class C
		{
		    public void M(bool enabled, int value)
		    {
		        Register(
		            x => x ? () =>
		            {
		                Run();
		            } : null);
		        var o = new Options
		        {
		            Handler = enabled ? () =>
		            {
		                Run();
		            } : null,
		        };
		        var s = value switch
		        {
		            1 => enabled ? () =>
		            {
		                Run();
		            } : null,
		            _ => null,
		        };
		    }
		}
		""",
		"""
		public class C
		{
		    public void M(bool enabled, int value)
		    {
		        Register(
		            x => x ? () =>
		            {
		                Run();
		            }
		            : null);
		        var o = new Options
		        {
		            Handler = enabled ? () =>
		            {
		                Run();
		            }
		            : null,
		        };
		        var s = value switch
		        {
		            1 => enabled ? () =>
		            {
		                Run();
		            }
		            : null,
		            _ => null,
		        };
		    }
		}
		""");

	[Test]
	public Task A_colon_after_a_block_lambda_with_a_trailing_comment_goes_to_the_statement() => Unchanged(
		"""
		public class C
		{
		    public void M(bool enabled)
		    {
		        Register(
		            enabled ? () =>
		            {
		                Run();
		            } // note
		        : null);
		    }
		}
		""");

	[Test]
	public Task A_reindented_statement_takes_the_colon_with_it() => Formats(
		"""
		public class C
		{
		    public void M(bool enabled)
		    {
		            Register(
		                enabled ? () =>
		                {
		                    Run();
		                } : null);
		    }
		}
		""",
		"""
		public class C
		{
		    public void M(bool enabled)
		    {
		        Register(
		            enabled ? () =>
		            {
		                Run();
		            }
		        : null);
		    }
		}
		""");

	[Test]
	public Task A_colon_after_a_block_lambda_goes_to_the_statement_with_tabs() => Formats(
		"public class C\n{\n\tpublic void M(bool enabled)\n\t{\n\t\tRegister(\n\t\t\tenabled ? () =>\n\t\t\t{\n\t\t\t\tRun();\n\t\t\t} : null);\n\t}\n}",
		"public class C\n{\n\tpublic void M(bool enabled)\n\t{\n\t\tRegister(\n\t\t\tenabled ? () =>\n\t\t\t{\n\t\t\t\tRun();\n\t\t\t}\n\t\t: null);\n\t}\n}",
		editorConfig: "indent_style = tab");

	// Issue #135: a lambda block keeps the offset the author gave it from the line the lambda
	// starts on.

	[Test]
	public Task A_lambda_block_indented_past_its_line_stays_there() => Unchanged(
		"""
		public class C
		{
		    public void M(bool enabled)
		    {
		        Action a = () =>
		            {
		                Run();
		            };
		        Register(() =>
		            {
		                Run();
		            });
		        Register(x, () =>
		            {
		                Run();
		            });
		        Action b = enabled ? () =>
		            {
		                Run();
		            }
		        : null;
		        Action e = delegate
		            {
		                Run();
		            };
		    }
		}
		""");

	[Test]
	public Task A_lambda_block_at_any_offset_stays_there() => Unchanged(
		"""
		public class C
		{
		    public void M()
		    {
		        Action c = () =>
		                {
		                    Run();
		                };
		        Action d = () =>
		          {
		              Run();
		          };
		        Action g = () =>
		    {
		        Run();
		    };
		        Register(() =>
		            {
		                Register(() =>
		                    {
		                        Run();
		                    });
		            });
		    }
		}
		""");

	[Test]
	public Task A_block_lambda_argument_the_author_put_on_its_own_line_stays_there() => Unchanged(
		"""
		public class C
		{
		    public void M()
		    {
		        Register(
		            () =>
		            {
		                Run();
		            });
		        Register(
		            () =>
		                {
		                    Run();
		                });
		        On(
		            "click", () =>
		            {
		                Run();
		            });
		        Register(() =>
		        {
		            Run();
		        }
		        );
		        Register(
		            new Options
		            {
		                A = 1,
		            });
		    }
		}
		""");

	[Test]
	public Task A_joined_block_lambda_argument_still_hugs() => Unchanged(
		"""
		public class C
		{
		    public void M()
		    {
		        On("click", () =>
		        {
		            Run();
		        });
		        Register(new Options
		        {
		            A = 1,
		        });
		    }
		}
		""");

	// Issue #144: a block callback after a break between two arguments keeps that break, at the
	// column the author gave the argument.

	[Test]
	public Task A_callback_after_a_break_between_arguments_keeps_the_break() => Unchanged(
		"""
		public class C
		{
		    public void M()
		    {
		        On("click",
		            () =>
		            {
		                Run();
		            });
		        On("click",
		            "button", () =>
		            {
		                Run();
		            });
		        On("click", "button",
		            () =>
		            {
		                Run();
		            });
		        On("click", // why
		            () =>
		            {
		                Run();
		            });
		    }
		}
		""");

	[Test]
	public Task A_callback_after_a_break_keeps_the_author_s_column() => Unchanged(
		"""
		public class C
		{
		    public void M()
		    {
		        On("click",
		                () =>
		                {
		                    Run();
		                });
		        On("click",
		            () =>
		                {
		                    Run();
		                });
		        Outer(Inner("click",
		            () =>
		            {
		                Run();
		            }));
		        Outer("a",
		            Inner("click",
		                () =>
		                {
		                    Run();
		                }));
		    }
		}
		""");

	[Test]
	public Task A_callback_first_or_before_a_trailing_argument_keeps_its_breaks() => Unchanged(
		"""
		public class C
		{
		    public void M()
		    {
		        On(() =>
		        {
		            Run();
		        },
		            "click");
		        On("click",
		            () =>
		            {
		                Run();
		            },
		            "x");
		    }
		}
		""");

	[Test]
	public Task A_callback_after_a_break_takes_its_brace_onto_a_line_of_its_own() => Formats(
		"""
		public class C
		{
		    public void M()
		    {
		        On("click",
		            () => {
		                Run();
		            });
		    }
		}
		""",
		"""
		public class C
		{
		    public void M()
		    {
		        On("click",
		            () =>
		            {
		                Run();
		            });
		    }
		}
		""");

	[Test]
	public Task A_reindented_statement_takes_its_callback_with_it() => Formats(
		"""
		public class C
		{
		    public void M()
		    {
		            On("click",
		                () =>
		                {
		                    Run();
		                });
		    }
		}
		""",
		"""
		public class C
		{
		    public void M()
		    {
		        On("click",
		            () =>
		            {
		                Run();
		            });
		    }
		}
		""");

	[Test]
	public Task A_callback_after_a_break_keeps_its_column_with_tabs() => Unchanged(
		"public class C\n{\n\tpublic void M()\n\t{\n\t\tOn(\"click\",\n\t\t\t() =>\n\t\t\t{\n\t\t\t\tRun();\n\t\t\t});\n\t\tOn(\"click\",\n\t\t   () =>\n\t\t   {\n\t\t\t   Run();\n\t\t   });\n\t}\n}",
		editorConfig: "indent_style = tab");

	[Test]
	public Task A_reindented_statement_takes_its_callback_with_it_with_tabs() => Formats(
		"public class C\n{\n\tpublic void M()\n\t{\n\t\t\tOn(\"click\",\n\t\t\t\t() =>\n\t\t\t\t{\n\t\t\t\t\tRun();\n\t\t\t\t});\n\t}\n}",
		"public class C\n{\n\tpublic void M()\n\t{\n\t\tOn(\"click\",\n\t\t\t() =>\n\t\t\t{\n\t\t\t\tRun();\n\t\t\t});\n\t}\n}",
		editorConfig: "indent_style = tab");

	// A middle argument's own continuation keeps the offset the author gave it from the argument's
	// line, as it does in a list without a callback. It had gone a level in from the argument, which
	// also moved the line a callback's block keeps its offset from.

	[Test]
	public Task A_middle_argument_keeps_its_continuation_before_a_callback() => Unchanged(
		"""
		public class C
		{
		    public void M()
		    {
		        On("a", Make(1,
		                     2),
		            () =>
		            {
		                Run();
		            });
		        On("a", Make(1,
		                2),
		            () =>
		            {
		                Run();
		            });
		        On("a",
		            Make(1,
		                     2),
		            () =>
		            {
		                Run();
		            });
		        On("a", x.Where(y => y > 0)
		                 .Select(y => y),
		            () =>
		            {
		                Run();
		            });
		    }
		}
		""");

	[Test]
	public Task A_callback_on_a_continued_argument_s_line_keeps_its_block() => Unchanged(
		"""
		public class C
		{
		    public void M()
		    {
		        On(Make(1,
		                2), () =>
		        {
		            Run();
		        });
		        On("a", Make(1,
		                     2), () =>
		        {
		            Run();
		        });
		        On("a", Make(1, 2), Make(3,
		            4), () =>
		        {
		            Run();
		        });
		    }
		}
		""");

	[Test]
	public Task A_reindented_statement_takes_a_middle_argument_s_continuation_with_it() => Formats(
		"""
		public class C
		{
		    public void M()
		    {
		      On("a", Make(1,
		                   2),
		          () =>
		          {
		              Run();
		          });
		            On("a",
		                Make(1,
		                         2),
		                () =>
		                {
		                    Run();
		                });
		    }
		}
		""",
		"""
		public class C
		{
		    public void M()
		    {
		        On("a", Make(1,
		                     2),
		            () =>
		            {
		                Run();
		            });
		        On("a",
		            Make(1,
		                     2),
		            () =>
		            {
		                Run();
		            });
		    }
		}
		""");

	[Test]
	public Task A_middle_argument_keeps_its_continuation_before_a_callback_with_tabs() => Formats(
		"public class C\n{\n\tpublic void M()\n\t{\n\t\tOn(\"a\", Make(1,\n\t\t             2),\n\t\t\t() =>\n\t\t\t{\n\t\t\t\tRun();\n\t\t\t});\n\t\tOn(\"a\",\n\t\t\tMake(1,\n\t\t\t\t\t 2),\n\t\t\t() =>\n\t\t\t{\n\t\t\t\tRun();\n\t\t\t});\n\t\t\tOn(\"a\", Make(1,\n\t\t\t             2),\n\t\t\t\t() =>\n\t\t\t\t{\n\t\t\t\t\tRun();\n\t\t\t\t});\n\t}\n}",
		"public class C\n{\n\tpublic void M()\n\t{\n\t\tOn(\"a\", Make(1,\n\t\t\t\t\t 2),\n\t\t\t() =>\n\t\t\t{\n\t\t\t\tRun();\n\t\t\t});\n\t\tOn(\"a\",\n\t\t\tMake(1,\n\t\t\t\t\t 2),\n\t\t\t() =>\n\t\t\t{\n\t\t\t\tRun();\n\t\t\t});\n\t\tOn(\"a\", Make(1,\n\t\t\t\t\t 2),\n\t\t\t() =>\n\t\t\t{\n\t\t\t\tRun();\n\t\t\t});\n\t}\n}",
		editorConfig: "indent_style = tab");

	// A statement Curb moves off an `if` line takes a lambda block that was flush with that line
	// nowhere: the block keeps its column, a level left of the call. dotnet format leaves the
	// statement on the `if` line (issue #145) and, after `case 1:`, which it does break, the block
	// at its column in just this way. Pinned so the shape is a decision rather than an accident.

	[Test]
	public Task A_lambda_block_keeps_its_column_when_its_statement_leaves_the_line() => Formats(
		"""
		public class C
		{
		    public void M(bool c, int x)
		    {
		        if (c) Register(() =>
		        {
		            Run();
		        });
		        switch (x)
		        {
		            case 1: Register(() =>
		            {
		                Run();
		            });
		                break;
		        }
		    }
		}
		""",
		"""
		public class C
		{
		    public void M(bool c, int x)
		    {
		        if (c)
		            Register(() =>
		        {
		            Run();
		        });
		        switch (x)
		        {
		            case 1:
		                Register(() =>
		            {
		                Run();
		            });
		                break;
		        }
		    }
		}
		""");

	[Test]
	public Task A_reindented_statement_takes_its_lambda_block_with_it() => Formats(
		"""
		public class C
		{
		    public void M()
		    {
		            Action a = () =>
		                {
		                    Run();
		                };
		    }
		}
		""",
		"""
		public class C
		{
		    public void M()
		    {
		        Action a = () =>
		            {
		                Run();
		            };
		    }
		}
		""");

	[Test]
	public Task A_lambda_block_off_the_tab_stops_keeps_its_column() => Formats(
		"public class C\n{\n\tpublic void M()\n\t{\n\t\tAction a = () =>\n\t\t\t{\n\t\t\t\tRun();\n\t\t\t};\n\t\tAction b = () =>\n\t\t  {\n\t\t      Run();\n\t\t  };\n\t}\n}",
		"public class C\n{\n\tpublic void M()\n\t{\n\t\tAction a = () =>\n\t\t\t{\n\t\t\t\tRun();\n\t\t\t};\n\t\tAction b = () =>\n\t\t  {\n\t\t\t  Run();\n\t\t  };\n\t}\n}",
		editorConfig: "indent_style = tab");
}
