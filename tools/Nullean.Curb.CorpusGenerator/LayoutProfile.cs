namespace Nullean.Curb.CorpusGenerator;

/// <summary>
/// How untidy one file is. Drawn per file from its own generator, so a corpus holds the spread a real repository
/// does: most files close to some house style, a fair share carelessly edited, and a few that look pasted in.
/// </summary>
public sealed record LayoutProfile
{
	/// <summary>One level of indentation as the author typed it: a tab, or two to eight spaces.</summary>
	public string IndentUnit { get; init; } = "\t";

	/// <summary>Chance a line indented with spaces uses tabs instead, as in a file two editors have touched.</summary>
	public double MixTabs { get; init; }

	/// <summary>Chance a line's indentation is off by one to three spaces.</summary>
	public double Jitter { get; init; }

	/// <summary>Chance a <c>{</c> on its own line is pulled up onto the previous one.</summary>
	public double BraceJoin { get; init; }

	/// <summary>Chance a <c>{</c> at the end of a line is pushed onto its own.</summary>
	public double BraceBreak { get; init; }

	/// <summary>Chance a space that is not needed to keep two tokens apart is dropped.</summary>
	public double SpaceDrop { get; init; }

	/// <summary>Chance a space is added where there was none.</summary>
	public double SpaceAdd { get; init; }

	/// <summary>Chance a space is doubled.</summary>
	public double SpaceDouble { get; init; }

	/// <summary>Chance an extra blank line appears between statements or members.</summary>
	public double BlankAdd { get; init; }

	/// <summary>Chance a blank line is removed.</summary>
	public double BlankDrop { get; init; }

	/// <summary>Chance a line break inside a statement is joined, which is what produces long lines.</summary>
	public double Join { get; init; }

	/// <summary>Chance a line is broken after an open parenthesis or comma, before a dot or an operator.</summary>
	public double Break { get; init; }

	/// <summary>Chance a line ends with a trailing <c>//</c> comment.</summary>
	public double TrailingComment { get; init; }

	/// <summary>Chance a statement or member gets a comment line of its own above it.</summary>
	public double LeadingComment { get; init; }

	/// <summary>Chance a <c>/* */</c> comment lands between two tokens on a line.</summary>
	public double BlockComment { get; init; }

	/// <summary>Chance a line keeps trailing whitespace.</summary>
	public double TrailingWhitespace { get; init; }

	public bool Crlf { get; init; }

	/// <summary>A CRLF file with the odd LF line in it, the way a file edited on two machines ends up.</summary>
	public bool MixedLineEndings { get; init; }

	public bool Bom { get; init; }

	/// <summary>Chance the file ends without a final newline.</summary>
	public bool NoFinalNewline { get; init; }

	/// <summary>No layout change at all apart from the indentation unit and the encoding: the control group.</summary>
	public static LayoutProfile Quiet(Rng r) => new() { IndentUnit = r.Pick("\t", "    ", "    ", "  "), Crlf = r.Chance(0.15), Bom = r.Chance(0.1), };

	/// <summary>Spaces, tabs, braces and blank lines, but no structural edits and no comments.</summary>
	public static LayoutProfile WithoutStructure(LayoutProfile p) => p with { Join = 0, Break = 0, TrailingComment = 0, LeadingComment = 0, BlockComment = 0, };

	/// <summary>Draws a profile. Roughly a quarter of files are nearly tidy, half moderately untidy, the rest messy.</summary>
	public static LayoutProfile Draw(Rng r)
	{
		var tier = r.NextDouble();
		var indent = r.Pick("\t", "\t", "    ", "    ", "    ", "  ", "   ", "        ");
		var crlf = r.Chance(0.2);
		var common = new LayoutProfile
		{
			IndentUnit = indent,
			Crlf = crlf,
			MixedLineEndings = crlf && r.Chance(0.1),
			Bom = r.Chance(0.15),
			NoFinalNewline = r.Chance(0.1),
		};

		if (tier < 0.25)
		{
			return common with
			{
				Jitter = r.Chance(0.5) ? 0.01 : 0,
				BraceJoin = r.Chance(0.2) ? 0.05 : 0,
				SpaceDrop = 0.005,
				SpaceAdd = 0.002,
				BlankAdd = 0.02,
				BlankDrop = 0.02,
				TrailingWhitespace = r.Chance(0.3) ? 0.02 : 0,
			};
		}

		if (tier < 0.75)
		{
			return common with
			{
				MixTabs = indent == "\t" ? 0 : r.Chance(0.3) ? 0.1 : 0,
				Jitter = 0.04,
				BraceJoin = r.Chance(0.4) ? 0.3 : 0.03,
				BraceBreak = 0.05,
				SpaceDrop = 0.04,
				SpaceAdd = 0.02,
				SpaceDouble = 0.01,
				BlankAdd = 0.06,
				BlankDrop = 0.15,
				Join = 0.05,
				Break = 0.04,
				TrailingComment = 0.1,
				LeadingComment = 0.03,
				BlockComment = 0.002,
				TrailingWhitespace = 0.05,
			};
		}

		return common with
		{
			MixTabs = indent == "\t" ? 0 : 0.25,
			Jitter = 0.12,
			BraceJoin = r.Chance(0.5) ? 0.7 : 0.15,
			BraceBreak = 0.15,
			SpaceDrop = 0.12,
			SpaceAdd = 0.06,
			SpaceDouble = 0.04,
			BlankAdd = 0.12,
			BlankDrop = 0.3,
			Join = 0.15,
			Break = 0.12,
			TrailingComment = 0.25,
			LeadingComment = 0.05,
			BlockComment = 0.008,
			TrailingWhitespace = 0.12,
		};
	}
}
