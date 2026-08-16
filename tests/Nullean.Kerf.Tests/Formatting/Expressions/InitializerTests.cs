namespace Nullean.Kerf.Tests.Formatting.Expressions;

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
	[Skip("reflow off collapses a multi-line construct onto one line; dotnet format never joins lines")]
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

	[Test]
	[Skip("query clause layout diverges from dotnet format — the value breaks after `=` and clauses double-indent")]
	public Task Query_expression_clauses_go_one_per_line() => Unchanged(
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
	[Skip("query clause layout diverges from dotnet format — the value breaks after `=` and clauses double-indent")]
	public Task Query_with_a_let_clause() => Unchanged(
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
	[Skip("query clause layout diverges from dotnet format — the value breaks after `=` and clauses double-indent")]
	public Task Query_with_a_group_clause() => Unchanged(
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
}
