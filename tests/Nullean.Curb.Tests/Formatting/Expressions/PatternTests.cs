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
}
