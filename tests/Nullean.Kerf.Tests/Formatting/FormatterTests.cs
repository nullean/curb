using AwesomeAssertions;
using Nullean.Kerf;

namespace Nullean.Kerf.Tests.Formatting;

/// <summary>
/// End-to-end formatting. Most of these are regressions from the first run over a real repository —
/// every one was a bug that either lost content or produced source that no longer compiled.
/// </summary>
public class FormatterTests
{
	private static FormatOptions Options(int width = FormatOptions.Off) => new()
	{
		MaxLineLength = width,
		IndentSize = 4,
		EndOfLine = EndOfLine.Lf,
	};

	private static string Format(string source, int width = FormatOptions.Off)
	{
		using var formatter = new CSharpFormatter();
		var result = formatter.Format(source, Options(width));
		result.Status.Should().Be(FormatStatus.Formatted, result.Message ?? "no message");
		return result.Text!;
	}

	/// <summary>Formatting must always produce source that still parses.</summary>
	private static void ShouldStillParse(string formatted) =>
		CSharpSource.TryParse(formatted, out _, out var errors)
			.Should().BeTrue(errors.Count > 0 ? errors[0].GetMessage() : "should parse");

	[Test]
	public async Task Preserves_documentation_comments()
	{
		// Doc comments are structured trivia: Span starts after the /// marker, so emitting Span
		// rather than FullSpan silently strips the marker off every one of them.
		const string source = """
			/// <summary>Does a thing.</summary>
			public class C
			{
			}
			""";

		Format(source).Should().Contain("/// <summary>Does a thing.</summary>");
		await Task.CompletedTask;
	}

	[Test]
	public async Task Preserves_attribute_lists()
	{
		const string source = """
			public class C
			{
			    [Fact]
			    public void M()
			    {
			    }
			}
			""";

		Format(source).Should().Contain("[Fact]");
		await Task.CompletedTask;
	}

	[Test]
	public async Task Preserves_assembly_attributes_after_usings()
	{
		// Assembly attributes come after usings in source order. Emitting them first reorders the
		// file and moves whatever licence header is attached to the first element.
		const string source = """
			// Licensed to someone.
			using System;

			[assembly: CLSCompliant(true)]

			public class C
			{
			}
			""";

		var formatted = Format(source);

		formatted.Should().Contain("// Licensed to someone.");
		formatted.IndexOf("using System;", StringComparison.Ordinal)
			.Should().BeLessThan(formatted.IndexOf("[assembly:", StringComparison.Ordinal));
		ShouldStillParse(formatted);
		await Task.CompletedTask;
	}

	[Test]
	public async Task Keeps_the_struct_keyword_of_a_record_struct()
	{
		// TypeDeclarationSyntax.Keyword is only `record`; the second keyword is separate.
		const string source = "public readonly record struct Point(int X, int Y);";

		Format(source).Should().Contain("record struct Point");
		await Task.CompletedTask;
	}

	[Test]
	public async Task Keeps_using_aliases_and_global_usings()
	{
		const string source = """
			global using System;
			using Alias = System.Collections.Generic.List<string>;
			using static System.Math;
			""";

		var formatted = Format(source);

		formatted.Should().Contain("global using System;");
		formatted.Should().Contain("using Alias = System.Collections.Generic.List<string>;");
		formatted.Should().Contain("using static System.Math;");
		ShouldStillParse(formatted);
		await Task.CompletedTask;
	}

	[Test]
	public async Task Keeps_the_space_after_an_argument_ref_kind()
	{
		// `out var x` losing its space becomes the single identifier `outvar`, which does not parse.
		const string source = """
			public class C
			{
			    public void M()
			    {
			        d.TryGetValue("k", out var value);
			    }
			}
			""";

		var formatted = Format(source);

		formatted.Should().Contain("out var value");
		ShouldStillParse(formatted);
		await Task.CompletedTask;
	}

	[Test]
	public async Task Puts_preprocessor_directives_on_their_own_line()
	{
		// A directive must be the first non-whitespace on its line, or the file stops compiling.
		const string source = """
			using System;
			#if DEBUG
			using System.Diagnostics;
			#endif
			using System.Linq;
			""";

		var formatted = Format(source);

		formatted.Should().Contain("\n#endif\n");
		formatted.Should().NotContain(";#endif");
		ShouldStillParse(formatted);
		await Task.CompletedTask;
	}

	[Test]
	public async Task Preserves_a_single_blank_line_and_collapses_runs()
	{
		const string source = """
			public class C
			{
			    public void A()
			    {
			    }




			    public void B()
			    {
			    }
			}
			""";

		var formatted = Format(source);

		formatted.Should().Contain("}\n\n    public void B");
		formatted.Should().NotContain("\n\n\n");
		await Task.CompletedTask;
	}

	[Test]
	public async Task Reflows_an_argument_list_that_does_not_fit()
	{
		const string source = """
			public class C
			{
			    public void M()
			    {
			        Call(alphaArgument, betaArgument, gammaArgument, deltaArgument);
			    }
			}
			""";

		var wide = Format(source);
		var narrow = Format(source, width: 40);

		wide.Should().Contain("Call(alphaArgument, betaArgument, gammaArgument, deltaArgument);");
		narrow.Should().Contain("alphaArgument,\n");
		ShouldStillParse(narrow);
		await Task.CompletedTask;
	}

	[Test]
	public async Task Reflow_is_off_by_default()
	{
		const string source = """
			public class C
			{
			    public void M()
			    {
			        Call(alphaArgument, betaArgument, gammaArgument, deltaArgument, epsilonArgument);
			    }
			}
			""";

		// The default keeps Kerf a no-op on repositories that are already IDE0055-clean.
		Format(source).Should().Contain("Call(alphaArgument, betaArgument, gammaArgument, deltaArgument, epsilonArgument);");
		await Task.CompletedTask;
	}

	[Test]
	public async Task Refuses_to_format_source_that_does_not_parse()
	{
		using var formatter = new CSharpFormatter();
		var result = formatter.Format("public class C { void M( { }", Options());

		result.Status.Should().Be(FormatStatus.SyntaxError);
		result.Text.Should().BeNull();
		await Task.CompletedTask;
	}

	[Test]
	public async Task Unhandled_syntax_is_emitted_verbatim_rather_than_guessed_at()
	{
		// Kerf has no printer for a switch expression yet. It must come through untouched, not
		// mangled — that is what lets printer coverage grow without risking anyone's code.
		const string source = """
			public class C
			{
			    public string M(int x) => x switch
			    {
			        1 => "one",
			        _ => "many",
			    };
			}
			""";

		var formatted = Format(source);

		formatted.Should().Contain("""1 => "one",""");
		formatted.Should().Contain("""_ => "many",""");
		ShouldStillParse(formatted);
		await Task.CompletedTask;
	}

	[Test]
	public async Task A_leading_comment_starts_its_own_line()
	{
		// Roslyn puts same-line trivia into the previous token's TRAILING trivia, so anything in
		// leading trivia began a fresh line. Gluing it onto the previous output made the next run
		// read it back as trailing trivia instead, and the output oscillated between the two.
		const string source = """
			public class C
			{
			    public void M()
			    {
			        Call(
			            a
			        );
			        // a comment about the next call
			        Other();
			    }
			}
			""";

		var formatted = Format(source);

		formatted.Should().NotContain(");// a comment");
		formatted.Should().Contain("\n        // a comment about the next call");
		Format(formatted).Should().Be(formatted);
		await Task.CompletedTask;
	}

	[Test]
	public async Task Disabled_preprocessor_branches_keep_their_own_indentation()
	{
		// Text inside a false #if is never parsed and carries its own indentation. Emitting the
		// enclosing indent in front of it stacks one on top of the other, and every subsequent run
		// indents the already-indented text again.
		const string source = """
			public class C
			{
			    public void M()
			    {
			        First();
			#if DEBUG
			        Debug.WriteLine("x");
			#endif
			        Last();
			    }
			}
			""";

		var once = Format(source);
		var twice = Format(once);

		twice.Should().Be(once, "indentation inside a disabled branch must not drift");
		once.Should().Contain("\n#if DEBUG\n");
		once.Should().Contain("\n#endif\n");
		ShouldStillParse(once);
		await Task.CompletedTask;
	}

	[Test]
	public async Task A_trailing_block_comment_keeps_code_after_it_separated()
	{
		const string source = """
			public class C
			{
			    public void M()
			    {
			        Call(/*lang=json*/ "{}");
			    }
			}
			""";

		var formatted = Format(source);

		formatted.Should().NotContain("*/\"");
		ShouldStillParse(formatted);
		await Task.CompletedTask;
	}

	[Test]
	public async Task Keeps_the_space_after_a_generic_variance_keyword()
	{
		// Without it `out` welds onto the parameter name and becomes part of the identifier. The
		// content verifier cannot see this — it ignores whitespace — so only re-parsing catches it.
		const string source = "public interface IReader<out T, in TKey> { }";

		var formatted = Format(source);

		formatted.Should().Contain("<out T, in TKey>");
		ShouldStillParse(formatted);
		await Task.CompletedTask;
	}

	[Test]
	public async Task Round_trip_verification_catches_damage_the_content_check_cannot()
	{
		// Both nets exist because they fail on different things: this one is whitespace-blind, so
		// welded tokens survive it and are caught by re-parsing instead.
		const string source = "public interface IReader<out T> { }";

		using var formatter = new CSharpFormatter();
		var result = formatter.Format(source, Options(), verifyRoundTrip: true);

		result.Status.Should().Be(FormatStatus.Formatted, result.Message ?? "no message");
		await Task.CompletedTask;
	}

	[Test]
	public async Task Formatting_is_idempotent()
	{
		const string source = """
			using System;

			namespace N;

			/// <summary>A type.</summary>
			public class C
			{
			    // a comment
			    public void M(string a, int b)
			    {
			        Console.WriteLine(a); // trailing comment

			        Console.WriteLine(b);
			    }
			}
			""";

		var once = Format(source);
		var twice = Format(once);

		twice.Should().Be(once);
		await Task.CompletedTask;
	}
}
