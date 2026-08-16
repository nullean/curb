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
}
