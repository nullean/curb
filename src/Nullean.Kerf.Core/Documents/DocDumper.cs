using System.Text;

namespace Nullean.Kerf.Documents;

/// <summary>
/// Renders an arena as an indented S-expression.
/// </summary>
/// <remarks>
/// A <c>Doc[]</c> is opaque in a debugger, which is the main cost of the arena representation. This
/// pays that cost back, and is deliberately written before any syntax printer exists so the IR is
/// never debugged blind. Surfaced as <c>kerf doc-tree</c> and used for snapshot assertions.
/// </remarks>
internal static class DocDumper
{
	public static string Dump(DocArena arena, ReadOnlySpan<char> source)
	{
		var builder = new StringBuilder();
		Write(arena, source, builder, 0, arena.Count, 0);
		return builder.ToString();
	}

	private static void Write(DocArena arena, ReadOnlySpan<char> source, StringBuilder output, int start, int end, int depth)
	{
		var i = start;
		while (i < end)
		{
			var doc = arena[i];
			output.Append('\t', depth);

			switch (doc.Kind)
			{
				case DocKind.SrcText:
					output.Append("text ").Append(Quote(source.Slice(doc.A, doc.B)));
					if (doc.Flags.HasFlag(DocFlags.IsDirective))
						output.Append(" directive");
					output.AppendLine();
					break;

				case DocKind.SynText:
					output.Append("syn ").Append(Quote(SyntheticText.Get(doc.A))).AppendLine();
					break;

				case DocKind.ExternalText:
					output.Append("ext ").Append(Quote(arena.ExternalTexts[doc.A])).AppendLine();
					break;

				case DocKind.Line:
					output.Append(doc.LineType switch
					{
						LineType.Normal => "line",
						LineType.Soft => "softline",
						LineType.Hard => "hardline",
						LineType.Literal => "literalline",
						_ => "line?",
					});
					AppendFlags(output, doc.Flags);
					output.AppendLine();
					break;

				case DocKind.BreakParent:
					output.AppendLine("breakparent");
					break;

				case DocKind.Trim:
					output.AppendLine("trim");
					break;

				case DocKind.Null:
					output.AppendLine("null");
					break;

				case DocKind.Indent:
					output.Append("indent ")
						.Append(doc.B == Doc.IndentToRoot ? "to-root" : doc.B.ToString(System.Globalization.CultureInfo.InvariantCulture))
						.AppendLine();
					Write(arena, source, output, i + 1, i + doc.Length, depth + 1);
					break;

				case DocKind.Group:
					output.Append("group");
					if (doc.GroupId != 0)
						output.Append(" #").Append(doc.GroupId);
					output.AppendLine();
					Write(arena, source, output, i + 1, i + doc.Length, depth + 1);
					break;

				case DocKind.IfBreak:
					{
						output.Append("ifbreak");
						if (doc.GroupId != 0)
							output.Append(" #").Append(doc.GroupId);
						output.AppendLine();

						var flatStart = i + 1;
						var breakStart = flatStart + doc.A;
						output.Append('\t', depth + 1).AppendLine("flat:");
						Write(arena, source, output, flatStart, breakStart, depth + 2);
						output.Append('\t', depth + 1).AppendLine("break:");
						Write(arena, source, output, breakStart, i + doc.Length, depth + 2);
						break;
					}

				default:
					output.Append(doc.Kind.ToString().ToLowerInvariant()).AppendLine();
					Write(arena, source, output, i + 1, i + doc.Length, depth + 1);
					break;
			}

			i += doc.Length;
		}
	}

	private static void AppendFlags(StringBuilder output, DocFlags flags)
	{
		if (flags == DocFlags.None)
			return;
		output.Append(" [").Append(flags.ToString().Replace(", ", "|", StringComparison.Ordinal)).Append(']');
	}

	private static string Quote(ReadOnlySpan<char> value)
	{
		var builder = new StringBuilder(value.Length + 2);
		builder.Append('"');
		foreach (var c in value)
		{
			_ = c switch
			{
				'\n' => builder.Append("\\n"),
				'\r' => builder.Append("\\r"),
				'\t' => builder.Append("\\t"),
				'"' => builder.Append("\\\""),
				_ => builder.Append(c),
			};
		}
		builder.Append('"');
		return builder.ToString();
	}
}
