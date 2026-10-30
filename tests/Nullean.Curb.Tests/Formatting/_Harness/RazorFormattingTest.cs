using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using System.IO.Abstractions.TestingHelpers;
using JetBrains.Annotations;
using Nullean.Curb.EditorConfig;
using Nullean.Curb.Razor;

namespace Nullean.Curb.Tests.Formatting;

/// <summary>
/// Base class for Razor tests: a whole <c>.razor</c> or <c>.cshtml</c> file in, the whole file out, and
/// how many <c>@code</c>/<c>@functions</c> blocks were left as written.
/// </summary>
/// <remarks>
/// <para>
/// Options are literal <c>.editorconfig</c> text under a <c>[*.{cs,razor,cshtml}]</c> section, bound
/// through the real binder for a file named <c>Test.razor</c>, which is how a repository gives its Razor
/// files C# style.
/// </para>
/// <para>
/// Every case is held to more than its expectation: the output is compared byte for byte (markup
/// included, so a changed byte outside a block fails the case), the number of declined blocks must match,
/// a declined block must come with a message, and formatting the expectation must change nothing and
/// decline the same blocks.
/// </para>
/// </remarks>
public abstract class RazorFormattingTest
{
	private static readonly ConcurrentDictionary<string, FormatOptions> Cache = new(StringComparer.Ordinal);

	/// <summary>Asserts that <paramref name="source"/> formats to exactly <paramref name="expected"/>.</summary>
	/// <param name="declined">How many blocks are expected to be left as written.</param>
	protected static Task Formats(
		[LanguageInjection("razor")] string source,
		[LanguageInjection("razor")] string expected,
		[StringSyntax("ini")] string? editorConfig = null,
		int declined = 0)
	{
		var options = Options(editorConfig);
		var actual = FormatOrFail(source, options, "source", declined);

		if (!string.Equals(actual, expected, StringComparison.Ordinal))
			throw new FormattingAssertionException(FormattingDiff.Describe(expected, actual, source, editorConfig));

		var second = FormatOrFail(expected, options, "expected output", declined);
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
		[LanguageInjection("razor")] string source,
		[StringSyntax("ini")] string? editorConfig = null,
		int declined = 0) =>
		Formats(source, source, editorConfig, declined);

	/// <summary>
	/// Asserts that every block in <paramref name="source"/> is declined and the file comes back exactly
	/// as it was, with a message naming why.
	/// </summary>
	protected static Task Rejects(
		[LanguageInjection("razor")] string source,
		string reason,
		int declined = 1,
		[StringSyntax("ini")] string? editorConfig = null)
	{
		using var formatter = new RazorFormatter();
		var result = formatter.Format(source, Options(editorConfig));
		if (result.Message is null || !result.Message.Contains(reason, StringComparison.Ordinal))
			throw new FormattingAssertionException($"expected a refusal saying '{reason}', got: {result.Message ?? "(no message)"}");

		return Unchanged(source, editorConfig, declined);
	}

	internal static FormatOptions Options(string? editorConfig) =>
		Cache.GetOrAdd(editorConfig ?? "", static text =>
		{
			var directory = $"/razor/{(uint)StringComparer.Ordinal.GetHashCode(text):x8}";
			var fileSystem = new MockFileSystem();
			fileSystem.AddFile(
				$"{directory}/.editorconfig",
				new MockFileData($"root = true\n[*.{{cs,razor,cshtml}}]\n{text}\n"));
			fileSystem.AddFile($"{directory}/Test.razor", new MockFileData(string.Empty));

			var diagnostics = new List<Nullean.Curb.Options.CurbDiagnostic>();
			var options = EditorConfigOptionsBinder.Bind(new CurbEditorConfig(fileSystem).For($"{directory}/Test.razor"), diagnostics);
			if (diagnostics.Count > 0)
				throw new FormattingAssertionException("the case's .editorconfig did not bind cleanly: " + diagnostics[0]);
			return options;
		});

	private static string FormatOrFail(string source, in FormatOptions options, string what, int declined)
	{
		using var formatter = new RazorFormatter();
		var result = formatter.Format(source, options, verifyRoundTrip: true);
		if (result.Status != FormatStatus.Formatted)
			throw new FormattingAssertionException($"formatting the {what} failed with {result.Status}: {result.Message}");

		if (result.Declined != declined)
		{
			throw new FormattingAssertionException(
				$"formatting the {what} declined {result.Declined} block(s), expected {declined}: {result.Message ?? "(no message)"}");
		}

		if ((result.Declined > 0) != (result.Message is not null))
			throw new FormattingAssertionException($"a declined block must come with a message, and only then: {result.Message ?? "(no message)"}");

		if (result.Changed == string.Equals(result.Text, source, StringComparison.Ordinal))
			throw new FormattingAssertionException($"formatting the {what} reported Changed = {result.Changed}, which its text contradicts");

		return result.Text!;
	}
}
