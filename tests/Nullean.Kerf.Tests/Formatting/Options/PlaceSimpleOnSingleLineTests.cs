namespace Nullean.Kerf.Tests.Formatting.Options;

/// <summary>
/// <c>csharp_place_simple_enum_on_single_line</c> and
/// <c>csharp_place_simple_accessorholder_on_single_line</c>.
/// </summary>
/// <remarks>
/// <para>
/// Finer-grained versions of <c>csharp_preserve_single_line_blocks</c>, which says the same thing
/// about every block in the file at once. Both are on by default, so neither moves a repository that
/// does not ask.
/// </para>
/// <para>
/// Free ground: expanding a construct is something <c>dotnet format</c> never undoes, so the expanded
/// form is a fixed point. Both expand unconditionally rather than only where the author had written
/// one line — see <see cref="An_expanded_accessor_list_is_not_joined_back_up"/> for why that matters.
/// </para>
/// </remarks>
public class PlaceSimpleOnSingleLineTests : FormattingTest
{
	private const string NoEnum = "csharp_place_simple_enum_on_single_line = false";
	private const string NoAccessors = "csharp_place_simple_accessorholder_on_single_line = false";

	// ---- enums -----------------------------------------------------------------------------------------

	[Test]
	public Task An_enum_written_on_one_line_stays_there_by_default() => Unchanged(
		"""
		public enum E { A, B }
		""");

	[Test]
	public Task The_key_expands_an_enum_body() => WithAndWithout(
		"""
		public enum E { A, B }
		""",
		"""
		public enum E { A, B }
		""",
		"""
		public enum E
		{
		    A,
		    B
		}
		""",
		NoEnum);

	// ---- accessor lists --------------------------------------------------------------------------------

	[Test]
	public Task An_accessor_list_stays_on_the_property_line_by_default() => Unchanged(
		"""
		public class C
		{
		    public int P { get; set; }
		}
		""");

	[Test]
	public Task The_key_gives_the_braces_their_own_lines() => WithAndWithout(
		"""
		public class C
		{
		    public int P { get; set; }
		}
		""",
		"""
		public class C
		{
		    public int P { get; set; }
		}
		""",
		"""
		public class C
		{
		    public int P
		    {
		        get; set;
		    }
		}
		""",
		NoAccessors);

	[Test]
	public Task An_expanded_accessor_list_is_not_joined_back_up() => Unchanged(
		// The reason both keys expand unconditionally rather than only where the source still has the
		// list on one line. Unlike every other block, this printer joins an accessor list that fits —
		// so a guard on the source is self-cancelling: run one expands, run two sees a multi-line list,
		// declines to expand, and the group flattens it straight back. This is that second run.
		"""
		public class C
		{
		    public int P
		    {
		        get; set;
		    }
		}
		""",
		editorConfig: NoAccessors);

	[Test]
	public Task The_block_key_expands_them_too_and_also_settles() => Formats(
		// csharp_preserve_single_line_blocks carried the same defect before either of these keys
		// existed: 455 corpus files oscillated under it, and 43 were moved by dotnet format afterwards.
		// Dropping the guard took those to 2 and 41 — better on both axes, so not a trade.
		"""
		public class C
		{
		    public int P
		    {
		        get; set;
		    }
		}
		""",
		"""
		public class C
		{
		    public int P
		    {
		        get; set;
		    }
		}
		""",
		editorConfig: "csharp_preserve_single_line_blocks = false");

	// ---- together --------------------------------------------------------------------------------------

	[Test]
	public Task Neither_key_touches_what_the_other_governs() => Formats(
		"""
		public enum E { A, B }

		public class C
		{
		    public int P { get; set; }
		}
		""",
		"""
		public enum E
		{
		    A,
		    B
		}

		public class C
		{
		    public int P { get; set; }
		}
		""",
		editorConfig: NoEnum);
}
