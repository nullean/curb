namespace Nullean.Curb.Tests.Formatting.Expressions;

/// <summary>Object creation, initializers, collections, tuples and patterns.</summary>
public class InitializerTests : FormattingTest
{
	// ---- object creation ----------------------------------------------------------------------

	[Test]
	public Task Object_creation() => Unchanged(
		"""
		public class C
		{
		    public void M()
		    {
		        var value = new Thing();
		    }
		}
		""");

	// A creation whose argument list the author broke, with the initializer's brace on its own line, had
	// two breaks ahead of that brace: one aimed at the list's group, one for where the author put it.
	// Printed together they left a blank line, issue #195.
	[Test]
	public Task Broken_arguments_keep_the_initializer_brace_on_the_next_line() => Unchanged(
		"""
		public class C
		{
		    public static Settings Create() =>
		        new Settings(
		            "first")
		        {
		            Value = 1,
		        };
		}
		""");

	[Test]
	public Task Broken_arguments_of_a_creation_passed_as_an_argument_keep_the_initializer_brace_on_the_next_line() => Unchanged(
		"""
		public class C
		{
		    public void M()
		    {
		        list.Add(new Settings(
		            "first")
		        {
		            Value = 1,
		        });
		    }
		}
		""");

	[Test]
	public Task Broken_arguments_of_an_implicit_creation_keep_the_initializer_brace_on_the_next_line() => Unchanged(
		"""
		public class C
		{
		    public void M()
		    {
		        Settings value = new(
		            "first")
		        {
		            Value = 1,
		        };
		    }
		}
		""");

	[Test]
	public Task Broken_arguments_of_a_with_expression_keep_the_initializer_brace_on_the_next_line() => Unchanged(
		"""
		public class C
		{
		    public Settings M()
		    {
		        return Make(
		            "first") with
		        {
		            Value = 1,
		        };
		    }
		}
		""");

	// A creation that hugs a block-bodied lambda spans lines because the block does, and dotnet format then
	// puts the initializer's brace on a line of its own. The hugged list gives the brace no group to aim at,
	// so with a width it stayed on the `}))` line, where dotnet format moved it (issue #240).
	[Test]
	public Task Issue_240_an_initializer_after_a_hugged_block_lambda_keeps_its_own_line_at_a_width() => Unchanged(
		"""
		public class C
		{
		    public HttpClient M()
		    {
		        return new HttpClient(new Stub(async request =>
		        {
		            await Task.Yield();
		            return new HttpResponseMessage();
		        }))
		        { BaseAddress = new Uri("https://example.test") };
		    }
		}
		""",
		editorConfig: "max_line_length = 120");

	// The block opens out at a width wherever the author put it, and the brace follows it down.
	[Test]
	public Task Issue_240_an_initializer_follows_a_block_lambda_the_width_opened_out() => Formats(
		"""
		public class C
		{
		    public HttpClient M()
		    {
		        return new HttpClient(new Stub(async request => { await Task.Yield(); return new HttpResponseMessage(); })) { BaseAddress = new Uri("https://example.test") };
		    }
		}
		""",
		"""
		public class C
		{
		    public HttpClient M()
		    {
		        return new HttpClient(new Stub(async request =>
		        {
		            await Task.Yield();
		            return new HttpResponseMessage();
		        }))
		        { BaseAddress = new Uri("https://example.test") };
		    }
		}
		""",
		editorConfig: "max_line_length = 120");

	// The same question through each other path a hugged block reaches the creation by: a trailing callback
	// after other arguments, an anonymous method, a callback inside an expression-bodied lambda, a `with`,
	// and an empty block, which deterministic layout opens below its arrow too.
	[Test]
	public Task Issue_240_every_hugged_block_sends_the_initializer_below_at_a_width() => Formats(
		"""
		public class C
		{
		    public void M()
		    {
		        var a = new Holder(1, () =>
		        {
		            Foo();
		        }) { X = 1 };
		        var b = new Holder(delegate
		        {
		            Foo();
		        }) { X = 1 };
		        var c = new Holder(x => x.Add(() =>
		        {
		            Foo();
		        })) { X = 1 };
		        var d = Make(() =>
		        {
		            Foo();
		        }) with { X = 1 };
		        var e = new Holder(() => { }) { X = 1 };
		    }
		}
		""",
		"""
		public class C
		{
		    public void M()
		    {
		        var a = new Holder(1, () =>
		        {
		            Foo();
		        })
		        { X = 1 };
		        var b = new Holder(delegate
		        {
		            Foo();
		        })
		        { X = 1 };
		        var c = new Holder(x => x.Add(() =>
		        {
		            Foo();
		        }))
		        { X = 1 };
		        var d = Make(() =>
		        {
		            Foo();
		        }) with
		        { X = 1 };
		        var e = new Holder(() =>
		        { })
		        { X = 1 };
		    }
		}
		""",
		editorConfig: "max_line_length = 120");

	// The block stays on the arrow's line here, so the creation does not span lines and the brace stays.
	[Test]
	public Task Issue_240_an_initializer_after_a_one_line_block_stays_on_its_line_at_a_width() => Unchanged(
		"""
		public class C
		{
		    public void M()
		    {
		        var e = new Holder(() => { }) { X = 1 };
		    }
		}
		""",
		editorConfig: """
			max_line_length = 120
			csharp_new_line_before_open_brace = types,methods,object_collection_array_initializers
			""");

	[Test]
	public Task Broken_arguments_followed_by_a_comment_keep_the_initializer_brace_on_the_next_line() => Unchanged(
		"""
		public class C
		{
		    public Settings M()
		    {
		        return new Settings(
		            "first") // comment
		        {
		            Value = 1,
		        };
		    }
		}
		""");

	[Test]
	public Task Broken_arguments_with_one_member_per_line_keep_the_initializer_brace_on_the_next_line() => Unchanged(
		"""
		public class C
		{
		    public Settings M()
		    {
		        return new Settings(
		            "first")
		        {
		            Value = 1,
		        };
		    }
		}
		""",
		editorConfig: "csharp_new_line_before_members_in_object_initializers = true");

	[Test]
	public Task Object_creation_with_arguments() => Unchanged(
		"""
		public class C
		{
		    public void M()
		    {
		        var value = new Thing(first, second);
		    }
		}
		""");

	[Test]
	public Task Implicit_object_creation() => Unchanged(
		"""
		public class C
		{
		    public void M()
		    {
		        Thing value = new();
		    }
		}
		""");

	[Test]
	public Task Object_initializer_on_one_line() => Unchanged(
		"""
		public class C
		{
		    public void M()
		    {
		        var value = new Thing { First = 1, Second = 2 };
		    }
		}
		""");

	[Test]
	public Task Object_initializer_across_lines_puts_the_brace_on_its_own_line() => Unchanged(
		"""
		public class C
		{
		    public void M()
		    {
		        var value = new Thing
		        {
		            First = 1,
		            Second = 2,
		        };
		    }
		}
		""");

	[Test]
	public Task Nested_object_initializer() => Unchanged(
		"""
		public class C
		{
		    public void M()
		    {
		        var value = new Thing { Inner = new Other { Value = 1 } };
		    }
		}
		""");

	[Test]
	public Task Collection_initializer() => Unchanged(
		"""
		public class C
		{
		    public void M()
		    {
		        var value = new List<int> { 1, 2, 3 };
		    }
		}
		""");

	[Test]
	public Task Dictionary_initializer() => Unchanged(
		"""
		public class C
		{
		    public void M()
		    {
		        var value = new Dictionary<string, int> { ["a"] = 1, ["b"] = 2 };
		    }
		}
		""");

	[Test]
	public Task Array_creation() => Unchanged(
		"""
		public class C
		{
		    public void M()
		    {
		        var value = new int[] { 1, 2, 3 };
		    }
		}
		""");

	[Test]
	public Task Implicit_array_creation() => Unchanged(
		"""
		public class C
		{
		    public void M()
		    {
		        var value = new[] { 1, 2, 3 };
		    }
		}
		""");

	[Test]
	public Task Empty_array_creation() => Unchanged(
		"""
		public class C
		{
		    public void M()
		    {
		        var value = new int[0];
		    }
		}
		""");

	[Test]
	public Task Array_size_expression_keeps_operator_spacing() => Formats(
		"""
		public class C
		{
		    public void M()
		    {
		        var value = new int[count+1];
		    }
		}
		""",
		"""
		public class C
		{
		    public void M()
		    {
		        var value = new int[count + 1];
		    }
		}
		""");

	[Test]
	public Task Collection_expression() => Unchanged(
		"""
		public class C
		{
		    public void M()
		    {
		        int[] value = [1, 2, 3];
		    }
		}
		""");

	[Test]
	public Task Empty_collection_expression() => Unchanged(
		"""
		public class C
		{
		    public void M()
		    {
		        int[] value = [];
		    }
		}
		""");

	[Test]
	public Task Spread_element_keeps_its_space() => Unchanged(
		"""
		public class C
		{
		    public void M()
		    {
		        int[] value = [first, .. rest];
		    }
		}
		""");

	[Test]
	public Task Nested_collection_expressions() => Unchanged(
		"""
		public class C
		{
		    public void M()
		    {
		        int[][] value = [[1, 2], [3, 4]];
		    }
		}
		""");

	[Test]
	public Task An_author_broken_collection_expression_keeps_its_bracket_on_its_own_line() => Unchanged(
		"""
		public class C
		{
		    public void M()
		    {
		        int[] values =
		        [
		            alpha,
		            beta,
		        ];
		    }
		}
		""");

	[Test]
	public Task A_reassigned_collection_expression_keeps_its_bracket_hugged_when_it_fits() => Unchanged(
		"""
		public class C
		{
		    public void M(int[] values)
		    {
		        values = [alpha, beta];
		    }
		}
		""");

	[Test]
	public Task Anonymous_object() => Unchanged(
		"""
		public class C
		{
		    public void M()
		    {
		        var value = new { First = 1, Second = 2 };
		    }
		}
		""");

	[Test]
	public Task With_expression() => Unchanged(
		"""
		public class C
		{
		    public void M()
		    {
		        var updated = original with { Value = 1 };
		    }
		}
		""");

	// Issue #214: dotnet format opens a `with` initializer that spans lines out one member per line even
	// with csharp_new_line_before_members_in_object_initializers off: the key reaches object initializers
	// only. One on a single line stays there.
	[Test]
	public Task A_with_initializer_that_spans_lines_puts_each_member_on_its_own_line() => Formats(
		"""
		public class C
		{
		    public void M(Customer customer)
		    {
		        var a = customer with
		        {
		            Name = customer.Name
		                .ToUpperInvariant(), Email = "x"
		        };
		        var b = customer with
		        {
		            Name = "a", Email = "x"
		        };
		        var c = customer with { Name = "a", Email = "x" };
		    }
		}
		""",
		"""
		public class C
		{
		    public void M(Customer customer)
		    {
		        var a = customer with
		        {
		            Name = customer.Name
		                .ToUpperInvariant(),
		            Email = "x"
		        };
		        var b = customer with
		        {
		            Name = "a",
		            Email = "x"
		        };
		        var c = customer with { Name = "a", Email = "x" };
		    }
		}
		""");

	// Issue #213: a creation's brace on a line of its own goes to the indent of the line its `new` (or
	// `with` receiver) is on, and what it holds follows it. In a query that is the `select` line; on the
	// statement's own line it is the statement's indent, as before. The members of `o` open out, which puts
	// `Nested = new` on a line of its own: the brace follows that line, not the one the source had it on.
	[Test]
	public Task A_brace_on_its_own_line_sits_at_the_indent_of_the_line_new_is_on() => Formats(
		"""
		class C
		{
		    void M(int[] xs, Customer customer)
		    {
		        var h = from x in xs
		                select new
		            { x, y = 1 };
		        var k = from x in xs
		                select new Customer
		            { Name = "a" };
		        var i = xs.Select(x => new
		              { x });
		        var l = xs
		            .SelectMany(x => xs, (x, y) => new
		             { x, y })
		            .ToList();
		        var m = customer with
		          { Name = "a" };
		        var n = from x in xs
		                select new
		                {
		                    x,
		                    y = 1,
		                };
		        var o = new { customer.Id, Nested = new
		            { Flag = true } };
		    }
		}
		""",
		"""
		class C
		{
		    void M(int[] xs, Customer customer)
		    {
		        var h = from x in xs
		                select new
		                { x, y = 1 };
		        var k = from x in xs
		                select new Customer
		                { Name = "a" };
		        var i = xs.Select(x => new
		        { x });
		        var l = xs
		            .SelectMany(x => xs, (x, y) => new
		            { x, y })
		            .ToList();
		        var m = customer with
		        { Name = "a" };
		        var n = from x in xs
		                select new
		                {
		                    x,
		                    y = 1,
		                };
		        var o = new
		        {
		            customer.Id,
		            Nested = new
		            { Flag = true }
		        };
		    }
		}
		""");

	// ---- tuples -------------------------------------------------------------------------------

	[Test]
	public Task Tuple_expression() => Formats(
		"""
		public class C
		{
		    public void M()
		    {
		        var value = (first,second);
		    }
		}
		""",
		"""
		public class C
		{
		    public void M()
		    {
		        var value = (first, second);
		    }
		}
		""");

	[Test]
	public Task Named_tuple_elements() => Unchanged(
		"""
		public class C
		{
		    public void M()
		    {
		        var value = (First: 1, Second: 2);
		    }
		}
		""");

	[Test]
	public Task Tuple_type() => Unchanged(
		"""
		public class C
		{
		    public (int First, string Second) M()
		    {
		        return (1, "two");
		    }
		}
		""");

	// ---- patterns -----------------------------------------------------------------------------

	[Test]
	public Task Is_pattern_with_a_declaration() => Unchanged(
		"""
		public class C
		{
		    public void M(object value)
		    {
		        if (value is string text)
		        {
		            Call(text);
		        }
		    }
		}
		""");

	[Test]
	public Task Property_pattern() => Unchanged(
		"""
		public class C
		{
		    public void M(object value)
		    {
		        if (value is Thing { First: 1 })
		        {
		            Call();
		        }
		    }
		}
		""");

	[Test]
	public Task Nested_property_pattern() => Unchanged(
		"""
		public class C
		{
		    public void M(object value)
		    {
		        if (value is Thing { Inner: Other { Value: 1 } })
		        {
		            Call();
		        }
		    }
		}
		""");

	[Test]
	public Task Combined_patterns() => Unchanged(
		"""
		public class C
		{
		    public void M(object value)
		    {
		        if (value is > 0 and < 10)
		        {
		            Call();
		        }
		    }
		}
		""");

	[Test]
	public Task Negated_pattern() => Unchanged(
		"""
		public class C
		{
		    public void M(object value)
		    {
		        if (value is not null)
		        {
		            Call();
		        }
		    }
		}
		""");

	[Test]
	public Task Or_pattern() => Unchanged(
		"""
		public class C
		{
		    public void M(object value)
		    {
		        if (value is 1 or 2 or 3)
		        {
		            Call();
		        }
		    }
		}
		""");

	[Test]
	public Task List_pattern() => Unchanged(
		"""
		public class C
		{
		    public void M(int[] value)
		    {
		        if (value is [1, 2, .. var rest])
		        {
		            Call(rest);
		        }
		    }
		}
		""");

	[Test]
	public Task Positional_pattern() => Unchanged(
		"""
		public class C
		{
		    public void M(object value)
		    {
		        if (value is Point(1, 2))
		        {
		            Call();
		        }
		    }
		}
		""");

	[Test]
	public Task Var_pattern() => Unchanged(
		"""
		public class C
		{
		    public void M(object value)
		    {
		        if (value is var captured)
		        {
		            Call(captured);
		        }
		    }
		}
		""");

	[Test]
	public Task Discard_pattern_in_a_switch_expression() => Unchanged(
		"""
		public class C
		{
		    public string M(object value)
		    {
		        return value switch
		        {
		            int => "number",
		            _ => "other",
		        };
		    }
		}
		""");

	// ---- LINQ ---------------------------------------------------------------------------------

	/// <summary>
	/// Continuation clauses align under <c>from</c> — measured against real <c>dotnet format</c>, which
	/// does the same regardless of indent style. The three tests below used to be skipped with a claim
	/// that this diverged from dotnet format; it does not. A flat, one-level-deeper indent (what a
	/// human is likely to type first) is therefore the input, not the expectation.
	/// </summary>
	[Test]
	public Task Query_expression_clauses_go_one_per_line() => Formats(
		"""
		public class C
		{
		    public void M()
		    {
		        var result = from item in items
		            where item > 0
		            orderby item descending
		            select item;
		    }
		}
		""",
		"""
		public class C
		{
		    public void M()
		    {
		        var result = from item in items
		                     where item > 0
		                     orderby item descending
		                     select item;
		    }
		}
		""");

	[Test]
	public Task Query_with_a_let_clause() => Formats(
		"""
		public class C
		{
		    public void M()
		    {
		        var result = from item in items
		            let doubled = item * 2
		            select doubled;
		    }
		}
		""",
		"""
		public class C
		{
		    public void M()
		    {
		        var result = from item in items
		                     let doubled = item * 2
		                     select doubled;
		    }
		}
		""");

	[Test]
	public Task Query_with_a_group_clause() => Formats(
		"""
		public class C
		{
		    public void M()
		    {
		        var result = from item in items
		            group item by item.Key;
		    }
		}
		""",
		"""
		public class C
		{
		    public void M()
		    {
		        var result = from item in items
		                     group item by item.Key;
		    }
		}
		""");

	// ---- comment-only initializers -----------------------------------------------------------

	[Test]
	public Task Object_initializer_holding_only_comments_keeps_them() => Unchanged(
		// The comments are the closing brace's leading trivia. The empty `{ }` shape skipped it, so
		// they were dropped and the verifier refused the file rather than format it.
		"""
		public class C
		{
		    public void M()
		    {
		        var args = new Args
		        {
		            // TenantId = tenantId,
		            // UserId = userId
		        };
		        Enqueue(args);
		    }
		}
		""");

	[Test]
	public Task Array_initializer_holding_only_a_block_comment_keeps_it() => Unchanged(
		"""
		public class C
		{
		    private int[] _values = new int[]
		    {
		        /* none yet */
		    };
		}
		""");

	[Test]
	public Task Comment_only_initializer_is_reindented_to_member_level() => Formats(
		"""
		public class C
		{
		    public void M()
		    {
		        var args = new Args
		        {
		  // TenantId = tenantId
		        };
		    }
		}
		""",
		"""
		public class C
		{
		    public void M()
		    {
		        var args = new Args
		        {
		            // TenantId = tenantId
		        };
		    }
		}
		""");

	// ---- an initializer after an expression body's arrow (issue #197) -------------------------------

	[Test]
	public Task Issue_197_an_initializer_below_a_hugging_paren_is_a_level_in() => Formats(
		// The arrow's line is a level deeper than the member, and dotnet format places the braces from it.
		"""
		public class C
		{
		    public static C For(
		        string id) => new()
		    {
		        Id = id,
		        Child = new Inner
		        {
		            Name = id,
		        },
		    };
		}
		""",
		"""
		public class C
		{
		    public static C For(
		        string id) => new()
		        {
		            Id = id,
		            Child = new Inner
		            {
		                Name = id,
		            },
		        };
		}
		""");

	[Test]
	public Task Issue_197_an_initializer_below_a_paren_on_its_own_line_stays_at_the_member() => Unchanged(
		"""
		public class C
		{
		    public static C For(
		        string id
		    ) => new()
		    {
		        Id = id,
		    };

		    public static C Other(string id) => new()
		    {
		        Id = id,
		    };

		    public C Property => new()
		    {
		        Id = "",
		    };
		}
		""");

	[Test]
	public Task Issue_197_every_owner_with_a_parameter_list_follows_the_arrow_line() => Unchanged(
		"""
		public class C
		{
		    public C(
		        string id) => Id = new()
		        {
		            Id = id,
		        };

		    public static C operator +(
		        C a, C b) => new()
		        {
		            Id = "",
		        };

		    public static implicit operator C(
		        string id) => new()
		        {
		            Id = id,
		        };

		    public void Host()
		    {
		        C Local(
		            string id) => new()
		            {
		                Id = id,
		            };
		    }
		}
		""");

	[Test]
	public Task Issue_197_other_bodies_with_braces_of_their_own_follow_the_arrow_line() => Unchanged(
		"""
		public class C
		{
		    public C With(
		        string id) => this with
		        {
		            Id = id,
		        };

		    public object Anonymous(
		        string id) => new
		        {
		            Id = id,
		        };

		    public bool Pattern(
		        string id) => id is C
		        {
		            Id: "a",
		        };

		    public async Task<C> Awaited(
		        string id) => await new C
		        {
		            Id = id,
		        };

		    public C Chained(
		        string id) => new C
		        {
		            Id = id,
		        }.Clone();

		    public C Wrapped(
		        string id) => Wrap(new C
		        {
		            Id = id,
		        });

		    public C Conditional(
		        string id) => id is null ? null : new C
		        {
		            Id = id,
		        };

		    public C Thrown(
		        string id) => throw new C
		        {
		            Id = id,
		        };
		}
		""");

	[Test]
	public Task Issue_197_collection_and_array_initializers_stay_where_the_author_put_them() => Unchanged(
		// dotnet format moves an object initializer with the arrow's line and leaves these two alone.
		"""
		public class C
		{
		    public static List<int> Items(
		        string id) => new()
		    {
		        1,
		    };

		    public static int[] Numbers(
		        string id) => new[]
		    {
		        1,
		    };
		}
		""");

	[Test]
	public Task Issue_197_a_body_ending_in_an_initializer_stays_at_a_one_line_member() => Unchanged(
		// The other half of the same disagreement: Curb hung these a level in and dotnet format took them back.
		"""
		public class C
		{
		    public C Fallback(string id) => id ?? new()
		    {
		        Id = id,
		    };

		    public C Cast(string id) => (C)new()
		    {
		        Id = id,
		    };

		    public C Thrown(string id) => throw new C
		    {
		        Id = id,
		    };
		}
		""");

	[Test]
	public Task Issue_197_a_forced_hug_indents_only_a_list_that_broke() => Formats(
		// The empty list and the property print no group of their own, so they must not read an earlier member's.
		"""
		public class C
		{
		    public static C For(
		        string id
		    ) => new()
		    {
		        Id = id,
		    };

		    public static C Empty() => new()
		    {
		        Id = "",
		    };

		    public C Property => new()
		    {
		        Id = "",
		    };
		}
		""",
		"""
		public class C
		{
		    public static C For(
		        string id) => new()
		        {
		            Id = id,
		        };

		    public static C Empty() => new()
		    {
		        Id = "",
		    };

		    public C Property => new()
		    {
		        Id = "",
		    };
		}
		""",
		editorConfig: "csharp_wrap_before_declaration_rpar = false");

	[Test]
	public Task Issue_197_a_converted_body_settles_where_the_next_run_puts_it() => Formats(
		"""
		public class C
		{
		    public static C For(
		        string id)
		    {
		        return new()
		        {
		            Id = id,
		        };
		    }
		}
		""",
		"""
		public class C
		{
		    public static C For(
		        string id) => new()
		        {
		            Id = id,
		        };
		}
		""",
		editorConfig: "csharp_style_expression_bodied_methods = true");
}
