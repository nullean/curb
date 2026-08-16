namespace Nullean.Kerf.Tests.Formatting.Trivia;

/// <summary>
/// Preprocessor directives, and the code inside branches the parser never sees.
/// </summary>
/// <remarks>
/// Two rules do the work here. A directive must be the first non-whitespace on its line, or it stops
/// being a directive and the file no longer compiles. And text inside a false <c>#if</c> is not
/// parsed at all — it arrives as one blob of trivia carrying its own indentation, so it is emitted
/// at column zero rather than re-indented, which would otherwise compound on every run.
/// </remarks>
public class DirectiveTests : FormattingTest
{
	[Test]
	public Task Conditional_around_a_using_directive() => Unchanged(
		"""
		using System;
		#if DEBUG
		using System.Diagnostics;
		#endif
		using System.Linq;
		""");

	[Test]
	public Task Conditional_directives_sit_at_column_zero_inside_indented_code() => Unchanged(
		"""
		public class C
		{
		    public void M()
		    {
		        First();
		#if DEBUG
		        Debug();
		#endif
		        Last();
		    }
		}
		""");

	[Test]
	public Task A_directive_never_ends_up_on_the_previous_line() => Formats(
		"""
		using System;
		#if DEBUG
		using System.Diagnostics;
		#endif
		""",
		"""
		using System;
		#if DEBUG
		using System.Diagnostics;
		#endif
		""");

	[Test]
	public Task Else_branch() => Unchanged(
		"""
		public class C
		{
		#if DEBUG
		    public int Value = 1;
		#else
		    public int Value = 2;
		#endif
		}
		""");

	[Test]
	public Task Elif_branch() => Unchanged(
		"""
		public class C
		{
		#if FIRST
		    public int Value = 1;
		#elif SECOND
		    public int Value = 2;
		#else
		    public int Value = 3;
		#endif
		}
		""");

	[Test]
	public Task Nested_conditionals() => Unchanged(
		"""
		public class C
		{
		#if OUTER
		#if INNER
		    public int Value;
		#endif
		#endif
		}
		""");

	[Test]
	public Task Disabled_code_keeps_its_own_indentation() => Unchanged(
		"""
		public class C
		{
		    public void M()
		    {
		#if NEVER_DEFINED
		        var untouched   =   1;
		#endif
		        Call();
		    }
		}
		""");

	[Test]
	public Task Disabled_code_is_not_reformatted() => Unchanged(
		"""
		public class C
		{
		#if NEVER_DEFINED
		    public void Old(   ){Call( );}
		#endif
		    public int Value;
		}
		""");

	[Test]
	public Task Pragma_warning_disable_and_restore() => Unchanged(
		"""
		public class C
		{
		#pragma warning disable CS0168
		    public int Value;
		#pragma warning restore CS0168
		}
		""");

	[Test]
	public Task Nullable_directive() => Unchanged(
		"""
		#nullable enable
		using System;

		public class C
		{
		    public string? Value;
		}
		""");

	[Test]
	public Task Define_and_undef_at_the_top_of_a_file() => Unchanged(
		"""
		#define FEATURE
		#undef OTHER
		using System;
		""");

	[Test]
	public Task Line_directive() => Unchanged(
		"""
		public class C
		{
		#line 42 "Other.cs"
		    public int Value;
		#line default
		}
		""");

	[Test]
	public Task Directive_immediately_before_a_type() => Unchanged(
		"""
		using System;

		#if DEBUG
		public class C
		{
		    public int Value;
		}
		#endif
		""");

	[Test]
	public Task Directive_wrapping_an_entire_method_body() => Unchanged(
		"""
		public class C
		{
		    public void M()
		    {
		#if DEBUG
		        Debug();
		#endif
		    }
		}
		""");

	[Test]
	public Task Directive_as_the_last_thing_in_a_file() => Unchanged(
		"""
		using System;
		#if DEBUG
		#endif
		""");

	[Test]
	public Task Directive_between_members() => Unchanged(
		"""
		public class C
		{
		    public int First;

		#if DEBUG
		    public int Second;
		#endif
		}
		""");

	[Test]
	public Task A_comment_above_a_directive() => Unchanged(
		"""
		using System;

		// only in debug builds
		#if DEBUG
		using System.Diagnostics;
		#endif
		""");

	[Test]
	public Task A_directive_inside_a_switch() => Unchanged(
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
		#if DEBUG
		            case 2:
		                Second();
		                break;
		#endif
		        }
		    }
		}
		""");

	[Test]
	[Skip("Kerf refuses a file containing #error; CSharpier filters that diagnostic instead")]
	public Task Error_directive_is_formatted_rather_than_refused() => Unchanged(
		"""
		public class C
		{
		#if NEVER_DEFINED
		#error not supported
		#endif
		    public int Value;
		}
		""");

	[Test]
	public Task Warning_directive() => Unchanged(
		"""
		public class C
		{
		#warning this is temporary
		    public int Value;
		}
		""");
}
