using System.Diagnostics.CodeAnalysis;
using JetBrains.Annotations;
using Nullean.Kerf;

namespace Nullean.Kerf.Tests.Formatting;

/// <summary>
/// Base class for formatting tests. Input and expected output are raw string literals in the test
/// body, so a test says exactly what it asserts without opening another file.
/// </summary>
/// <remarks>
/// <para>
/// Options are supplied as literal <c>.editorconfig</c> text and bound through the real binder, so
/// every formatting test exercises the configuration path as a side effect. That also makes width
/// and indent style ordinary test inputs — CSharpier encodes them in fixture filenames and holds
/// width constant at 100 for its whole suite, forcing breaks by padding identifiers with
/// underscores instead.
/// </para>
/// <para>
/// Expected outputs here are written by hand and cross-checked against <c>dotnet format</c> through
/// <c>./build.sh conformance</c>. They are never snapshotted from Kerf's own output, which would
/// bake in the bugs these tests exist to catch.
/// </para>
/// </remarks>
public abstract class FormattingTest
{
	/// <summary>
	/// Asserts that <paramref name="source"/> formats to <paramref name="expected"/>, ignoring the
	/// trailing newline.
	/// </summary>
	/// <remarks>
	/// A raw string literal ends at its closing delimiter with no trailing newline, but
	/// <c>insert_final_newline</c> defaults to true, so a byte-exact comparison would fail every test
	/// for a reason none of them are about. Trailing newlines are therefore compared by
	/// <see cref="FormatsExactly"/>, which the final-newline and line-ending tests use.
	/// </remarks>
	protected static Task Formats(
		[LanguageInjection("csharp")][StringSyntax("C#")] string source,
		[LanguageInjection("csharp")][StringSyntax("C#")] string expected,
		[StringSyntax("ini")] string? editorConfig = null)
	{
		var options = TestOptions.Parse(editorConfig);
		var actual = FormatOrFail(source, options, "source").TrimEnd('\n', '\r');
		expected = expected.TrimEnd('\n', '\r');

		if (!string.Equals(actual, expected, StringComparison.Ordinal))
			throw new FormattingAssertionException(FormattingDiff.Describe(expected, actual, source, editorConfig));

		// The expected output must be a fixed point. CSharpier's harness has no equivalent check,
		// and every idempotency bug found so far grew a blank line or an indent level per run.
		var second = FormatOrFail(expected, options, "expected output").TrimEnd('\n', '\r');
		if (!string.Equals(second, expected, StringComparison.Ordinal))
		{
			throw new FormattingAssertionException(
				"formatting is not idempotent — formatting the expected output changed it again:"
				+ Environment.NewLine
				+ FormattingDiff.Describe(expected, second, expected, editorConfig));
		}

		return Task.CompletedTask;
	}

	/// <summary>
	/// Asserts that <paramref name="source"/> is already formatted, i.e. that formatting is a no-op.
	/// </summary>
	/// <remarks>
	/// The inline equivalent of CSharpier's convention that a fixture without a companion expectation
	/// file must format to itself. It carries most of the suite: one string instead of two, and it is
	/// the shape that matters most, since Kerf's whole premise is not churning conformant code.
	/// </remarks>
	protected static Task Unchanged(
		[LanguageInjection("csharp")][StringSyntax("C#")] string source,
		[StringSyntax("ini")] string? editorConfig = null) =>
		Formats(source, source, editorConfig);

	/// <summary>
	/// Asserts what an opinion does when it is on and, just as importantly, that it does nothing
	/// when it is off.
	/// </summary>
	/// <remarks>
	/// <para>
	/// Everything <c>kerf_opinionated</c> enables is a change <c>dotnet format</c> declines to make
	/// and will not undo, so the opinion is safe to hold — but the default has to stay minimal-churn
	/// or adopting Kerf stops being undramatic. Asserting both halves in one place is what stops an
	/// opinion leaking into the default: <paramref name="asDefault"/> fails the moment it does.
	/// </para>
	/// <para>
	/// Pass the source unchanged as <paramref name="asDefault"/> for the usual case, where the
	/// default leaves the construct exactly as the author wrote it.
	/// </para>
	/// </remarks>
	/// <param name="source">The input.</param>
	/// <param name="asDefault">What it formats to with the switch off.</param>
	/// <param name="asOpinionated">What it formats to with <c>kerf_opinionated = true</c>.</param>
	/// <param name="editorConfig">Extra settings, applied to both halves.</param>
	protected static Task Opinionated(
		[LanguageInjection("csharp")][StringSyntax("C#")] string source,
		[LanguageInjection("csharp")][StringSyntax("C#")] string asDefault,
		[LanguageInjection("csharp")][StringSyntax("C#")] string asOpinionated,
		[StringSyntax("ini")] string? editorConfig = null)
	{
		Formats(source, asDefault, editorConfig);
		return Formats(source, asOpinionated, JoinConfig(editorConfig, "kerf_opinionated = true"));
	}

	private static string JoinConfig(string? editorConfig, string extra) =>
		string.IsNullOrEmpty(editorConfig) ? extra : editorConfig + "\n" + extra;

	/// <summary>
	/// Asserts a byte-exact result, trailing newline and line endings included.
	/// </summary>
	/// <remarks>
	/// For the tests that are specifically about <c>insert_final_newline</c>, <c>end_of_line</c> and
	/// trailing whitespace, where the characters <see cref="Formats"/> ignores are the whole point.
	/// Expected values are ordinary strings rather than raw literals so the trailing bytes are
	/// visible at the call site.
	/// </remarks>
	protected static Task FormatsExactly(
		[LanguageInjection("csharp")][StringSyntax("C#")] string source,
		string expected,
		[StringSyntax("ini")] string? editorConfig = null)
	{
		var options = TestOptions.Parse(editorConfig);
		var actual = FormatOrFail(source, options, "source");

		if (!string.Equals(actual, expected, StringComparison.Ordinal))
			throw new FormattingAssertionException(FormattingDiff.Describe(expected, actual, source, editorConfig));

		return Task.CompletedTask;
	}

	/// <summary>Asserts that Kerf refuses to format the input, rather than formatting it wrongly.</summary>
	protected static Task Rejects(
		[LanguageInjection("csharp")][StringSyntax("C#")] string source,
		FormatStatus status = FormatStatus.SyntaxError,
		[StringSyntax("ini")] string? editorConfig = null)
	{
		using var formatter = new CSharpFormatter();
		var result = formatter.Format(source, TestOptions.Parse(editorConfig), verifyRoundTrip: true, forceRoundTrip: true);

		if (result.Status != status)
		{
			throw new FormattingAssertionException(
				$"expected the formatter to report {status} but it reported {result.Status}"
				+ (result.Message is null ? string.Empty : $" ({result.Message})"));
		}

		return Task.CompletedTask;
	}

	/// <summary>
	/// Runs the formatter with every safety net engaged, including the round-trip token comparer
	/// forced on so the risk detector is bypassed rather than trusted.
	/// </summary>
	private static string FormatOrFail(string source, in FormatOptions options, string what)
	{
		using var formatter = new CSharpFormatter();
		var result = formatter.Format(source, options, produceText: true, forceRoundTrip: true, verifyRoundTrip: true);

		if (result.Status != FormatStatus.Formatted)
			throw new FormattingAssertionException($"formatting the {what} failed with {result.Status}: {result.Message}");

		return result.Text!;
	}
}

/// <summary>A formatting expectation that did not hold. Carries a rendered diff as its message.</summary>
public sealed class FormattingAssertionException(string message) : Exception(message);
