using AwesomeAssertions;

using Microsoft.CodeAnalysis.Text;
using Nullean.Curb.Cleanup;

namespace Nullean.Curb.Tests.Cleanup;

/// <summary>
/// IDE0005, the rule that removes using directives nothing needs.
/// </summary>
/// <remarks>
/// <para>
/// Every case here supplies the diagnostic by hand, which is the point of the design: the compiler's
/// verdict is data, so the rule is testable without a build, a restore or a reference. The spans are
/// shaped the way the real ones are — one diagnostic per contiguous run, end exclusive — measured from
/// <c>dotnet build</c> rather than assumed.
/// </para>
/// <para>
/// Both directions are asserted. A rule that fixes the right thing but also fixes the wrong thing passes
/// half a test suite, so each refusal has a case of its own.
/// </para>
/// </remarks>
public class UnnecessaryUsingsTests
{
	private const string Path = "/repo/Widget.cs";

	private static CleanupResult Clean(string source, params CleanupDiagnostic[] diagnostics) =>
		new CSharpCleaner().Clean(source, diagnostics);

	/// <summary>Builds the diagnostic the compiler would report for a run, from one-based coordinates.</summary>
	private static CleanupDiagnostic Ide0005(int startLine, int startColumn, int endLine, int endColumn) =>
		new("IDE0005", Path, new LinePosition(startLine - 1, startColumn - 1), new LinePosition(endLine - 1, endColumn - 1));

	/// <summary>
	/// Spans the whole of a directive that occupies <paramref name="line"/> on its own, or a run of them
	/// ending at <paramref name="through"/>.
	/// </summary>
	/// <remarks>
	/// The start column skips the line's indentation, because that is where the compiler reports: it
	/// names the directive, not the line it sits on. Measured — an indented field's IDE0040 came back at
	/// the identifier's column, not column 1.
	/// </remarks>
	private static CleanupDiagnostic Ide0005(string source, int line, int through = 0)
	{
		var text = SourceText.From(source);
		var first = text.Lines[line - 1];
		var last = text.Lines[(through == 0 ? line : through) - 1];

		var indent = 0;
		while (first.Start + indent < first.End && char.IsWhiteSpace(text[first.Start + indent]))
			indent++;

		return Ide0005(line, indent + 1, last.LineNumber + 1, last.End - last.Start + 1);
	}

	// ---- What it fixes ----------------------------------------------------------------------------

	[Test]
	public async Task A_single_unnecessary_directive_is_removed_with_its_line()
	{
		const string source = """
			using System.Text;
			using System.Collections.Generic;

			namespace N;

			public class Widget
			{
				public List<string> Items { get; } = [];
			}

			""";

		var result = Clean(source, Ide0005(source, 1));

		result.Status.Should().Be(CleanupStatus.Cleaned);
		result.Applied.Should().Be(1);
		result.Text.Should().Be("""
			using System.Collections.Generic;

			namespace N;

			public class Widget
			{
				public List<string> Items { get; } = [];
			}

			""", "the line goes, not just the tokens, so no hole is left behind");

		await Task.CompletedTask;
	}

	[Test]
	public async Task A_run_of_directives_is_removed_together()
	{
		// One diagnostic, two directives. This is the shape that makes SARIF the primary format.
		const string source = """
			using System.Text;
			using System.Globalization;
			using System.Collections.Generic;

			namespace N;

			public class Widget
			{
				public List<string> Items { get; } = [];
			}

			""";

		var result = Clean(source, Ide0005(source, 1, through: 2));

		result.Applied.Should().Be(1);
		result.Text.Should().StartWith("using System.Collections.Generic;\n\nnamespace N;");
		result.Text.Should().NotContain("System.Text").And.NotContain("System.Globalization");

		await Task.CompletedTask;
	}

	[Test]
	public async Task Two_runs_are_removed_and_what_sits_between_them_is_kept()
	{
		// The measured case: five directives where the third is needed, so the compiler reports two
		// diagnostics rather than one span over all five.
		const string source = """
			using System.Text;
			using System.Globalization;
			using System.Collections.Generic;
			using System.Diagnostics;
			using System.Numerics;

			namespace N;

			public class Widget
			{
				public List<string> Items { get; } = [];
			}

			""";

		var result = Clean(source, Ide0005(source, 1, through: 2), Ide0005(source, 4, through: 5));

		result.Applied.Should().Be(2);
		result.Text.Should().StartWith("using System.Collections.Generic;\n\nnamespace N;");
		result.Text.Should().NotContain("System.Diagnostics").And.NotContain("System.Numerics");

		await Task.CompletedTask;
	}

	[Test]
	public async Task A_directive_inside_a_namespace_is_removed()
	{
		const string source = """
			namespace N
			{
				using System.Text;

				public class Widget
				{
				}
			}

			""";

		var result = Clean(source, Ide0005(source, 3));

		result.Applied.Should().Be(1);
		result.Text.Should().NotContain("System.Text");
		result.Text.Should().Contain("public class Widget");

		await Task.CompletedTask;
	}

	[Test]
	public async Task An_alias_directive_is_removed()
	{
		const string source = """
			using Sb = System.Text.StringBuilder;

			namespace N;

			public class Widget
			{
			}

			""";

		Clean(source, Ide0005(source, 1)).Text.Should().NotContain("Sb");
		await Task.CompletedTask;
	}

	// ---- What it keeps ---------------------------------------------------------------------------

	[Test]
	public async Task A_comment_above_the_directive_survives()
	{
		const string source = """
			// Needed on Mono. Probably.
			using System.Text;

			namespace N;

			public class Widget
			{
			}

			""";

		var result = Clean(source, Ide0005(source, 2));

		result.Applied.Should().Be(1);
		result.Text.Should().Contain("// Needed on Mono. Probably.",
			"the comment is content, and losing it would fail the content verifier anyway");
		result.Text.Should().NotContain("using System.Text;");

		await Task.CompletedTask;
	}

	[Test]
	public async Task A_comment_after_the_directive_on_the_same_line_survives()
	{
		const string source = """
			using System.Text; // why though
			using System.Collections.Generic;

			namespace N;

			public class Widget
			{
				public List<string> Items { get; } = [];
			}

			""";

		var result = Clean(source, Ide0005(source, 1, 1) with
		{
			End = new LinePosition(0, "using System.Text;".Length),
		});

		result.Applied.Should().Be(1);
		result.Text.Should().Contain("// why though", "widening stops at anything that is not whitespace");
		result.Text.Should().NotContain("using System.Text;");

		await Task.CompletedTask;
	}

	// ---- What it refuses -------------------------------------------------------------------------

	[Test]
	public async Task A_file_with_a_conditional_directive_is_refused()
	{
		// The compiler decided for one symbol set. A directive needed only under another would be
		// reported here and then lost, so this is a refusal rather than a guess.
		const string source = """
			using System.Text;

			namespace N;

			public class Widget
			{
			#if NET
				public string Name => new StringBuilder().ToString();
			#endif
			}

			""";

		var result = Clean(source, Ide0005(source, 1));

		result.Changed.Should().BeFalse();
		result.Applied.Should().Be(0);
		result.Refusals.Should().ContainSingle().Which.Should().Contain("#if");

		await Task.CompletedTask;
	}

	[Test]
	public async Task A_start_only_diagnostic_is_refused()
	{
		// MSBuild's console output carries no end, and without one the run's extent is unknowable.
		const string source = """
			using System.Text;

			namespace N;

			""";

		var result = Clean(source, new CleanupDiagnostic("IDE0005", Path, new LinePosition(0, 0)));

		result.Changed.Should().BeFalse();
		result.Refusals.Should().ContainSingle().Which.Should().Contain("no end position");

		await Task.CompletedTask;
	}

	[Test]
	public async Task A_position_that_is_not_a_using_directive_is_refused()
	{
		// What a stale log looks like: the file changed, and the position now names something else.
		const string source = """
			namespace N;

			public class Widget
			{
			}

			""";

		var result = Clean(source, Ide0005(1, 1, 1, 13));

		result.Changed.Should().BeFalse();
		result.Refusals.Should().ContainSingle();

		await Task.CompletedTask;
	}

	[Test]
	public async Task A_span_that_cuts_a_directive_in_half_is_refused()
	{
		const string source = """
			using System.Text;
			using System.Globalization;

			namespace N;

			""";

		// Ends in the middle of the second directive, which no real diagnostic does.
		var result = Clean(source, Ide0005(1, 1, 2, 10));

		result.Changed.Should().BeFalse();
		result.Refusals.Should().ContainSingle().Which.Should().Contain("part of a using directive");

		await Task.CompletedTask;
	}

	[Test]
	public async Task A_span_reaching_past_the_directives_is_refused()
	{
		const string source = """
			using System.Text;

			namespace N;

			public class Widget
			{
			}

			""";

		// Reaches into the namespace declaration. Deleting to there would take source no directive owns.
		var result = Clean(source, Ide0005(1, 1, 3, 13));

		result.Changed.Should().BeFalse();
		result.Refusals.Should().ContainSingle().Which.Should().Contain("past the last using directive");

		await Task.CompletedTask;
	}

	[Test]
	public async Task A_position_outside_the_file_is_refused()
	{
		const string source = "namespace N;\n";

		var result = Clean(source, Ide0005(40, 1, 40, 8));

		result.Changed.Should().BeFalse();
		result.Refusals.Should().ContainSingle().Which.Should().Contain("stale");

		await Task.CompletedTask;
	}

	[Test]
	public async Task Source_that_does_not_parse_is_left_alone()
	{
		const string source = "using System.Text; class {{{";

		var result = Clean(source, Ide0005(source, 1));

		result.Status.Should().Be(CleanupStatus.SyntaxError);
		result.Changed.Should().BeFalse();
		result.Text.Should().BeNull();

		await Task.CompletedTask;
	}

	[Test]
	public async Task A_rule_curb_does_not_own_is_ignored_rather_than_guessed_at()
	{
		const string source = """
			using System.Text;

			namespace N;

			""";

		var result = Clean(source, new CleanupDiagnostic("IDE0051", Path, new LinePosition(0, 0), new LinePosition(0, 18)));

		result.Changed.Should().BeFalse();
		result.Applied.Should().Be(0);

		await Task.CompletedTask;
	}

	// ---- Idempotency and one pass -----------------------------------------------------------------

	[Test]
	public async Task Cleaning_the_output_again_changes_nothing()
	{
		const string source = """
			using System.Text;
			using System.Globalization;

			namespace N;

			public class Widget
			{
			}

			""";

		var once = Clean(source, Ide0005(source, 1, through: 2));
		once.Changed.Should().BeTrue();

		// The same diagnostics no longer apply to the output, which is what a stale log looks like and is
		// exactly what the gate is for.
		var twice = Clean(once.Text!, Ide0005(source, 1, through: 2));
		twice.Changed.Should().BeFalse();

		await Task.CompletedTask;
	}

	[Test]
	public async Task Two_overlapping_fixes_are_both_dropped()
	{
		// Neither is applied, so a second pass sees the same overlap and drops it again. Keeping one
		// would make the second pass differ from the first.
		const string source = """
			using System.Text;
			using System.Globalization;

			namespace N;

			""";

		var result = Clean(source, Ide0005(source, 1, through: 2), Ide0005(source, 1));

		result.Changed.Should().BeFalse();
		result.Applied.Should().Be(0);
		result.Refusals.Should().Contain(refusal => refusal.Contains("overlaps", StringComparison.Ordinal));

		await Task.CompletedTask;
	}
}
