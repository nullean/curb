using Nullean.Curb.Documents;

namespace Nullean.Curb.Printing;

/// <summary>Convenience entry point for laying a document out, used by tests and the doc-tree command.</summary>
internal static class DocLayout
{
	public static string Render<TLanguage>(DocArena arena, string source, in LayoutOptions options)
		where TLanguage : struct, ILayoutLanguage
	{
		using var output = new OutputBuffer(source.Length + 64);
		new DocPrinter<TLanguage>().Print(arena, source.AsMemory(), options, output);
		return output.ToString();
	}
}
