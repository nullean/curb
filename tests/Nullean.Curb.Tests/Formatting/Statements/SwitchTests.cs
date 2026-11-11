namespace Nullean.Curb.Tests.Formatting.Statements;

/// <summary>Switch statements: labels indent one level, their contents a second.</summary>
public class SwitchTests : FormattingTest
{
	[Test]
	public Task Switch_with_cases() => Unchanged(
		"""
		public class C
		{
		    public void M(int value)
		    {
		        switch (value)
		        {
		            case 1:
		                First();
		                break;
		            case 2:
		                Second();
		                break;
		        }
		    }
		}
		""");

	[Test]
	public Task Case_keyword_keeps_its_space() => Formats(
		"""
		public class C
		{
		    public void M(int value)
		    {
		        switch (value)
		        {
		        case 1:
		        First();
		        break;
		        }
		    }
		}
		""",
		"""
		public class C
		{
		    public void M(int value)
		    {
		        switch (value)
		        {
		            case 1:
		                First();
		                break;
		        }
		    }
		}
		""");

	[Test]
	public Task Default_case() => Unchanged(
		"""
		public class C
		{
		    public void M(int value)
		    {
		        switch (value)
		        {
		            case 1:
		                First();
		                break;
		            default:
		                Other();
		                break;
		        }
		    }
		}
		""");

	[Test]
	public Task Several_labels_on_one_section() => Unchanged(
		"""
		public class C
		{
		    public void M(int value)
		    {
		        switch (value)
		        {
		            case 1:
		            case 2:
		                Both();
		                break;
		        }
		    }
		}
		""");

	/// <remarks>
	/// The extra level is <c>csharp_indent_case_contents_when_block</c>, whose default is true:
	/// when a case body is a block, both the braces and their contents indent under the label.
	/// </remarks>
	[Test]
	public Task Case_with_a_braced_block_indents_the_braces_too() => Unchanged(
		"""
		public class C
		{
		    public void M(int value)
		    {
		        switch (value)
		        {
		            case 1:
		                {
		                    var local = 1;
		                    Call(local);
		                    break;
		                }
		        }
		    }
		}
		""");

	[Test]
	public Task Empty_switch() => Unchanged(
		"""
		public class C
		{
		    public void M(int value)
		    {
		        switch (value)
		        {
		        }
		    }
		}
		""");

	[Test]
	public Task Pattern_case_label() => Unchanged(
		"""
		public class C
		{
		    public void M(object value)
		    {
		        switch (value)
		        {
		            case int number:
		                Call(number);
		                break;
		            case string text:
		                Call(text);
		                break;
		        }
		    }
		}
		""");

	[Test]
	public Task Case_label_with_a_when_clause() => Unchanged(
		"""
		public class C
		{
		    public void M(object value)
		    {
		        switch (value)
		        {
		            case int number when number > 0:
		                Call(number);
		                break;
		        }
		    }
		}
		""");

	[Test]
	public Task Case_returning_instead_of_breaking() => Unchanged(
		"""
		public class C
		{
		    public int M(int value)
		    {
		        switch (value)
		        {
		            case 1:
		                return 1;
		            default:
		                return 0;
		        }
		    }
		}
		""");

	[Test]
	public Task Switch_expression() => Unchanged(
		"""
		public class C
		{
		    public string M(int value)
		    {
		        return value switch
		        {
		            1 => "one",
		            2 => "two",
		            _ => "many",
		        };
		    }
		}
		""");

	[Test]
	public Task Switch_expression_with_patterns() => Unchanged(
		"""
		public class C
		{
		    public string M(object value)
		    {
		        return value switch
		        {
		            int number when number > 0 => "positive",
		            string { Length: 0 } => "empty",
		            null => "null",
		            _ => "other",
		        };
		    }
		}
		""");

	[Test]
	public Task Switch_expression_arms_go_one_per_line() => Formats(
		"""
		public class C
		{
		    public string M(int value)
		    {
		        return value switch { 1 => "one", _ => "many", };
		    }
		}
		""",
		"""
		public class C
		{
		    public string M(int value)
		    {
		        return value switch
		        {
		            1 => "one",
		            _ => "many",
		        };
		    }
		}
		""");

	[Test]
	public Task Switch_expression_with_a_throw_arm() => Unchanged(
		"""
		public class C
		{
		    public string M(int value)
		    {
		        return value switch
		        {
		            1 => "one",
		            _ => throw new ArgumentOutOfRangeException(nameof(value)),
		        };
		    }
		}
		""");

	[Test]
	public Task Nested_switch() => Unchanged(
		"""
		public class C
		{
		    public void M(int outer, int inner)
		    {
		        switch (outer)
		        {
		            case 1:
		                switch (inner)
		                {
		                    case 2:
		                        Call();
		                        break;
		                }
		                break;
		        }
		    }
		}
		""");

	[Test]
	public Task Comment_inside_a_switch_section() => Unchanged(
		"""
		public class C
		{
		    public void M(int value)
		    {
		        switch (value)
		        {
		            case 1:
		                // explains the case
		                First();
		                break;
		        }
		    }
		}
		""");

	// Issue #238: a switch expression that is a lambda's body keeps a brace the author put on a line of its
	// own at their offset, with the arms one level in from it, as dotnet format leaves it.

	[Test]
	public Task A_switch_lambda_body_keeps_the_offset_its_brace_was_given() => Unchanged(
		"""
		public class C
		{
		    public bool N(int[] xs)
		    {
		        var a = xs.Any(x => x switch
		                {
		                    0 => true,
		                    _ => false,
		                });
		        Func<int, bool> f = x => x switch
		                {
		                    // the common case
		                    0 => true,

		                    _ => false,
		                };
		        var g = xs.Select((x) =>
		            x switch
		                {
		                    0 => y => y switch
		                        {
		                            _ => 1,
		                        },
		                    _ => null,
		                });
		        return xs.Any(x => x switch
		                {
		                    0 => true,
		                    _ => false,
		                }.Equals(true));
		    }
		}
		""");

	[Test]
	public Task A_switch_lambda_body_brace_lines_its_arms_and_closing_brace_up_from_it() => Formats(
		"""
		public class C
		{
		    public bool N(int[] xs)
		    {
		    var a = xs.Any(x => x switch
		                {
		                0 => true,
		                      _ => false,
		            });
		        return false;
		    }
		}
		""",
		"""
		public class C
		{
		    public bool N(int[] xs)
		    {
		        var a = xs.Any(x => x switch
		                    {
		                        0 => true,
		                        _ => false,
		                    });
		        return false;
		    }
		}
		""");

	[Test]
	public Task A_switch_elsewhere_in_a_lambda_body_still_takes_its_lines_indent() => Formats(
		"""
		public class C
		{
		    public void M()
		    {
		        Foo(x => Bar(1, x switch
		                {
		                    0 => true,
		                    _ => false,
		                }));
		        Foo(x =>
		        {
		            return x switch
		                {
		                    _ => 1,
		                };
		        });
		    }
		}
		""",
		"""
		public class C
		{
		    public void M()
		    {
		        Foo(x => Bar(1, x switch
		        {
		            0 => true,
		            _ => false,
		        }));
		        Foo(x =>
		        {
		            return x switch
		            {
		                _ => 1,
		            };
		        });
		    }
		}
		""");

	[Test]
	public Task Deterministic_layout_still_places_a_switch_lambda_body_brace() => Formats(
		"""
		public class C
		{
		    public bool N(int[] xs)
		    {
		        var a = xs.Any(x => x switch
		                {
		                    0 => true,
		                    _ => false,
		                });
		        return a;
		    }
		}
		""",
		"""
		public class C
		{
		    public bool N(int[] xs)
		    {
		        var a = xs.Any(
		            x =>
		                x switch
		                {
		                    0 => true,
		                    _ => false,
		                }
		        );
		        return a;
		    }
		}
		""",
		editorConfig: "max_line_length = 120");

	/// <summary>
	/// A comment after the last section's statements goes where those statements are, as dotnet format puts
	/// it, whatever column the author wrote it at. Curb moved it out to the brace's column. See
	/// <see href="https://github.com/nullean/curb/issues/230">issue #230</see>.
	/// </summary>
	[Test]
	public Task A_comment_at_the_end_of_the_last_section_goes_with_its_statements() => Formats(
		"""
		public class C
		{
		    public int M(int x)
		    {
		        switch (x)
		        {
		            case 1:
		                return 1;
		            case 2:
		                break;
		            // trailing note in the last section
		        }
		        switch (x)
		        {
		            case 1:
		                break;
		// column zero

		            // one
		            /* two */
		        }
		        switch (x)
		        {
		            case 1:
		            // label only
		        }
		        switch (x)
		        {
		            default:
		                {
		                    break;
		                }
		                    // after a block
		        }
		        switch (x)
		        {
		            case 1: break;
		            // after a one-line section
		        }
		        switch (x)
		        {
		                // no section
		        }

		        return 0;
		    }
		}
		""",
		// Checked against dotnet format, which writes exactly this.
		"""
		public class C
		{
		    public int M(int x)
		    {
		        switch (x)
		        {
		            case 1:
		                return 1;
		            case 2:
		                break;
		                // trailing note in the last section
		        }
		        switch (x)
		        {
		            case 1:
		                break;
		                // column zero

		                // one
		                /* two */
		        }
		        switch (x)
		        {
		            case 1:
		                // label only
		        }
		        switch (x)
		        {
		            default:
		                {
		                    break;
		                }
		                // after a block
		        }
		        switch (x)
		        {
		            case 1: break;
		                // after a one-line section
		        }
		        switch (x)
		        {
		            // no section
		        }

		        return 0;
		    }
		}
		""");

	/// <summary>
	/// The comment follows the indent options of the statements it goes with: a block's
	/// <c>csharp_indent_case_contents_when_block</c>, anything else's <c>csharp_indent_case_contents</c>.
	/// </summary>
	[Test]
	public Task A_comment_at_the_end_of_the_last_section_follows_the_case_contents_options() => WithAndWithout(
		"""
		public class C
		{
		    public int M(int x)
		    {
		        switch (x)
		        {
		            case 1:
		            break;
		        // after a statement
		        }
		        switch (x)
		        {
		            default:
		            {
		                break;
		            }
		        // after a block
		        }

		        return 0;
		    }
		}
		""",
		"""
		public class C
		{
		    public int M(int x)
		    {
		        switch (x)
		        {
		            case 1:
		            break;
		            // after a statement
		        }
		        switch (x)
		        {
		            default:
		                {
		                    break;
		                }
		                // after a block
		        }

		        return 0;
		    }
		}
		""",
		"""
		public class C
		{
		    public int M(int x)
		    {
		        switch (x)
		        {
		            case 1:
		            break;
		            // after a statement
		        }
		        switch (x)
		        {
		            default:
		            {
		                break;
		            }
		            // after a block
		        }

		        return 0;
		    }
		}
		""",
		opinionConfig: "csharp_indent_case_contents_when_block = false",
		editorConfig: "csharp_indent_case_contents = false");

	// Issue #254: a `switch` keyword the author put below its governing expression stays on its own line,
	// moved as far as the statement's first line moved, as dotnet format leaves it.

	[Test]
	public Task A_switch_keyword_on_a_line_of_its_own_stays_there() => Formats(
		"""
		public class C
		{
		    public int P => Q
		        switch
		    {
		        1 => 2,
		        _ => 3,
		    };

		    public int M(int[] xs, int s, object o)
		    {
		            var a = s
		                switch
		            {
		                1 => 2,
		                _ => 3,
		            };
		    var b = xs.Select(x => x
		        switch
		        {
		            1 => 2,
		            _ => 3,
		        });
		        var c = (s
		            // a note
		            switch
		        {
		            1 => 2,
		            _ => 3,
		        });
		        switch (o)
		        {
		            case int i when i
		                switch
		            {
		                1 => true,
		                _ => false,
		            }:
		                return 1;
		        }
		        return a + c;
		    }
		}
		""",
		"""
		public class C
		{
		    public int P => Q
		        switch
		    {
		        1 => 2,
		        _ => 3,
		    };

		    public int M(int[] xs, int s, object o)
		    {
		        var a = s
		            switch
		        {
		            1 => 2,
		            _ => 3,
		        };
		        var b = xs.Select(x => x
		            switch
		            {
		                1 => 2,
		                _ => 3,
		            });
		        var c = (s
		            // a note
		            switch
		        {
		            1 => 2,
		            _ => 3,
		        });
		        switch (o)
		        {
		            case int i when i
		                switch
		            {
		                1 => true,
		                _ => false,
		            }:
		                return 1;
		        }
		        return a + c;
		    }
		}
		""");

	[Test]
	public Task A_brace_kept_on_a_switch_line_of_its_own_indents_the_arms_from_that_line() => Formats(
		"""
		public class C
		{
		    public int M(int s)
		    {
		        var a = s
		            switch {
		            1 => s
		                switch {
		                _ => 4,
		            },
		            _ => 3,
		        };
		        return a;
		    }
		}
		""",
		"""
		public class C {
		    public int M(int s) {
		        var a = s
		            switch {
		                1 => s
		                    switch {
		                        _ => 4,
		                    },
		                _ => 3,
		            };
		        return a;
		    }
		}
		""",
		editorConfig: "csharp_new_line_before_open_brace = none");

	[Test]
	public Task Deterministic_layout_joins_a_switch_keyword_onto_its_expression() => Formats(
		"""
		public class C
		{
		    public int M(int s)
		    {
		        var a = s
		            switch
		        {
		            1 => 2,
		            _ => 3,
		        };
		        return a;
		    }
		}
		""",
		"""
		public class C
		{
		    public int M(int s)
		    {
		        var a = s switch
		        {
		            1 => 2,
		            _ => 3,
		        };
		        return a;
		    }
		}
		""",
		editorConfig: "max_line_length = 120");

	// An operand switch and a query's `select` switch in a lambda body, and a brace left on the `switch`
	// line, take the indent of the line the governing expression starts on, as dotnet format places them.
	// These came out a level deeper before the #229 fix and are kept here so they stay put.

	[Test]
	public Task An_operand_or_select_switch_takes_its_governing_lines_indent() => Formats(
		"""
		public class C
		{
		    public void M(int[] xs)
		    {
		        var a = xs.Select(x => 1 + x switch
		            {
		                1 => 2,
		                _ => 3,
		            });
		        var b = xs.Select(
		            x => x switch
		                {
		                    1 => 2,
		                    _ => 3,
		                } + 1);
		        var c = xs
		            .Select(x => x switch {
		                1 => 2,
		                _ => 3,
		            });
		        var q = from x in xs
		                select x switch
		        {
		            1 => 2,
		            _ => 3,
		        };
		    }
		}
		""",
		"""
		public class C
		{
		    public void M(int[] xs)
		    {
		        var a = xs.Select(x => 1 + x switch
		        {
		            1 => 2,
		            _ => 3,
		        });
		        var b = xs.Select(
		            x => x switch
		            {
		                1 => 2,
		                _ => 3,
		            } + 1);
		        var c = xs
		            .Select(x => x switch
		            {
		                1 => 2,
		                _ => 3,
		            });
		        var q = from x in xs
		                select x switch
		                {
		                    1 => 2,
		                    _ => 3,
		                };
		    }
		}
		""");
}
