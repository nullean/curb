namespace Nullean.Curb.Documents;

/// <summary>Raised when the document arena is structurally inconsistent. Always a Curb bug.</summary>
internal sealed class DocArenaCorruptException(string message) : Exception(message);

/// <summary>
/// Checks the structural invariants of an arena.
/// </summary>
/// <remarks>
/// Reserve/patch construction means a printer that forgets to close a scope, or closes one twice,
/// produces a subtly wrong subtree length rather than an obvious error. This turns that class of bug
/// into an immediate, located failure. Run under DEBUG on every format, and in every test.
/// </remarks>
internal static class DocValidator
{
	public static void Validate(DocArena arena, int sourceLength)
	{
		if (arena.Count == 0)
			return;

		ValidateRange(arena, 0, arena.Count, sourceLength);
	}

	private static void ValidateRange(DocArena arena, int start, int end, int sourceLength)
	{
		var i = start;
		while (i < end)
		{
			var doc = arena[i];

			if (doc.Length < 1)
				throw new DocArenaCorruptException($"doc {i} ({doc.Kind}) has non-positive subtree length {doc.Length}; a scope was probably closed twice");

			if (i + doc.Length > end)
				throw new DocArenaCorruptException($"doc {i} ({doc.Kind}) claims a subtree of {doc.Length} slots, overrunning its parent which ends at {end}; a scope was probably left open");

			switch (doc.Kind)
			{
				case DocKind.SrcText:
					if (doc.A < 0 || doc.B < 0 || doc.A + doc.B > sourceLength)
						throw new DocArenaCorruptException($"doc {i} references source [{doc.A}, {doc.A + doc.B}) which is outside the {sourceLength}-char source");
					if (doc.Length != 1)
						throw new DocArenaCorruptException($"doc {i} is a text leaf but claims {doc.Length} slots");
					break;

				case DocKind.SynText:
					if (doc.A < 0 || doc.A >= SyntheticText.Table.Length)
						throw new DocArenaCorruptException($"doc {i} references synthetic text {doc.A}, which is not in the table");
					break;

				case DocKind.ExternalText:
					if (doc.A < 0 || doc.A >= arena.ExternalTexts.Count)
						throw new DocArenaCorruptException($"doc {i} references external text {doc.A}, which was never supplied");
					break;

				case DocKind.Line or DocKind.BreakParent or DocKind.Trim or DocKind.Null:
					if (doc.Length != 1)
						throw new DocArenaCorruptException($"doc {i} is a leaf ({doc.Kind}) but claims {doc.Length} slots");
					break;

				case DocKind.IfBreak:
					{
						// Exactly two branches, flat first, and the stored flat length must agree with
						// the branch actually written there.
						if (doc.Length < 3)
							throw new DocArenaCorruptException($"doc {i} is an ifbreak with fewer than two branches");

						var flatLength = arena[i + 1].Length;
						if (doc.A != flatLength)
							throw new DocArenaCorruptException($"doc {i} ifbreak records a flat branch of {doc.A} slots but the branch there is {flatLength}");

						var breakStart = i + 1 + doc.A;
						if (breakStart >= i + doc.Length)
							throw new DocArenaCorruptException($"doc {i} ifbreak has no break branch");
						if (arena[breakStart].Length != doc.Length - 1 - doc.A)
							throw new DocArenaCorruptException($"doc {i} ifbreak branches do not fill its subtree");

						ValidateRange(arena, i + 1, i + doc.Length, sourceLength);
						break;
					}

				default:
					ValidateRange(arena, i + 1, i + doc.Length, sourceLength);
					break;
			}

			i += doc.Length;
		}

		if (i != end)
			throw new DocArenaCorruptException($"subtree ending at {end} was overrun; last node finished at {i}");
	}
}
