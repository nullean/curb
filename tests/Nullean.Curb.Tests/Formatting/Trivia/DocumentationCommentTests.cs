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

	// ---- continuation lines take the first line's indent (issue #180) ---------------------------
	//
	// Every expectation below is what `dotnet format whitespace` writes for the same input and
	// settings. The whitespace in front of each later `///` sits inside the trivia, so it used to be
	// emitted as written: only the first line moved, and dotnet format then moved the rest.

	[Test]
	public Task Converting_spaces_to_tabs_reindents_every_line() => Formats(
		"namespace N\n{\n    public class C\n    {\n        /// <summary>\n        /// Adds one.\n        /// </summary>\n        /// <param name=\"x\">\n        /// The value.\n        /// </param>\n        /// <code>\n        /// if (x > 0)\n        ///     return x + 1;\n        /// </code>\n        public int M(int x) => x + 1;\n    }\n}\n",
		"namespace N\n{\n\tpublic class C\n\t{\n\t\t/// <summary>\n\t\t/// Adds one.\n\t\t/// </summary>\n\t\t/// <param name=\"x\">\n\t\t/// The value.\n\t\t/// </param>\n\t\t/// <code>\n\t\t/// if (x > 0)\n\t\t///     return x + 1;\n\t\t/// </code>\n\t\tpublic int M(int x) => x + 1;\n\t}\n}\n",
		editorConfig: "indent_style = tab");

	[Test]
	public Task Converting_tabs_to_spaces_reindents_every_line() => Formats(
		// The tab after the marker is the comment's own text and stays.
		"namespace N\n{\n\tpublic class C\n\t{\n\t\t/// <summary>\n\t\t/// Adds one.\n\t\t/// </summary>\n\t\t/// <code>\n\t\t///\tindented with a tab\n\t\t/// </code>\n\t\tpublic int M(int x) => x + 1;\n\t}\n}\n",
		"namespace N\n{\n    public class C\n    {\n        /// <summary>\n        /// Adds one.\n        /// </summary>\n        /// <code>\n        ///\tindented with a tab\n        /// </code>\n        public int M(int x) => x + 1;\n    }\n}\n");

	[Test]
	public Task Every_line_moves_with_a_member_two_types_deep() => Formats(
		"""
		public class A
		{
		public class B
		{
		public class C
		{
		/// <summary>
		/// Deep.
		/// </summary>
		public void M()
		{
		/// <summary>Not a member.</summary>
		/// more
		M();
		/// before the brace
		/// too
		}
		}
		}
		}
		""",
		"""
		public class A
		{
		    public class B
		    {
		        public class C
		        {
		            /// <summary>
		            /// Deep.
		            /// </summary>
		            public void M()
		            {
		                /// <summary>Not a member.</summary>
		                /// more
		                M();
		                /// before the brace
		                /// too
		            }
		        }
		    }
		}
		""");

	[Test]
	public Task Misaligned_lines_move_to_the_first_lines_indent() => Formats(
		// dotnet format does not keep a later line's offset from the first: every `///` lands on the
		// same column, whatever mix of tabs and spaces it had.
		"public class C\n{\n    /// <summary>\n\t  /// a\n  \t/// b\n          /// </summary>\n    public void M() { }\n}\n",
		"public class C\n{\n    /// <summary>\n    /// a\n    /// b\n    /// </summary>\n    public void M() { }\n}\n");

	[Test]
	public Task Misaligned_lines_move_to_the_first_lines_indent_in_tabs() => Formats(
		"public class C\n{\n    /// <summary>\n\t  /// a\n  \t/// b\n          /// </summary>\n    public void M() { }\n}\n",
		"public class C\n{\n\t/// <summary>\n\t/// a\n\t/// b\n\t/// </summary>\n\tpublic void M() { }\n}\n",
		editorConfig: "indent_style = tab");

	[Test]
	public Task Text_after_the_marker_keeps_its_indentation_and_trailing_whitespace() => Formats(
		"public class C\n{\n  /// <summary>   \n  /// <code>\n  ///   var x = 1;  \n  ///\t\tvar y = 2;\n  /// </code>\n  /// </summary>\n  public void M() { }\n}\n",
		"public class C\n{\n    /// <summary>   \n    /// <code>\n    ///   var x = 1;  \n    ///\t\tvar y = 2;\n    /// </code>\n    /// </summary>\n    public void M() { }\n}\n");

	[Test]
	public Task Converting_to_tabs_with_crlf_endings() => Formats(
		"public class C\r\n{\r\n    /// <summary>\r\n    /// One.\r\n    /// </summary>\r\n    public int P { get; }\r\n}\r\n",
		"public class C\r\n{\r\n\t/// <summary>\r\n\t/// One.\r\n\t/// </summary>\r\n\tpublic int P { get; }\r\n}\r\n",
		editorConfig: "indent_style = tab\nend_of_line = crlf");

	[Test]
	public Task A_delimited_comment_on_the_same_column_keeps_its_continuation_lines() => Formats(
		// `/** */` is not `///`: dotnet format moves its later lines only by as far as the first line
		// moved, and four spaces to one tab moves it nowhere.
		"public class C\n{\n    /**\n     * <summary>Does a thing.</summary>\n     */\n    public int Value;\n}\n",
		"public class C\n{\n\t/**\n     * <summary>Does a thing.</summary>\n     */\n\tpublic int Value;\n}\n",
		editorConfig: "indent_style = tab");
}
