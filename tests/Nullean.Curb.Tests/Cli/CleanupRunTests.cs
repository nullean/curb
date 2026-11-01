using System.IO.Abstractions.TestingHelpers;
using System.Text;
using AwesomeAssertions;
using Microsoft.CodeAnalysis.Text;
using Nullean.Curb.Cli;

namespace Nullean.Curb.Tests.Cli;

/// <summary>
/// <c>curb cleanup</c>'s own byte-order-mark contract, mirroring <see cref="FormattingRunTests"/>'s
/// coverage for <c>curb format</c>. The two runs used to disagree: this one still read and wrote through
/// <c>string</c>-based <c>File</c> APIs, which silently drop a mark on the way in and never add one on
/// the way out.
/// </summary>
public class CleanupRunTests
{
	private const string Root = "/repo";

	private static MockFileSystem Repo(string editorConfig)
	{
		var fs = new MockFileSystem();
		fs.AddFile($"{Root}/.editorconfig", new MockFileData(editorConfig));
		return fs;
	}

	[Test]
	public async Task A_byte_order_mark_survives_a_cleanup_that_was_told_to_keep_it()
	{
		var fs = Repo("root = true\n\n[*.cs]\nindent_style = space\ncharset = utf-8-bom\n");

		const string source =
			"""
			namespace N;

			public sealed class Widget
			{
				private string _name;

				public Widget() => _name = "w";
			}

			""";

		var text = SourceText.From(source);
		var offset = source.IndexOf("_name;", StringComparison.Ordinal);
		var line = text.Lines.GetLineFromPosition(offset);

		// MSBuild's console-log shape: one-based line and column, start position only.
		var diagnosticLine = $"{Root}/Widget.cs({line.LineNumber + 1},{offset - line.Start + 1}): warning IDE0044: " +
			$"Make field readonly (https://example/ide0044) [{Root}/e1.csproj::TargetFramework=net10.0]\n";

		byte[] withMark = [0xEF, 0xBB, 0xBF, .. Encoding.UTF8.GetBytes(source)];
		fs.AddFile($"{Root}/Widget.cs", new MockFileData(withMark));
		fs.AddFile($"{Root}/curb.sarif", new MockFileData(diagnosticLine));

		// The freshness gate rejects a source file newer than the log, so the log has to date after it.
		fs.File.SetLastWriteTimeUtc($"{Root}/Widget.cs", DateTime.UtcNow);
		fs.File.SetLastWriteTimeUtc($"{Root}/curb.sarif", DateTime.UtcNow.AddMinutes(1));

		var exitCode = CleanupRun.Execute(fs, Root, write: true);

		exitCode.Should().Be(0);

		var bytes = fs.File.ReadAllBytes($"{Root}/Widget.cs");
		bytes.Take(3).Should().Equal([0xEF, 0xBB, 0xBF], "charset = utf-8-bom asked to keep it");
		Encoding.UTF8.GetString(bytes[3..]).Should().Contain("private readonly string _name;");

		await Task.CompletedTask;
	}

	[Test]
	public async Task A_file_that_is_not_utf8_is_reported_rather_than_rewritten()
	{
		var fs = Repo("root = true\n\n[*.cs]\nindent_style = space\n");

		// Latin-1 é in a string literal: decoded leniently it became U+FFFD and was written back that way.
		byte[] latin1 =
		[
			.. Encoding.ASCII.GetBytes("namespace N;\n\npublic sealed class Widget\n{\n\tprivate string _name;\n\n\tpublic Widget() => _name = \"caf"),
			0xE9,
			.. Encoding.ASCII.GetBytes("\";\n}\n"),
		];
		var diagnosticLine = $"{Root}/Widget.cs(5,17): warning IDE0044: " +
			$"Make field readonly (https://example/ide0044) [{Root}/e1.csproj::TargetFramework=net10.0]\n";

		fs.AddFile($"{Root}/Widget.cs", new MockFileData(latin1));
		fs.AddFile($"{Root}/curb.sarif", new MockFileData(diagnosticLine));
		fs.File.SetLastWriteTimeUtc($"{Root}/Widget.cs", DateTime.UtcNow);
		fs.File.SetLastWriteTimeUtc($"{Root}/curb.sarif", DateTime.UtcNow.AddMinutes(1));

		var exitCode = CleanupRun.Execute(fs, Root, write: true);

		exitCode.Should().NotBe(0, "a file that cannot be read is a failure, not a clean run");
		fs.File.ReadAllBytes($"{Root}/Widget.cs").Should().Equal(latin1, "a file Curb cannot decode is left exactly as it was");

		await Task.CompletedTask;
	}

	[Test]
	public async Task A_byte_order_mark_is_stripped_when_charset_asks_for_plain_utf8()
	{
		var fs = Repo("root = true\n\n[*.cs]\nindent_style = space\ncharset = utf-8\n");

		const string source =
			"""
			namespace N;

			public sealed class Widget
			{
				private string _name;

				public Widget() => _name = "w";
			}

			""";

		var text = SourceText.From(source);
		var offset = source.IndexOf("_name;", StringComparison.Ordinal);
		var line = text.Lines.GetLineFromPosition(offset);

		var diagnosticLine = $"{Root}/Widget.cs({line.LineNumber + 1},{offset - line.Start + 1}): warning IDE0044: " +
			$"Make field readonly (https://example/ide0044) [{Root}/e1.csproj::TargetFramework=net10.0]\n";

		byte[] withMark = [0xEF, 0xBB, 0xBF, .. Encoding.UTF8.GetBytes(source)];
		fs.AddFile($"{Root}/Widget.cs", new MockFileData(withMark));
		fs.AddFile($"{Root}/curb.sarif", new MockFileData(diagnosticLine));

		fs.File.SetLastWriteTimeUtc($"{Root}/Widget.cs", DateTime.UtcNow);
		fs.File.SetLastWriteTimeUtc($"{Root}/curb.sarif", DateTime.UtcNow.AddMinutes(1));

		var exitCode = CleanupRun.Execute(fs, Root, write: true);

		exitCode.Should().Be(0);

		var bytes = fs.File.ReadAllBytes($"{Root}/Widget.cs");
		bytes.Take(3).Should().NotEqual([0xEF, 0xBB, 0xBF], "charset = utf-8 asked to drop it");

		await Task.CompletedTask;
	}

	[Test]
	public async Task A_write_failure_in_one_file_is_reported_rather_than_aborting_the_run()
	{
		// A read-only file's write throws inside the worker — standing in for any per-file fault,
		// cleaner or formatter bug included, that has nothing to do with the rest of the batch.
		// Chosen because MockFileSystem genuinely throws UnauthorizedAccessException for it,
		// without a fake seam. Mirrors FormattingRunTests' coverage of the same contract.
		var fs = Repo("root = true\n\n[*.cs]\nindent_style = space\n");

		const string source =
			"""
			namespace N;

			public sealed class Widget
			{
				private string _name;

				public Widget() => _name = "w";
			}

			""";

		var text = SourceText.From(source);
		var offset = source.IndexOf("_name;", StringComparison.Ordinal);
		var line = text.Lines.GetLineFromPosition(offset);
		var position = $"{line.LineNumber + 1},{offset - line.Start + 1}";

		var diagnosticLine =
			$"{Root}/Widget.cs({position}): warning IDE0044: Make field readonly (https://example/ide0044) [{Root}/e1.csproj::TargetFramework=net10.0]\n"
			+ $"{Root}/Broken.cs({position}): warning IDE0044: Make field readonly (https://example/ide0044) [{Root}/e1.csproj::TargetFramework=net10.0]\n";

		fs.AddFile($"{Root}/Widget.cs", new MockFileData(source));
		fs.AddFile($"{Root}/Broken.cs", new MockFileData(source));
		fs.AddFile($"{Root}/curb.sarif", new MockFileData(diagnosticLine));
		fs.File.SetAttributes($"{Root}/Broken.cs", FileAttributes.ReadOnly);

		fs.File.SetLastWriteTimeUtc($"{Root}/Widget.cs", DateTime.UtcNow);
		fs.File.SetLastWriteTimeUtc($"{Root}/Broken.cs", DateTime.UtcNow);
		fs.File.SetLastWriteTimeUtc($"{Root}/curb.sarif", DateTime.UtcNow.AddMinutes(1));

		var exitCode = CleanupRun.Execute(fs, Root, write: true);

		exitCode.Should().Be(3, "one file failed to write");
		fs.File.ReadAllText($"{Root}/Widget.cs").Should().Contain(
			"private readonly string _name;", "the other file in the same batch still gets cleaned");
		fs.File.ReadAllText($"{Root}/Broken.cs").Should().Be(
			source, "a file the write failed for is left exactly as it was found");

		await Task.CompletedTask;
	}

	[Test]
	public async Task A_site_every_target_framework_reported_is_fixed_once()
	{
		// A multi-targeted project writes one log per target framework, and a site both frameworks agree on is
		// in each. Cleanup handed both reports to the cleaner and inserted every modifier twice: `readonly
		// readonly`, `private private`, CS1004. Found by the generated corpus's multi-targeted project.
		var fs = Repo("root = true\n\n[*.cs]\nindent_style = tab\n");

		const string source =
			"""
			namespace N;

			public sealed class Widget
			{
				string _label = "w";
				private string _name;

				public Widget() => _name = "w";

				public override string ToString() => _label + _name;
			}

			public struct Point
			{
				public readonly int X;

				public Point(int x) => X = x;
			}

			""";

		var sites = new[] { ("IDE0040", "_label"), ("IDE0044", "_name;"), ("IDE0250", "Point\n") };

		fs.AddFile($"{Root}/Widget.cs", new MockFileData(source));
		fs.File.SetLastWriteTimeUtc($"{Root}/Widget.cs", DateTime.UtcNow);

		// The real layout: one log under each framework's intermediate directory, found by discovery. The
		// second reports at another severity, which is still the same site.
		foreach (var (framework, level) in new[] { ("net10.0", "warning"), ("netstandard2.0", "note") })
		{
			var log = $"{Root}/obj/Debug/{framework}/curb.sarif";
			fs.AddFile(log, new MockFileData(Sarif(source, level, sites)));
			fs.File.SetLastWriteTimeUtc(log, DateTime.UtcNow.AddMinutes(1));
		}

		var exitCode = CleanupRun.Execute(fs, Root, write: true);

		exitCode.Should().Be(0);

		var cleaned = fs.File.ReadAllText($"{Root}/Widget.cs");
		cleaned.Should().Contain("private string _label").And.Contain("private readonly string _name;").And.Contain("public readonly struct Point");
		cleaned.Should().NotContain("private private").And.NotContain("readonly readonly");

		await Task.CompletedTask;
	}

	[Test]
	public async Task A_site_only_one_target_framework_reported_is_declined()
	{
		// #189: net10.0 finds the directive unnecessary, netstandard2.0 needs it. Removing it would break the
		// netstandard2.0 build, so it stays; the field both frameworks agree on is still fixed.
		var fs = Repo("root = true\n\n[*.cs]\nindent_style = tab\n");

		const string source =
			"""
			using Corpus.Compat;

			namespace N;

			public sealed class Widget
			{
				private string _name;

				public Widget() => _name = "w";

				public bool Qualified => _name.Contains(':');
			}

			""";

		fs.AddFile($"{Root}/Widget.cs", new MockFileData(source));
		fs.File.SetLastWriteTimeUtc($"{Root}/Widget.cs", DateTime.UtcNow);

		AddLog(fs, "net10.0", Sarif(source, "warning", [("IDE0005", "using Corpus.Compat;"), ("IDE0044", "_name;")]));
		AddLog(fs, "netstandard2.0", Sarif(source, "warning", [("IDE0044", "_name;")]));

		var exitCode = CleanupRun.Execute(fs, Root, write: true);

		exitCode.Should().Be(0, "a declined site is reported, not a failure");

		var cleaned = fs.File.ReadAllText($"{Root}/Widget.cs");
		cleaned.Should().StartWith("using Corpus.Compat;", "only net10.0 reported it");
		cleaned.Should().Contain("private readonly string _name;", "both frameworks reported this one");

		await Task.CompletedTask;
	}

	[Test]
	[Arguments(true)]
	[Arguments(false)]
	public async Task A_file_only_one_target_framework_compiles_keeps_its_fixes_when_the_compile_lists_say_so(bool lists)
	{
		// <Compile Remove="Modern.cs" Condition="'$(TargetFramework)' == 'netstandard2.0'" />. netstandard2.0's
		// log says nothing about Modern.cs either way; only its compile list can say it never saw the file.
		// Without one, cleanup has to assume it compiled the file and found nothing, and declines.
		var fs = Repo("root = true\n\n[*.cs]\nindent_style = tab\n");

		const string modern =
			"""
			namespace N;

			public sealed class Modern
			{
				private string _name;

				public Modern() => _name = "m";

				public override string ToString() => _name;
			}

			""";

		const string widget = "namespace N;\n\npublic sealed class Widget\n{\n}\n";

		fs.AddFile($"{Root}/Modern.cs", new MockFileData(modern));
		fs.AddFile($"{Root}/Widget.cs", new MockFileData(widget));
		fs.File.SetLastWriteTimeUtc($"{Root}/Modern.cs", DateTime.UtcNow);
		fs.File.SetLastWriteTimeUtc($"{Root}/Widget.cs", DateTime.UtcNow);

		AddLog(fs, "net10.0", Sarif(modern, "warning", [("IDE0044", "_name;")], "Modern.cs"));
		AddLog(fs, "netstandard2.0", Sarif(widget, "warning", []));

		if (lists)
		{
			fs.AddFile($"{Root}/obj/Debug/net10.0/curb.files", new MockFileData($"{Root}/Modern.cs\n{Root}/Widget.cs\n"));
			fs.AddFile($"{Root}/obj/Debug/netstandard2.0/curb.files", new MockFileData($"{Root}/Widget.cs\n"));
		}

		CleanupRun.Execute(fs, Root, write: true).Should().Be(0);

		var cleaned = fs.File.ReadAllText($"{Root}/Modern.cs");
		if (lists)
			cleaned.Should().Contain("private readonly string _name;");
		else
			cleaned.Should().Be(modern);

		await Task.CompletedTask;
	}

	[Test]
	public async Task A_console_log_declines_a_site_one_target_framework_reported()
	{
		var fs = Repo("root = true\n\n[*.cs]\nindent_style = tab\n");

		const string source =
			"""
			namespace N;

			public sealed class Widget
			{
				private string _name;
				private string _label;

				public Widget()
				{
					_name = "w";
					_label = "l";
				}

				public override string ToString() => _name + _label;
			}

			""";

		fs.AddFile($"{Root}/Widget.cs", new MockFileData(source));
		fs.File.SetLastWriteTimeUtc($"{Root}/Widget.cs", DateTime.UtcNow);

		static string Line(string at, string framework)
		{
			var text = SourceText.From(source);
			var offset = source.IndexOf(at, StringComparison.Ordinal);
			var line = text.Lines.GetLineFromPosition(offset);
			return $"{Root}/Widget.cs({line.LineNumber + 1},{offset - line.Start + 1}): warning IDE0044: Make field readonly "
				+ $"[{Root}/e1.csproj::TargetFramework={framework}]";
		}

		// _name from both frameworks, _label from net10.0 only, and each line twice the way MSBuild prints them.
		string[] lines = [Line("_name;", "net10.0"), Line("_name;", "netstandard2.0"), Line("_label;", "net10.0")];
		fs.AddFile($"{Root}/build.log", new MockFileData(string.Join('\n', lines.Concat(lines))));
		fs.File.SetLastWriteTimeUtc($"{Root}/build.log", DateTime.UtcNow.AddMinutes(1));

		CleanupRun.Execute(fs, Root, write: true, logs: [$"{Root}/build.log"]).Should().Be(0);

		var cleaned = fs.File.ReadAllText($"{Root}/Widget.cs");
		cleaned.Should().Contain("private readonly string _name;").And.Contain("private string _label;");

		await Task.CompletedTask;
	}

	private static void AddLog(MockFileSystem fs, string framework, string sarif)
	{
		var log = $"{Root}/obj/Debug/{framework}/curb.sarif";
		fs.AddFile(log, new MockFileData(sarif));
		fs.File.SetLastWriteTimeUtc(log, DateTime.UtcNow.AddMinutes(1));
	}

	/// <summary>A SARIF log reporting each rule at the first occurrence of its text in <paramref name="source"/>.</summary>
	/// <remarks>
	/// The span stops before a trailing <c>;</c>, which is how a field's name is reported, except for IDE0005,
	/// whose span is the whole directive.
	/// </remarks>
	private static string Sarif(string source, string level, IEnumerable<(string RuleId, string At)> sites, string file = "Widget.cs")
	{
		var text = SourceText.From(source);
		var results = sites.Select(site =>
		{
			var offset = source.IndexOf(site.At, StringComparison.Ordinal);
			var line = text.Lines.GetLineFromPosition(offset);
			var column = offset - line.Start + 1;
			var length = site.RuleId == "IDE0005" ? site.At.Length : site.At.TrimEnd(';', '\n').Length;

			return $$"""
				{
				  "ruleId": "{{site.RuleId}}",
				  "level": "{{level}}",
				  "message": { "text": "m" },
				  "locations": [ { "physicalLocation": {
				    "artifactLocation": { "uri": "file://{{Root}}/{{file}}" },
				    "region": { "startLine": {{line.LineNumber + 1}}, "startColumn": {{column}}, "endLine": {{line.LineNumber + 1}}, "endColumn": {{column + length}} }
				  } } ]
				}
				""";
		});

		return $$"""
			{
			  "$schema": "https://json.schemastore.org/sarif-2.1.0.json",
			  "version": "2.1.0",
			  "runs": [ { "results": [ {{string.Join(",\n", results)}} ], "columnKind": "utf16CodeUnits" } ]
			}
			""";
	}
}
