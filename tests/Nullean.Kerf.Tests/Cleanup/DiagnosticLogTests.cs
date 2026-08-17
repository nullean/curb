using System.Text;
using AwesomeAssertions;
using Microsoft.CodeAnalysis.Text;
using Nullean.Kerf.Cleanup;

namespace Nullean.Kerf.Tests.Cleanup;

/// <summary>
/// Reading the two formats a build reports its diagnostics in.
/// </summary>
/// <remarks>
/// The literals here are the real thing, measured from <c>dotnet build</c> on SDK 10.0.400 rather than
/// written from the SARIF specification or from memory. That matters more here than anywhere else in
/// the suite: this reader is Kerf's only source of truth about whether a semantic rule applies, so a
/// misread field is a wrong edit to somebody's source.
/// </remarks>
public class DiagnosticLogTests
{
	/// <summary>A trimmed real log: the results array first, the rule metadata after it.</summary>
	private const string Sarif = """
		{
		  "$schema": "https://json.schemastore.org/sarif-2.1.0.json",
		  "version": "2.1.0",
		  "runs": [
		    {
		      "results": [
		        {
		          "ruleId": "IDE0044",
		          "ruleIndex": 319,
		          "level": "warning",
		          "message": { "text": "Make field readonly" },
		          "locations": [
		            {
		              "physicalLocation": {
		                "artifactLocation": { "uri": "file:///repo/Widget.cs" },
		                "region": { "startLine": 8, "startColumn": 17, "endLine": 8, "endColumn": 22 }
		              }
		            }
		          ],
		          "properties": { "warningLevel": 1 }
		        },
		        {
		          "ruleId": "IDE0005",
		          "level": "warning",
		          "message": { "text": "Using directive is unnecessary." },
		          "locations": [
		            {
		              "physicalLocation": {
		                "artifactLocation": { "uri": "file:///repo/Widget.cs" },
		                "region": { "startLine": 1, "startColumn": 1, "endLine": 2, "endColumn": 28 }
		              }
		            }
		          ]
		        }
		      ],
		      "properties": { "analyzerExecutionTime": "0.4" },
		      "tool": {
		        "driver": {
		          "name": "csc",
		          "rules": [ { "id": "IDE0005", "shortDescription": { "text": "…" } } ]
		        }
		      },
		      "columnKind": "utf16CodeUnits"
		    }
		  ]
		}
		""";

	private static List<CleanupDiagnostic> Read(string log)
	{
		DiagnosticLog.TryRead(Encoding.UTF8.GetBytes(log), out var diagnostics, out var failure)
			.Should().BeTrue(failure);

		return diagnostics;
	}

	// ---- SARIF ------------------------------------------------------------------------------------

	[Test]
	public async Task Sarif_reads_the_rule_the_file_and_the_span()
	{
		var diagnostics = Read(Sarif);

		diagnostics.Should().HaveCount(2);

		var readonlyField = diagnostics[0];
		readonlyField.RuleId.Should().Be("IDE0044");
		readonlyField.FilePath.Should().Be("/repo/Widget.cs");

		// One-based on the wire, zero-based in a LinePosition.
		readonlyField.Start.Should().Be(new LinePosition(7, 16));
		readonlyField.End.Should().Be(new LinePosition(7, 21));
		readonlyField.HasSpan.Should().BeTrue();

		await Task.CompletedTask;
	}

	[Test]
	public async Task Sarif_skips_the_rule_metadata_that_follows_the_results()
	{
		// The payload is ~220 KB per project per target framework and almost all of it is this
		// metadata. Reading the results without materialising it is the reason for the streaming reader,
		// so a log whose `tool` comes after `results` has to work.
		Read(Sarif).Should().HaveCount(2, "the results were read despite the metadata sitting behind them");
		await Task.CompletedTask;
	}

	[Test]
	public async Task Ide0005_spans_the_whole_run_of_unnecessary_usings()
	{
		// The measured behaviour, and the reason SARIF is the primary format: Roslyn emits one
		// diagnostic per maximal contiguous run of unnecessary directives, not one per directive.
		var source = SourceText.From("using System.Text;\nusing System.Globalization;\n\nnamespace N;\n");

		var ide0005 = Read(Sarif).Single(d => d.RuleId == "IDE0005");
		ide0005.TryResolve(source, out var span).Should().BeTrue();

		source.ToString(span).Should().Be("using System.Text;\nusing System.Globalization;",
			"the span covers both directives and stops before the newline");

		await Task.CompletedTask;
	}

	[Test]
	public async Task Two_concatenated_logs_are_refused_rather_than_half_read()
	{
		// What a single ErrorLog path shared by the inner builds of a multi-targeting project produces.
		// Measured: both inner builds write to the same file and the result is two JSON documents.
		var doubled = Encoding.UTF8.GetBytes(Sarif + Sarif);

		DiagnosticLog.TryRead(doubled, out _, out var failure).Should().BeFalse();
		failure.Should().NotBeNullOrEmpty("a corrupt log has to say so rather than yield a partial fix set");

		await Task.CompletedTask;
	}

	[Test]
	public async Task A_relative_or_non_file_uri_is_refused()
	{
		var log = Sarif.Replace("file:///repo/Widget.cs", "Widget.cs", StringComparison.Ordinal);

		Read(log).Should().BeEmpty("a uri needing a uriBaseId cannot be resolved to a path, so it is dropped");
		await Task.CompletedTask;
	}

	// ---- MSBuild console output -------------------------------------------------------------------

	[Test]
	public async Task A_console_line_yields_the_rule_the_file_and_a_start()
	{
		const string log =
			"/repo/Widget.cs(8,17): warning IDE0044: Make field readonly "
			+ "(https://learn.microsoft.com/dotnet/fundamentals/code-analysis/style-rules/ide0044) "
			+ "[/repo/e1.csproj::TargetFramework=net10.0]";

		var diagnostic = Read(log).Single();

		diagnostic.RuleId.Should().Be("IDE0044");
		diagnostic.FilePath.Should().Be("/repo/Widget.cs");
		diagnostic.Start.Should().Be(new LinePosition(7, 16));
		diagnostic.End.Should().BeNull("MSBuild reports a start only");
		diagnostic.HasSpan.Should().BeFalse();

		await Task.CompletedTask;
	}

	[Test]
	public async Task The_severity_word_is_not_matched_on()
	{
		// `warning` and `error` are localised by the SDK's display language; a rule id is not. Matching
		// the id is what keeps the reader working on a non-English machine.
		const string log = "/repo/Widget.cs(8,17): Warnung IDE0044: Feld als readonly markieren";

		Read(log).Single().RuleId.Should().Be("IDE0044");
		await Task.CompletedTask;
	}

	[Test]
	public async Task A_path_containing_parentheses_still_parses()
	{
		const string log = "/repo/My (vendored) code/Widget.cs(3,5): warning IDE0040: Accessibility modifiers required";

		var diagnostic = Read(log).Single();

		diagnostic.FilePath.Should().Be("/repo/My (vendored) code/Widget.cs");
		diagnostic.Start.Should().Be(new LinePosition(2, 4));

		await Task.CompletedTask;
	}

	[Test]
	public async Task The_four_part_position_form_is_read_as_a_span()
	{
		// MSBuild's canonical diagnostic format allows an end as well. The compiler does not emit one,
		// but another logger in the chain may.
		const string log = "/repo/Widget.cs(1,1,2,28): warning IDE0005: Using directive is unnecessary.";

		var diagnostic = Read(log).Single();

		diagnostic.Start.Should().Be(new LinePosition(0, 0));
		diagnostic.End.Should().Be(new LinePosition(1, 27));

		await Task.CompletedTask;
	}

	[Test]
	public async Task Lines_that_are_not_diagnostics_are_ignored()
	{
		const string log = """
			  Determining projects to restore...
			  e1 -> /repo/bin/Debug/net10.0/e1.dll

			Build succeeded.
			    10 Warning(s)
			    0 Error(s)
			""";

		Read(log).Should().BeEmpty();
		await Task.CompletedTask;
	}

	// ---- Deduplication ---------------------------------------------------------------------------

	[Test]
	public async Task The_same_site_reported_four_times_is_one_diagnostic()
	{
		// Measured: every diagnostic appears once in the build stream and once in the trailing summary,
		// and both of those once per target framework. Five sites produced twenty console lines.
		const string line = "/repo/Widget.cs(8,17): warning IDE0044: Make field readonly";
		var log = string.Join('\n', line, line, line, line);

		Read(log).Should().HaveCount(1);
		await Task.CompletedTask;
	}

	[Test]
	public async Task Two_sites_of_the_same_rule_are_kept_apart()
	{
		const string log = """
			/repo/Widget.cs(8,17): warning IDE0044: Make field readonly
			/repo/Widget.cs(9,6): warning IDE0044: Make field readonly
			""";

		Read(log).Should().HaveCount(2);
		await Task.CompletedTask;
	}

	// ---- Filtering -------------------------------------------------------------------------------

	[Test]
	public async Task Fixable_keeps_only_what_cleanup_attempts()
	{
		const string log = """
			/repo/Widget.cs(1,1): warning IDE0005: Using directive is unnecessary.
			/repo/Widget.cs(4,9): warning IDE0051: Remove unused private members
			/repo/Widget.cs(7,2): warning CA1822: Mark members as static
			""";

		var fixable = DiagnosticLog.Fixable(Read(log));

		fixable.Should().HaveCount(1);
		fixable[0].RuleId.Should().Be("IDE0005", "IDE0051 deletes a declaration and CA1822 is not ours");

		await Task.CompletedTask;
	}

	// ---- Resolving against the file ---------------------------------------------------------------

	[Test]
	public async Task A_position_past_the_end_of_the_file_does_not_resolve()
	{
		// The cheap half of the staleness check: a log describing a file that has since shrunk cannot
		// be applied to it, and saying so is how that stays harmless.
		var source = SourceText.From("class Foo { }\n");
		var stale = new CleanupDiagnostic("IDE0044", "/repo/Widget.cs", new LinePosition(40, 0));

		stale.TryResolve(source, out _).Should().BeFalse();
		await Task.CompletedTask;
	}

	[Test]
	public async Task A_column_past_the_end_of_its_line_does_not_resolve()
	{
		var source = SourceText.From("class Foo { }\n");
		var stale = new CleanupDiagnostic("IDE0044", "/repo/Widget.cs", new LinePosition(0, 99));

		stale.TryResolve(source, out _).Should().BeFalse();
		await Task.CompletedTask;
	}

	[Test]
	public async Task A_start_only_diagnostic_resolves_to_an_empty_span_at_the_position()
	{
		var source = SourceText.From("class Foo { }\n");
		var diagnostic = new CleanupDiagnostic("IDE0040", "/repo/Widget.cs", new LinePosition(0, 6));

		diagnostic.TryResolve(source, out var span).Should().BeTrue();
		span.Start.Should().Be(6);
		span.IsEmpty.Should().BeTrue("a fixer walks out from the position to the node it owns");

		await Task.CompletedTask;
	}
}
