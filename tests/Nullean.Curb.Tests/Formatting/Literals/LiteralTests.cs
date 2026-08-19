namespace Nullean.Curb.Tests.Formatting.Literals;

/// <summary>
/// String, character and numeric literals.
/// </summary>
/// <remarks>
/// The interior of a literal is <i>content</i>, not layout. Verbatim and raw strings carry
/// significant whitespace and line structure, so they are reproduced exactly rather than re-indented
/// — the one place where "format it" means "do not touch it".
/// </remarks>
public class LiteralTests : FormattingTest
{
	[Test]
	public Task String_literal() => Unchanged(
		"""
		public class C
		{
		    public string Value = "hello";
		}
		""");

	[Test]
	public Task String_literal_containing_escapes() => Unchanged(
		"""
		public class C
		{
		    public string Value = "a\tb\r\nc\"d";
		}
		""");

	[Test]
	public Task String_literal_containing_significant_spaces() => Unchanged(
		"""
		public class C
		{
		    public string Value = "a    b";
		}
		""");

	[Test]
	public Task String_literal_containing_braces() => Unchanged(
		"""
		public class C
		{
		    public string Value = "{ not an interpolation }";
		}
		""");

	[Test]
	public Task Empty_string() => Unchanged(
		"""
		public class C
		{
		    public string Value = "";
		}
		""");

	[Test]
	public Task Verbatim_string() => Unchanged(
		"""
		public class C
		{
		    public string Value = @"C:\path\to\file";
		}
		""");

	/// <remarks>The four-quote delimiter is needed because the content itself ends in three quotes.</remarks>
	[Test]
	public Task Verbatim_string_with_doubled_quotes() => Unchanged(
		""""
		public class C
		{
		    public string Value = @"say ""hello""";
		}
		"""");

	[Test]
	public Task Utf8_string_literal() => Unchanged(
		"""
		public class C
		{
		    public ReadOnlySpan<byte> Value => "hello"u8;
		}
		""");

	[Test]
	public Task Character_literal() => Unchanged(
		"""
		public class C
		{
		    public char Value = 'a';
		}
		""");

	[Test]
	public Task Escaped_character_literal() => Unchanged(
		"""
		public class C
		{
		    public char Value = '\n';
		}
		""");

	[Test]
	public Task Numeric_literals() => Unchanged(
		"""
		public class C
		{
		    public int Decimal = 42;
		    public int Hex = 0xFF;
		    public int Binary = 0b1010;
		    public int Separated = 1_000_000;
		    public long Long = 42L;
		    public double Double = 1.5;
		    public decimal Decimal2 = 1.5m;
		    public float Float = 1.5f;
		}
		""");

	[Test]
	public Task Boolean_and_null_literals() => Unchanged(
		"""
		public class C
		{
		    public bool Flag = true;
		    public bool Other = false;
		    public string Nothing = null;
		}
		""");

	[Test]
	public Task Default_literal() => Unchanged(
		"""
		public class C
		{
		    public int Value = default;
		}
		""");

	[Test]
	public Task Interpolated_string() => Unchanged(
		"""
		public class C
		{
		    public void M()
		    {
		        var value = $"hello {name}";
		    }
		}
		""");

	[Test]
	public Task Interpolated_string_with_a_format_specifier() => Unchanged(
		"""
		public class C
		{
		    public void M()
		    {
		        var value = $"{amount,10:C}";
		    }
		}
		""");

	[Test]
	public Task Interpolated_string_with_an_expression() => Unchanged(
		"""
		public class C
		{
		    public void M()
		    {
		        var value = $"total {first + second}";
		    }
		}
		""");

	[Test]
	public Task Interpolated_verbatim_string() => Unchanged(
		"""
		public class C
		{
		    public void M()
		    {
		        var value = $@"C:\{folder}\file";
		    }
		}
		""");

	[Test]
	public Task Interpolated_string_interior_is_left_alone() => Unchanged(
		"""
		public class C
		{
		    public void M()
		    {
		        var value = $"a    b {name}   c";
		    }
		}
		""");

	[Test]
	public Task Nested_interpolation() => Unchanged(
		"""
		public class C
		{
		    public void M()
		    {
		        var value = $"outer {$"inner {name}"}";
		    }
		}
		""");

	[Test]
	public Task Raw_string_literal() => Unchanged(
		"""
		public class C
		{
		    public void M()
		    {
		        var value = @"line one
		line two";
		    }
		}
		""");

	[Test]
	public Task Verbatim_string_spanning_lines_keeps_its_own_indentation() => Unchanged(
		"""
		public class C
		{
		    public void M()
		    {
		        var value = @"
		        indented inside the string
		            further indented
		";
		    }
		}
		""");

	[Test]
	public Task Comment_above_an_interpolated_string_is_kept() => Unchanged(
		"""
		public class C
		{
		    public void M()
		    {
		        // lang=json
		        var value = $"{{ \"key\": \"{name}\" }}";
		    }
		}
		""");
}
