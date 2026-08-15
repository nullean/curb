using AwesomeAssertions;
using Nullean.Kerf.Documents;

namespace Nullean.Kerf.Tests.Documents;

/// <summary>
/// Structural tests for the arena itself — no C# parsing involved. These exist so the IR is proven
/// before any syntax printer is written on top of it.
/// </summary>
public class DocArenaTests
{
	private const string Source = "abcdefghij";

	[Test]
	public async Task Leaves_occupy_one_slot()
	{
		var arena = new DocArena();
		arena.SourceText(0, 3);
		arena.Line();
		arena.HardLine();

		arena.Count.Should().Be(3);
		arena[0].Length.Should().Be(1);
		arena[1].Length.Should().Be(1);
		arena[2].Length.Should().Be(1);
		await Task.CompletedTask;
	}

	[Test]
	public async Task Closing_a_scope_patches_its_subtree_length()
	{
		var arena = new DocArena();
		using (arena.Group())
		{
			arena.SourceText(0, 1);
			using (arena.Indent())
			{
				arena.SourceText(1, 1);
				arena.SourceText(2, 1);
			}
		}

		// group(1) + text(1) + indent(1) + text(1) + text(1)
		arena.Count.Should().Be(5);
		arena[0].Kind.Should().Be(DocKind.Group);
		arena[0].Length.Should().Be(5, "the group covers its whole subtree including itself");
		arena[2].Kind.Should().Be(DocKind.Indent);
		arena[2].Length.Should().Be(3);
		await Task.CompletedTask;
	}

	[Test]
	public async Task Subtrees_can_be_skipped_by_length()
	{
		var arena = new DocArena();
		using (arena.Group())
		{
			arena.SourceText(0, 1);
		}
		arena.SourceText(5, 1);

		// Walking by Length must land exactly on the sibling that follows the group.
		var next = 0 + arena[0].Length;
		next.Should().Be(2);
		arena[next].Kind.Should().Be(DocKind.SrcText);
		arena[next].A.Should().Be(5);
		await Task.CompletedTask;
	}

	[Test]
	public async Task Concat_records_its_direct_child_count()
	{
		var arena = new DocArena();
		using (arena.Concat())
		{
			arena.SourceText(0, 1);
			using (arena.Group())
			{
				arena.SourceText(1, 1);
				arena.SourceText(2, 1);
			}
			arena.SourceText(3, 1);
		}

		arena[0].Kind.Should().Be(DocKind.Concat);
		arena[0].B.Should().Be(3, "the nested group counts as one child, not three");
		await Task.CompletedTask;
	}

	[Test]
	public async Task IfBreak_records_the_length_of_its_flat_branch()
	{
		var arena = new DocArena();
		using (var ifBreak = arena.IfBreak())
		{
			using (ifBreak.Branch())
				arena.Synthetic(SyntheticText.Empty);
			using (ifBreak.Branch())
			{
				arena.Synthetic(SyntheticText.Comma);
				arena.HardLine();
			}
		}

		arena[0].Kind.Should().Be(DocKind.IfBreak);
		arena[0].A.Should().Be(2, "the flat branch is a concat wrapping one leaf");
		arena[0].Length.Should().Be(6);
		await Task.CompletedTask;
	}

	[Test]
	public async Task Reset_reuses_the_buffer()
	{
		var arena = new DocArena();
		using (arena.Group())
			arena.SourceText(0, 1);

		arena.Count.Should().Be(2);
		arena.Reset();
		arena.Count.Should().Be(0);
		await Task.CompletedTask;
	}

	[Test]
	public async Task Validator_accepts_a_well_formed_arena()
	{
		var arena = new DocArena();
		using (arena.Group())
		{
			arena.SourceText(0, 4);
			using (arena.Indent())
			{
				arena.HardLine();
				arena.SourceText(4, 4);
			}
		}

		var validate = () => DocValidator.Validate(arena, Source.Length);
		validate.Should().NotThrow();
		await Task.CompletedTask;
	}

	[Test]
	public async Task Validator_rejects_a_text_span_outside_the_source()
	{
		var arena = new DocArena();
		arena.SourceText(5, 99);

		var validate = () => DocValidator.Validate(arena, Source.Length);
		validate.Should().Throw<DocArenaCorruptException>().WithMessage("*outside the 10-char source*");
		await Task.CompletedTask;
	}
}
