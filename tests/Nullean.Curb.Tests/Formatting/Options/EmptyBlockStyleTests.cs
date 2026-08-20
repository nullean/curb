namespace Nullean.Curb.Tests.Formatting.Options;

/// <summary>
/// <c>[resharper_]csharp_empty_block_style</c> / <c>[resharper_]empty_block_style</c>. See
/// <see href="https://github.com/nullean/curb/issues/8">issue #8</see>.
/// </summary>
/// <remarks>
/// ReSharper's own key, independent of <c>csharp_preserve_single_line_blocks</c> — that option already
/// collapses an empty pair to <c>{ }</c> under deterministic layout but never moves it off wherever
/// <c>csharp_new_line_before_open_brace</c> puts it. This key is unset by default, so none of this
/// changes anything for a repository that never mentions it.
/// </remarks>
public class EmptyBlockStyleTests : FormattingTest
{
	// ---- together_same_line: the issue's own scenario ---------------------------------------------

	[Test]
	public Task An_empty_constructor_stays_on_its_signature_line() => Formats(
		"""
		public class C
		{
		    private C()
		    {
		    }
		}
		""",
		"""
		public class C
		{
		    private C() { }
		}
		""",
		editorConfig: """
		max_line_length = 120
		csharp_new_line_before_open_brace = all
		resharper_csharp_empty_block_style = together_same_line
		""");

	[Test]
	public Task Without_the_key_the_same_input_still_splits() => Formats(
		"""
		public class C
		{
		    private C()
		    {
		    }
		}
		""",
		"""
		public class C
		{
		    private C()
		    { }
		}
		""",
		editorConfig: """
		max_line_length = 120
		csharp_new_line_before_open_brace = all
		""");

	[Test]
	public Task An_empty_control_block_stays_on_its_header_line_too() => Formats(
		"""
		public class C
		{
		    public void M(bool a)
		    {
		        if (a)
		        {
		        }
		    }
		}
		""",
		"""
		public class C
		{
		    public void M(bool a)
		    {
		        if (a) { }
		    }
		}
		""",
		editorConfig: """
		max_line_length = 120
		csharp_new_line_before_open_brace = all
		resharper_csharp_empty_block_style = together_same_line
		""");

	[Test]
	public Task A_wrapped_signature_still_takes_its_own_line_for_the_pair() => Formats(
		"""
		public class C
		{
		    public void Register(int a, int b)
		    {
		    }
		}
		""",
		"""
		public class C
		{
		    public void Register(
		        int a,
		        int b
		    )
		    { }
		}
		""",
		editorConfig: """
		csharp_wrap_parameters_style = chop_always
		resharper_csharp_empty_block_style = together_same_line
		""");

	// ---- together: collapses but does not move the brace -------------------------------------------

	[Test]
	public Task Together_collapses_an_expanded_pair_without_joining_the_header() => Formats(
		"""
		public class C
		{
		    public void Empty()
		    {
		    }
		}
		""",
		"""
		public class C
		{
		    public void Empty()
		    { }
		}
		""",
		editorConfig: "csharp_empty_block_style = together");

	[Test]
	public Task Together_ignores_preserve_single_line_blocks_being_off() => Formats(
		"""
		public class C
		{
		    public void Empty()
		    {
		    }
		}
		""",
		"""
		public class C
		{
		    public void Empty()
		    { }
		}
		""",
		editorConfig: """
		csharp_preserve_single_line_blocks = false
		csharp_empty_block_style = together
		""");

	// ---- multiline: forces an empty pair apart, even one preservation would keep joined ------------

	[Test]
	public Task Multiline_expands_a_pair_the_author_wrote_on_one_line() => Formats(
		"""
		public class C
		{
		    public void Empty() { }
		}
		""",
		"""
		public class C
		{
		    public void Empty()
		    {
		    }
		}
		""",
		editorConfig: "csharp_empty_block_style = multiline");

	// ---- unset leaves every existing rule alone -----------------------------------------------------

	[Test]
	public Task Unset_leaves_preservation_exactly_as_it_was() => Unchanged(
		"""
		public class C
		{
		    public void Empty() { }
		}
		""");

	[Test]
	public Task All_four_resharper_spellings_are_accepted() => Formats(
		"""
		public class C
		{
		    public void Empty()
		    {
		    }
		}
		""",
		"""
		public class C
		{
		    public void Empty() { }
		}
		""",
		editorConfig: "empty_block_style = together_same_line");
}
