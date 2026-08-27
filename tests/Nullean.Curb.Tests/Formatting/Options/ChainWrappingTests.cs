namespace Nullean.Curb.Tests.Formatting.Options;

/// <summary>
/// <c>csharp_wrap_chained_method_calls</c> and <c>csharp_max_chained_method_calls_on_line</c>.
/// </summary>
/// <remarks>
/// The chain printer already breaks a chain of three or more calls at the dots once it does not fit
/// under <c>max_line_length</c>, with no key required — that is Curb's own opinion, not an option.
/// <c>csharp_wrap_chained_method_calls = chop_if_long</c> only confirms it, so it never changes output
/// on its own. <c>csharp_max_chained_method_calls_on_line</c> is the one that does: it forces a break
/// once a chain carries more calls than the limit, whether or not the joined chain would have fit.
/// </remarks>
public class ChainWrappingTests : FormattingTest
{
	// ---- csharp_wrap_chained_method_calls: confirms the default, changes nothing ----------------------

	[Test]
	public Task Chop_if_long_binds_without_changing_a_chain_that_breaks() => Formats(
		"""
		public class C
		{
		    void M()
		    {
		        instance.First().Second().Third().Fourth();
		    }
		}
		""",
		"""
		public class C
		{
		    void M()
		    {
		        instance.First()
		            .Second()
		            .Third()
		            .Fourth();
		    }
		}
		""",
		editorConfig: "max_line_length = 40\ncsharp_wrap_chained_method_calls = chop_if_long");

	[Test]
	public Task Chop_if_long_binds_without_changing_a_chain_that_fits() => Unchanged(
		"""
		public class C
		{
		    void M() => foo.Bar().Baz().Qux();
		}
		""",
		editorConfig: "max_line_length = 120\ncsharp_wrap_chained_method_calls = chop_if_long");

	// ---- csharp_max_chained_method_calls_on_line: a real, count-based force -----------------------------

	[Test]
	public Task A_chain_over_the_limit_breaks_even_though_it_fits() => Formats(
		"""
		public class C
		{
		    void M()
		    {
		        var deduped = allUrls.GroupBy(u => u.Location).Select(g => g.First()).ToList();
		    }
		}
		""",
		"""
		public class C
		{
		    void M()
		    {
		        var deduped = allUrls.GroupBy(u => u.Location)
		            .Select(g => g.First())
		            .ToList();
		    }
		}
		""",
		editorConfig: "max_line_length = 120\ncsharp_max_chained_method_calls_on_line = 2");

	[Test]
	public Task A_chain_at_the_limit_is_left_alone() => Unchanged(
		// Breaking only, the same rule the initializer and argument limits hold to: a chain within the
		// limit is never closed up by it, and this one already fits under the width.
		"""
		public class C
		{
		    void M()
		    {
		        var deduped = allUrls.GroupBy(u => u.Location).Select(g => g.First()).ToList();
		    }
		}
		""",
		editorConfig: "max_line_length = 120\ncsharp_max_chained_method_calls_on_line = 3");

	[Test]
	public Task A_chain_in_an_argument_is_left_to_the_call() => Unchanged(
		// Where a construct with its own break opportunity is measured by whatever encloses it — an
		// argument list here — forcing a break the source does not have puts it a level out.
		"""
		public class C
		{
		    void M()
		    {
		        Send(allUrls.GroupBy(u => u.Location).Select(g => g.First()).ToList());
		    }
		}
		""",
		editorConfig: "max_line_length = 120\ncsharp_max_chained_method_calls_on_line = 2");

	[Test]
	public Task Two_or_fewer_links_never_break_even_under_the_limit() => Unchanged(
		// The chain printer only engages at three links or more — two calls read fine on one line, and
		// the count limit narrows an existing break, it does not create the break opportunity itself.
		"""
		public class C
		{
		    void M() => foo.Bar().Baz();
		}
		""",
		editorConfig: "max_line_length = 120\ncsharp_max_chained_method_calls_on_line = 1");

	// ---- a receiver that is itself a call or an indexer still chains --------------------------------

	[Test]
	public Task A_bare_call_receiver_still_breaks_at_the_dots() => Formats(
		// GetFactory() has no leading identifier to collect a link from, so the walk that finds a
		// chain's receiver used to see its own argument list as unresolved and give up on the whole
		// expression — the chain never engaged at all, and the line broke wherever an argument list
		// happened to overflow instead of at the dots.
		"""
		public class C
		{
		    void M()
		    {
		        var result = GetFactory().AddDependency("core").AddDependency("shared").Build();
		    }
		}
		""",
		"""
		public class C
		{
		    void M()
		    {
		        var result = GetFactory()
		            .AddDependency("core")
		            .AddDependency("shared")
		            .Build();
		    }
		}
		""",
		editorConfig: "max_line_length = 60");

	[Test]
	public Task An_indexer_receiver_still_breaks_at_the_dots() => Formats(
		"""
		public class C
		{
		    void M()
		    {
		        var result = factories[0].AddDependency("core").AddDependency("shared").Build();
		    }
		}
		""",
		"""
		public class C
		{
		    void M()
		    {
		        var result = factories[0]
		            .AddDependency("core")
		            .AddDependency("shared")
		            .Build();
		    }
		}
		""",
		editorConfig: "max_line_length = 60");
}
