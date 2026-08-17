using Microsoft.CodeAnalysis.Text;

namespace Nullean.Kerf.Cleanup;

/// <summary>
/// Reads code style diagnostics out of MSBuild's console output.
/// </summary>
/// <remarks>
/// <para>
/// The secondary format, for a saved build log. Measured shape, on SDK 10.0.400:
/// </para>
/// <code>
/// /repo/Widget.cs(8,17): warning IDE0044: Make field readonly (https://…/ide0044) [/repo/e1.csproj::TargetFramework=net10.0]
/// </code>
/// <para>
/// Paths are absolute, the line and column are one-based, and a tab counts as one column — so the
/// position converts the same way a SARIF one does. What is missing is the end: MSBuild reports a
/// start only, which is why this reader cannot serve IDE0005 and why SARIF is primary.
/// </para>
/// <para>
/// The rule id is what this matches on, not the severity word. <c>warning</c> and <c>error</c> are
/// localised by the SDK's display language; an id is not.
/// </para>
/// </remarks>
public static class BuildLogReader
{
	/// <summary>Appends every code style diagnostic found in the log to <paramref name="into"/>.</summary>
	public static void Read(ReadOnlySpan<char> log, ICollection<CleanupDiagnostic> into)
	{
		foreach (var line in log.EnumerateLines())
		{
			if (TryReadLine(line, out var diagnostic))
				into.Add(diagnostic);
		}
	}

	internal static bool TryReadLine(ReadOnlySpan<char> line, out CleanupDiagnostic diagnostic)
	{
		diagnostic = default;

		if (!TryFindRuleId(line, out var idStart, out var idLength))
			return false;

		var id = line.Slice(idStart, idLength);

		// Everything before the id is `<path>(<position>): <severity> `. The position is the last
		// parenthesised group in it, so the path may contain parentheses of its own.
		var head = line[..idStart];
		var close = head.LastIndexOf(')');
		if (close < 0)
			return false;

		var open = head[..close].LastIndexOf('(');
		if (open < 0)
			return false;

		if (!TryReadPosition(head.Slice(open + 1, close - open - 1), out var start, out var end))
			return false;

		var path = head[..open].Trim();
		if (path.IsEmpty)
			return false;

		diagnostic = new CleanupDiagnostic(id.ToString(), path.ToString(), start, end);
		return true;
	}

	/// <summary>Finds an <c>IDEnnnn</c> immediately followed by a colon, which is the diagnostic's id.</summary>
	private static bool TryFindRuleId(ReadOnlySpan<char> line, out int start, out int length)
	{
		start = 0;
		length = 0;

		const int idLength = 7;
		var offset = 0;

		while (true)
		{
			var found = line[offset..].IndexOf("IDE", StringComparison.Ordinal);
			if (found < 0)
				return false;

			var at = offset + found;
			offset = at + 3;

			// `IDEnnnn:` — four digits and the colon that separates the id from the message.
			if (at + idLength >= line.Length || line[at + idLength] != ':')
				continue;

			var digits = true;
			for (var i = at + 3; i < at + idLength; i++)
			{
				if (char.IsAsciiDigit(line[i]))
					continue;

				digits = false;
				break;
			}

			if (!digits)
				continue;

			start = at;
			length = idLength;
			return true;
		}
	}

	/// <summary>
	/// Reads <c>line,column</c> or <c>line,column,endLine,endColumn</c>. MSBuild's canonical diagnostic
	/// format allows both; the compiler emits the first.
	/// </summary>
	private static bool TryReadPosition(ReadOnlySpan<char> text, out LinePosition start, out LinePosition? end)
	{
		start = default;
		end = null;

		Span<int> parts = stackalloc int[4];
		var count = 0;

		foreach (var range in text.Split(','))
		{
			if (count == parts.Length)
				return false;

			if (!int.TryParse(text[range].Trim(), out var value) || value < 1)
				return false;

			parts[count++] = value;
		}

		if (count is not (2 or 4))
			return false;

		start = new LinePosition(parts[0] - 1, parts[1] - 1);
		if (count == 4)
			end = new LinePosition(parts[2] - 1, parts[3] - 1);

		return true;
	}
}
