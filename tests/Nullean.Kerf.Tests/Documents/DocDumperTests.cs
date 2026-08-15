using AwesomeAssertions;
using Nullean.Kerf.Documents;

namespace Nullean.Kerf.Tests.Documents;

public class DocDumperTests
{
	private const string Source = "if (a) { b(); }";

	[Test]
	public async Task Renders_the_arena_as_an_indented_s_expression()
	{
		var arena = new DocArena();
		var groupId = arena.NextGroupId();

		using (arena.Group(groupId))
		{
			arena.SourceText(0, 2);           // "if"
			arena.Synthetic(SyntheticText.Space);
			using (arena.Indent())
			{
				arena.SoftLine();
				arena.SourceText(4, 1);       // "a"
			}
			using (var ifBreak = arena.IfBreak(groupId))
			{
				using (ifBreak.Branch())
					arena.Synthetic(SyntheticText.Empty);
				using (ifBreak.Branch())
					arena.Synthetic(SyntheticText.Comma);
			}
		}

		var dump = DocDumper.Dump(arena, Source);

		await Assert.That(dump).IsEqualTo(
			"""
			group #1
				text "if"
				syn " "
				indent 1
					softline
					text "a"
				ifbreak #1
					flat:
						concat
							syn ""
					break:
						concat
							syn ","

			""".ReplaceLineEndings("\n"));
	}

	[Test]
	public async Task Escapes_newlines_and_tabs_in_source_text()
	{
		const string source = "a\n\tb";
		var arena = new DocArena();
		arena.SourceText(0, source.Length);

		DocDumper.Dump(arena, source).Should().Contain(@"text ""a\n\tb""");
		await Task.CompletedTask;
	}

	[Test]
	public async Task Marks_line_flags()
	{
		var arena = new DocArena();
		arena.HardLine(DocFlags.NoTrim);

		DocDumper.Dump(arena, Source).Should().Contain("hardline [NoTrim]");
		await Task.CompletedTask;
	}
}
