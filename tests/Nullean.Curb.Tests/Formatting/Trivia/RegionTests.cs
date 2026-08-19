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
	public Task Region_around_a_single_member() => Unchanged(
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
	public Task Region_inside_a_method_body() => Unchanged(
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
	public Task Nested_regions() => Unchanged(
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
	public Task Empty_region() => Unchanged(
		"""
		public class C
		{
		    #region Nothing
		    #endregion
		}
		""");

	[Test]
	public Task Region_with_a_comment_above_it() => Unchanged(
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
