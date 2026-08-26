namespace Nullean.Curb.Tests.Formatting.Trivia;

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
	public Task A_blank_line_between_a_documentation_comment_and_its_member_is_stripped() => Formats(
		// A doc comment separated from what it documents reads as detached rather than as deliberate
		// spacing, unlike an ordinary comment — no option gates this, every formatter with an opinion
		// on it agrees.
		"""
		public class C
		{
		    /// <summary>The value.</summary>

		    public int Value;
		}
		""",
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
	[Test]
	public Task A_doc_comment_takes_the_configured_line_ending() => Formats(
		// Roslyn groups consecutive `///` lines into one trivia node, so a doc comment reaches the
		// printer as a single span with its own newlines inside it. Emitted verbatim they kept
		// whatever the source used, while every break Curb writes uses end_of_line — leaving files
		// with both endings, which cost efcore 3,189 fixed points and log4net 322 of theirs.
		"public class C\n{\n    /// <summary>\n    /// One.\n    /// </summary>\n    public int P { get; }\n}\n",
		"public class C\r\n{\r\n    /// <summary>\r\n    /// One.\r\n    /// </summary>\r\n    public int P { get; }\r\n}\r\n",
		editorConfig: "end_of_line = crlf");

}
