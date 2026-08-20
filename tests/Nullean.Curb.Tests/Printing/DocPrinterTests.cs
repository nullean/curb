using AwesomeAssertions;
using Nullean.Curb;
using Nullean.Curb.Documents;
using Nullean.Curb.Printing;

namespace Nullean.Curb.Tests.Printing;

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
				using var ifBreak = arena.IfBreak(outerId);
				using (ifBreak.Branch())
					Text(arena, "abc");
				using (ifBreak.Branch())
					Text(arena, "def");
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

		// max_line_length = off is the default, and is what makes Curb a no-op on an IDE0055-clean
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

	// ---- aiming at another group's decision --------------------------------------------------------

	/// <summary>
	/// A construct laid out against whether a *different* group broke.
	/// </summary>
	/// <remarks>
	/// The question a printer cannot answer from the source: whether the thing in front of it wrapped
	/// is decided on this same run, so reading the source gives one answer on the first pass and
	/// another on the second. Both of these aim at a named group instead, which the printer has
	/// already resolved by the time it reaches them.
	/// </remarks>
	[Test]
	public async Task An_indent_can_be_aimed_at_another_groups_decision()
	{
		static string Render(int width)
		{
			var arena = new DocArena();
			var group = arena.NextGroupId();

			using (arena.Group(group))
			using (arena.Indent())
			{
				Text(arena, "012");
				arena.Line();
				Text(arena, "345");
			}

			// Indented only when the group above did not fit.
			using (arena.IndentIfBroken(group))
			{
				arena.HardLine();
				Text(arena, "abc");
			}

			DocValidator.Validate(arena, Source.Length);
			return DocLayout.Render(arena, Source, new FormatOptions
			{
				MaxLineLength = width,
				IndentSize = 2,
				EndOfLine = EndOfLine.Lf,
				InsertFinalNewLine = false,
			});
		}

		Render(80).Should().Be("012 345\nabc", "the group fit, so nothing is indented");
		Render(4).Should().Be("012\n  345\n  abc", "the group broke, so the tail moved in with it");
		await Task.CompletedTask;
	}

	[Test]
	public async Task An_ifbreak_is_measured_against_the_group_it_names()
	{
		// The half that was wrong: the print pass honoured the named group, but fit measurement did
		// not, so the group *around* an aimed IfBreak was sized against a branch it would never emit.
		static string Render(int width)
		{
			var arena = new DocArena();
			var group = arena.NextGroupId();

			using (arena.Group(group))
			{
				Text(arena, "012");
				arena.Line();
				Text(arena, "345");
			}

			using (arena.Group())
			{
				using var ifBreak = arena.IfBreak(group);
				using (ifBreak.Branch())
					Text(arena, "abc");
				using (ifBreak.Branch())
					Text(arena, "abcdefghij");
			}

			DocValidator.Validate(arena, Source.Length);
			return DocLayout.Render(arena, Source, new FormatOptions
			{
				MaxLineLength = width,
				IndentSize = 2,
				EndOfLine = EndOfLine.Lf,
				InsertFinalNewLine = false,
			});
		}

		Render(80).Should().Be("012 345abc", "the named group fit, so the flat branch prints");
		Render(4).Should().Be("012\n345abcdefghij", "the named group broke, so the broken branch does");
		await Task.CompletedTask;
	}

	[Test]
	public async Task A_line_can_be_aimed_at_another_groups_decision()
	{
		// The case IfBreak cannot serve: the choice is between a real break and nothing at all, and a
		// hard line inside the branch that is not taken would still force every enclosing group to
		// break, because propagation cannot know which branch prints.
		static string Render(int width)
		{
			var arena = new DocArena();
			var group = arena.NextGroupId();

			using (arena.Group(group))
			{
				Text(arena, "012");
				arena.Line();
				Text(arena, "345");
			}

			arena.LineIfBroken(group);
			Text(arena, "abc");

			DocValidator.Validate(arena, Source.Length);
			return DocLayout.Render(arena, Source, new FormatOptions
			{
				MaxLineLength = width,
				IndentSize = 2,
				EndOfLine = EndOfLine.Lf,
				InsertFinalNewLine = false,
			});
		}

		Render(80).Should().Be("012 345abc", "the named group fit, so the aimed line prints nothing");
		Render(4).Should().Be("012\n345\nabc", "the named group broke, so it prints a newline");
		await Task.CompletedTask;
	}

	[Test]
	public async Task An_aimed_line_does_not_break_the_group_around_it()
	{
		// The property the whole design rests on. An ordinary hard line here would force the outer
		// group broken before anything was measured; this one must not, or every construct that
		// carries one would lose its flat rendering.
		var arena = new DocArena();
		var aimed = arena.NextGroupId();

		using (arena.Group())
		{
			Text(arena, "012");
			arena.Line();
			arena.LineIfBroken(aimed);
			Text(arena, "345");
		}

		DocValidator.Validate(arena, Source.Length);
		var rendered = DocLayout.Render(arena, Source, new FormatOptions
		{
			MaxLineLength = 80,
			IndentSize = 2,
			EndOfLine = EndOfLine.Lf,
			InsertFinalNewLine = false,
		});

		rendered.Should().Be("012 345", "the outer group still had the option of fitting on one line");
		await Task.CompletedTask;
	}

	// ---- fill ---------------------------------------------------------------------------------------

	private static void CommaSeparator(DocArena arena)
	{
		arena.Synthetic(SyntheticText.Comma);
		arena.Line();
	}

	private static DocArena BuildFill(string[] items)
	{
		var arena = new DocArena();
		using (var fill = arena.Fill())
		{
			for (var i = 0; i < items.Length; i++)
			{
				using (fill.Item())
					Text(arena, items[i]);

				if (i < items.Length - 1)
					using (fill.Separator())
						CommaSeparator(arena);
			}
		}
		return arena;
	}

	[Test]
	public async Task Fill_packs_everything_flat_when_it_all_fits()
	{
		Render(BuildFill(["0", "1", "2", "3"]), width: 80).Should().Be("0, 1, 2, 3");
		await Task.CompletedTask;
	}

	[Test]
	public async Task Fill_breaks_only_where_the_next_pair_does_not_fit()
	{
		// "0, 1" (4 columns) fits in 5; the next pair, "1, 2" starting at column 3, does not, so the
		// separator after 1 breaks — its comma still prints, only the line inside it does, giving a
		// trailing comma before the break — and item 1 still prints flat because it fits alone. The
		// same reasoning repeats from the fresh line: "2, 3" fits, so the list ends up two per line.
		Render(BuildFill(["0", "1", "2", "3"]), width: 5).Should().Be("0, 1,\n2, 3");
		await Task.CompletedTask;
	}

	[Test]
	public async Task Fill_gives_every_item_its_own_line_once_no_pair_fits()
	{
		Render(BuildFill(["0", "1", "2", "3"]), width: 1).Should().Be("0,\n1,\n2,\n3");
		await Task.CompletedTask;
	}

	[Test]
	public async Task Fill_indents_the_lines_it_breaks_to()
	{
		var arena = new DocArena();
		using (arena.Indent())
		{
			arena.HardLine();
			using var fill = arena.Fill();

			using (fill.Item())
				Text(arena, "0");
			using (fill.Separator())
				CommaSeparator(arena);
			using (fill.Item())
				Text(arena, "1");
			using (fill.Separator())
				CommaSeparator(arena);
			using (fill.Item())
				Text(arena, "2");
		}

		Render(arena, width: 1).Should().Be("\n  0,\n  1,\n  2", "indent_size is 2");
		await Task.CompletedTask;
	}

	[Test]
	public async Task ForceFlat_keeps_a_fill_flat_regardless_of_width()
	{
		var arena = new DocArena();
		using (arena.ForceFlat())
		using (var fill = arena.Fill())
		{
			using (fill.Item())
				Text(arena, "0123456789");
			using (fill.Separator())
				CommaSeparator(arena);
			using (fill.Item())
				Text(arena, "abcdefghij");
		}

		Render(arena, width: 3).Should().Be("0123456789, abcdefghij");
		await Task.CompletedTask;
	}

	[Test]
	public async Task Reflow_disabled_never_breaks_a_fill()
	{
		Render(BuildFill(["0123456789", "abcdefghij"])).Should().Be("0123456789, abcdefghij");
		await Task.CompletedTask;
	}

	[Test]
	public async Task A_single_item_fill_needs_no_separator_decision()
	{
		Render(BuildFill(["012"]), width: 1).Should().Be("012");
		await Task.CompletedTask;
	}

	[Test]
	public async Task Fill_forces_a_break_when_a_separator_carries_a_hard_line()
	{
		// A trailing // comment on a list element is exactly this shape: text, then an unconditional
		// hard line the comma's own trailing trivia emits. Measuring the pair by width alone said it
		// fit, and PrintLine renders a hard line as a bare space once the scope it is in has already
		// committed to flat — so without checking for the hard line first, the fill swallowed
		// whatever printed after it into the comment. Width is generous here so only that check, not
		// the ordinary fits measurement, is what forces the break.
		var arena = new DocArena();
		using (var fill = arena.Fill())
		{
			using (fill.Item())
				Text(arena, "0");
			using (fill.Separator())
			{
				arena.Synthetic(SyntheticText.Comma);
				arena.HardLine();
			}
			using (fill.Item())
				Text(arena, "1");
		}

		Render(arena, width: 80).Should().Be("0,\n1");
		await Task.CompletedTask;
	}

	[Test]
	public async Task Reflow_disabled_still_breaks_after_a_hard_line_inside_a_fill()
	{
		// The reflow-off shortcut forces everything flat unless something inside cannot be — a hard
		// line is exactly that, and needs the same guard the width-generous case above does.
		var arena = new DocArena();
		using (var fill = arena.Fill())
		{
			using (fill.Item())
				Text(arena, "0");
			using (fill.Separator())
			{
				arena.Synthetic(SyntheticText.Comma);
				arena.HardLine();
			}
			using (fill.Item())
				Text(arena, "1");
		}

		Render(arena).Should().Be("0,\n1");
		await Task.CompletedTask;
	}
}
