namespace Nullean.Kerf.Tests.Formatting.Options;

/// <summary>
/// <c>csharp_preferred_modifier_order</c>, code style rule IDE0036.
/// </summary>
/// <remarks>
/// <para>
/// A real .NET key that <c>dotnet format style</c> already implements. What Kerf adds is doing it
/// without a compilation: the rule is decidable from the token list alone, so it runs in the same
/// pass as everything else on a bare folder rather than costing a workspace load.
/// </para>
/// <para>
/// Off unless the key is present. It carries a documented default value, so binding it whether or not
/// anyone wrote it would reorder a repository that never asked — the same reason using sorting treats
/// the presence of its key as the ask.
/// </para>
/// <para>
/// This permutes tokens, so it declares the span to the content verifier, which compares that region
/// as a multiset and stays strict everywhere else. The token comparer handles it without spans at
/// all: a mismatch where both sides are modifiers means a permuted run, and the two runs are compared
/// as multisets from where they start.
/// </para>
/// </remarks>
public class ModifierOrderTests : FormattingTest
{
	/// <summary>The value Microsoft documents for the key, which is what most repositories set.</summary>
	private const string Preferred =
		"csharp_preferred_modifier_order = public,private,protected,internal,file,static,extern,new,"
		+ "virtual,abstract,sealed,override,readonly,unsafe,required,volatile,async";

	// ---- the default ------------------------------------------------------------------------------

	[Test]
	public Task Modifiers_are_left_alone_without_the_key() => Unchanged(
		"""
		public class C
		{
		    static public void M()
		    {
		    }
		}
		""");

	// ---- ordering ---------------------------------------------------------------------------------

	[Test]
	public Task Accessibility_comes_before_static() => WithAndWithout(
		"""
		public class C
		{
		    static public void M()
		    {
		    }
		}
		""",
		"""
		public class C
		{
		    static public void M()
		    {
		    }
		}
		""",
		"""
		public class C
		{
		    public static void M()
		    {
		    }
		}
		""",
		Preferred);

	[Test]
	public Task Three_modifiers_all_find_their_place() => Formats(
		"""
		public class C
		{
		    readonly private static int _x = 1;
		}
		""",
		"""
		public class C
		{
		    private static readonly int _x = 1;
		}
		""",
		editorConfig: Preferred);

	[Test]
	public Task Async_sorts_last_because_the_configuration_puts_it_last() => Formats(
		"""
		public class C
		{
		    async static Task M() => Task.CompletedTask;
		}
		""",
		"""
		public class C
		{
		    static async Task M() => Task.CompletedTask;
		}
		""",
		editorConfig: Preferred);

	[Test]
	public Task Modifiers_already_in_order_are_untouched() => Unchanged(
		"""
		public class C
		{
		    public static void M()
		    {
		    }
		}
		""",
		editorConfig: Preferred);

	[Test]
	public Task A_single_modifier_has_nothing_to_order() => Unchanged(
		"""
		public class C
		{
		    public void M()
		    {
		    }
		}
		""",
		editorConfig: Preferred);

	[Test]
	public Task A_modifier_the_configuration_omits_sorts_after_the_ones_it_names() => Formats(
		// `partial` is absent from the documented default value, so it has no rank and lands after
		// everything that does — rather than being dropped or moved arbitrarily.
		"""
		public class C
		{
		    partial static public void M();
		}
		""",
		"""
		public class C
		{
		    public static partial void M();
		}
		""",
		editorConfig: Preferred);

	[Test]
	public Task A_shorter_configuration_only_governs_what_it_names() => Formats(
		"""
		public class C
		{
		    static public void M()
		    {
		    }
		}
		""",
		"""
		public class C
		{
		    public static void M()
		    {
		    }
		}
		""",
		editorConfig: "csharp_preferred_modifier_order = public,static");

	[Test]
	public Task A_severity_suffix_belongs_to_the_rule_not_to_the_last_modifier() => Formats(
		// `public,...,async:error` is how code style options are written, and the corpus writes it
		// that way. Left on, the suffix makes `async` match nothing and sort as though it were
		// unnamed — silent, and wrong for every repository that states a severity.
		// The check has to discriminate. Pairing the suffixed modifier with a named one would pass
		// either way, since it is the last named one and sorts last regardless; pairing it with an
		// *unnamed* one is what separates "ranked last" from "not ranked at all".
		"""
		public class C
		{
		    unsafe async Task M() => Task.CompletedTask;
		}
		""",
		"""
		public class C
		{
		    async unsafe Task M() => Task.CompletedTask;
		}
		""",
		editorConfig: "csharp_preferred_modifier_order = public,async:error");

	// ---- comments ---------------------------------------------------------------------------------

	[Test]
	public Task A_doc_comment_stays_in_front_of_the_reordered_modifiers() => Formats(
		// The declaration's doc comment is the leading trivia of whichever modifier came first, so it
		// has to be emitted before the sort rather than travelling with that keyword. Getting this
		// wrong put the comment mid-declaration; a `///` runs to end of line, so it swallowed the rest
		// of the member and the re-parse check caught it.
		"""
		public class C
		{
		    /// <summary>Does a thing.</summary>
		    static public void M()
		    {
		    }
		}
		""",
		"""
		public class C
		{
		    /// <summary>Does a thing.</summary>
		    public static void M()
		    {
		    }
		}
		""",
		editorConfig: Preferred);

	[Test]
	public Task A_plain_comment_above_the_member_stays_in_front_too() => Formats(
		"""
		public class C
		{
		    // why this exists
		    static internal void M()
		    {
		    }
		}
		""",
		"""
		public class C
		{
		    // why this exists
		    internal static void M()
		    {
		    }
		}
		""",
		editorConfig: Preferred);

	[Test]
	public Task A_comment_between_modifiers_stands_the_reorder_down() => Unchanged(
		// Moving the keywords takes their trivia along, so a comment written between two of them would
		// end up describing the wrong one. Not worth a reorder, so this declaration is left as written.
		"""
		public class C
		{
		    static /* deliberately */ public void M()
		    {
		    }
		}
		""",
		editorConfig: Preferred);

	// ---- reach ------------------------------------------------------------------------------------

	[Test]
	public Task It_reaches_types_fields_and_local_functions_alike() => Formats(
		"""
		static internal class C
		{
		    readonly static int Field = 1;

		    public static void M()
		    {
		        async static Task Local() => Task.CompletedTask;
		    }
		}
		""",
		"""
		internal static class C
		{
		    static readonly int Field = 1;

		    public static void M()
		    {
		        static async Task Local() => Task.CompletedTask;
		    }
		}
		""",
		editorConfig: Preferred);
}
