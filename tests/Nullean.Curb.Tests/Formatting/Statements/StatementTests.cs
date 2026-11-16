namespace Nullean.Curb.Tests.Formatting.Statements;

/// <summary>Try/catch, local declarations, jumps and the remaining statement forms.</summary>
public class StatementTests : FormattingTest
{
	// ---- try / catch / finally ----------------------------------------------------------------

	[Test]
	public Task Try_catch() => Unchanged(
		"""
		public class C
		{
		    public void M()
		    {
		        try
		        {
		            Call();
		        }
		        catch (Exception ex)
		        {
		            Handle(ex);
		        }
		    }
		}
		""");

	[Test]
	public Task Catch_moves_off_the_closing_brace() => Formats(
		"""
		public class C
		{
		    public void M()
		    {
		        try
		        {
		            Call();
		        } catch (Exception ex)
		        {
		            Handle(ex);
		        }
		    }
		}
		""",
		"""
		public class C
		{
		    public void M()
		    {
		        try
		        {
		            Call();
		        }
		        catch (Exception ex)
		        {
		            Handle(ex);
		        }
		    }
		}
		""");

	[Test]
	public Task Try_finally() => Unchanged(
		"""
		public class C
		{
		    public void M()
		    {
		        try
		        {
		            Call();
		        }
		        finally
		        {
		            Cleanup();
		        }
		    }
		}
		""");

	[Test]
	public Task Try_catch_finally() => Unchanged(
		"""
		public class C
		{
		    public void M()
		    {
		        try
		        {
		            Call();
		        }
		        catch (Exception ex)
		        {
		            Handle(ex);
		        }
		        finally
		        {
		            Cleanup();
		        }
		    }
		}
		""");

	[Test]
	public Task Several_catch_clauses() => Unchanged(
		"""
		public class C
		{
		    public void M()
		    {
		        try
		        {
		            Call();
		        }
		        catch (IOException ex)
		        {
		            Handle(ex);
		        }
		        catch (Exception ex)
		        {
		            Handle(ex);
		        }
		    }
		}
		""");

	[Test]
	public Task Catch_without_a_declaration() => Unchanged(
		"""
		public class C
		{
		    public void M()
		    {
		        try
		        {
		            Call();
		        }
		        catch
		        {
		            Handle();
		        }
		    }
		}
		""");

	[Test]
	public Task Catch_with_a_type_but_no_name() => Unchanged(
		"""
		public class C
		{
		    public void M()
		    {
		        try
		        {
		            Call();
		        }
		        catch (IOException)
		        {
		            Handle();
		        }
		    }
		}
		""");

	[Test]
	public Task Catch_with_a_when_filter() => Unchanged(
		"""
		public class C
		{
		    public void M()
		    {
		        try
		        {
		            Call();
		        }
		        catch (IOException ex) when (ex.HResult != 0)
		        {
		            Handle(ex);
		        }
		    }
		}
		""");

	[Test]
	public Task Nested_try() => Unchanged(
		"""
		public class C
		{
		    public void M()
		    {
		        try
		        {
		            try
		            {
		                Call();
		            }
		            catch (Exception ex)
		            {
		                Handle(ex);
		            }
		        }
		        finally
		        {
		            Cleanup();
		        }
		    }
		}
		""");

	// ---- an empty try, catch or finally ------------------------------------------------------

	/// <summary>
	/// With no width, an empty <c>try</c>/<c>catch</c>/<c>finally</c> block is laid out as the author wrote it, as
	/// dotnet format leaves it: opened out stays opened out, and <c>{ }</c> on a line of its own stays there. Curb
	/// joined both to their keyword as <c>{ }</c> (issue #364). With a width, the block is always <c>{ }</c> on its
	/// keyword's line, which dotnet format keeps too.
	/// </summary>
	[Test]
	public Task Empty_try_catch_and_finally_blocks_the_author_opened_out_stay_opened_out() => Unchanged(OpenedOutEmptyClauses);

	[Test]
	public Task Empty_try_catch_and_finally_blocks_join_their_keyword_at_a_width() => Formats(
		OpenedOutEmptyClauses,
		"""
		public class C
		{
		    public void M()
		    {
		        try { }
		        finally { }
		        try
		        {
		            Call();
		        }
		        catch (IOException ex) when (ex.HResult != 0) { }
		        catch (Exception e) { }
		        catch { }
		        finally { }
		        try { }
		        catch (Exception e) { }
		        finally { }
		    }
		}
		""",
		editorConfig: "max_line_length = 120");

	private const string OpenedOutEmptyClauses = """
		public class C
		{
		    public void M()
		    {
		        try
		        {
		        }
		        finally
		        {
		        }
		        try
		        {
		            Call();
		        }
		        catch (IOException ex) when (ex.HResult != 0)
		        {
		        }
		        catch (Exception e)
		        {
		        }
		        catch
		        {
		        }
		        finally
		        {
		        }
		        try
		        { }
		        catch (Exception e)
		        { }
		        finally
		        { }
		    }
		}
		""";

	// dotnet format opens out a `{ }` on a line of its own under csharp_preserve_single_line_blocks = false.
	[Test]
	public Task An_empty_clause_block_below_its_keyword_opens_out_when_blocks_are_not_preserved() => Formats(
		"""
		public class C
		{
		    public void M()
		    {
		        try
		        { }
		        catch
		        { }
		    }
		}
		""",
		"""
		public class C
		{
		    public void M()
		    {
		        try
		        {
		        }
		        catch
		        {
		        }
		    }
		}
		""",
		editorConfig: "csharp_preserve_single_line_blocks = false");

	// An empty block opened on its keyword's line takes its brace down, as a non-empty one does.
	[Test]
	public Task An_empty_clause_block_opened_on_its_keyword_line_moves_its_brace_down() => Formats(
		"""
		public class C
		{
		    public void M()
		    {
		        try {
		        }
		        catch (Exception e) {
		        }
		        finally {
		        }
		    }
		}
		""",
		"""
		public class C
		{
		    public void M()
		    {
		        try
		        {
		        }
		        catch (Exception e)
		        {
		        }
		        finally
		        {
		        }
		    }
		}
		""");

	// dotnet format moves `try { }` off its keyword with statements off but leaves `catch { }` and `finally { }`.
	[Test]
	public Task An_empty_try_stays_off_its_keyword_when_statements_are_not_preserved() => Formats(
		"""
		public class C
		{
		    public void M()
		    {
		        try { }
		        catch { }
		        finally { }
		    }
		}
		""",
		"""
		public class C
		{
		    public void M()
		    {
		        try
		        { }
		        catch { }
		        finally { }
		    }
		}
		""",
		editorConfig: "csharp_preserve_single_line_statements = false");

	// One the author wrote on its keyword's line stays there even with both preserve options off, as dotnet
	// format leaves it (issue #25).
	[Test]
	public Task An_empty_catch_stays_collapsed_with_both_preserve_options_off() => Unchanged(
		"""
		public class C
		{
		    public void M()
		    {
		        try
		        {
		            Call();
		        }
		        catch { }
		        finally { }
		    }
		}
		""",
		editorConfig: """
		csharp_preserve_single_line_blocks = false
		csharp_preserve_single_line_statements = false
		""");

	// ---- local declarations -------------------------------------------------------------------

	[Test]
	public Task Var_declaration() => Unchanged(
		"""
		public class C
		{
		    public void M()
		    {
		        var value = 1;
		    }
		}
		""");

	[Test]
	public Task Explicitly_typed_declaration() => Unchanged(
		"""
		public class C
		{
		    public void M()
		    {
		        int value = 1;
		    }
		}
		""");

	[Test]
	public Task Declaration_without_an_initializer() => Unchanged(
		"""
		public class C
		{
		    public void M()
		    {
		        int value;
		    }
		}
		""");

	[Test]
	public Task Several_declarators() => Formats(
		"""
		public class C
		{
		    public void M()
		    {
		        int first=1,second=2;
		    }
		}
		""",
		"""
		public class C
		{
		    public void M()
		    {
		        int first = 1, second = 2;
		    }
		}
		""");

	[Test]
	public Task Const_local() => Unchanged(
		"""
		public class C
		{
		    public void M()
		    {
		        const int Value = 1;
		    }
		}
		""");

	[Test]
	public Task Deconstruction_declaration() => Unchanged(
		"""
		public class C
		{
		    public void M()
		    {
		        var (first, second) = pair;
		    }
		}
		""");

	[Test]
	public Task Nullable_local() => Unchanged(
		"""
		public class C
		{
		    public void M()
		    {
		        string? value = null;
		    }
		}
		""");

	[Test]
	public Task Generic_local() => Unchanged(
		"""
		public class C
		{
		    public void M()
		    {
		        List<string> values = new();
		    }
		}
		""");

	// ---- jumps and the rest --------------------------------------------------------------------

	[Test]
	public Task Return_with_a_value() => Unchanged(
		"""
		public class C
		{
		    public int M()
		    {
		        return 1;
		    }
		}
		""");

	[Test]
	public Task Bare_return() => Unchanged(
		"""
		public class C
		{
		    public void M()
		    {
		        return;
		    }
		}
		""");

	[Test]
	public Task Throw_statement() => Unchanged(
		"""
		public class C
		{
		    public void M()
		    {
		        throw new InvalidOperationException("no");
		    }
		}
		""");

	[Test]
	public Task Rethrow() => Unchanged(
		"""
		public class C
		{
		    public void M()
		    {
		        try
		        {
		            Call();
		        }
		        catch (Exception)
		        {
		            throw;
		        }
		    }
		}
		""");

	[Test]
	public Task Break_and_continue() => Unchanged(
		"""
		public class C
		{
		    public void M()
		    {
		        foreach (var item in items)
		        {
		            if (first)
		            {
		                continue;
		            }

		            break;
		        }
		    }
		}
		""");

	[Test]
	public Task Yield_return_and_break() => Unchanged(
		"""
		public class C
		{
		    public IEnumerable<int> M()
		    {
		        yield return 1;
		        yield break;
		    }
		}
		""");

	[Test]
	public Task Empty_statement() => Unchanged(
		"""
		public class C
		{
		    public void M()
		    {
		        ;
		    }
		}
		""");

	[Test]
	public Task Expression_statement() => Unchanged(
		"""
		public class C
		{
		    public void M()
		    {
		        Call();
		    }
		}
		""");

	[Test]
	public Task Assignment_gets_spaces_around_the_equals() => Formats(
		"""
		public class C
		{
		    public void M()
		    {
		        value=1;
		    }
		}
		""",
		"""
		public class C
		{
		    public void M()
		    {
		        value = 1;
		    }
		}
		""");

	[Test]
	public Task Compound_assignment() => Unchanged(
		"""
		public class C
		{
		    public void M()
		    {
		        value += 1;
		        other ??= 2;
		    }
		}
		""");

	[Test]
	public Task Nested_blocks() => Unchanged(
		"""
		public class C
		{
		    public void M()
		    {
		        {
		            Call();
		        }
		    }
		}
		""");

	[Test]
	public Task Statements_are_re_indented() => Formats(
		"""
		public class C
		{
		    public void M()
		    {
		            First();
		    Second();
		    }
		}
		""",
		"""
		public class C
		{
		    public void M()
		    {
		        First();
		        Second();
		    }
		}
		""");
}
