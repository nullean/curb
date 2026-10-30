using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using System.IO.Abstractions.TestingHelpers;
using System.Xml;
using System.Xml.Linq;
using JetBrains.Annotations;
using Nullean.Curb.EditorConfig;
using Nullean.Curb.Xml;

namespace Nullean.Curb.Tests.Formatting;

/// <summary>
/// Base class for XML formatting tests: input and expected output as raw string literals, options as
/// literal <c>.editorconfig</c> text bound through the real binder.
/// </summary>
/// <remarks>
/// <para>
/// There is no reference formatter for XML, so expectations are written by hand, and every case is
/// held to more than its expectation. Each one also asserts that the lexer's tokens cover the input
/// and the output exactly, that the verifier passed, that formatting the expectation changes nothing,
/// and that the input and output parse with <see cref="XDocument"/> to the same infoset once
/// whitespace in element-only content is set aside. That last one is a second opinion from a parser
/// Curb did not write.
/// </para>
/// <para>
/// Options sit under an <c>[*.{csproj,props,targets,slnx}]</c> section that already sets
/// <c>curb_xml_format = true</c>, the way a repository opts in. The file name picks the profile, so a
/// case about solution files passes <c>Test.slnx</c>.
/// </para>
/// </remarks>
public abstract class XmlFormattingTest
{
	protected const string Project = "Test.csproj";

	private static readonly ConcurrentDictionary<(string, string), XmlOptions> Cache = new();

	/// <summary>Asserts that <paramref name="source"/> formats to exactly <paramref name="expected"/>.</summary>
	protected static Task Formats(
		[LanguageInjection("xml")][StringSyntax("xml")] string source,
		[LanguageInjection("xml")][StringSyntax("xml")] string expected,
		[StringSyntax("ini")] string? editorConfig = null,
		string fileName = Project)
	{
		var options = Options(editorConfig, fileName);
		var actual = FormatOrFail(source, options, "source");

		if (!string.Equals(actual, expected, StringComparison.Ordinal))
			throw new FormattingAssertionException(FormattingDiff.Describe(expected, actual, source, editorConfig));

		AssertSameInfoset(source, actual, options.Profile);

		var second = FormatOrFail(expected, options, "expected output");
		if (!string.Equals(second, expected, StringComparison.Ordinal))
		{
			throw new FormattingAssertionException(
				"formatting is not idempotent — formatting the expected output changed it again:"
				+ Environment.NewLine
				+ FormattingDiff.Describe(expected, second, expected, editorConfig));
		}

		return Task.CompletedTask;
	}

	/// <summary>Asserts that <paramref name="source"/> is already formatted.</summary>
	protected static Task Unchanged(
		[LanguageInjection("xml")][StringSyntax("xml")] string source,
		[StringSyntax("ini")] string? editorConfig = null,
		string fileName = Project) =>
		Formats(source, source, editorConfig, fileName);

	/// <summary>
	/// Asserts that Curb refuses the input. For a syntax error, also that <see cref="XDocument"/>
	/// refuses it: Curb must not be stricter than XML for no reason, nor looser than it.
	/// </summary>
	protected static Task Rejects(
		[LanguageInjection("xml")][StringSyntax("xml")] string source,
		FormatStatus status = FormatStatus.SyntaxError,
		string fileName = Project,
		bool parserAgrees = true)
	{
		using var formatter = new XmlFormatter();
		var result = formatter.Format(source, Options(null, fileName));

		if (result.Status != status)
		{
			throw new FormattingAssertionException(
				$"expected the formatter to report {status} but it reported {result.Status}"
				+ (result.Message is null ? string.Empty : $" ({result.Message})"));
		}

		if (status == FormatStatus.SyntaxError && parserAgrees)
		{
			try
			{
				XDocument.Parse(source, LoadOptions.PreserveWhitespace);
			}
			catch (XmlException)
			{
				return Task.CompletedTask;
			}

			throw new FormattingAssertionException("Curb refused a document XDocument accepts: " + result.Message);
		}

		return Task.CompletedTask;
	}

	internal static XmlOptions Options(string? editorConfig, string fileName) =>
		Cache.GetOrAdd((editorConfig ?? "", fileName), static key =>
		{
			var directory = $"/xml/{(uint)StringComparer.Ordinal.GetHashCode(key.Item1):x8}";
			var fileSystem = new MockFileSystem();
			fileSystem.AddFile(
				$"{directory}/.editorconfig",
				new MockFileData($"root = true\n[*.{{csproj,props,targets,slnx,config,xml}}]\ncurb_xml_format = true\n{key.Item1}\n"));
			fileSystem.AddFile($"{directory}/{key.Item2}", new MockFileData(string.Empty));

			var diagnostics = new List<Nullean.Curb.Options.CurbDiagnostic>();
			var options = XmlOptionsBinder.Bind(new CurbEditorConfig(fileSystem), $"{directory}/{key.Item2}", diagnostics);
			if (diagnostics.Count > 0)
				throw new FormattingAssertionException("the case's .editorconfig did not bind cleanly: " + diagnostics[0]);
			if (!options.Enabled)
				throw new FormattingAssertionException("curb_xml_format did not reach the file");
			return options;
		});

	/// <summary>Formats with every check the harness owns: lexer coverage both ways, and the verifier's verdict.</summary>
	private static string FormatOrFail(string source, in XmlOptions options, string what)
	{
		AssertLexesLosslessly(source, what);

		using var formatter = new XmlFormatter();
		var result = formatter.Format(source, options);
		if (result.Status != FormatStatus.Formatted)
			throw new FormattingAssertionException($"formatting the {what} failed with {result.Status}: {result.Message}");

		AssertLexesLosslessly(result.Text!, what + "'s output");
		return result.Text!;
	}

	private static void AssertLexesLosslessly(string text, string what)
	{
		using var tokens = new XmlTokenBuffer();
		if (XmlLexer.Lex(text, tokens, out var error) && !XmlLexer.Covers(text, tokens.Tokens))
			throw new FormattingAssertionException($"the lexer's tokens do not cover the {what} exactly");
		_ = error;
	}

	/// <summary>
	/// Parses both with <see cref="XDocument"/>, drops whitespace in element-only content and between
	/// top-level nodes, and compares what is left.
	/// </summary>
	internal static void AssertSameInfoset(string before, string after, XmlProfile profile)
	{
		var left = Infoset(before, profile);
		var right = Infoset(after, profile);
		if (!XNode.DeepEquals(left, right))
		{
			throw new FormattingAssertionException(
				$"the {profile} output does not parse to the same document as the input:"
				+ Environment.NewLine + left + Environment.NewLine + "---" + Environment.NewLine + right);
		}
	}

	/// <remarks>
	/// Written from the XML and MSBuild rules directly, not from <see cref="XmlTree"/>'s decisions, so a
	/// tree that wrongly marked a value as layout is caught here rather than agreed with.
	/// </remarks>
	private static XDocument Infoset(string text, XmlProfile profile)
	{
		var document = XDocument.Parse(text, LoadOptions.PreserveWhitespace);
		foreach (var element in document.Descendants().ToList())
		{
			if (Preserved(element) || (profile == XmlProfile.MsBuild && InsideMsBuildValue(element)))
				continue;

			var texts = element.Nodes().OfType<XText>().ToList();
			var elementOnly = texts.All(t => t is not XCData && string.IsNullOrWhiteSpace(t.Value))
				&& (element.Nodes().Any(n => n is XElement or XComment or XProcessingInstruction) || texts.Any(t => t.Value.Contains('\n')));
			if (elementOnly)
				texts.ForEach(t => t.Remove());
		}

		foreach (var topLevel in document.Nodes().OfType<XText>().ToList())
			topLevel.Remove();
		return document;
	}

	/// <summary>
	/// A property, an item's metadata, a project extension or an inline task body, or anything inside
	/// one: MSBuild reads all of it as a value, whitespace included.
	/// </summary>
	private static bool InsideMsBuildValue(XElement element)
	{
		for (var current = element; current is not null; current = current.Parent)
		{
			var name = current.Name.LocalName;
			var parent = current.Parent?.Name.LocalName;
			var grandparent = current.Parent?.Parent?.Name.LocalName;
			if (name == "ProjectExtensions" || parent == "PropertyGroup" || (name == "Task" && parent == "UsingTask")
				|| grandparent is "ItemGroup" or "ItemDefinitionGroup")
				return true;
		}

		return false;
	}

	private static bool Preserved(XElement element)
	{
		for (var current = element; current is not null; current = current.Parent)
		{
			if (current.Attribute(XNamespace.Xml + "space") is { } space)
				return space.Value == "preserve";
		}

		return false;
	}
}
