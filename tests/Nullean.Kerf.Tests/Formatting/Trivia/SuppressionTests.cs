namespace Nullean.Kerf.Tests.Formatting.Trivia;

/// <summary>
/// <c>#pragma warning disable IDE0055</c> — the region-level opt-out.
/// </summary>
/// <remarks>
/// .NET's own mechanism rather than one Kerf invented: <c>dotnet format</c> has no ignore comment,
/// but Roslyn does honour this pragma in Format Document. Anything wholly inside a suppressed region
/// is emitted exactly as written, which is the same verbatim path the two <c>= ignore</c> spacing
/// options already take.
/// </remarks>
public class SuppressionTests : FormattingTest
{
	[Test]
	public Task A_suppressed_region_keeps_its_alignment() => Unchanged(
		"""
		public class C
		{
		#pragma warning disable IDE0055
		    private static readonly int[] Grid =
		    {
		        1,   2,   3,
		        40,  50,  60,
		    };
		#pragma warning restore IDE0055
		}
		""");

	[Test]
	public Task Code_outside_the_region_is_still_formatted() => Formats(
		"""
		public class C
		{
		#pragma warning disable IDE0055
		    private int    Kept   =   1;
		#pragma warning restore IDE0055

		        private int    Normalised   =   2;
		}
		""",
		"""
		public class C
		{
		#pragma warning disable IDE0055
		    private int    Kept   =   1;
		#pragma warning restore IDE0055

		    private int Normalised = 2;
		}
		""");

	[Test]
	public Task A_bare_disable_covers_every_rule_including_this_one() => Unchanged(
		"""
		public class C
		{
		#pragma warning disable
		    private int    Kept   =   1;
		#pragma warning restore
		}
		""");

	[Test]
	public Task A_pragma_for_another_rule_does_not_suppress_formatting() => Formats(
		"""
		public class C
		{
		#pragma warning disable IDE0059
		    private int    Normalised   =   1;
		#pragma warning restore IDE0059
		}
		""",
		"""
		public class C
		{
		#pragma warning disable IDE0059
		    private int Normalised = 1;
		#pragma warning restore IDE0059
		}
		""");

	[Test]
	public Task A_disable_that_is_never_restored_runs_to_the_end_of_the_file() => Unchanged(
		// What the compiler does with an unrestored pragma, so it is what Kerf does too.
		"""
		public class C
		{
		#pragma warning disable IDE0055
		    private int    First   =   1;
		    private int    Second   =   2;
		}
		""");

	[Test]
	public Task Statements_are_suppressed_as_well_as_members() => Unchanged(
		"""
		public class C
		{
		    public void M()
		    {
		#pragma warning disable IDE0055
		        var a   =   1;
		        var bb  =   22;
		#pragma warning restore IDE0055
		    }
		}
		""");

	[Test]
	public Task A_file_with_no_pragma_is_untouched_by_any_of_this() => Formats(
		"""
		public class C
		{
		        private int    X   =   1;
		}
		""",
		"""
		public class C
		{
		    private int X = 1;
		}
		""");
}
