namespace Nullean.Curb.Tests.Formatting.Expressions;

/// <summary>Recursive (property) patterns in <c>is</c> expressions.</summary>
public class PatternTests : FormattingTest
{
	[Test]
	public Task Typed_recursive_pattern_fits_on_one_line() => Unchanged(
		"""
		public class C
		{
		    public void M(Exception ex)
		    {
		        if (ex is HttpRequestException { StatusCode: 404 })
		        { }
		    }
		}
		""",
		editorConfig: "max_line_length = 100");

	[Test]
	public Task Typed_recursive_pattern_moves_its_brace_to_its_own_line_when_it_does_not_fit() => Formats(
		"""
		public class C
		{
		    public void M(Exception ex)
		    {
		        if (ex is HttpRequestException { StatusCode: HttpStatusCode.NotFound }) { }
		    }
		}
		""",
		"""
		public class C
		{
		    public void M(Exception ex)
		    {
		        if (
		            ex is HttpRequestException
		            {
		                StatusCode: HttpStatusCode.NotFound
		            }
		        )
		        { }
		    }
		}
		""",
		editorConfig: "max_line_length = 40");

	[Test]
	public Task Bare_recursive_pattern_fits_on_one_line() => Unchanged(
		"""
		public class C
		{
		    public void M(object value)
		    {
		        if (value is { Length: 0 })
		        { }
		    }
		}
		""",
		editorConfig: "max_line_length = 100");

	[Test]
	public Task Bare_recursive_pattern_moves_its_brace_to_its_own_line_when_it_does_not_fit() => Formats(
		"""
		public class C
		{
		    public void M(object value)
		    {
		        if (value is { Length: 0, Count: 1 }) { }
		    }
		}
		""",
		"""
		public class C
		{
		    public void M(object value)
		    {
		        if (
		            value is
		            {
		                Length: 0,
		                Count: 1
		            }
		        )
		        { }
		    }
		}
		""",
		editorConfig: "max_line_length = 40");

	// ---- preservation: a pattern the author opened out stays opened out -----------------------------

	[Test]
	public Task Multiline_property_pattern_with_a_trailing_comma_is_kept() => Unchanged(
		"""
		public class C
		{
		    public bool M(object value)
		    {
		        return value is Version
		        {
		            Major: 1,
		            Minor: 2,
		        } version;
		    }
		}
		""");

	[Test]
	public Task Multiline_property_pattern_is_kept() => Unchanged(
		"""
		public class C
		{
		    public bool M(object value)
		    {
		        return value is Version
		        {
		            Major: 1,
		            Minor: 2
		        };
		    }
		}
		""");

	[Test]
	public Task Multiline_bare_property_pattern_is_kept() => Unchanged(
		"""
		public class C
		{
		    public bool M(object value)
		    {
		        return value is
		        {
		            Length: 0,
		            Count: 1,
		        };
		    }
		}
		""");

	[Test]
	public Task Nested_multiline_property_pattern_is_kept() => Unchanged(
		"""
		public class C
		{
		    public bool M(object value)
		    {
		        return value is Person
		        {
		            Name: "x",
		            Address:
		            {
		                City: "y",
		                Zip: "z",
		            },
		        };
		    }
		}
		""");

	[Test]
	public Task Property_pattern_members_the_author_kept_together_stay_together() => Unchanged(
		"""
		public class C
		{
		    public bool M(object value)
		    {
		        return value is Version
		        {
		            Major: 1, Minor: 2,
		            Build: 3,
		        };
		    }
		}
		""");

	[Test]
	public Task Multiline_positional_pattern_is_kept() => Unchanged(
		"""
		public class C
		{
		    public bool M(object value)
		    {
		        return value is Point(
		            1,
		            2) point;
		    }
		}
		""");

	[Test]
	public Task Positional_pattern_keeps_a_break_between_subpatterns() => Unchanged(
		"""
		public class C
		{
		    public bool M(object value)
		    {
		        return value is Point(1,
		            2) point;
		    }
		}
		""");

	[Test]
	public Task Property_pattern_braces_open_out_once_its_members_span_lines() => Formats(
		// dotnet format moves both edges onto lines of their own, as it does an initializer's.
		"""
		public class C
		{
		    public bool M(object value)
		    {
		        return value is Version { Major: 1,
		            Minor: 2 };
		    }
		}
		""",
		"""
		public class C
		{
		    public bool M(object value)
		    {
		        return value is Version
		        {
		            Major: 1,
		            Minor: 2
		        };
		    }
		}
		""");

	[Test]
	public Task Property_pattern_brace_follows_the_initializer_brace_option() => Formats(
		"""
		public class C {
		    public bool M(object value) {
		        return value is Version { Major: 1,
		            Minor: 2 };
		    }
		}
		""",
		"""
		public class C {
		    public bool M(object value) {
		        return value is Version {
		            Major: 1,
		            Minor: 2
		        };
		    }
		}
		""",
		editorConfig: "csharp_new_line_before_open_brace = none");

	[Test]
	public Task Property_pattern_after_a_broken_positional_pattern_is_kept() => Unchanged(
		"""
		public class C
		{
		    public bool M(object value)
		    {
		        return value is Point(
		            1,
		            2)
		        {
		            X: 1,
		            Y: 2,
		        };
		    }
		}
		""");

	[Test]
	public Task Multiline_list_pattern_is_kept() => Unchanged(
		"""
		public class C
		{
		    public bool M(int[] values)
		    {
		        return values is
		        [
		            1,
		            2,
		        ];
		    }
		}
		""");

	[Test]
	public Task Multiline_property_pattern_in_a_switch_arm_is_kept() => Unchanged(
		"""
		public class C
		{
		    public int M(object value) => value switch
		    {
		        Version
		        {
		            Major: 1,
		            Minor: 2,
		        } => 1,
		        _ => 0,
		    };
		}
		""");

	[Test]
	public Task Single_line_property_pattern_with_a_trailing_comma_has_one_space_before_its_brace() => Unchanged(
		"""
		public class C
		{
		    public bool M(object value)
		    {
		        return value is Version { Major: 1, Minor: 2, } version;
		    }
		}
		""");

	[Test]
	public Task Multiline_property_pattern_after_not_is_kept() => Unchanged(
		"""
		public class C
		{
		    public bool M(object value)
		    {
		        return value is not
		        {
		            Length: 0,
		            Count: 1,
		        };
		    }
		}
		""");

	[Test]
	public Task Multiline_empty_property_pattern_is_kept() => Unchanged(
		"""
		public class C
		{
		    public bool M(object value)
		    {
		        return value is Version
		        {
		        } version;
		    }
		}
		""");

	[Test]
	public Task Empty_property_pattern_with_a_comment_keeps_it_at_the_members_level() => Unchanged(
		"""
		public class C
		{
		    public bool M(object value)
		    {
		        return value is Version
		        {
		            // nothing to match
		        };
		    }
		}
		""");

	[Test]
	public Task Empty_property_pattern_brace_follows_the_initializer_brace_option() => Formats(
		"""
		public class C {
		    public bool M(object value) {
		        return value is Version
		        {
		        };
		    }
		}
		""",
		"""
		public class C {
		    public bool M(object value) {
		        return value is Version {
		        };
		    }
		}
		""",
		editorConfig: "csharp_new_line_before_open_brace = none");

	[Test]
	public Task Property_pattern_under_indent_braces_matches_dotnet_format() => Formats(
		// dotnet format indents the brace a level, the members a level past it, and the closing brace
		// to the members' column, the same at every depth.
		"""
		public class C
		{
		    public bool M(object value)
		    {
		        var a = value is Version
		        {
		            Major: 1,
		            Minor: 2,
		        } v;
		        var b = value is
		        {
		            Length: 0,
		        };
		        var c = value is Version { Major: 1,
		            Minor: 2 };
		        return value is Person
		        {
		            Name: "x",
		            Address:
		            {
		                City: "y",
		            },
		            Home: Address
		            {
		                Zip: 1,
		            },
		        };
		    }
		}
		""",
		"""
		public class C
		    {
		    public bool M(object value)
		        {
		        var a = value is Version
		            {
		                Major: 1,
		                Minor: 2,
		                } v;
		        var b = value is
		            {
		                Length: 0,
		                };
		        var c = value is Version
		            {
		                Major: 1,
		                Minor: 2
		                };
		        return value is Person
		            {
		                Name: "x",
		                Address:
		                    {
		                        City: "y",
		                        },
		                Home: Address
		                    {
		                        Zip: 1,
		                        },
		                };
		        }
		    }
		""",
		editorConfig: "csharp_indent_braces = true");

	[Test]
	public Task Empty_property_pattern_and_patterns_after_not_and_or_under_indent_braces() => Formats(
		"""
		public class C
		{
		    public bool M(object value)
		    {
		        var a = value is not
		        {
		            Length: 0,
		        };
		        var b = value is string or
		        {
		            Length: 0,
		        };
		        var c = value is Version
		        {
		        } w;
		        return value is Version
		        {
		            // none
		        };
		    }
		}
		""",
		"""
		public class C
		    {
		    public bool M(object value)
		        {
		        var a = value is not
		            {
		                Length: 0,
		                };
		        var b = value is string or
		            {
		                Length: 0,
		                };
		        var c = value is Version
		            {
		                } w;
		        return value is Version
		            {
		                // none
		                };
		        }
		    }
		""",
		editorConfig: "csharp_indent_braces = true");

	[Test]
	public Task Property_pattern_brace_after_a_colon_not_or_and_takes_its_own_line() => Formats(
		// dotnet format moves the brace of a pattern that spans lines onto its own line after any of
		// these, not only after a type or `is`.
		"""
		public class C
		{
		    public void M(object o)
		    {
		        var a = o is Person
		        {
		            Name: "x",
		            Address: {
		                City: "y",
		                Zip: 1
		            }
		        };
		        var b = o is not {
		            Length: 1
		        };
		        var c = o is null or {
		            Length: 1
		        };
		        var d = o is { A: 1 } and {
		            B: 1
		        };
		    }
		}
		""",
		"""
		public class C
		{
		    public void M(object o)
		    {
		        var a = o is Person
		        {
		            Name: "x",
		            Address:
		            {
		                City: "y",
		                Zip: 1
		            }
		        };
		        var b = o is not
		        {
		            Length: 1
		        };
		        var c = o is null or
		        {
		            Length: 1
		        };
		        var d = o is { A: 1 } and
		        {
		            B: 1
		        };
		    }
		}
		""");

	[Test]
	public Task Property_pattern_chain_that_spans_lines_puts_each_brace_on_its_own_line() => Formats(
		"""
		public class C
		{
		    public void M(object o)
		    {
		        var t = o is {
		            A: 1 } or {
		            A: 2 };
		    }
		}
		""",
		"""
		public class C
		{
		    public void M(object o)
		    {
		        var t = o is
		        {
		            A: 1
		        } or
		        {
		            A: 2
		        };
		    }
		}
		""");

	[Test]
	public Task Property_pattern_on_one_line_after_a_colon_not_or_and_stays_put() => Unchanged(
		"""
		public class C
		{
		    public void M(object o)
		    {
		        var a = o is Person
		        {
		            Address: { City: "y" }
		        };
		        var b = o is not { Length: 1 };
		        var c = o is null or { Length: 1 };
		        var d = o is { A: 1 } and { B: 1 };
		    }
		}
		""");

	[Test]
	public Task Property_pattern_brace_joins_a_broken_is_or_colon_under_none() => Formats(
		// With the initializer flag off, dotnet format joins the brace onto `is`, a type or a colon, but
		// leaves a break after `not` or `or` where the author put it.
		"""
		class C {
		    void M(object o) {
		        var f = o is
		            { A: 1 };
		        var f2 = o is
		        {
		            A: 1,
		            B: 2
		        };
		        var a = o is Person
		        {
		            Name: "x",
		            Address:
		            {
		                City: "y"
		            }
		        };
		        var a2 = o is Person {
		            Address:
		                { City: "y" }
		        };
		        var b = o is not
		        {
		            Length: 1
		        };
		        var c = o is null or
		        {
		            Length: 1
		        };
		        var x = o is
		        { Length: 1 } or
		        { Length: 2 };
		        var y = o is Person {
		            Address:
		            { City: "y" } or
		            { City: "z" }
		        };
		        var n = o is not {
		            A: 1
		        };
		    }
		}
		""",
		"""
		class C {
		    void M(object o) {
		        var f = o is { A: 1 };
		        var f2 = o is {
		            A: 1,
		            B: 2
		        };
		        var a = o is Person {
		            Name: "x",
		            Address: {
		                City: "y"
		            }
		        };
		        var a2 = o is Person {
		            Address: { City: "y" }
		        };
		        var b = o is not
		        {
		            Length: 1
		        };
		        var c = o is null or
		        {
		            Length: 1
		        };
		        var x = o is { Length: 1 } or
		        { Length: 2 };
		        var y = o is Person {
		            Address: { City: "y" } or
		            { City: "z" }
		        };
		        var n = o is not {
		            A: 1
		        };
		    }
		}
		""",
		editorConfig: "csharp_new_line_before_open_brace = none");

	[Test]
	public Task Line_comments_in_a_property_pattern_add_no_blank_lines() => Formats(
		"""
		public class C
		{
		    public void M(object o)
		    {
		        var g = o is
		        { // open
		            A: 1, // first
		            B: 2 // last
		        };
		        var h = o is Person
		        { // open
		            A: 1, // first
		            B: 2, // last
		        };
		        var k = o is // c
		            { A: 1 };
		    }
		}
		""",
		"""
		public class C
		{
		    public void M(object o)
		    {
		        var g = o is
		        { // open
		            A: 1, // first
		            B: 2 // last
		        };
		        var h = o is Person
		        { // open
		            A: 1, // first
		            B: 2, // last
		        };
		        var k = o is // c
		        { A: 1 };
		    }
		}
		""");

	// ---- preservation: a pattern's continuation lines keep the author's offset ----------------------

	// Issue #111: each continuation line used to break to the statement's own column, or one level in.
	[Test]
	public Task Pattern_continuation_lines_keep_the_authors_indent() => Unchanged(
		"""
		public class C
		{
		    public bool M(object value)
		    {
		        var a = value is
		            null;
		        var b = value is string or
		            null;
		        var c = value is Point(1,
		                               2);
		        var d = value is not
		            null;
		        return value is [1,
		                         2];
		    }
		}
		""");

	[Test]
	public Task Pattern_continuation_lines_move_with_a_reindented_statement() => Formats(
		"""
		public class C
		{
		    public bool M(object value)
		    {
		            var a = value is
		                null;
		            var b = value is string or
		                null;
		            var c = value is Point(1,
		                                   2);
		            var e = value is Point(
		                1,
		                2
		                );
		            var f = value is
		                [
		                    1,
		                    2,
		                ];
		            var g = value is { } and
		                not null;
		            return value is [1,
		                             2];
		    }
		}
		""",
		"""
		public class C
		{
		    public bool M(object value)
		    {
		        var a = value is
		            null;
		        var b = value is string or
		            null;
		        var c = value is Point(1,
		                               2);
		        var e = value is Point(
		            1,
		            2
		            );
		        var f = value is
		            [
		                1,
		                2,
		            ];
		        var g = value is { } and
		            not null;
		        return value is [1,
		                         2];
		    }
		}
		""");

	[Test]
	public Task Pattern_broken_before_is_or_or_keeps_the_authors_indent() => Formats(
		"""
		public class C
		{
		    public bool M(object value)
		    {
		            var a = value
		                is null;
		            return value is string
		                or null;
		    }
		}
		""",
		"""
		public class C
		{
		    public bool M(object value)
		    {
		        var a = value
		            is null;
		        return value is string
		            or null;
		    }
		}
		""");

	[Test]
	public Task Pattern_continuation_in_a_switch_arm_or_case_label_keeps_its_offset() => Formats(
		"""
		public class C
		{
		    public bool M(object value)
		    {
		            switch (value)
		            {
		                    case 1 or
		                        2:
		                        break;
		            }
		            return value switch
		            {
		                    1 or
		                        2 => true,
		                    _ => false
		            };
		    }
		}
		""",
		"""
		public class C
		{
		    public bool M(object value)
		    {
		        switch (value)
		        {
		            case 1 or
		                    2:
		                break;
		        }
		        return value switch
		        {
		            1 or
		                2 => true,
		            _ => false
		        };
		    }
		}
		""");

	// A case label is not a statement: dotnet format moves its continuation lines with the switch, so
	// a label it indents under csharp_indent_switch_labels leaves them where they were.
	[Test]
	public Task Pattern_continuation_in_a_case_label_moves_with_the_switch() => Formats(
		"""
		public class C
		{
		    public void M(object value)
		    {
		        switch (value)
		        {
		        case 1 or
		            2:
		            break;
		        case Point(1,
		                   2):
		            break;
		        case [1,
		              2] when value is
		                  null:
		            break;
		        }
		    }
		}
		""",
		"""
		public class C
		{
		    public void M(object value)
		    {
		        switch (value)
		        {
		            case 1 or
		            2:
		                break;
		            case Point(1,
		                   2):
		                break;
		            case [1,
		              2] when value is
		                  null:
		                break;
		        }
		    }
		}
		""");

	[Test]
	public Task Nested_pattern_continuation_lines_keep_their_offsets() => Formats(
		"""
		public class C
		{
		    public bool M(object value)
		    {
		            var a = value is [1,
		                    [2,
		                     3]];
		            var b = value is (1 or
		                              2);
		            return value is Point(
		                    X: 1,
		                    Y:
		                        2);
		    }
		}
		""",
		"""
		public class C
		{
		    public bool M(object value)
		    {
		        var a = value is [1,
		                [2,
		                 3]];
		        var b = value is (1 or
		                          2);
		        return value is Point(
		                X: 1,
		                Y:
		                    2);
		    }
		}
		""");

	// The type keeps the author's offset; the brace after it is still placed by the brace option.
	[Test]
	public Task Typed_pattern_on_its_own_line_after_is_keeps_its_offset() => Formats(
		"""
		public class C
		{
		    public bool M(object value)
		    {
		            var a = value is
		                Version
		                {
		                    Major: 1
		                };
		            var b = value is not
		                Version { Major: 1 };
		            return value is Point(1,
		                                  2)
		            {
		                X: 1
		            };
		    }
		}
		""",
		"""
		public class C
		{
		    public bool M(object value)
		    {
		        var a = value is
		            Version
		        {
		            Major: 1
		        };
		        var b = value is not
		            Version { Major: 1 };
		        return value is Point(1,
		                              2)
		        {
		            X: 1
		        };
		    }
		}
		""");

	[Test]
	public Task Pattern_after_a_comment_on_the_is_line_keeps_its_offset() => Formats(
		"""
		public class C
		{
		    public bool M(object value)
		    {
		            return value is // note
		                null;
		    }
		}
		""",
		"""
		public class C
		{
		    public bool M(object value)
		    {
		        return value is // note
		            null;
		    }
		}
		""");

	[Test]
	public Task Pattern_continuation_lines_keep_their_offset_under_tabs_and_crlf() => FormatsExactly(
		"public class C\r\n{\r\n\tpublic bool M(object value)\r\n\t{\r\n\t\t\tvar a = value is\r\n\t\t\t\tnull;\r\n"
		+ "\t\t\tvar c = value is Point(1,\r\n\t\t\t                       2);\r\n"
		+ "\t\t\treturn value is [1,\r\n\t\t\t\t2] or\r\n\t\t\t\tnull;\r\n\t}\r\n}\r\n",
		"public class C\r\n{\r\n\tpublic bool M(object value)\r\n\t{\r\n\t\tvar a = value is\r\n\t\t\tnull;\r\n"
		+ "\t\tvar c = value is Point(1,\r\n\t\t\t\t\t\t\t   2);\r\n"
		+ "\t\treturn value is [1,\r\n\t\t\t2] or\r\n\t\t\tnull;\r\n\t}\r\n}\r\n",
		editorConfig: "indent_style = tab\nend_of_line = crlf");

	// A chain whose first token is a property pattern's brace is placed as a bare pattern's brace is,
	// at the brace column, not at the author's offset.
	[Test]
	public Task Pattern_chain_that_starts_with_a_property_brace_breaks_to_the_brace_column() => Formats(
		"""
		public class C
		{
		    public void M(object o)
		    {
		        var x = o is
		            { Length: 1 } or
		            { Length: 2 };
		        var y = o is Person
		        {
		            Address:
		                { City: "y" } or
		                { City: "z" }
		        };
		        var w = o is not
		            { Length: 1 } and { Length: 2 };
		    }
		}
		""",
		"""
		public class C
		{
		    public void M(object o)
		    {
		        var x = o is
		        { Length: 1 } or
		        { Length: 2 };
		        var y = o is Person
		        {
		            Address:
		            { City: "y" } or
		            { City: "z" }
		        };
		        var w = o is not
		        { Length: 1 } and { Length: 2 };
		    }
		}
		""");

	// Under `none` the brace joins `is` and a colon; after `not` or `or`, and after a comment, dotnet
	// format keeps the author's line at the offset they gave it.
	[Test]
	public Task Property_brace_the_author_broke_keeps_its_offset_where_none_does_not_join_it() => Formats(
		"""
		class C {
		    void M(object o) {
		            var b = o is not
		                { Length: 1 };
		            var c = o is null or
		                { Length: 1 };
		            var k = o is // c
		                { A: 1 };
		            var x = o is
		                { Length: 1 } or
		                { Length: 2 };
		            var y = o is Person {
		                Address:
		                    { City: "y" } or
		                    { City: "z" }
		            };
		    }
		}
		""",
		"""
		class C {
		    void M(object o) {
		        var b = o is not
		            { Length: 1 };
		        var c = o is null or
		            { Length: 1 };
		        var k = o is // c
		            { A: 1 };
		        var x = o is { Length: 1 } or
		            { Length: 2 };
		        var y = o is Person {
		            Address: { City: "y" } or
		                { City: "z" }
		        };
		    }
		}
		""",
		editorConfig: "csharp_new_line_before_open_brace = none");

	// ---- deterministic: a pattern that fits is joined, and the comma follows the options ------------

	[Test]
	public Task Collapsed_property_pattern_keeps_one_space_before_its_brace() => Formats(
		"""
		public class C
		{
		    public bool M(object value)
		    {
		        return value is Version
		        {
		            Major: 1,
		            Minor: 2,
		        } version;
		    }
		}
		""",
		"""
		public class C
		{
		    public bool M(object value)
		    {
		        return value is Version { Major: 1, Minor: 2, } version;
		    }
		}
		""",
		editorConfig: "max_line_length = 100");

	[Test]
	public Task Collapsed_property_pattern_drops_its_comma_under_the_multiline_option() => Formats(
		"""
		public class C
		{
		    public bool M(object value)
		    {
		        return value is Version
		        {
		            Major: 1,
		            Minor: 2,
		        } version;
		    }
		}
		""",
		"""
		public class C
		{
		    public bool M(object value)
		    {
		        return value is Version { Major: 1, Minor: 2 } version;
		    }
		}
		""",
		editorConfig: "max_line_length = 100\ncsharp_trailing_comma_in_multiline_lists = true");

	[Test]
	public Task Broken_property_pattern_gains_a_comma_under_the_multiline_option() => Formats(
		"""
		public class C
		{
		    public bool M(object value)
		    {
		        return value is Version { Major: 1, Minor: 2 } version;
		    }
		}
		""",
		"""
		public class C
		{
		    public bool M(object value)
		    {
		        return value is Version
		        {
		            Major: 1,
		            Minor: 2,
		        } version;
		    }
		}
		""",
		editorConfig: "max_line_length = 40\ncsharp_trailing_comma_in_multiline_lists = true");
}
