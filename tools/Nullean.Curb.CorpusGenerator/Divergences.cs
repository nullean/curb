using System.Text.Json;

namespace Nullean.Curb.CorpusGenerator;

/// <summary>Reads the test cases build/conformance-divergences.json documents as not a fixed point of dotnet format.</summary>
public static class Divergences
{
	public static IReadOnlySet<string> Read(string path)
	{
		using var document = JsonDocument.Parse(File.ReadAllText(path));
		return document
			.RootElement
			.GetProperty("divergences")
			.EnumerateArray()
			.Where(e => e.GetProperty("tool").GetString() == "dotnet-format-whitespace")
			.Select(e => e.GetProperty("exampleCase").GetString()!)
			.ToHashSet(StringComparer.Ordinal);
	}
}
