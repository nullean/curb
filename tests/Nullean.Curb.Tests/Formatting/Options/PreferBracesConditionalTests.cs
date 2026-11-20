namespace Nullean.Curb.Tests.Formatting.Options;

public class PreferBracesConditionalTests : FormattingTest
{
	private const string Always = "csharp_prefer_braces = true";

	[Test]
	[Arguments("", Always + ":error")]
	[Arguments("#define NETSTANDARD2_0\n", Always + ":error")]
	[Arguments("", "csharp_prefer_braces = when_multiline\nmax_line_length = 80")]
	public Task Conditional_headers_can_share_an_unbraced_body(string define, string editorConfig) =>
		Unchanged(
			define +
				"""
		class Example
		{
		    static int M(bool condition)
		    {
		#if NETSTANDARD2_0
		        if (condition)
		#else
		        if (!condition)
		#endif
		            return 1;
		        return 0;
		    }
		}
		""",
			editorConfig
		);

	[Test]
	public Task A_conditional_header_can_share_its_last_tokens_with_an_invocation() =>
		Unchanged(
			"""
		class Example
		{
		    static void M(bool condition)
		    {
		#if ALT
		        M(
		#else
		        if (
		#endif
		            condition)
		            ;
		    }
		}
		""",
			Always
		);

	[Test]
	public Task An_inactive_else_keeps_its_inner_if() =>
		Formats(
			"""
		class Example
		{
		    static int M(bool outer, bool inner)
		    {
		        var result = 0;
		        if (outer)
		            if (inner)
		                result = 1;
		#if ALT
		            else
		                result = 2;
		#endif
		        return result;
		    }
		}
		""",
			"""
		class Example
		{
		    static int M(bool outer, bool inner)
		    {
		        var result = 0;
		        if (outer)
		            if (inner)
		            {
		                result = 1;
		            }
		#if ALT
		            else
		                result = 2;
		#endif
		        return result;
		    }
		}
		""",
			Always
		);

	[Test]
	public Task A_whole_if_inside_a_conditional_branch_still_gains_braces() =>
		Formats(
			"""
		class Example
		{
		    static void M(bool condition)
		    {
		#if !ALT
		        if (condition)
		            M(false);
		#endif
		    }
		}
		""",
			"""
		class Example
		{
		    static void M(bool condition)
		    {
		#if !ALT
		        if (condition)
		        {
		            M(false);
		        }
		#endif
		    }
		}
		""",
			Always
		);
}
