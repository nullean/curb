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
}
