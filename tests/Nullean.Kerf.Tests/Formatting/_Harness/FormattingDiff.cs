using System.Text;
using DiffPlex.DiffBuilder;
using DiffPlex.DiffBuilder.Model;

namespace Nullean.Kerf.Tests.Formatting;

/// <summary>
/// Renders a failed formatting expectation.
/// </summary>
/// <remarks>
/// Formatting failures are almost always whitespace, so a plain string comparison message is close
/// to useless — the two sides look identical. This shows a line diff with spaces and tabs made
/// visible, and then prints the actual output already indented for pasting back into the raw string
/// literal, because hand-writing expected blocks is otherwise the dominant cost of the suite.
/// </remarks>
internal static class FormattingDiff
{
	private const string Space = "·";
	private const string Tab = "→";

	public static string Describe(string expected, string actual, string source, string? editorConfig)
	{
		var report = new StringBuilder();
		report.AppendLine("formatted output did not match the expectation.");

		if (!string.IsNullOrWhiteSpace(editorConfig))
		{
			report.AppendLine().AppendLine("--- editorconfig ---");
			foreach (var line in editorConfig.ReplaceLineEndings("\n").Split('\n'))
				report.Append("  ").AppendLine(line);
		}

		report.AppendLine().AppendLine("--- input ---");
		AppendVisible(report, source);

		report.AppendLine().AppendLine($"--- diff (- expected, + actual; {Space} space, {Tab} tab) ---");
		AppendDiff(report, expected, actual);

		report.AppendLine().AppendLine("--- actual, ready to paste as the expectation ---");
		foreach (var line in actual.ReplaceLineEndings("\n").TrimEnd('\n').Split('\n'))
			report.Append("    ").AppendLine(line);

		return report.ToString();
	}

	private static void AppendDiff(StringBuilder report, string expected, string actual)
	{
		var diff = InlineDiffBuilder.Diff(expected.ReplaceLineEndings("\n"), actual.ReplaceLineEndings("\n"));

		foreach (var line in diff.Lines)
		{
			var marker = line.Type switch
			{
				ChangeType.Inserted => "+ ",
				ChangeType.Deleted => "- ",
				ChangeType.Modified => "~ ",
				_ => "  ",
			};

			// Unchanged lines are context; only make whitespace visible where it is the subject.
			var text = line.Type == ChangeType.Unchanged ? line.Text : Visible(line.Text);
			report.Append(marker).AppendLine(text);
		}
	}

	private static void AppendVisible(StringBuilder report, string text)
	{
		foreach (var line in text.ReplaceLineEndings("\n").Split('\n'))
			report.Append("  ").AppendLine(Visible(line));
	}

	private static string Visible(string line) =>
		line.Replace(" ", Space, StringComparison.Ordinal).Replace("\t", Tab, StringComparison.Ordinal);
}
