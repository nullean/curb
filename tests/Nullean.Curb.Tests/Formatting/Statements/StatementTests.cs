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

	// ---- an empty try, catch or finally is always `{ }` --------------------------------------

	/// <summary>
	/// Curb's one unconditional opinion in this family: an empty <c>try</c>/<c>catch</c>/<c>finally</c>
	/// is always <c>{ }</c>, whatever the source had and whatever the preserve options say. Unlike a
	/// non-empty clause (below), this is <em>not</em> matching dotnet format — dotnet format is lazy
	/// about all three in every direction, so there is nothing of its to match, and <c>{ }</c> reads
	/// better than the alternative either way.
	/// See <see href="https://github.com/nullean/curb/issues/25">issue #25</see>.
	/// </summary>
	[Test]
	public Task An_empty_try_the_author_expanded_still_collapses() => Formats(
		"""
		public class C
		{
		    public void M()
		    {
		        try
		        {
		        }
		        finally
		        {
		            Cleanup();
		        }
		    }
		}
		""",
		"""
		public class C
		{
		    public void M()
		    {
		        try { }
		        finally
		        {
		            Cleanup();
		        }
		    }
		}
		""");

	[Test]
	public Task An_empty_catch_the_author_expanded_still_collapses() => Formats(
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
		        catch { }
		    }
		}
		""");

	[Test]
	public Task An_empty_finally_the_author_expanded_still_collapses() => Formats(
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
		        finally { }
		    }
		}
		""");

	[Test]
	public Task An_empty_catch_with_a_declaration_still_collapses() => Formats(
		"""
		public class C
		{
		    public void M()
		    {
		        try
		        {
		            Call();
		        }
		        catch (Exception e)
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
		        try
		        {
		            Call();
		        }
		        catch (Exception e) { }
		    }
		}
		""");

	[Test]
	public Task An_empty_catch_with_a_declaration_and_filter_still_collapses() => Formats(
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
		        catch (IOException ex) when (ex.HResult != 0) { }
		    }
		}
		""");

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
		    }
		}
		""",
		editorConfig: """
		csharp_preserve_single_line_blocks = false
		csharp_preserve_single_line_statements = false
		""");

	// A non-empty catch or finally is untouched by this: PrintStatementBody's alwaysJoinsEmpty is
	// ignored once the block is not empty, so behaviour there is exactly what it was before.

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
