namespace Nullean.Kerf.Tests.Formatting.Trivia;

/// <summary>
/// XML documentation comments.
/// </summary>
/// <remarks>
/// These are <i>structured</i> trivia, which is the trap: the <c>///</c> marker is exterior trivia
/// inside the structure, so a printer that emits the trivia's <c>Span</c> rather than its
/// <c>FullSpan</c> silently strips the marker off every doc comment in the file. That was the first
/// bug the corpus found.
/// </remarks>
public class DocumentationCommentTests : FormattingTest
{
	[Test]
	public Task Single_line_documentation_comment() => Unchanged(
		"""
		/// <summary>Does a thing.</summary>
		public class C
		{
		    public int Value;
		}
		""");

	[Test]
	public Task Multi_line_documentation_comment() => Unchanged(
		"""
		/// <summary>
		/// Does a thing, at length.
		/// </summary>
		public class C
		{
		    public int Value;
		}
		""");

	[Test]
	public Task Documentation_comment_with_parameters_and_returns() => Unchanged(
		"""
		public class C
		{
		    /// <summary>Adds two numbers.</summary>
		    /// <param name="left">The left operand.</param>
		    /// <param name="right">The right operand.</param>
		    /// <returns>Their sum.</returns>
		    public int Add(int left, int right)
		    {
		        return left + right;
		    }
		}
		""");

	[Test]
	public Task Documentation_comment_is_indented_with_its_member() => Formats(
		"""
		public class C
		{
		/// <summary>A value.</summary>
		    public int Value;
		}
		""",
		"""
		public class C
		{
		    /// <summary>A value.</summary>
		    public int Value;
		}
		""");

	[Test]
	public Task Delimited_documentation_comment() => Unchanged(
		"""
		/**
		 * <summary>Does a thing.</summary>
		 */
		public class C
		{
		    public int Value;
		}
		""");

	[Test]
	public Task Documentation_comment_on_a_property() => Unchanged(
		"""
		public class C
		{
		    /// <summary>The value.</summary>
		    public int Value { get; set; }
		}
		""");

	[Test]
	public Task Documentation_comment_containing_a_code_block() => Unchanged(
		"""
		public class C
		{
		    /// <summary>
		    /// Use it like this:
		    /// <code>
		    /// var c = new C();
		    /// </code>
		    /// </summary>
		    public int Value;
		}
		""");

	[Test]
	public Task Documentation_comment_after_an_attribute() => Unchanged(
		"""
		public class C
		{
		    /// <summary>The value.</summary>
		    [Obsolete]
		    public int Value;
		}
		""");

	[Test]
	public Task A_blank_line_between_a_documentation_comment_and_its_member_is_kept() => Unchanged(
		"""
		public class C
		{
		    /// <summary>The value.</summary>

		    public int Value;
		}
		""");

	[Test]
	public Task Documentation_comment_on_a_nested_type() => Unchanged(
		"""
		public class Outer
		{
		    /// <summary>Nested.</summary>
		    public class Inner
		    {
		        public int Value;
		    }
		}
		""");

	[Test]
	public Task Documentation_comment_with_an_inheritdoc_tag() => Unchanged(
		"""
		public class C
		{
		    /// <inheritdoc />
		    public int Value;
		}
		""");
}
