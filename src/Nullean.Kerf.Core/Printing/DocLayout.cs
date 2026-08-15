using Nullean.Kerf.Documents;

namespace Nullean.Kerf.Printing;

/// <summary>Convenience entry point for laying a document out, used by tests and the doc-tree command.</summary>
internal static class DocLayout
{
	public static string Render(DocArena arena, string source, FormatOptions options)
	{
		using var output = new OutputBuffer(source.Length + 64);
		new DocPrinter().Print(arena, source.AsMemory(), options, output);
		return output.ToString();
	}
}
