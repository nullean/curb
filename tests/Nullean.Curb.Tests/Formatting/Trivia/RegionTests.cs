namespace Nullean.Curb.Tests.Formatting.Trivia;

/// <summary>
/// <c>#region</c> and <c>#endregion</c>.
/// </summary>
/// <remarks>
/// Regions are the exception among directives: <c>dotnet format</c> indents them with the code they
/// wrap, while <c>#if</c> and friends stay at column zero. Curb follows that split, which is why
/// they get their own file rather than living with the other directives.
/// </remarks>
public class RegionTests : FormattingTest
{
	[Test]
	public Task Region_around_members_is_indented_with_them() => Unchanged(
		"""
		public class C
		{
		    #region Values

		    public int First;
		    public int Second;

		    #endregion
		}
		""");

	[Test]
	public Task Region_around_a_single_member() => Formats(
		// csharp_blank_lines_inside_region defaults to one and is forced rather than merely floored
		// (see Region_around_members_is_indented_with_them's sibling case, which already has the
		// blank lines this one is missing), matching jb's own default on both sides.
		"""
		public class C
		{
		    #region Value
		    public int Value;
		    #endregion
		}
		""",
		"""
		public class C
		{
		    #region Value

		    public int Value;

		    #endregion
		}
		""");

	[Test]
	public Task Region_at_file_scope() => Unchanged(
		"""
		#region Usings

		using System;
		using System.Linq;

		#endregion
		""");

	[Test]
	public Task Region_inside_a_method_body() => Formats(
		"""
		public class C
		{
		    public void M()
		    {
		        #region Setup
		        First();
		        #endregion

		        Second();
		    }
		}
		""",
		"""
		public class C
		{
		    public void M()
		    {
		        #region Setup

		        First();

		        #endregion

		        Second();
		    }
		}
		""");

	[Test]
	public Task Nested_regions() => Formats(
		// The gap between #region Outer and #region Inner already had its one blank line (both
		// sides want one, coalescing rather than stacking to two — see TokenPrinter's forcedGapPending);
		// what was missing is around Inner's own content, the same as Region_around_a_single_member.
		"""
		public class C
		{
		    #region Outer

		    #region Inner
		    public int Value;
		    #endregion

		    #endregion
		}
		""",
		"""
		public class C
		{
		    #region Outer

		    #region Inner

		    public int Value;

		    #endregion

		    #endregion
		}
		""");

	[Test]
	public Task Empty_region() => Formats(
		// Both sides of an empty region still want one blank line each, coalescing to the one
		// between #region and #endregion rather than stacking to two.
		"""
		public class C
		{
		    #region Nothing
		    #endregion
		}
		""",
		"""
		public class C
		{
		    #region Nothing

		    #endregion
		}
		""");

	[Test]
	public Task Region_with_a_comment_above_it() => Formats(
		// A comment counts as real content in front of a #region for csharp_blank_lines_around_
		// region's purposes, even though it is trivia rather than a token — jb adds a blank line
		// after it here the same as it would after a statement.
		"""
		public class C
		{
		    // grouped for readability
		    #region Values
		    public int Value;
		    #endregion
		}
		""",
		"""
		public class C
		{
		    // grouped for readability

		    #region Values

		    public int Value;

		    #endregion
		}
		""");

	[Test]
	public Task Region_is_re_indented_when_nesting_changes() => Formats(
		"""
		public class C
		{
		#region Values
		    public int Value;
		#endregion
		}
		""",
		"""
		public class C
		{
		    #region Values

		    public int Value;

		    #endregion
		}
		""");

	[Test]
	public Task Region_wrapping_a_whole_type() => Unchanged(
		"""
		#region The type

		public class C
		{
		    public int Value;
		}

		#endregion
		""");
}
