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

	// Issue #100: a broken &&/||/?? chain kept its breaks but lost its continuation indent, so every
	// operand after the first landed at the statement's own column. dotnet format leaves a
	// continuation line where the author put it, relative to the line above.
	[Test]
	public Task Broken_logical_chain_keeps_operands_aligned_under_the_first() => Unchanged(
		"""
		public class C
		{
		    public static bool Binary(string trimmed)
		    {
		        return trimmed.Length > 0
		               && !string.Equals(trimmed, "false", StringComparison.OrdinalIgnoreCase)
		               && !string.Equals(trimmed, "0", StringComparison.Ordinal);
		    }
		}
		""");

	[Test]
	public Task Broken_coalesce_keeps_its_aligned_continuation() => Unchanged(
		"""
		public class C
		{
		    public static string Coalesce(string? value)
		    {
		        var resolved = value?.Trim()
		                       ?? throw new InvalidOperationException("missing");
		        return resolved;
		    }
		}
		""");

	[Test]
	public Task Broken_logical_chain_keeps_a_one_level_continuation_indent() => Unchanged(
		"""
		public class C
		{
		    public bool M(string value)
		    {
		        var ok = value.Length > 0
		            || value.Length < 5;
		        return value.Length > 0
		            && value.Length < 5
		            && value[0] == 'a';
		    }
		}
		""");

	[Test]
	public Task Broken_logical_chain_with_the_operator_ending_the_line_keeps_its_indent() => Unchanged(
		"""
		public class C
		{
		    public bool M(string value)
		    {
		        return value.Length > 0 &&
		               value.Length < 5;
		    }
		}
		""");

	// Issue #101 reported the same lost indent after a property pattern.
	[Test]
	public Task Broken_logical_chain_after_a_pattern_keeps_its_indent() => Unchanged(
		"""
		public class C
		{
		    public bool M(object value)
		    {
		        return value is Version { Major: 1 } version
		            && version.Build > 0;
		    }
		}
		""");

	// The continuation moves with its statement when the statement is re-indented, keeping its
	// offset from the line above rather than its absolute column.
	[Test]
	public Task Broken_logical_chain_moves_with_a_reindented_statement() => Formats(
		"""
		public class C
		{
		    public bool M(string value)
		    {
		            return value.Length > 0
		                   && value.Length < 5;
		    }
		}
		""",
		"""
		public class C
		{
		    public bool M(string value)
		    {
		        return value.Length > 0
		               && value.Length < 5;
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
	public Task Expression_bodied_lambda_wrapping_a_block_bodied_one() => Unchanged(
		"""
		public class C
		{
		    public void M()
		    {
		        Configure(builder => builder.Add(x =>
		        {
		            return x.Value;
		        }));
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
