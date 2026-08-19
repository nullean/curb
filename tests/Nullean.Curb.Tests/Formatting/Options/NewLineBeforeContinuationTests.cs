namespace Nullean.Curb.Tests.Formatting.Options;

/// <summary>
/// <c>csharp_new_line_before_else</c>, <c>_catch</c> and <c>_finally</c> — whether a continuation
/// keyword gets a line of its own or joins the brace that closed the clause before it.
/// </summary>
/// <remarks>
/// All three default to true. They only ever pull a keyword up onto a closing brace: after a
/// braceless <c>if</c> there is no brace to join, so <c>else</c> keeps its own line whatever the
/// option says — which is what dotnet format does.
/// </remarks>
public class NewLineBeforeContinuationTests : FormattingTest
{
	// ---- else ------------------------------------------------------------------------------------

	[Test]
	public Task Else_gets_its_own_line_by_default() => Formats(
		"""
		public class C
		{
		    public void M()
		    {
		        if (a)
		        {
		        } else
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
		        if (a)
		        {
		        }
		        else
		        {
		        }
		    }
		}
		""");

	[Test]
	public Task Else_joins_the_closing_brace_when_disabled() => Formats(
		"""
		public class C
		{
		    public void M()
		    {
		        if (a)
		        {
		        }
		        else
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
		        if (a)
		        {
		        } else
		        {
		        }
		    }
		}
		""",
		editorConfig: "csharp_new_line_before_else = false");

	[Test]
	public Task Else_if_joins_the_closing_brace_when_disabled() => Formats(
		"""
		public class C
		{
		    public void M()
		    {
		        if (a)
		        {
		        }
		        else if (b)
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
		        if (a)
		        {
		        } else if (b)
		        {
		        }
		    }
		}
		""",
		editorConfig: "csharp_new_line_before_else = false");

	[Test]
	public Task A_braceless_else_keeps_its_own_line_even_when_disabled() => Unchanged(
		"""
		public class C
		{
		    public void M()
		    {
		        if (a)
		            One();
		        else
		            Two();
		    }
		}
		""",
		editorConfig: "csharp_new_line_before_else = false");

	[Test]
	public Task Disabling_else_does_not_move_the_opening_brace() => Formats(
		"""
		public class C
		{
		    public void M()
		    {
		        if (a) {
		        } else {
		        }
		    }
		}
		""",
		"""
		public class C
		{
		    public void M()
		    {
		        if (a)
		        {
		        } else
		        {
		        }
		    }
		}
		""",
		editorConfig: "csharp_new_line_before_else = false");

	[Test]
	public Task Else_and_the_brace_can_both_be_pulled_up() => Formats(
		"""
		public class C
		{
		    public void M()
		    {
		        if (a)
		        {
		        }
		        else
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
		        if (a) {
		        } else {
		        }
		    }
		}
		""",
		editorConfig: """
		csharp_new_line_before_else = false
		csharp_new_line_before_open_brace = types,methods
		""");

	// ---- catch -----------------------------------------------------------------------------------

	[Test]
	public Task Catch_gets_its_own_line_by_default() => Formats(
		"""
		public class C
		{
		    public void M()
		    {
		        try
		        {
		        } catch (Exception e)
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
		        }
		        catch (Exception e)
		        {
		        }
		    }
		}
		""");

	[Test]
	public Task Catch_joins_the_closing_brace_when_disabled() => Formats(
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
		        } catch (Exception e)
		        {
		        }
		    }
		}
		""",
		editorConfig: "csharp_new_line_before_catch = false");

	[Test]
	public Task Every_catch_in_a_chain_joins() => Formats(
		"""
		public class C
		{
		    public void M()
		    {
		        try
		        {
		        }
		        catch (ArgumentException)
		        {
		        }
		        catch (Exception e) when (e.Message.Length > 0)
		        {
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
		        } catch (ArgumentException)
		        {
		        } catch (Exception e) when (e.Message.Length > 0)
		        {
		        } catch
		        {
		        }
		    }
		}
		""",
		editorConfig: "csharp_new_line_before_catch = false");

	// ---- finally ---------------------------------------------------------------------------------

	[Test]
	public Task Finally_gets_its_own_line_by_default() => Formats(
		"""
		public class C
		{
		    public void M()
		    {
		        try
		        {
		        } finally
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
		        }
		        finally
		        {
		        }
		    }
		}
		""");

	[Test]
	public Task Finally_joins_the_closing_brace_when_disabled() => Formats(
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
		        } finally
		        {
		        }
		    }
		}
		""",
		editorConfig: "csharp_new_line_before_finally = false");

	[Test]
	public Task Finally_joins_the_last_catch_rather_than_the_try() => Formats(
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
		        } finally
		        {
		        }
		    }
		}
		""",
		editorConfig: "csharp_new_line_before_finally = false");

	// ---- the three together ----------------------------------------------------------------------

	[Test]
	public Task All_three_disabled_gives_the_compact_shape() => Formats(
		"""
		public class C
		{
		    public void M()
		    {
		        try
		        {
		            if (a)
		            {
		            }
		            else
		            {
		            }
		        }
		        catch (Exception e)
		        {
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
		            if (a)
		            {
		            } else
		            {
		            }
		        } catch (Exception e)
		        {
		        } finally
		        {
		        }
		    }
		}
		""",
		editorConfig: """
		csharp_new_line_before_else = false
		csharp_new_line_before_catch = false
		csharp_new_line_before_finally = false
		""");

	[Test]
	public Task An_unrecognised_value_falls_back_to_the_default() => Unchanged(
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
		        }
		    }
		}
		""",
		editorConfig: "csharp_new_line_before_finally = no");
}
