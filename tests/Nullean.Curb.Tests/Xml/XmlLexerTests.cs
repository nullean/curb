using System.Runtime.CompilerServices;
using System.Text;
using System.Xml;
using System.Xml.Linq;
using AwesomeAssertions;
using Nullean.Curb.Tests.Formatting;
using Nullean.Curb.Xml;

namespace Nullean.Curb.Tests.Xml;

/// <summary>
/// The lexer's one promise is that its tokens are the source, every character in order. These hold it
/// to that on hand-picked shapes, on every XML file in this repository, and on generated noise.
/// </summary>
public class XmlLexerTests
{
	[Test]
	public async Task Every_kind_of_token_is_cut_where_expected()
	{
		const string source = "<?xml version=\"1.0\"?>\n<!DOCTYPE a [<!ENTITY e \"]>\">]>\n<a x = 'y' z=\"&amp;\"\n  ><!-- c --><?p d?><![CDATA[<]]>t&e;<b/></a  >";
		using var tokens = new XmlTokenBuffer();

		XmlLexer.Lex(source, tokens, out var error).Should().BeTrue(error.Message);

		var kinds = tokens.Tokens.ToArray().Select(t => $"{t.Kind}:{source.Substring(t.Start, t.Length)}").ToArray();
		kinds.Should().Equal(
			"XmlDeclaration:<?xml version=\"1.0\"?>",
			"Text:\n",
			"Doctype:<!DOCTYPE a [<!ENTITY e \"]>\">]>",
			"Text:\n",
			"StartTagOpen:<a",
			"TagSpace: ",
			"AttributeName:x",
			"AttributeEquals: = ",
			"AttributeValue:'y'",
			"TagSpace: ",
			"AttributeName:z",
			"AttributeEquals:=",
			"AttributeValue:\"&amp;\"",
			"TagSpace:\n  ",
			"TagClose:>",
			"Comment:<!-- c -->",
			"ProcessingInstruction:<?p d?>",
			"CData:<![CDATA[<]]>",
			"Text:t&e;",
			"StartTagOpen:<b",
			"EmptyTagClose:/>",
			"EndTag:</a  >");
		tokens[1].Is(XmlTokenFlags.WhitespaceOnly).Should().BeTrue();
		tokens[13].Is(XmlTokenFlags.HasNewLine).Should().BeTrue();
		await Task.CompletedTask;
	}

	[Test]
	public async Task Every_xml_file_in_this_repository_lexes_losslessly_and_formats_safely()
	{
		var root = RepositoryRoot();
		var files = Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories)
			.Where(path => !path.Contains("/.git/", StringComparison.Ordinal) && !path.Contains("/.artifacts/", StringComparison.Ordinal)
				&& !path.Contains("/bin/", StringComparison.Ordinal) && !path.Contains("/obj/", StringComparison.Ordinal)
				&& !path.Contains("/build/output/", StringComparison.Ordinal))
			.Where(path => XmlOptions.ProfileFor(path) != XmlProfile.Generic || Path.GetFileName(path).Equals("nuget.config", StringComparison.OrdinalIgnoreCase))
			.ToArray();

		files.Should().NotBeEmpty();
		using var tokens = new XmlTokenBuffer();
		using var formatter = new XmlFormatter();
		foreach (var file in files)
		{
			var source = File.ReadAllText(file);
			XmlLexer.Lex(source, tokens, out var error).Should().BeTrue($"{file} {error.Describe(source)}");
			XmlLexer.Covers(source, tokens.Tokens).Should().BeTrue(file);

			var options = new XmlOptions { Profile = XmlOptions.ProfileFor(file) };
			var result = formatter.Format(source, options);
			result.Status.Should().Be(FormatStatus.Formatted, $"{file}: {result.Message}");
			XmlFormattingTest.AssertSameInfoset(source, result.Text!, options.Profile);
			formatter.Format(result.Text!, options).Changed.Should().BeFalse($"{file} is not idempotent");
		}

		await Task.CompletedTask;
	}

	/// <summary>
	/// Random documents built from XML's punctuation. Whatever the lexer accepts it must cover exactly;
	/// whatever Curb accepts as a whole, <see cref="XDocument"/> must accept too, and formatting it must
	/// pass the verifier, keep the infoset and be idempotent.
	/// </summary>
	[Test]
	public async Task Generated_documents_never_break_the_lexers_promise()
	{
		using var tokens = new XmlTokenBuffer();
		using var formatter = new XmlFormatter();
		var accepted = 0;

		// CURB_XML_FUZZ raises the count for a long run by hand; the suite keeps it quick.
		var iterations = int.TryParse(Environment.GetEnvironmentVariable("CURB_XML_FUZZ"), out var n) ? n : 20_000;
		var random = new Random(int.TryParse(Environment.GetEnvironmentVariable("CURB_XML_FUZZ_SEED"), out var seed) ? seed : 20261030);
		for (var i = 0; i < iterations; i++)
		{
			var source = Generate(random);
			if (!XmlLexer.Lex(source, tokens, out _))
				continue;

			XmlLexer.Covers(source, tokens.Tokens).Should().BeTrue(source);

			var options = new XmlOptions
			{
				Profile = XmlProfile.Generic,
				MaxBlankLines = random.Next(3),
				AttributeIndent = (AttributeIndent)random.Next(4),
				SpaceBeforeSelfClosingOption = (SelfClosingSpace)random.Next(3),
				Layout = XmlOptions.DefaultLayout with { MaxLineLength = random.Next(2) == 0 ? LayoutOptions.Off : random.Next(8, 60), UseTabs = random.Next(4) == 0 },
			};
			var result = formatter.Format(source, options);
			if (result.Status == FormatStatus.SyntaxError)
				continue;

			accepted++;
			result.Status.Should().Be(FormatStatus.Formatted, $"{source}\n{result.Message}");

			var parse = () => XDocument.Parse(source, LoadOptions.PreserveWhitespace);
			parse.Should().NotThrow<XmlException>($"Curb accepted a document XDocument refuses:\n{source}");

			XmlFormattingTest.AssertSameInfoset(source, result.Text!, XmlProfile.Generic);
			formatter.Format(result.Text!, options).Changed.Should().BeFalse(source);
		}

		accepted.Should().BeGreaterThan(1000, "the generator has to produce enough well-formed documents to mean anything");
		await Task.CompletedTask;
	}

	private static readonly string[] Pieces =
	[
		"<a>", "</a>", "<b x=\"1\">", "</b>", "<c/>", "<c />", "<d\n  y='2'\n  z=\"3\"/>", " ", "\n", "\n\n\n", "\t", "  \n    ",
		"text", "&amp;", "&#x41;", "<!-- c -->", "<!--\n  multi\n-->", "<?p q?>", "<![CDATA[ <x> ]]>", "\r\n",
		"<e xml:space=\"preserve\">", "</e>", "<n:f xmlns:n=\"u\">", "</n:f>", "<", ">", "&", "\"", "'", "=", "/",
	];

	private static string Generate(Random random)
	{
		var builder = new StringBuilder("<r>");
		var count = random.Next(1, 24);
		for (var i = 0; i < count; i++)
			builder.Append(Pieces[random.Next(Pieces.Length)]);
		builder.Append("</r>");
		return builder.ToString();
	}

	private static string RepositoryRoot([CallerFilePath] string path = "")
	{
		var directory = new DirectoryInfo(Path.GetDirectoryName(path)!);
		while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "curb.slnx")))
			directory = directory.Parent;
		return directory?.FullName ?? throw new InvalidOperationException("no repository root above " + path);
	}
}
