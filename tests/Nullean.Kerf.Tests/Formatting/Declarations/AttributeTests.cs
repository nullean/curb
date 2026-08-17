namespace Nullean.Kerf.Tests.Formatting.Declarations;

/// <summary>
/// Where attribute sections go, and why the one obvious option here does not exist.
/// </summary>
/// <remarks>
/// <para>
/// A section decorating a member takes a line of its own — <c>dotnet format</c> splits
/// <c>[A][B] public void M()</c> itself, so this is agreement rather than an opinion. Sections that
/// share a line with what they decorate, on parameters and type parameters, stay glued.
/// </para>
/// <para>
/// ReSharper offers <c>space_between_attribute_sections</c> and defaults it to spaced, so it is the
/// obvious next key to adopt. It was tried and rejected: <c>dotnet format</c> does not merely decline
/// to add that space, it <b>removes</b> one that is already there. An option for it could not be a
/// fixed point, and Format Document would undo it on every save — the one thing Kerf's defaults exist
/// to prevent. The tests below are what stop it being reintroduced.
/// </para>
/// </remarks>
public class AttributeTests : FormattingTest
{
	[Test]
	public Task Sections_on_a_member_each_take_a_line() => Formats(
		// dotnet format does this too, so the split is not an opinion Kerf holds alone.
		"""
		public class C
		{
		    [Obsolete][Serializable]
		    public void M()
		    {
		    }
		}
		""",
		"""
		public class C
		{
		    [Obsolete]
		    [Serializable]
		    public void M()
		    {
		    }
		}
		""");

	[Test]
	public Task A_section_sharing_a_line_with_its_member_is_split_off() => Formats(
		"""
		public class C
		{
		    [Obsolete] public void M()
		    {
		    }
		}
		""",
		// dotnet format leaves this one alone, but it never rejoins what Kerf splits, so splitting is
		// a safe opinion and one Kerf holds by default.
		"""
		public class C
		{
		    [Obsolete]
		    public void M()
		    {
		    }
		}
		""");

	[Test]
	public Task Sections_on_a_parameter_stay_glued() => Unchanged(
		// `[A] [B]` is what ReSharper would write. dotnet format actively removes that space, so Kerf
		// writes what the IDE would leave alone.
		"""
		public class C
		{
		    public void M([A][B] int x, [C] int y)
		    {
		    }
		}
		""");

	[Test]
	public Task A_space_between_sections_on_a_parameter_is_removed() => Formats(
		// The direction that matters: given the spaced form, Kerf normalises to glued exactly as
		// dotnet format does, rather than preserving it.
		"""
		public class C
		{
		    public void M([A] [B] int x)
		    {
		    }
		}
		""",
		"""
		public class C
		{
		    public void M([A][B] int x)
		    {
		    }
		}
		""");

	[Test]
	public Task Sections_on_a_type_parameter_stay_glued_too() => Formats(
		// This one used to space them, which was a divergence from dotnet format rather than a choice.
		"""
		public class C<[A][B] T>
		{
		}
		""",
		"""
		public class C<[A][B] T>
		{
		}
		""");

	[Test]
	public Task A_target_specifier_does_not_change_any_of_this() => Unchanged(
		"""
		public class C
		{
		    public void M([property: A][B] int x)
		    {
		    }
		}
		""");

	private const string JoinAttributes = """
		[*.cs]
		max_line_length = 120
		csharp_place_property_attribute_on_same_line = if_owner_is_single_line
		csharp_place_method_attribute_on_same_line = if_owner_is_single_line
		""";

	[Test]
	public Task An_attribute_joins_a_member_that_fits_on_one_line() => Formats(
		"""
		public class C
		{
		    [JsonPropertyName("branch")]
		    public string Branch { get; init; }
		}
		""",
		"""
		public class C
		{
		    [JsonPropertyName("branch")] public string Branch { get; init; }
		}
		""",
		JoinAttributes);

	/// <summary>
	/// The case that killed this family three times over: run 1 leaves the attribute above a member that
	/// spans lines, reflow joins the member, run 2 answers the same question differently.
	/// </summary>
	/// <remarks>
	/// It cannot happen here because the attribute and the member are inside one group, so "does this fit
	/// on a line" is asked once with the attribute already counted. The harness formats every expectation
	/// a second time, which is the assertion that matters most in this file.
	/// </remarks>
	/// <remarks>
	/// The member is 113 columns and the join would take it to 126, so the attribute stays put — and it is
	/// the join being counted that decides that, which is the whole difference from the reverted rule.
	/// </remarks>
	[Test]
	public Task An_attribute_stays_above_a_member_too_long_to_join() => Unchanged(
		"""
		public class C
		{
		    [JsonIgnore]
		    public string? GitHubRepositoryOfRemote => Remote is "elastic/docs-builder-unknown" ? null : Extract(Remote);
		}
		""",
		JoinAttributes);

	/// <summary>
	/// Sections glue to each other when the unit fits and stack when it does not, which is one decision
	/// rather than two.
	/// </summary>
	/// <remarks>
	/// Deciding the two separately collapsed a <c>[Theory]</c> and nine <c>[InlineData]</c>s onto a single
	/// line on the corpus. <c>dotnet format</c> closes up a gap between sections already sharing a line; it
	/// never moves sections onto one.
	/// </remarks>
	[Test]
	public Task Two_sections_glue_to_each_other_when_the_member_fits() => Formats(
		"""
		public class C
		{
		    [Keyword]
		    [JsonPropertyName("tag")]
		    public string? Tag { get; set; }
		}
		""",
		"""
		public class C
		{
		    [Keyword][JsonPropertyName("tag")] public string? Tag { get; set; }
		}
		""",
		JoinAttributes);

	[Test]
	public Task Sections_stack_again_once_the_member_will_not_fit() => Unchanged(
		"""
		public class C
		{
		    [Keyword]
		    [JsonPropertyName("a_rather_long_serialised_property_name_here")]
		    public string? TagWithAnEquallyLongName { get; set; } = "and an initialiser too";
		}
		""",
		JoinAttributes);

	/// <summary>
	/// The key does nothing in preservation mode, and says so rather than going quiet.
	/// </summary>
	/// <remarks>
	/// Preservation has to be asked for now, because a width alone selects deterministic layout — which is
	/// the whole point of <see cref="JoinAttributes"/> above needing nothing but a width. Setting the key
	/// here without the mode it needs reports KERF1005 and changes nothing.
	/// </remarks>
	[Test]
	public Task Joining_needs_deterministic_layout() => Unchanged(
		"""
		public class C
		{
		    [JsonPropertyName("branch")]
		    public string Branch { get; init; }
		}
		""",
		"""
		[*.cs]
		max_line_length = 120
		csharp_keep_existing_linebreaks = true
		csharp_place_property_attribute_on_same_line = if_owner_is_single_line
		""");
}
