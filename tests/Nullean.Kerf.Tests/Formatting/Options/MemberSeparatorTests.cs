namespace Nullean.Kerf.Tests.Formatting.Options;

/// <summary>
/// <c>csharp_new_line_before_members_in_object_initializers</c>,
/// <c>_before_members_in_anonymous_types</c> and <c>_between_query_expression_clauses</c>.
/// </summary>
/// <remarks>
/// <para>
/// All three are documented with a default of true and Kerf defaults all three to false. That is
/// not a preference: <c>dotnet format whitespace</c> does not act on any of them. Set all three to
/// true, hand it <c>new Point{X=1,Y=2}</c>, <c>new{First=1}</c> and a one-line query so it has to
/// rewrite the whitespace anyway, and it normalises the spacing and inserts no break at all.
/// </para>
/// <para>
/// With no observable behaviour to match, defaulting to true would expand every one-line
/// initializer in a repository on first run, on the strength of an option no other tool applies.
/// False keeps Kerf a fixed point of dotnet format and leaves the expanded layout one line away.
/// </para>
/// <para>
/// What that reasoning missed, and the roslyn corpus later caught: the same is true with the key set
/// to <c>true</c>. dotnet format does not open an initializer out then either. The key means one
/// member per line <em>once the initializer is opened</em>, and Kerf read it as a licence to open
/// one — exploding `new[] { a, b }` and every other one-line initializer in any repository setting
/// it. roslyn sets it, and this was that repository's largest single source of churn.
/// </para>
/// </remarks>
public class MemberSeparatorTests : FormattingTest
{
	// ---- object, collection and array initializers -----------------------------------------------

	[Test]
	public Task An_initializer_that_fits_stays_on_one_line_by_default() => Unchanged(
		"""
		public class C
		{
		    public void M()
		    {
		        var o = new Point { X = 1, Y = 2 };
		    }
		}
		""");

	[Test]
	public Task An_initializer_can_be_asked_for_one_member_per_line() => Formats(
		// One member per line *when the initializer is opened out*. It is not a reason to open one:
		// measured with the key set, dotnet format leaves `new Point { X = 1, Y = 2 }` exactly where
		// it is and only spreads the members of an initializer the author had already opened.
		"""
		public class C
		{
		    public void M()
		    {
		        var inline = new Point { X = 1, Y = 2 };
		        var opened = new Point
		        {
		            X = 1, Y = 2
		        };
		    }
		}
		""",
		"""
		public class C
		{
		    public void M()
		    {
		        var inline = new Point { X = 1, Y = 2 };
		        var opened = new Point
		        {
		            X = 1,
		            Y = 2
		        };
		    }
		}
		""",
		editorConfig: "csharp_new_line_before_members_in_object_initializers = true");

	[Test]
	public Task A_collection_initializer_follows_the_same_option() => Formats(
		"""
		public class C
		{
		    public void M()
		    {
		        var l = new List<int> { 1, 2 };
		        var arr = new[] { 1, 2 };
		    }
		}
		""",
		"""
		public class C
		{
		    public void M()
		    {
		        var l = new List<int> { 1, 2 };
		        var arr = new[] { 1, 2 };
		    }
		}
		""",
		editorConfig: "csharp_new_line_before_members_in_object_initializers = true");

	[Test]
	public Task An_expanded_initializer_stays_expanded_with_the_option_off() => Unchanged(
		"""
		public class C
		{
		    public void M()
		    {
		        var o = new Point
		        {
		            X = 1,
		            Y = 2
		        };
		    }
		}
		""");

	[Test]
	public Task The_option_is_about_expanding_not_about_joining() => Unchanged(
		// Turning it off does not gather up a layout the author chose; nothing in Kerf joins lines,
		// which is what dotnet format does and what makes it safe to introduce to a repository.
		"""
		public class C
		{
		    public void M()
		    {
		        var compact = new Point { X = 1, Y = 2 };
		        var expanded = new Point
		        {
		            X = 1,
		            Y = 2
		        };
		    }
		}
		""");

	[Test]
	public Task One_member_per_line_does_not_reach_anonymous_types() => Formats(
		"""
		public class C
		{
		    public void M()
		    {
		        var o = new Point
		        {
		            X = 1, Y = 2
		        };
		        var a = new
		        {
		            First = 1, Second = 2
		        };
		    }
		}
		""",
		"""
		public class C
		{
		    public void M()
		    {
		        var o = new Point
		        {
		            X = 1,
		            Y = 2
		        };
		        var a = new
		        {
		            First = 1, Second = 2
		        };
		    }
		}
		""",
		editorConfig: "csharp_new_line_before_members_in_object_initializers = true");

	// ---- anonymous types --------------------------------------------------------------------------

	[Test]
	public Task An_anonymous_type_that_fits_stays_on_one_line_by_default() => Unchanged(
		"""
		public class C
		{
		    public void M()
		    {
		        var a = new { First = 1, Second = 2 };
		    }
		}
		""");

	[Test]
	public Task An_anonymous_type_can_be_asked_for_one_member_per_line() => Formats(
		// Again: one per line once it is opened out, and no reason to open it.
		"""
		public class C
		{
		    public void M()
		    {
		        var inline = new { First = 1, Second = 2 };
		        var opened = new
		        {
		            First = 1, Second = 2
		        };
		    }
		}
		""",
		"""
		public class C
		{
		    public void M()
		    {
		        var inline = new { First = 1, Second = 2 };
		        var opened = new
		        {
		            First = 1,
		            Second = 2
		        };
		    }
		}
		""",
		editorConfig: "csharp_new_line_before_members_in_anonymous_types = true");

	// ---- query expressions ------------------------------------------------------------------------

	[Test]
	public Task A_query_that_fits_stays_on_one_line_by_default() => Unchanged(
		"""
		public class C
		{
		    public void M()
		    {
		        var q = from x in items where x > 1 select x;
		    }
		}
		""");

	[Test]
	public Task A_query_can_be_asked_for_one_clause_per_line() => Formats(
		"""
		public class C
		{
		    public void M()
		    {
		        var q = from x in items where x > 1 select x;
		    }
		}
		""",
		"""
		public class C
		{
		    public void M()
		    {
		        var q = from x in items
		                where x > 1
		                select x;
		    }
		}
		""",
		editorConfig: "csharp_new_line_between_query_expression_clauses = true");

	[Test]
	public Task A_long_query_still_breaks_with_the_option_off() => Formats(
		"""
		public class C
		{
		    public void M()
		    {
		        var q = from customer in customers where customer.Age > 18 select customer.Name;
		    }
		}
		""",
		"""
		public class C
		{
		    public void M()
		    {
		        var q = from customer in customers
		                where customer.Age > 18
		                select customer.Name;
		    }
		}
		""",
		// Under the `from`, not at an indent level. This asserted the indent for a while, which is
		// what Kerf did rather than what dotnet format does — the difference only shows when the
		// break comes from the width rather than from
		// csharp_new_line_between_query_expression_clauses, and nothing compared the two until
		// verifyexpectations did.
		editorConfig: "max_line_length = 60");

	[Test]
	public Task An_orderby_clause_keeps_its_commas() => Unchanged(
		"""
		public class C
		{
		    public void M()
		    {
		        var q = from p in items orderby p.A, p.B select p;
		    }
		}
		""");

	// ---- the three are independent ----------------------------------------------------------------

	[Test]
	public Task All_three_at_once() => Formats(
		"""
		public class C
		{
		    public void M()
		    {
		        var o = new Point
		        {
		            X = 1, Y = 2
		        };
		        var a = new
		        {
		            First = 1, Second = 2
		        };
		        var q = from x in items select x;
		    }
		}
		""",
		"""
		public class C
		{
		    public void M()
		    {
		        var o = new Point
		        {
		            X = 1,
		            Y = 2
		        };
		        var a = new
		        {
		            First = 1,
		            Second = 2
		        };
		        var q = from x in items
		                select x;
		    }
		}
		""",
		editorConfig: """
		csharp_new_line_before_members_in_object_initializers = true
		csharp_new_line_before_members_in_anonymous_types = true
		csharp_new_line_between_query_expression_clauses = true
		""");
}
