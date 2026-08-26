using System.Globalization;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
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
		[StringSyntax("ini")] string? editorConfig = null,
		string? fileName = null,
		[CallerMemberName] string? caller = null,
		[CallerFilePath] string? callerFile = null)
	{
		var options = TestOptions.Parse(editorConfig);
		var actual = FormatOrFail(source, options, "source", fileName).TrimEnd('\n', '\r');
		expected = expected.TrimEnd('\n', '\r');

		ExpectationDump.Record(source, expected, editorConfig, TestCase(callerFile, caller));

		if (!string.Equals(actual, expected, StringComparison.Ordinal))
			throw new FormattingAssertionException(FormattingDiff.Describe(expected, actual, source, editorConfig));

		AssertCaseIsNotVacuous(source, expected, editorConfig, fileName);

		// The expected output must be a fixed point. CSharpier's harness has no equivalent check,
		// and every idempotency bug found so far grew a blank line or an indent level per run.
		var second = FormatOrFail(expected, options, "expected output", fileName).TrimEnd('\n', '\r');
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
		[StringSyntax("ini")] string? editorConfig = null,
		string? fileName = null,
		[CallerMemberName] string? caller = null,
		[CallerFilePath] string? callerFile = null) =>
		Formats(source, source, editorConfig, fileName, caller, callerFile);

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
		[StringSyntax("ini")] string? editorConfig = null,
		[CallerMemberName] string? caller = null,
		[CallerFilePath] string? callerFile = null)
	{
		Formats(source, asDefault, editorConfig, caller: caller, callerFile: callerFile);
		return Formats(source, asOpinion, JoinConfig(editorConfig, opinionConfig), caller: caller, callerFile: callerFile);
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
	/// <remarks>
	/// Does not run <see cref="AssertCaseIsNotVacuous"/>: that check trims trailing newlines and line
	/// endings before comparing, which is exactly the byte-level distinction this method exists to test —
	/// applying it here would make every final-newline and line-ending case look vacuous by construction.
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

	/// <summary>
	/// Asserts that an option value Curb does not recognise falls back to the default, rather than being
	/// guessed at.
	/// </summary>
	/// <remarks>
	/// The one case where <paramref name="expected"/> equalling what bare defaults would produce is the
	/// point, not a sign the case proves nothing — <see cref="AssertCaseIsNotVacuous"/> would reject it for
	/// the wrong reason, so this bypasses that check rather than the case being rewritten to dodge it.
	/// </remarks>
	protected static Task FallsBackToDefault(
		[LanguageInjection("csharp")][StringSyntax("C#")] string source,
		[LanguageInjection("csharp")][StringSyntax("C#")] string expected,
		[StringSyntax("ini")] string editorConfig,
		string? fileName = null,
		[CallerMemberName] string? caller = null,
		[CallerFilePath] string? callerFile = null)
	{
		var options = TestOptions.Parse(editorConfig);
		var actual = FormatOrFail(source, options, "source", fileName).TrimEnd('\n', '\r');
		expected = expected.TrimEnd('\n', '\r');

		ExpectationDump.Record(source, expected, editorConfig, TestCase(callerFile, caller));

		if (!string.Equals(actual, expected, StringComparison.Ordinal))
			throw new FormattingAssertionException(FormattingDiff.Describe(expected, actual, source, editorConfig));

		return Task.CompletedTask;
	}

	/// <summary>
	/// Asserts that <paramref name="editorConfig"/> is actually responsible for reaching
	/// <paramref name="expected"/> from <paramref name="source"/>, rather than the case passing by
	/// coincidence.
	/// </summary>
	/// <remarks>
	/// Formatting <paramref name="source"/> under a bare <c>.editorconfig</c> — none of the settings this
	/// case asks for — must <b>not</b> already land on <paramref name="expected"/>. If it does, the option
	/// under test had nothing to do with reaching it: the case would pass just as well against a printer
	/// that never read the option at all, which is indistinguishable from a case whose "badly formatted"
	/// input was never actually bad, or a construct the option in question does not touch. Skipped when
	/// there is no configuration to be responsible for anything (<paramref name="editorConfig"/> is empty)
	/// or when the case is asserting a no-op (<paramref name="source"/> already equals
	/// <paramref name="expected"/>) — that is what <see cref="Unchanged"/> is for. A case whose whole point
	/// is that an unrecognised value falls back to the default uses <see cref="FallsBackToDefault"/>
	/// instead, which does not run this check at all.
	/// </remarks>
	private static void AssertCaseIsNotVacuous(string source, string expected, string? editorConfig, string? fileName)
	{
		if (string.IsNullOrEmpty(editorConfig))
			return;
		if (string.Equals(source, expected, StringComparison.Ordinal))
			return;

		var baseline = FormatOrFail(source, TestOptions.Parse(null), "source under bare defaults", fileName)
			.TrimEnd('\n', '\r');

		if (string.Equals(baseline, expected, StringComparison.Ordinal))
		{
			throw new FormattingAssertionException(
				"this case proves nothing about the option under test — formatting `source` with a bare "
				+ ".editorconfig (none of this case's settings) already produces `expected`:"
				+ Environment.NewLine
				+ FormattingDiff.Describe(expected, baseline, source, editorConfig));
		}
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
	/// Names the test case as <c>ClassName.MethodName</c>, for attributing a dumped
	/// <see cref="ExpectationDump"/> case back to the test that produced it.
	/// </summary>
	/// <remarks>
	/// The class name is read from the caller's file rather than <c>GetType()</c> — these are static
	/// methods with no instance to reflect on — which relies on this project's one-class-per-file
	/// convention. <c>./build.sh verifyexpectations</c> uses the result to match a discovered
	/// disagreement against <c>build/conformance-divergences.json</c>.
	/// </remarks>
	private static string TestCase(string? callerFile, string? caller) =>
		$"{(callerFile is null ? "?" : Path.GetFileNameWithoutExtension(callerFile))}.{caller ?? "?"}";

	/// <summary>
	/// Runs the formatter with every safety net engaged, including the round-trip token comparer
	/// forced on so the risk detector is bypassed rather than trusted.
	/// </summary>
	private static string FormatOrFail(string source, in FormatOptions options, string what, string? fileName = null)
	{
		using var formatter = new CSharpFormatter();
		var result = formatter.Format(source, options, fileName, produceText: true, forceRoundTrip: true, verifyRoundTrip: true);

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
	private static int Next;

	public static void Record(string source, string expected, string? editorConfig, string testCase)
	{
		if (string.IsNullOrEmpty(Root))
			return;

		// A directory each, because the expectations do not share a configuration: one asserts tab
		// indentation, the next a width of 40. Pooling them under one .editorconfig would judge most
		// of them against settings they were never written for.
		var directory = Path.Combine(Root, Interlocked.Increment(ref Next).ToString("D5", CultureInfo.InvariantCulture));
		Directory.CreateDirectory(directory);

		File.WriteAllText(
			Path.Combine(directory, ".editorconfig"),
			"root = true\n[*.cs]\n" + (editorConfig ?? "") + "\n");

		File.WriteAllText(Path.Combine(directory, "Expected.cs"), expected + "\n");

		// The raw, badly formatted input the case started from. Kept alongside Expected.cs so
		// ./build.sh verifyexpectations can ask what the reference tool makes of it on its own (X) and
		// compare that to what Curb chose (Z) — the two are allowed to differ, but the difference has to
		// be a documented, deliberate one rather than a silent one.
		File.WriteAllText(Path.Combine(directory, "Source.cs"), source + "\n");

		// ClassName.MethodName, so a discovered X != Z disagreement can be matched against
		// build/conformance-divergences.json without guessing which test a numbered directory came from.
		File.WriteAllText(Path.Combine(directory, "TestCase.txt"), testCase);
	}
}

/// <summary>A formatting expectation that did not hold. Carries a rendered diff as its message.</summary>
public sealed class FormattingAssertionException(string message) : Exception(message);
