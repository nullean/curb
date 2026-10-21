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
