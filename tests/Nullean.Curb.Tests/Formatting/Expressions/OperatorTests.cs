namespace Nullean.Curb.Tests.Formatting.Expressions;

/// <summary>Binary, unary, conditional and assignment operators.</summary>
public class OperatorTests : FormattingTest
{
	[Test]
	public Task Arithmetic_operators_get_spaces() => Formats(
		"""
		public class C
		{
		    public void M()
		    {
		        var value = first+second*third;
		    }
		}
		""",
		"""
		public class C
		{
		    public void M()
		    {
		        var value = first + second * third;
		    }
		}
		""");

	[Test]
	public Task Comparison_operators() => Unchanged(
		"""
		public class C
		{
		    public void M()
		    {
		        var value = first < second && third >= fourth;
		    }
		}
		""");

	[Test]
	public Task Equality_operators() => Unchanged(
		"""
		public class C
		{
		    public void M()
		    {
		        var value = first == second || third != fourth;
		    }
		}
		""");

	[Test]
	public Task Bitwise_operators() => Unchanged(
		"""
		public class C
		{
		    public void M()
		    {
		        var value = first & second | third ^ fourth;
		    }
		}
		""");

	[Test]
	public Task Shift_operators() => Unchanged(
		"""
		public class C
		{
		    public void M()
		    {
		        var value = first << 2 >> 1;
		    }
		}
		""");

	[Test]
	public Task Coalesce_operator() => Unchanged(
		"""
		public class C
		{
		    public void M()
		    {
		        var value = first ?? second;
		    }
		}
		""");

	[Test]
	public Task Coalesce_assignment() => Unchanged(
		"""
		public class C
		{
		    public void M()
		    {
		        value ??= fallback;
		    }
		}
		""");

	[Test]
	public Task Is_and_as_operators() => Unchanged(
		"""
		public class C
		{
		    public void M()
		    {
		        var first = value is string;
		        var second = value as string;
		    }
		}
		""");

	[Test]
	public Task Prefix_unary_operators_hug_their_operand() => Unchanged(
		"""
		public class C
		{
		    public void M()
		    {
		        var value = -number;
		        var negated = !flag;
		        var complement = ~bits;
		    }
		}
		""");

	[Test]
	public Task Increment_and_decrement() => Unchanged(
		"""
		public class C
		{
		    public void M()
		    {
		        counter++;
		        counter--;
		        ++counter;
		        --counter;
		    }
		}
		""");

	[Test]
	public Task Cast_has_no_space_after_it() => Formats(
		"""
		public class C
		{
		    public void M()
		    {
		        var value = (int) number;
		    }
		}
		""",
		"""
		public class C
		{
		    public void M()
		    {
		        var value = (int)number;
		    }
		}
		""");

	[Test]
	public Task Parenthesized_expression() => Unchanged(
		"""
		public class C
		{
		    public void M()
		    {
		        var value = (first + second) * third;
		    }
		}
		""");

	[Test]
	public Task Ternary_expression() => Unchanged(
		"""
		public class C
		{
		    public void M()
		    {
		        var value = condition ? first : second;
		    }
		}
		""");

	[Test]
	public Task Nested_ternary() => Unchanged(
		"""
		public class C
		{
		    public void M()
		    {
		        var value = first ? one : second ? two : three;
		    }
		}
		""");

	[Test]
	public Task Compound_assignments() => Unchanged(
		"""
		public class C
		{
		    public void M()
		    {
		        value += 1;
		        value -= 1;
		        value *= 2;
		        value /= 2;
		        value %= 2;
		    }
		}
		""");

	[Test]
	public Task Throw_expression() => Unchanged(
		"""
		public class C
		{
		    public void M()
		    {
		        var value = argument ?? throw new ArgumentNullException(nameof(argument));
		    }
		}
		""");

	[Test]
	public Task Lambda_arrow_gets_spaces() => Formats(
		"""
		public class C
		{
		    public void M()
		    {
		        Call(x=>x.Value);
		    }
		}
		""",
		"""
		public class C
		{
		    public void M()
		    {
		        Call(x => x.Value);
		    }
		}
		""");

	[Test]
	public Task Parenthesized_lambda() => Unchanged(
		"""
		public class C
		{
		    public void M()
		    {
		        Call((first, second) => first + second);
		    }
		}
		""");

	[Test]
	public Task Lambda_with_no_parameters() => Unchanged(
		"""
		public class C
		{
		    public void M()
		    {
		        Call(() => Value);
		    }
		}
		""");

	[Test]
	[Skip("an argument list holding a block-bodied lambda breaks; the lambda should stay on the call line")]
	public Task Lambda_with_a_block_body() => Unchanged(
		"""
		public class C
		{
		    public void M()
		    {
		        Call(x =>
		        {
		            return x.Value;
		        });
		    }
		}
		""");

	[Test]
	public Task Async_lambda() => Unchanged(
		"""
		public class C
		{
		    public void M()
		    {
		        Call(async x => await x.ValueAsync());
		    }
		}
		""");

	[Test]
	public Task Static_lambda() => Unchanged(
		"""
		public class C
		{
		    public void M()
		    {
		        Call(static x => x.Value);
		    }
		}
		""");

	[Test]
	public Task Typed_lambda_parameters() => Unchanged(
		"""
		public class C
		{
		    public void M()
		    {
		        Call((int first, int second) => first + second);
		    }
		}
		""");

	[Test]
	public Task Discard_lambda_parameter() => Unchanged(
		"""
		public class C
		{
		    public void M()
		    {
		        Call((_, _) => Value);
		    }
		}
		""");
}
