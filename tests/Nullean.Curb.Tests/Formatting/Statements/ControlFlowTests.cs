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
		// dotnet format spaces an empty header out rather than collapsing it, and the two
		// csharp_space_*_semicolon_in_for_statement options apply whether or not a clause follows.
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
	public Task Chained_using_statements_stay_on_one_line() => Unchanged(
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
