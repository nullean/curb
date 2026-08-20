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
}
