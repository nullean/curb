namespace Nullean.Curb.Tests.Formatting.Statements;

/// <summary>Conditionals and loops, braced and unbraced.</summary>
public class ControlFlowTests : FormattingTest
{
	[Test]
	public Task If_with_a_block() => Unchanged(
		"""
		public class C
		{
		    public void M()
		    {
		        if (condition)
		        {
		            Call();
		        }
		    }
		}
		""");

	[Test]
	public Task If_without_braces_indents_its_statement() => Unchanged(
		"""
		public class C
		{
		    public void M()
		    {
		        if (condition)
		            Call();
		    }
		}
		""");

	[Test]
	public Task A_braceless_if_on_one_line_stays_on_one_line() => Unchanged(
		"""
		public class C
		{
		    public void M()
		    {
		        if (condition) Call();
		    }
		}
		""");

	[Test]
	public Task A_braceless_if_is_re_indented_once_it_is_not_preserved() => Formats(
		"""
		public class C
		{
		    public void M()
		    {
		        if (condition) Call();
		    }
		}
		""",
		"""
		public class C
		{
		    public void M()
		    {
		        if (condition)
		            Call();
		    }
		}
		""",
		editorConfig: "csharp_preserve_single_line_statements = false");

	/// <summary>
	/// A statement that starts on its header's line and continues below it stays on that line. See
	/// <see href="https://github.com/nullean/curb/issues/145">issue #145</see>.
	/// </summary>
	[Test]
	public Task A_multi_line_statement_on_its_header_line_stays_there() => Unchanged(
		"""
		public class C
		{
		    public int M(bool enabled, bool ready, int[] items)
		    {
		        if (enabled) Register(
		            1);
		        if (enabled) Run();
		        while (enabled) Register(
		            2);
		        if (enabled) Run();
		        else Register(
		            3);
		        foreach (var item in items) Register(
		            item);
		        if (enabled) if (ready)
		            Run();
		        if (enabled) return Compute(
		            4);
		        return 0;
		    }
		}
		""");

	/// <summary>
	/// The <c>else</c> after one still takes its own line: the <c>if</c> is no longer a single-line
	/// statement, so there is nothing to keep it on.
	/// </summary>
	[Test]
	public Task An_else_after_a_multi_line_statement_on_its_header_line_takes_its_own_line() => Formats(
		"""
		public class C
		{
		    public void M(bool enabled)
		    {
		        if (enabled) Register(
		            1); else Run();
		    }
		}
		""",
		// Checked against dotnet format, which writes exactly this.
		"""
		public class C
		{
		    public void M(bool enabled)
		    {
		        if (enabled) Register(
		            1);
		        else Run();
		    }
		}
		""");

	[Test]
	public Task Space_after_the_if_keyword() => Formats(
		"""
		public class C
		{
		    public void M()
		    {
		        if(condition)
		        {
		            Call();
		        }
		    }
		}
		""",
		"""
		public class C
		{
		    public void M()
		    {
		        if (condition)
		        {
		            Call();
		        }
		    }
		}
		""");

	[Test]
	public Task Else_goes_on_its_own_line() => Unchanged(
		"""
		public class C
		{
		    public void M()
		    {
		        if (condition)
		        {
		            First();
		        }
		        else
		        {
		            Second();
		        }
		    }
		}
		""");

	[Test]
	public Task Else_is_moved_off_the_closing_brace() => Formats(
		"""
		public class C
		{
		    public void M()
		    {
		        if (condition)
		        {
		            First();
		        } else
		        {
		            Second();
		        }
		    }
		}
		""",
		"""
		public class C
		{
		    public void M()
		    {
		        if (condition)
		        {
		            First();
		        }
		        else
		        {
		            Second();
		        }
		    }
		}
		""");

	[Test]
	public Task Else_if_stays_on_one_line() => Unchanged(
		"""
		public class C
		{
		    public void M()
		    {
		        if (first)
		        {
		            First();
		        }
		        else if (second)
		        {
		            Second();
		        }
		        else
		        {
		            Third();
		        }
		    }
		}
		""");

	[Test]
	public Task Nested_ifs() => Unchanged(
		"""
		public class C
		{
		    public void M()
		    {
		        if (first)
		        {
		            if (second)
		            {
		                Call();
		            }
		        }
		    }
		}
		""");

	[Test]
	public Task Unbraced_else() => Unchanged(
		"""
		public class C
		{
		    public void M()
		    {
		        if (condition)
		            First();
		        else
		            Second();
		    }
		}
		""");

	[Test]
	public Task For_loop() => Unchanged(
		"""
		public class C
		{
		    public void M()
		    {
		        for (var i = 0; i < 10; i++)
		        {
		            Call();
		        }
		    }
		}
		""");

	[Test]
	public Task For_loop_semicolons_get_a_following_space() => Formats(
		"""
		public class C
		{
		    public void M()
		    {
		        for (var i = 0;i < 10;i++)
		        {
		        }
		    }
		}
		""",
		"""
		public class C
		{
		    public void M()
		    {
		        for (var i = 0; i < 10; i++)
		        {
		        }
		    }
		}
		""");

	[Test]
	public Task For_loop_with_no_clauses() => Formats(
		"""
		public class C
		{
		    public void M()
		    {
		        for (;;)
		        {
		            Call();
		        }
		    }
		}
		""",
		// dotnet format spaces an empty header out rather than collapsing it. Roslyn has a rule of its
		// own for a header with no clauses at all; see the next case for one with only some.
		"""
		public class C
		{
		    public void M()
		    {
		        for (; ; )
		        {
		            Call();
		        }
		    }
		}
		""");

	/// <summary>
	/// An empty incrementor takes no space before <c>)</c> unless every clause is empty. See
	/// <see href="https://github.com/nullean/curb/issues/133">issue #133</see>.
	/// </summary>
	[Test]
	public Task For_loop_with_some_clauses_empty() => Formats(
		"""
		public class C
		{
		    public void M(bool a, int i)
		    {
		        for (;;) { }
		        for (; a; ) { }
		        for (var j = 0; ; j++) { }
		        for (var j = 0; j < 3; ) { }
		        for (;;i++) { }
		        for (var j = 0;;) { }
		        for (;;);
		        for (; a;) ;
		    }
		}
		""",
		"""
		public class C
		{
		    public void M(bool a, int i)
		    {
		        for (; ; ) { }
		        for (; a;) { }
		        for (var j = 0; ; j++) { }
		        for (var j = 0; j < 3;) { }
		        for (; ; i++) { }
		        for (var j = 0; ;) { }
		        for (; ; );
		        for (; a;) ;
		    }
		}
		""");

	/// <summary>
	/// The commas between initializers that are not a declaration are printed, so the file formats
	/// rather than failing verification. See
	/// <see href="https://github.com/nullean/curb/issues/150">issue #150</see>.
	/// </summary>
	[Test]
	public Task For_loop_with_two_initializers() => Formats(
		"""
		public class C
		{
		    public void M()
		    {
		        int i, j;
		        for (i = 0, j = 10; i < j; i++, j--)
		        {
		        }
		        for (i = 0,j = 10; i < j; i++,j--)
		        {
		        }
		    }
		}
		""",
		// dotnet format leaves the first header alone and gives the second's commas a space each.
		"""
		public class C
		{
		    public void M()
		    {
		        int i, j;
		        for (i = 0, j = 10; i < j; i++, j--)
		        {
		        }
		        for (i = 0, j = 10; i < j; i++, j--)
		        {
		        }
		    }
		}
		""");

	[Test]
	public Task Foreach_loop() => Unchanged(
		"""
		public class C
		{
		    public void M()
		    {
		        foreach (var item in items)
		        {
		            Call(item);
		        }
		    }
		}
		""");

	[Test]
	public Task Await_foreach_loop() => Unchanged(
		"""
		public class C
		{
		    public async Task M()
		    {
		        await foreach (var item in items)
		        {
		            Call(item);
		        }
		    }
		}
		""");

	[Test]
	public Task Foreach_over_a_tuple_deconstruction() => Unchanged(
		"""
		public class C
		{
		    public void M()
		    {
		        foreach (var (key, value) in pairs)
		        {
		            Call(key, value);
		        }
		    }
		}
		""");

	[Test]
	public Task While_loop() => Unchanged(
		"""
		public class C
		{
		    public void M()
		    {
		        while (condition)
		        {
		            Call();
		        }
		    }
		}
		""");

	[Test]
	public Task Do_while_loop() => Unchanged(
		"""
		public class C
		{
		    public void M()
		    {
		        do
		        {
		            Call();
		        }
		        while (condition);
		    }
		}
		""");

	/// <summary>
	/// The <c>while</c> of a <c>do</c> stays on the closing brace's line when the author put it there.
	/// See <see href="https://github.com/nullean/curb/issues/132">issue #132</see>.
	/// </summary>
	/// <remarks>
	/// Roslyn has no option for this gap. It asks for no line break between <c>}</c> and <c>while</c>
	/// and keeps whatever the author wrote, so both shapes are fixed points of <c>dotnet format</c>.
	/// </remarks>
	[Test]
	public Task Do_while_loop_keeps_while_on_the_closing_brace_line() => Unchanged(
		"""
		public class C
		{
		    public void M()
		    {
		        do
		        {
		            Call();
		        } while (condition);

		        do
		        { } while (condition);

		        do
		        {
		            Call();
		        } /* again */ while (condition);
		    }
		}
		""");

	[Test]
	public Task Do_while_loop_written_on_one_line_stays_there() => Unchanged(
		"""
		public class C
		{
		    public void M()
		    {
		        do { } while (condition);

		        do { Call(); } while (condition);

		        do Call(); while (condition);
		    }
		}
		""");

	[Test]
	public Task Do_while_loop_after_a_line_comment_keeps_its_own_line() => Unchanged(
		"""
		public class C
		{
		    public void M()
		    {
		        do { Call(); } // again
		        while (condition);

		        do
		            Call();
		        while (condition);
		    }
		}
		""");

	[Test]
	public Task Do_while_loop_under_deterministic_layout_puts_while_on_its_own_line() => Formats(
		"""
		public class C
		{
		    public void M()
		    {
		        do
		        {
		            Call();
		        } while (condition);
		    }
		}
		""",
		"""
		public class C
		{
		    public void M()
		    {
		        do
		        {
		            Call();
		        }
		        while (condition);
		    }
		}
		""",
		editorConfig: "max_line_length = 120");

	[Test]
	public Task Nested_loops_are_indented() => Unchanged(
		"""
		public class C
		{
		    public void M()
		    {
		        foreach (var outer in outers)
		        {
		            foreach (var inner in inners)
		            {
		                Call(outer, inner);
		            }
		        }
		    }
		}
		""");

	[Test]
	public Task Unbraced_nested_loops() => Unchanged(
		"""
		public class C
		{
		    public void M()
		    {
		        foreach (var outer in outers)
		            foreach (var inner in inners)
		                Call(outer, inner);
		    }
		}
		""");

	[Test]
	public Task Using_statement() => Unchanged(
		"""
		public class C
		{
		    public void M()
		    {
		        using (var stream = Open())
		        {
		            Call();
		        }
		    }
		}
		""");

	[Test]
	public Task Using_declaration() => Unchanged(
		"""
		public class C
		{
		    public void M()
		    {
		        using var stream = Open();
		        Call();
		    }
		}
		""");

	[Test]
	public Task Await_using_declaration() => Unchanged(
		"""
		public class C
		{
		    public async Task M()
		    {
		        await using var stream = Open();
		        Call();
		    }
		}
		""");

	[Test]
	public Task Chained_using_statements_are_split_even_when_the_author_joined_them() => Formats(
		// A chain is a sequence: both usings guard the same block and neither is the body of the other, so
		// each gets its own line at the same indent. Nothing is configured here, which is the case that
		// matters — joining is a rewrite, and an absent key is nobody having asked for one.
		"""
		public class C
		{
		    public void M()
		    {
		        using (var first = Open()) using (var second = Open())
		        {
		            Call();
		        }
		    }
		}
		""",
		"""
		public class C
		{
		    public void M()
		    {
		        using (var first = Open())
		        using (var second = Open())
		        {
		            Call();
		        }
		    }
		}
		""");

	[Test]
	public Task Chained_using_statements_the_author_split_stay_split() => Unchanged(
		"""
		public class C
		{
		    public void M()
		    {
		        using (var first = Open())
		        using (var second = Open())
		        {
		            Call();
		        }
		    }
		}
		""");

	[Test]
	public Task Chained_using_statements_stay_joined_when_the_option_is_written_out() => Unchanged(
		// The one way back to the old behaviour. Splitting is what an absent key does, not an opinion Curb
		// holds over the author's head: somebody who writes the option out and turns it on has asked for
		// their own line to be kept, and gets it. Without this .editorconfig the same source splits, which
		// is the case above.
		"""
		public class C
		{
		    public void M()
		    {
		        using (var first = Open()) using (var second = Open())
		        {
		            Call();
		        }
		    }
		}
		""",
		editorConfig: """
		[*.cs]
		csharp_preserve_single_line_statements = true
		""");

	[Test]
	public Task Three_chained_usings_split_together() => Formats(
		"""
		public class C
		{
		    public void M()
		    {
		        using (var a = Open()) using (var b = Open()) using (var c = Open())
		        {
		            Call();
		        }
		    }
		}
		""",
		"""
		public class C
		{
		    public void M()
		    {
		        using (var a = Open())
		        using (var b = Open())
		        using (var c = Open())
		        {
		            Call();
		        }
		    }
		}
		""");

	[Test]
	public Task Lock_statement() => Unchanged(
		"""
		public class C
		{
		    public void M()
		    {
		        lock (gate)
		        {
		            Call();
		        }
		    }
		}
		""");

	[Test]
	public Task Condition_breaks_when_it_does_not_fit() => Formats(
		// csharp_wrap_chained_binary_expressions is not set, so this is the ordinary per-operator
		// path rather than the chain flattener — the operator trails the line it follows rather
		// than leading the next, since csharp_wrap_before_binary_opsign only applies once that key
		// is breaking a chain. Every operand still lands one level in from `if` (issue #11).
		"""
		public class C
		{
		    public void M()
		    {
		        if (alphaCondition && betaCondition && gammaCondition)
		        {
		            Call();
		        }
		    }
		}
		""",
		"""
		public class C
		{
		    public void M()
		    {
		        if (
		            alphaCondition &&
		            betaCondition &&
		            gammaCondition
		        )
		        {
		            Call();
		        }
		    }
		}
		""",
		editorConfig: "max_line_length = 40");
	[Test]
	public Task A_condition_the_author_broke_keeps_its_parentheses_hugging() => Formats(
		// The break opportunities just inside the parentheses were wrong. A condition holding the
		// author's own breaks makes the header's group broken, and every soft line in it goes with
		// it, so an ordinary wrapped `if` came out as `if (` / condition / `)` on four lines.
		//
		// dotnet format keeps the condition on the keyword's line and the `)` against its last
		// operand. This was worth roughly a fifth of all the lines Curb changed on roslyn.
		"""
		public class C
		{
		    bool M(Decl decl)
		    {
		        if (decl.ExplicitInterfaceSpecifier != null &&
		            !decl.ParameterList.IsMissing &&
		            !decl.ParameterList.CloseParenToken.IsMissing)
		        {
		            return true;
		        }
		        return false;
		    }
		}
		""",
		"""
		public class C
		{
		    bool M(Decl decl)
		    {
		        if (decl.ExplicitInterfaceSpecifier != null &&
		            !decl.ParameterList.IsMissing &&
		            !decl.ParameterList.CloseParenToken.IsMissing)
		        {
		            return true;
		        }
		        return false;
		    }
		}
		""");

	[Test]
	public Task A_condition_the_author_opened_after_the_parenthesis_keeps_that_too() => Formats(
		// The other side of the same rule: where the break *is* just inside the parenthesis, it is
		// the author's and stays. Asking about exactly the positions the printer reproduces is what
		// keeps this stable across runs.
		"""
		public class C
		{
		    bool M(Decl decl)
		    {
		        if (
		            decl.ExplicitInterfaceSpecifier != null &&
		            !decl.ParameterList.IsMissing
		        )
		        {
		            return true;
		        }
		        return false;
		    }
		}
		""",
		"""
		public class C
		{
		    bool M(Decl decl)
		    {
		        if (
		            decl.ExplicitInterfaceSpecifier != null &&
		            !decl.ParameterList.IsMissing
		        )
		        {
		            return true;
		        }
		        return false;
		    }
		}
		""");

}
