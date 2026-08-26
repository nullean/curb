using AwesomeAssertions;

namespace Nullean.Curb.Tests.Formatting.Options;

/// <summary>
/// <c>csharp_place_simple_declaration_blocks_on_single_line</c> and
/// <c>csharp_place_simple_blocks_on_single_line</c>.
/// </summary>
/// <remarks>
/// Unlike <see cref="PlaceSimpleOnSingleLineTests"/>'s two keys, which only preserve a construct the
/// author already wrote on one line, these two actively collapse one the author wrote across several —
/// measured directly against jb, including that a too-long single statement is left expanded rather
/// than forced to overflow, and that a comment inside the body blocks the collapse rather than being
/// dropped. Both default to false: jb itself does nothing with a bare <c>.editorconfig</c> either.
/// </remarks>
public class PlaceSimpleDeclarationBlocksTests : FormattingTest
{
	private const string DeclarationBlocks = "csharp_place_simple_declaration_blocks_on_single_line = true";
	private const string Blocks = "csharp_place_simple_blocks_on_single_line = true";

	// ---- the shared capability flag PrintBody gates its whole eligibility check behind ---------------

	[Test]
	public async Task Neither_key_set_reports_nothing_to_collapse()
	{
		TestOptions.Parse(null).CollapsesSimpleBlocks.Should().BeFalse();
		await Task.CompletedTask;
	}

	[Test]
	public async Task Either_key_alone_reports_something_to_collapse()
	{
		TestOptions.Parse(DeclarationBlocks).CollapsesSimpleBlocks.Should().BeTrue();
		TestOptions.Parse(Blocks).CollapsesSimpleBlocks.Should().BeTrue();
		await Task.CompletedTask;
	}

	// ---- off by default ----------------------------------------------------------------------------

	[Test]
	public Task A_multi_line_method_body_stays_multi_line_by_default() => Unchanged(
		"""
		public class C
		{
		    public void M()
		    {
		        Call();
		    }
		}
		""");

	// ---- declaration blocks: methods, constructors, operators, local functions, accessors -----------

	[Test]
	public Task A_single_statement_method_body_collapses() => Formats(
		"""
		public class C
		{
		    public void M()
		    {
		        Call();
		    }
		}
		""",
		"""
		public class C
		{
		    public void M() { Call(); }
		}
		""",
		editorConfig: DeclarationBlocks);

	[Test]
	public Task A_two_statement_body_does_not_collapse() => Unchanged(
		"""
		public class C
		{
		    public void M()
		    {
		        Call1();
		        Call2();
		    }
		}
		""",
		editorConfig: DeclarationBlocks);

	[Test]
	public Task A_body_with_a_comment_does_not_collapse() => Unchanged(
		// Collapsing would have to drop the comment or misplace it — measured directly against jb,
		// which declines to collapse here too.
		"""
		public class C
		{
		    public void M()
		    {
		        // note
		        Call();
		    }
		}
		""",
		editorConfig: DeclarationBlocks);

	[Test]
	public Task A_constructor_collapses() => Formats(
		"""
		public class C
		{
		    public C()
		    {
		        Call();
		    }
		}
		""",
		"""
		public class C
		{
		    public C() { Call(); }
		}
		""",
		editorConfig: DeclarationBlocks);

	[Test]
	public Task An_operator_collapses() => Formats(
		"""
		public class C
		{
		    public static C operator +(C a, C b)
		    {
		        return a;
		    }
		}
		""",
		"""
		public class C
		{
		    public static C operator +(C a, C b) { return a; }
		}
		""",
		editorConfig: DeclarationBlocks);

	[Test]
	public Task A_destructor_collapses() => Formats(
		"""
		public class C
		{
		    ~C()
		    {
		        Call();
		    }
		}
		""",
		"""
		public class C
		{
		    ~C() { Call(); }
		}
		""",
		editorConfig: DeclarationBlocks);

	[Test]
	public Task A_local_function_collapses_without_forcing_a_blank_line_around_it() => Formats(
		// csharp_blank_lines_around_local_method defaults to one, and forced a blank line here before
		// this option's own collapse eligibility fed into that decision too — the collapsed local
		// function stayed "renders on several lines" as far as that separate rule was concerned, so it
		// disagreed with jb, which sees a one-line local function once this key collapses it. See
		// Printers.LocalFunctionRendersOnOneLine.
		"""
		public class C
		{
		    public void M()
		    {
		        int Local(int x)
		        {
		            return x + 1;
		        }
		        Call(Local(1));
		    }
		}
		""",
		"""
		public class C
		{
		    public void M()
		    {
		        int Local(int x) { return x + 1; }
		        Call(Local(1));
		    }
		}
		""",
		editorConfig: DeclarationBlocks);

	[Test]
	public Task An_accessor_body_collapses() => Formats(
		// The property line joins too: once each accessor's own body collapses to one line,
		// csharp_place_simple_accessorholder_on_single_line's own default (true, unrelated to this
		// key) joins the now-short accessor list back onto the property's line as well.
		"""
		public class C
		{
		    public int P
		    {
		        get
		        {
		            return _p;
		        }
		        set
		        {
		            _p = value;
		        }
		    }

		    private int _p;
		}
		""",
		"""
		public class C
		{
		    public int P { get { return _p; } set { _p = value; } }

		    private int _p;
		}
		""",
		editorConfig: DeclarationBlocks + "\ncsharp_style_expression_bodied_accessors = false");

	[Test]
	public Task An_accessor_body_collapses_alone_when_the_holder_key_is_off() => Formats(
		"""
		public class C
		{
		    public int P
		    {
		        get
		        {
		            return _p;
		        }
		        set
		        {
		            _p = value;
		        }
		    }

		    private int _p;
		}
		""",
		"""
		public class C
		{
		    public int P
		    {
		        get { return _p; }
		        set { _p = value; }
		    }

		    private int _p;
		}
		""",
		editorConfig: DeclarationBlocks
			+ "\ncsharp_style_expression_bodied_accessors = false"
			+ "\ncsharp_place_simple_accessorholder_on_single_line = false");

	// ---- collapsing overrides brace placement, but only when it actually collapses -------------------

	[Test]
	public Task Collapsing_glues_the_brace_to_the_header_even_under_allman() => Formats(
		// The default csharp_new_line_before_open_brace = all is what put the brace on its own line in
		// the input; collapsing overrides that the same way csharp_preserve_single_line_blocks already
		// does for an author-joined body, gluing the whole declaration onto one line — measured
		// directly against jb.
		"""
		public class C
		{
		    public void M()
		    {
		        Call();
		    }
		}
		""",
		"""
		public class C
		{
		    public void M() { Call(); }
		}
		""",
		editorConfig: DeclarationBlocks + "\ncsharp_new_line_before_open_brace = all");

	[Test]
	public Task A_statement_too_long_to_fit_is_left_expanded() => Unchanged(
		// Measured directly against jb: a single statement that would overflow the configured width
		// stays on its own multi-line body rather than being forced onto an overlong line. Short
		// enough itself to need no wrapping of its own once expanded, isolating the collapse decision
		// from an unrelated argument-wrapping one.
		"""
		public class C
		{
		    public void M()
		    {
		        CallLongEnoughToOverflowTheLineBudgetNow(x);
		    }
		}
		""",
		editorConfig: DeclarationBlocks + "\nmax_line_length = 60");

	// ---- csharp_place_simple_blocks_on_single_line: lambdas and anonymous methods --------------------

	[Test]
	public Task A_lambda_block_body_collapses() => Formats(
		"""
		public class C
		{
		    public void M()
		    {
		        Action<int> a = x =>
		        {
		            Call(x);
		        };
		    }
		}
		""",
		"""
		public class C
		{
		    public void M()
		    {
		        Action<int> a = x => { Call(x); };
		    }
		}
		""",
		editorConfig: Blocks);

	[Test]
	public Task An_anonymous_method_collapses() => Formats(
		"""
		public class C
		{
		    public void M()
		    {
		        Action a = delegate ()
		        {
		            Call();
		        };
		    }
		}
		""",
		"""
		public class C
		{
		    public void M()
		    {
		        Action a = delegate () { Call(); };
		    }
		}
		""",
		editorConfig: Blocks);

	[Test]
	public Task The_declaration_key_does_not_collapse_a_lambda_on_its_own() => Unchanged(
		// The two keys are independent in jb — each responds only to its own — measured directly.
		"""
		public class C
		{
		    public void M()
		    {
		        Action a = () =>
		        {
		            Call();
		        };
		    }
		}
		""",
		editorConfig: DeclarationBlocks);

	[Test]
	public Task The_blocks_key_does_not_collapse_a_method_on_its_own() => Unchanged(
		"""
		public class C
		{
		    public void M()
		    {
		        Call();
		    }
		}
		""",
		editorConfig: Blocks);
}
