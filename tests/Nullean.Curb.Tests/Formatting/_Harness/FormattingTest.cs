using System.Globalization;
using System.Diagnostics.CodeAnalysis;
using JetBrains.Annotations;
using Nullean.Curb;

namespace Nullean.Curb.Tests.Formatting;

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
/// <c>./build.sh conformance</c>. They are never snapshotted from Curb's own output, which would
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

		ExpectationDump.Record(expected, editorConfig);

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
	/// the shape that matters most, since Curb's whole premise is not churning conformant code.
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
	/// Every opinion Curb holds is a change <c>dotnet format</c> declines to make and will not undo,
	/// so it is safe to hold — but each is off unless asked for, or adopting Curb stops being
	/// undramatic. Asserting both halves in one place is what stops an opinion leaking into the
	/// default: <paramref name="asDefault"/> fails the moment it does.
	/// </para>
	/// <para>
	/// Pass the source unchanged as <paramref name="asDefault"/> for the usual case, where the
	/// default leaves the construct exactly as the author wrote it.
	/// </para>
	/// </remarks>
	/// <param name="source">The input.</param>
	/// <param name="asDefault">What it formats to with <paramref name="opinionConfig"/> absent.</param>
	/// <param name="asOpinion">What it formats to with it present.</param>
	/// <param name="opinionConfig">The key that asks for the opinion, such as a trailing-comma one.</param>
	/// <param name="editorConfig">Extra settings, applied to both halves.</param>
	protected static Task WithAndWithout(
		[LanguageInjection("csharp")][StringSyntax("C#")] string source,
		[LanguageInjection("csharp")][StringSyntax("C#")] string asDefault,
		[LanguageInjection("csharp")][StringSyntax("C#")] string asOpinion,
		[StringSyntax("ini")] string opinionConfig,
		[StringSyntax("ini")] string? editorConfig = null)
	{
		Formats(source, asDefault, editorConfig);
		return Formats(source, asOpinion, JoinConfig(editorConfig, opinionConfig));
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

	/// <summary>Asserts that Curb refuses to format the input, rather than formatting it wrongly.</summary>
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

/// <summary>
/// Writes every expectation the suite asserts to a directory, when asked to.
/// </summary>
/// <remarks>
/// <para>
/// So that <c>./build.sh verifyexpectations</c> can run <c>dotnet format</c> over the lot and prove
/// they are all fixed points of it. Until this existed only the corpus proved that; the ~800
/// hand-written expectations proved only that Curb agrees with itself, and an expectation written
/// from a wrong belief about dotnet format would sit there passing forever.
/// </para>
/// <para>
/// Off unless <c>CURB_EXPECTATION_DUMP</c> names a directory, so an ordinary test run neither writes
/// anything nor pays for the hashing.
/// </para>
/// </remarks>
internal static class ExpectationDump
{
	private static readonly string? Root = Environment.GetEnvironmentVariable("CURB_EXPECTATION_DUMP");
	private static int _next;

	public static void Record(string expected, string? editorConfig)
	{
		if (string.IsNullOrEmpty(Root))
			return;

		// A directory each, because the expectations do not share a configuration: one asserts tab
		// indentation, the next a width of 40. Pooling them under one .editorconfig would judge most
		// of them against settings they were never written for.
		var directory = Path.Combine(Root, Interlocked.Increment(ref _next).ToString("D5", CultureInfo.InvariantCulture));
		Directory.CreateDirectory(directory);

		File.WriteAllText(
			Path.Combine(directory, ".editorconfig"),
			"root = true\n[*.cs]\n" + (editorConfig ?? "") + "\n");

		File.WriteAllText(Path.Combine(directory, "Expected.cs"), expected + "\n");
	}
}

/// <summary>A formatting expectation that did not hold. Carries a rendered diff as its message.</summary>
public sealed class FormattingAssertionException(string message) : Exception(message);
