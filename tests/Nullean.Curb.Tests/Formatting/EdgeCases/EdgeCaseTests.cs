using Nullean.Curb;

namespace Nullean.Curb.Tests.Formatting.EdgeCases;

/// <summary>
/// Degenerate files, deep nesting, and syntax Curb does not model.
/// </summary>
/// <remarks>
/// The last group matters most: anything without a printer is emitted verbatim rather than guessed
/// at, which is what lets printer coverage grow without ever risking someone's code.
/// </remarks>
public class EdgeCaseTests : FormattingTest
{
	[Test]
	public Task Empty_file() => FormatsExactly("", "");

	[Test]
	public Task File_of_only_whitespace() => FormatsExactly("   \n  \n", "");

	[Test]
	public Task File_of_only_a_comment() => Unchanged(
		"""
		// nothing else here
		""");

	[Test]
	public Task File_of_only_usings() => Unchanged(
		"""
		using System;
		using System.Linq;
		""");

	[Test]
	public Task File_without_a_trailing_newline() => FormatsExactly(
		"public class C\n{\n}",
		"public class C\n{\n}\n");

	[Test]
	public Task Top_level_statements() => Unchanged(
		"""
		using System;

		Console.WriteLine("hello");
		""");

	[Test]
	public Task Deeply_nested_blocks() => Unchanged(
		"""
		public class C
		{
		    public void M()
		    {
		        if (first)
		        {
		            if (second)
		            {
		                if (third)
		                {
		                    if (fourth)
		                    {
		                        Call();
		                    }
		                }
		            }
		        }
		    }
		}
		""");

	[Test]
	public Task Deeply_nested_types() => Unchanged(
		"""
		public class A
		{
		    public class B
		    {
		        public class C
		        {
		            public class D
		            {
		                public int Value;
		            }
		        }
		    }
		}
		""");

	[Test]
	public Task Very_long_single_line_is_left_alone_when_reflow_is_off() => Unchanged(
		"""
		public class C
		{
		    public void M()
		    {
		        Call(one, two, three, four, five, six, seven, eight, nine, ten, eleven, twelve);
		    }
		}
		""");

	[Test]
	public Task Unicode_identifiers_and_strings() => Unchanged(
		"""
		public class C
		{
		    public string Grüße = "こんにちは";
		}
		""");

	[Test]
	public Task Unsafe_and_fixed_are_emitted_verbatim() => Unchanged(
		"""
		public class C
		{
		    public unsafe void M(byte[] data)
		    {
		        fixed (byte* pointer = data)
		        {
		            Call();
		        }
		    }
		}
		""");

	[Test]
	public Task Checked_and_unchecked_blocks() => Unchanged(
		"""
		public class C
		{
		    public void M()
		    {
		        checked
		        {
		            Call();
		        }

		        unchecked
		        {
		            Call();
		        }
		    }
		}
		""");

	[Test]
	public Task Goto_and_labels() => Formats(
		"""
		public class C
		{
		    public void M()
		    {
		        goto done;
		        done:
		        Call();
		    }
		}
		""",
		// csharp_indent_labels defaults to one_less_than_current, so the label moves left of the
		// statements it introduces and the statement itself returns to the ordinary indent.
		"""
		public class C
		{
		    public void M()
		    {
		        goto done;
		    done:
		        Call();
		    }
		}
		""");

	[Test]
	public Task Stackalloc() => Unchanged(
		"""
		public class C
		{
		    public void M()
		    {
		        Span<byte> buffer = stackalloc byte[16];
		    }
		}
		""");

	[Test]
	public Task Function_pointer_type_is_emitted_verbatim() => Unchanged(
		"""
		public unsafe class C
		{
		    public delegate*<int, void> Callback;
		}
		""");

	[Test]
	public Task An_empty_anonymous_object_has_nothing_to_lay_out() => Formats(
		// `new { }` is valid C#, and both ends of the anonymous-type printer indexed the initializer
		// list without checking it had any. Because the CLI formats in parallel and does not isolate a
		// file's exceptions, this one expression aborted the entire run — found on MassTransit, efcore
		// and roslyn, which is to say the three largest repositories in the comparison corpus.
		"""
		public class C
		{
		    void M() => Publish(new {});
		}
		""",
		"""
		public class C
		{
		    void M() => Publish(new { });
		}
		""");

	[Test]
	public Task A_file_based_program_is_formatted_like_any_other() => Formats(
		// `#:` directives head a `dotnet run app.cs` single-file program. Roslyn reports them as an
		// error unless the FileBasedProgram feature is on, so Curb refused every one of these
		// outright while dotnet format formatted them happily. Shipping C#, and a growing shape.
		"""
		#:sdk Microsoft.NET.Sdk
		#:package Humanizer@2.14.1

		using Humanizer;

		Console.WriteLine( "hello".Humanize( ) );
		""",
		"""
		#:sdk Microsoft.NET.Sdk
		#:package Humanizer@2.14.1

		using Humanizer;

		Console.WriteLine("hello".Humanize());
		""");

	[Test]
	public Task Source_that_does_not_parse_is_refused() =>
		Rejects("public class C { void M( { }");

	[Test]
	public Task Unbalanced_braces_are_refused() =>
		Rejects("public class C {");

	[Test]
	public Task Stray_token_is_refused() =>
		Rejects("public class C { } }");

	[Test]
	public Task An_expression_alone_is_refused() =>
		Rejects("1 +");
}
