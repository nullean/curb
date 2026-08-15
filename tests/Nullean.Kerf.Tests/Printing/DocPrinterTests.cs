using AwesomeAssertions;
using Nullean.Kerf;
using Nullean.Kerf.Documents;
using Nullean.Kerf.Printing;

namespace Nullean.Kerf.Tests.Printing;

/// <summary>
/// The layout algorithm, exercised directly against the arena with no C# parsing in the way.
/// Getting group/fits/ifbreak semantics right here is a precondition for writing any syntax printer.
/// </summary>
public class DocPrinterTests
{
	/// <summary>Source the tests slice text leaves out of. Offsets below index into this.</summary>
	private const string Source = "0123456789abcdefghijklmnopqrstuvwxyz";

	private static FormatOptions Options(int width = FormatOptions.Off) => new()
	{
		MaxLineLength = width,
		IndentSize = 2,
		EndOfLine = EndOfLine.Lf,
		InsertFinalNewLine = false,
	};

	private static string Render(DocArena arena, int width = FormatOptions.Off)
	{
		DocValidator.Validate(arena, Source.Length);
		return DocLayout.Render(arena, Source, Options(width));
	}

	/// <summary>Appends the literal <paramref name="text"/> by finding it in <see cref="Source"/>.</summary>
	private static void Text(DocArena arena, string text)
	{
		var offset = Source.IndexOf(text, StringComparison.Ordinal);
		offset.Should().BeGreaterThanOrEqualTo(0, "test text must exist in the shared source");
		arena.SourceText(offset, text.Length);
	}

	[Test]
	public async Task Emits_source_spans_verbatim()
	{
		var arena = new DocArena();
		Text(arena, "012");
		Text(arena, "abc");

		Render(arena).Should().Be("012abc");
		await Task.CompletedTask;
	}

	[Test]
	public async Task A_group_that_fits_prints_flat()
	{
		var arena = new DocArena();
		using (arena.Group())
		{
			Text(arena, "012");
			arena.Line();
			Text(arena, "abc");
		}

		Render(arena, width: 80).Should().Be("012 abc", "a normal line is a space when flat");
		await Task.CompletedTask;
	}

	[Test]
	public async Task A_group_that_does_not_fit_breaks()
	{
		var arena = new DocArena();
		using (arena.Group())
		{
			Text(arena, "012");
			arena.Line();
			Text(arena, "abc");
		}

		Render(arena, width: 5).Should().Be("012\nabc");
		await Task.CompletedTask;
	}

	[Test]
	public async Task Soft_lines_vanish_when_flat_and_break_when_not()
	{
		var arena = new DocArena();
		using (arena.Group())
		{
			Text(arena, "012");
			arena.SoftLine();
			Text(arena, "abc");
		}

		Render(arena, width: 80).Should().Be("012abc");

		var broken = new DocArena();
		using (broken.Group())
		{
			Text(broken, "012");
			broken.SoftLine();
			Text(broken, "abc");
		}

		Render(broken, width: 4).Should().Be("012\nabc");
		await Task.CompletedTask;
	}

	[Test]
	public async Task A_hard_line_forces_every_enclosing_group_to_break()
	{
		var arena = new DocArena();
		using (arena.Group())
		{
			Text(arena, "012");
			arena.Line();
			using (arena.Group())
			{
				Text(arena, "abc");
				arena.HardLine();
				Text(arena, "def");
			}
		}

		// Width is ample, so only the hard line can be responsible for either break.
		Render(arena, width: 200).Should().Be("012\nabc\ndef");
		await Task.CompletedTask;
	}

	[Test]
	public async Task Indent_applies_to_broken_lines_inside_it()
	{
		var arena = new DocArena();
		using (arena.Group())
		{
			Text(arena, "012");
			using (arena.Indent())
			{
				arena.HardLine();
				Text(arena, "abc");
			}
			arena.HardLine();
			Text(arena, "def");
		}

		Render(arena).Should().Be("012\n  abc\ndef", "indent_size is 2 and the dedent is restored after the scope");
		await Task.CompletedTask;
	}

	[Test]
	public async Task Indent_accepts_a_negative_delta()
	{
		var arena = new DocArena();
		using (arena.Indent(2))
		{
			arena.HardLine();
			Text(arena, "012");
			using (arena.Indent(-1))
			{
				arena.HardLine();
				Text(arena, "abc");
			}
		}

		// Signed deltas are what csharp_indent_braces and csharp_indent_labels need.
		Render(arena).Should().Be("\n    012\n  abc");
		await Task.CompletedTask;
	}

	[Test]
	public async Task Indent_to_root_returns_to_column_zero()
	{
		var arena = new DocArena();
		using (arena.Indent(3))
		{
			arena.HardLine();
			Text(arena, "012");
			using (arena.IndentToRoot())
			{
				arena.HardLine();
				Text(arena, "abc");
			}
		}

		Render(arena).Should().Be("\n      012\nabc", "csharp_indent_labels = flush_left needs column 0");
		await Task.CompletedTask;
	}

	[Test]
	public async Task IfBreak_follows_the_enclosing_group()
	{
		static DocArena Build()
		{
			var arena = new DocArena();
			using (arena.Group())
			{
				Text(arena, "012");
				using (var ifBreak = arena.IfBreak())
				{
					using (ifBreak.Branch())
						arena.Synthetic(SyntheticText.Empty);
					using (ifBreak.Branch())
						arena.Synthetic(SyntheticText.Comma);
				}
				arena.SoftLine();
				Text(arena, "abc");
			}
			return arena;
		}

		Render(Build(), width: 80).Should().Be("012abc", "flat takes the empty branch");
		Render(Build(), width: 4).Should().Be("012,\nabc", "broken takes the comma branch — a trailing comma");
		await Task.CompletedTask;
	}

	[Test]
	public async Task IfBreak_can_target_a_named_group()
	{
		var arena = new DocArena();
		var outerId = arena.NextGroupId();

		using (arena.Group(outerId))
		{
			Text(arena, "012");
			arena.HardLine();

			// This inner group fits comfortably, so without the group id the ifbreak would take the
			// flat branch. Targeting the outer, broken group must override that.
			using (arena.Group())
			{
				using (var ifBreak = arena.IfBreak(outerId))
				{
					using (ifBreak.Branch())
						Text(arena, "abc");
					using (ifBreak.Branch())
						Text(arena, "def");
				}
			}
		}

		Render(arena, width: 200).Should().Be("012\ndef");
		await Task.CompletedTask;
	}

	[Test]
	public async Task ForceFlat_keeps_a_hard_line_flat()
	{
		var arena = new DocArena();
		using (arena.Group())
		{
			using (arena.ForceFlat())
			{
				Text(arena, "012");
				arena.HardLine();
				Text(arena, "abc");
			}
		}

		// This is the mechanism behind csharp_preserve_single_line_blocks.
		Render(arena, width: 3).Should().Be("012 abc");
		await Task.CompletedTask;
	}

	[Test]
	public async Task A_literal_line_breaks_even_inside_ForceFlat()
	{
		var arena = new DocArena();
		using (arena.Indent(2))
		using (arena.ForceFlat())
		{
			Text(arena, "012");
			arena.LiteralLine();
			Text(arena, "abc");
		}

		// Raw string content must not be re-indented, and its newlines are content, not layout.
		Render(arena).Should().Be("012\nabc");
		await Task.CompletedTask;
	}

	[Test]
	public async Task ConditionalGroup_takes_the_first_option_that_fits()
	{
		static DocArena Build()
		{
			var arena = new DocArena();
			using (arena.ConditionalGroup())
			{
				using (arena.Concat())
					Text(arena, "0123456789");
				using (arena.Concat())
					Text(arena, "abc");
			}
			return arena;
		}

		Render(Build(), width: 80).Should().Be("0123456789", "the first option fits");
		Render(Build(), width: 5).Should().Be("abc", "it does not, so the next is tried");
		await Task.CompletedTask;
	}

	[Test]
	public async Task ConditionalGroup_falls_back_to_the_last_option()
	{
		var arena = new DocArena();
		using (arena.ConditionalGroup())
		{
			using (arena.Concat())
				Text(arena, "0123456789");
			using (arena.Concat())
				Text(arena, "abcdef");
		}

		Render(arena, width: 2).Should().Be("abcdef", "nothing fits, so the most expanded option wins");
		await Task.CompletedTask;
	}

	[Test]
	public async Task Trailing_whitespace_is_trimmed_at_a_break()
	{
		var arena = new DocArena();
		Text(arena, "012");
		arena.Synthetic(SyntheticText.Space);
		arena.HardLine();
		Text(arena, "abc");

		Render(arena).Should().Be("012\nabc");
		await Task.CompletedTask;
	}

	[Test]
	public async Task Trim_removes_whitespace_already_written()
	{
		var arena = new DocArena();
		Text(arena, "012");
		arena.Synthetic(SyntheticText.Space);
		arena.Trim();
		Text(arena, "abc");

		Render(arena).Should().Be("012abc");
		await Task.CompletedTask;
	}

	[Test]
	public async Task AlwaysFits_content_is_emitted_but_not_measured()
	{
		var arena = new DocArena();
		using (arena.Group())
		{
			using (arena.AlwaysFits())
				Text(arena, "0123456789");
			arena.Line();
			Text(arena, "abc");
		}

		// Measured, the group is 14 columns and would break at width 8. The suppressed run must not
		// count, which is how directive trivia avoids pushing real code into wrapping.
		Render(arena, width: 8).Should().Be("0123456789 abc");
		await Task.CompletedTask;
	}

	[Test]
	public async Task Lookahead_past_a_group_influences_whether_it_breaks()
	{
		static DocArena Build()
		{
			var arena = new DocArena();
			using (arena.Group())
			{
				using (arena.Group())
				{
					Text(arena, "012");
					arena.Line();
					Text(arena, "abc");
				}
				Text(arena, "defghijkl");
			}
			return arena;
		}

		// The inner group is 7 columns and fits in 10 on its own, but the 9 columns trailing it do
		// not. Measuring the group in isolation would wrap in the wrong place.
		Render(Build(), width: 10).Should().Be("012\nabcdefghijkl");
		Render(Build(), width: 40).Should().Be("012 abcdefghijkl");
		await Task.CompletedTask;
	}

	[Test]
	public async Task Reflow_disabled_never_breaks_a_soft_group()
	{
		var arena = new DocArena();
		using (arena.Group())
		{
			Text(arena, "0123456789");
			arena.Line();
			Text(arena, "abcdefghij");
		}

		// max_line_length = off is the default, and is what makes Kerf a no-op on an IDE0055-clean
		// repository rather than a reformatting event.
		Render(arena).Should().Be("0123456789 abcdefghij");
		await Task.CompletedTask;
	}

	[Test]
	public async Task Final_newline_is_inserted_when_requested()
	{
		var arena = new DocArena();
		Text(arena, "012");

		var options = Options() with { InsertFinalNewLine = true };
		DocLayout.Render(arena, Source, options).Should().Be("012\n");
		await Task.CompletedTask;
	}

	[Test]
	public async Task Crlf_is_honoured()
	{
		var arena = new DocArena();
		Text(arena, "012");
		arena.HardLine();
		Text(arena, "abc");

		var options = Options() with { EndOfLine = EndOfLine.CrLf };
		DocLayout.Render(arena, Source, options).Should().Be("012\r\nabc");
		await Task.CompletedTask;
	}
}
