using System.Text.Json;
using Microsoft.CodeAnalysis.Text;

namespace Nullean.Kerf.Cleanup;

/// <summary>
/// Reads the diagnostics out of a SARIF 2.1 log written by the compiler's <c>/errorlog</c>.
/// </summary>
/// <remarks>
/// <para>
/// The primary input format, because it is the only one that carries a diagnostic's full span. See
/// <see cref="CleanupDiagnostic.End"/> for why that decides IDE0005.
/// </para>
/// <para>
/// Read with <see cref="Utf8JsonReader"/> and no model type on purpose. A log is around 220 KB per
/// project per target framework even for a trivial project, and almost all of that is the
/// <c>tool.driver.rules</c> metadata for every analyser rule the build loaded — which
/// deserialising into objects would materialise in full to reach the handful of results in front of
/// it. Skipping it costs nothing, and there is no reflection, so this links under native AOT.
/// </para>
/// <para>
/// Everything not needed is skipped rather than validated. A log Kerf cannot understand yields no
/// diagnostics and therefore no fixes, which is the safe direction.
/// </para>
/// </remarks>
public static class SarifReader
{
	/// <summary>True when the bytes look like JSON, so the caller can pick a reader without parsing twice.</summary>
	public static bool LooksLikeSarif(ReadOnlySpan<byte> utf8)
	{
		foreach (var b in utf8)
		{
			if (b is (byte)' ' or (byte)'\t' or (byte)'\r' or (byte)'\n' or 0xEF or 0xBB or 0xBF)
				continue;

			return b == (byte)'{';
		}

		return false;
	}

	/// <summary>Appends every diagnostic in the log to <paramref name="into"/>.</summary>
	/// <returns>False when the bytes are not readable as SARIF, with <paramref name="failure"/> saying why.</returns>
	public static bool TryRead(ReadOnlySpan<byte> utf8, ICollection<CleanupDiagnostic> into, out string? failure)
	{
		failure = null;

		try
		{
			var reader = new Utf8JsonReader(utf8, new JsonReaderOptions { CommentHandling = JsonCommentHandling.Skip });

			if (!reader.Read() || reader.TokenType != JsonTokenType.StartObject)
			{
				failure = "the log does not start with a JSON object";
				return false;
			}

			while (reader.Read() && reader.TokenType == JsonTokenType.PropertyName)
			{
				if (!reader.ValueTextEquals("runs"))
				{
					SkipValue(ref reader);
					continue;
				}

				reader.Read();
				if (reader.TokenType != JsonTokenType.StartArray)
				{
					reader.Skip();
					continue;
				}

				while (reader.Read() && reader.TokenType == JsonTokenType.StartObject)
					ReadRun(ref reader, into);
			}

			// The root object has closed. Anything after it means this is not one log, and reading only
			// the first document would quietly drop the rest — so it is refused with the cause named.
			if (reader.Read())
			{
				failure = "the log holds more than one JSON document, which is what a single ErrorLog path "
					+ "shared by the inner builds of a multi-targeting project produces. Give each target "
					+ "framework its own path.";
				return false;
			}

			return true;
		}
		catch (JsonException exception)
		{
			// A truncated log, or two logs concatenated — which is what a single ErrorLog path shared by
			// the inner builds of a multi-targeting project produces.
			failure = exception.Message;
			return false;
		}
	}

	private static void ReadRun(ref Utf8JsonReader reader, ICollection<CleanupDiagnostic> into)
	{
		while (reader.Read() && reader.TokenType == JsonTokenType.PropertyName)
		{
			if (!reader.ValueTextEquals("results"))
			{
				SkipValue(ref reader);
				continue;
			}

			reader.Read();
			if (reader.TokenType != JsonTokenType.StartArray)
			{
				reader.Skip();
				continue;
			}

			while (reader.Read() && reader.TokenType == JsonTokenType.StartObject)
				ReadResult(ref reader, into);
		}
	}

	private static void ReadResult(ref Utf8JsonReader reader, ICollection<CleanupDiagnostic> into)
	{
		string? ruleId = null;
		string? path = null;
		var start = default(LinePosition);
		LinePosition? end = null;
		var located = false;

		while (reader.Read() && reader.TokenType == JsonTokenType.PropertyName)
		{
			if (reader.ValueTextEquals("ruleId"))
			{
				reader.Read();
				ruleId = reader.TokenType == JsonTokenType.String ? reader.GetString() : null;
				continue;
			}

			if (!reader.ValueTextEquals("locations"))
			{
				SkipValue(ref reader);
				continue;
			}

			reader.Read();
			if (reader.TokenType != JsonTokenType.StartArray)
			{
				reader.Skip();
				continue;
			}

			// The first physical location is the diagnostic's own; anything after it is a related site,
			// which is not what a fix is aimed at.
			while (reader.Read() && reader.TokenType == JsonTokenType.StartObject)
			{
				if (located)
					reader.Skip();
				else
					located = ReadLocation(ref reader, ref path, ref start, ref end);
			}
		}

		if (ruleId is not null && located && path is not null)
			into.Add(new CleanupDiagnostic(ruleId, path, start, end));
	}

	private static bool ReadLocation(ref Utf8JsonReader reader, ref string? path, ref LinePosition start, ref LinePosition? end)
	{
		var located = false;

		while (reader.Read() && reader.TokenType == JsonTokenType.PropertyName)
		{
			if (!reader.ValueTextEquals("physicalLocation"))
			{
				SkipValue(ref reader);
				continue;
			}

			reader.Read();
			if (reader.TokenType != JsonTokenType.StartObject)
			{
				reader.Skip();
				continue;
			}

			located = ReadPhysicalLocation(ref reader, ref path, ref start, ref end);
		}

		return located;
	}

	private static bool ReadPhysicalLocation(ref Utf8JsonReader reader, ref string? path, ref LinePosition start, ref LinePosition? end)
	{
		var startLine = 0;
		var startColumn = 1;
		var endLine = 0;
		var endColumn = 0;

		while (reader.Read() && reader.TokenType == JsonTokenType.PropertyName)
		{
			if (reader.ValueTextEquals("artifactLocation"))
			{
				reader.Read();
				if (reader.TokenType == JsonTokenType.StartObject)
					path = ReadUri(ref reader);
				else
					reader.Skip();

				continue;
			}

			if (!reader.ValueTextEquals("region"))
			{
				SkipValue(ref reader);
				continue;
			}

			reader.Read();
			if (reader.TokenType != JsonTokenType.StartObject)
			{
				reader.Skip();
				continue;
			}

			while (reader.Read() && reader.TokenType == JsonTokenType.PropertyName)
			{
				if (reader.ValueTextEquals("startLine"))
					startLine = ReadInt(ref reader);
				else if (reader.ValueTextEquals("startColumn"))
					startColumn = ReadInt(ref reader);
				else if (reader.ValueTextEquals("endLine"))
					endLine = ReadInt(ref reader);
				else if (reader.ValueTextEquals("endColumn"))
					endColumn = ReadInt(ref reader);
				else
					SkipValue(ref reader);
			}
		}

		if (path is null || startLine < 1)
			return false;

		// SARIF lines and columns are one-based and its end column is exclusive, which is exactly
		// TextSpan's own convention once both are shifted down by one.
		start = new LinePosition(startLine - 1, Math.Max(0, startColumn - 1));
		end = endLine >= 1 && endColumn >= 1 ? new LinePosition(endLine - 1, endColumn - 1) : null;
		return true;
	}

	private static string? ReadUri(ref Utf8JsonReader reader)
	{
		string? uri = null;

		while (reader.Read() && reader.TokenType == JsonTokenType.PropertyName)
		{
			if (reader.ValueTextEquals("uri"))
			{
				reader.Read();
				uri = reader.TokenType == JsonTokenType.String ? reader.GetString() : null;
				continue;
			}

			SkipValue(ref reader);
		}

		if (uri is null)
			return null;

		// The compiler writes an absolute file: URI. Anything else — a relative uri needing a
		// uriBaseId, or a different scheme — is refused rather than guessed at.
		return Uri.TryCreate(uri, UriKind.Absolute, out var parsed) && parsed.IsFile ? parsed.LocalPath : null;
	}

	private static int ReadInt(ref Utf8JsonReader reader)
	{
		reader.Read();
		return reader.TokenType == JsonTokenType.Number && reader.TryGetInt32(out var value) ? value : 0;
	}

	/// <summary>Steps over the value of the property the reader is sitting on.</summary>
	private static void SkipValue(ref Utf8JsonReader reader)
	{
		reader.Read();
		reader.Skip();
	}
}
