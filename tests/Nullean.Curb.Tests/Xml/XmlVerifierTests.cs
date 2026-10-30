using AwesomeAssertions;
using Nullean.Curb.Xml;

namespace Nullean.Curb.Tests.Xml;

/// <summary>
/// The verifier is the net under the printer, so it is tested on its own against outputs the printer
/// would never write: each must be caught, and each layout-only change must not be.
/// </summary>
public class XmlVerifierTests
{
	private const string Source = "<Project>\n  <PropertyGroup>\n    <A> x </A>\n  </PropertyGroup>\n  <!-- c --><B  x=\"1\"   y='2'/>\n</Project>\n";

	[Test]
	[Arguments("<Project>\n<PropertyGroup>\n<A> x </A>\n</PropertyGroup>\n<!-- c --><B x=\"1\" y='2' />\n</Project>\n")]
	[Arguments("<Project> <PropertyGroup> <A> x </A> </PropertyGroup> <!-- c --><B\n x=\"1\"\n y='2'\n/> </Project>")]
	[Arguments("<Project>\r\n  <PropertyGroup>\r\n    <A> x </A>\r\n  </PropertyGroup>\r\n  <!-- c --><B x=\"1\" y='2'/>\r\n</Project>\r\n")]
	public async Task Layout_changes_pass(string output) =>
		await Assert(output, pass: true);

	[Test]
	[Arguments("<Project>\n  <PropertyGroup>\n    <A>x</A>\n  </PropertyGroup>\n  <!-- c --><B x=\"1\" y='2'/>\n</Project>\n")]
	[Arguments("<Project>\n  <PropertyGroup>\n    <A> x </A>\n  </PropertyGroup>\n  <!-- c --><B x=\"1\" y=\"2\"/>\n</Project>\n")]
	[Arguments("<Project>\n  <PropertyGroup>\n    <A> x </A>\n  </PropertyGroup>\n  <B x=\"1\" y='2'/>\n</Project>\n")]
	[Arguments("<Project>\n  <PropertyGroup>\n    <A> x </A>\n  </PropertyGroup>\n  <!-- c --> <B x=\"1\" y='2'/>\n</Project>\n")]
	[Arguments("<Project>\n  <PropertyGroup>\n    <A> x </A>\n  </PropertyGroup>\n  <!-- c --><B y='2' x=\"1\"/>\n</Project>\n")]
	[Arguments("<Project>\n  <PropertyGroup>\n    <A> x </A>\n  </PropertyGroup>\n  <!-- c --><B x=\"1\" y='2'></B>\n</Project>\n")]
	[Arguments("<Project>\n  <PropertyGroup>\n    <A> x </A>\n  </PropertyGroup>\n  <!-- c --><B x=\"1\" y='2'/>\n</Project>\n<!-- extra -->")]
	[Arguments("<Project><PropertyGroup>\n    <A> x </A>\n  </PropertyGroup>\n  <!-- c --><B x=\"1\" y='2'/>\n</Project>\n")]
	[Arguments("<Project><PropertyGroup>\n    <A> x </A>\n  </PropertyGroup>\n  <!-- c --><B x=\"1\" y='2'/>\n</Project>\n")]
	[Arguments("<Project>\n  text<PropertyGroup>\n    <A> x </A>\n  </PropertyGroup>\n  <!-- c --><B x=\"1\" y='2'/>\n</Project>\n")]
	public async Task Content_changes_fail(string output) =>
		await Assert(output, pass: false);

	private static Task Assert(string output, bool pass)
	{
		using var before = new XmlTokenBuffer();
		using var after = new XmlTokenBuffer();
		using var tree = new XmlTree();
		XmlLexer.Lex(Source, before, out _).Should().BeTrue();
		tree.Build(Source, before, XmlProfile.MsBuild, out _).Should().BeTrue();
		XmlLexer.Lex(output, after, out var error).Should().BeTrue(error.Message);

		XmlVerifier.Verify(Source, before.Tokens, output, after.Tokens, out var failure).Should().Be(pass, failure);
		return Task.CompletedTask;
	}
}
