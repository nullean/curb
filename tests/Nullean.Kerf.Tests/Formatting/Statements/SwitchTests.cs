namespace Nullean.Kerf.Tests.Formatting.Statements;

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
}
