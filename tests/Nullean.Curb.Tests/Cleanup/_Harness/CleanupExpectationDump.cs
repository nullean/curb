using System.Diagnostics;
using System.Globalization;
using System.Reflection;
using Nullean.Curb.Cleanup;

namespace Nullean.Curb.Tests.Cleanup;

/// <summary>
/// Writes every rule-fixing cleanup case the suite asserts to a directory, when asked to.
/// </summary>
/// <remarks>
/// The cleanup rule tests assert with <c>result.Text.Should().Contain(...)</c> rather than a hand-written
/// full expectation the way <c>FormattingTest.Formats</c> does, so there is no separate "expected" string
/// to dump — <see cref="CleanupResult.Text"/> itself, Curb's actual cleaned output, is what gets recorded.
/// That is fine here specifically: the property <c>./build.sh verifycleanupexpectations</c> checks is that
/// whatever Curb produced is a fixed point of <c>dotnet format style</c>, not that a hand-written string is
/// correct — the same reasoning <c>ExpectationDump</c> documents for the formatting side.
/// </remarks>
internal static class CleanupExpectationDump
{
	private static readonly string? Root = Environment.GetEnvironmentVariable("CURB_CLEANUP_EXPECTATION_DUMP");
	private static int Next;

	public static void Record(CleanupResult result, IEnumerable<string> ruleIds)
	{
		if (string.IsNullOrEmpty(Root))
			return;

		// Only a rule actually firing is a case worth checking against dotnet format style — a refusal
		// test has no Z to compare, and Text is null whenever nothing changed.
		if (result.Status != CleanupStatus.Cleaned || result.Applied == 0 || result.Text is null)
			return;

		var directory = System.IO.Path.Combine(Root, Interlocked.Increment(ref Next).ToString("D5", CultureInfo.InvariantCulture));
		Directory.CreateDirectory(directory);

		File.WriteAllText(System.IO.Path.Combine(directory, "Case.cs"), result.Text);

		// The rule ids actually applied, so the build script can escalate exactly those and no others —
		// same reasoning as cleanupConformance's ownedIds, at case rather than corpus scale.
		File.WriteAllText(System.IO.Path.Combine(directory, "RuleIds.txt"), string.Join(" ", ruleIds.Distinct(StringComparer.Ordinal)));

		File.WriteAllText(System.IO.Path.Combine(directory, "TestCase.txt"), CallingTestCase());
	}

	/// <summary>
	/// Names the nearest <c>[Test]</c> method up the call stack as <c>ClassName.MethodName</c>.
	/// </summary>
	/// <remarks>
	/// A stack walk rather than <c>[CallerMemberName]</c>/<c>[CallerFilePath]</c>, because every
	/// <c>Clean</c> helper this is called from has a trailing <c>params CleanupDiagnostic[]</c> parameter,
	/// and C# does not allow optional caller-info parameters after a params array. Searches for the
	/// nearest frame carrying TUnit's <c>TestAttribute</c> rather than assuming a fixed frame count, so it
	/// does not care how many helper methods sit between here and the test — including whether the JIT
	/// inlined any of them.
	/// </remarks>
	private static string CallingTestCase()
	{
		var method = new StackTrace()
			.GetFrames()
			.Select(f => f.GetMethod())
			.FirstOrDefault(m => m is not null && Attribute.IsDefined(m, typeof(TUnit.Core.TestAttribute)));

		return $"{method?.DeclaringType?.Name ?? "?"}.{method?.Name ?? "?"}";
	}
}
