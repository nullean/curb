namespace Nullean.Curb.Tests.Formatting.Options;

/// <summary>
/// The four keys that decide where a clause sits relative to the declaration it belongs to.
/// </summary>
/// <remarks>
/// <para>
/// Three of them — <c>csharp_wrap_before_first_type_parameter_constraint</c>,
/// <c>csharp_wrap_before_extends_colon</c> and
/// <c>csharp_place_constructor_initializer_on_same_line</c> — govern clauses that are otherwise the
/// author's to place. Rider gives all three a line of their own. The fourth,
/// <c>csharp_wrap_before_arrow_with_expressions</c>, moves the <c>=&gt;</c> to the far side of a break
/// the printer was making anyway.
/// </para>
/// <para>
/// Free ground, measured in both directions: <c>dotnet format</c> neither joins a clause the author
/// broke nor breaks one they joined, so either rendering is a fixed point.
/// </para>
/// <para>
/// Set, the first three force their placement unconditionally. Unset, they read only a break the
/// author owns and Curb reproduces, which is why no run can change the answer. The arrow's is
/// width-driven but only relocates a break already decided on this run, and prints identically where
/// the body fits. That is what separates them from the single-line blank-line family, which had to be
/// dropped for asking a question this run's own output changed the answer to.
/// </para>
/// </remarks>
public class WrapBeforeClauseTests : FormattingTest
{
	private const string Constraint = "csharp_wrap_before_first_type_parameter_constraint = true";
	private const string Extends = "csharp_wrap_before_extends_colon = true";

	// ---- type parameter constraints ------------------------------------------------------------------

	[Test]
	public Task A_constraint_the_author_broke_stays_broken() => Unchanged(
		"""
		public class C
		{
		    public T M<T>()
		        where T : class
		    {
		        return default;
		    }
		}
		""");

	[Test]
	public Task Setting_the_key_to_false_joins_a_constraint_the_author_broke() => Formats(
		"""
		public class C
		{
		    public T M<T>()
		        where T : class
		    {
		        return default;
		    }
		}
		""",
		"""
		public class C
		{
		    public T M<T>() where T : class
		    {
		        return default;
		    }
		}
		""",
		editorConfig: "csharp_wrap_before_first_type_parameter_constraint = false");

	[Test]
	public Task A_width_decides_where_a_constraint_goes() => Formats(
		"""
		public class C
		{
		    public T M<T>()
		        where T : class
		    {
		        return default;
		    }
		}
		""",
		"""
		public class C
		{
		    public T M<T>() where T : class
		    {
		        return default;
		    }
		}
		""",
		editorConfig: "max_line_length = 120");

	[Test]
	public Task A_width_with_existing_linebreaks_kept_leaves_the_author_in_charge() => Unchanged(
		"""
		public class C
		{
		    public C(int x)
		        : this()
		    {
		    }

		    public T M<T>()
		        where T : class
		    {
		        return default;
		    }
		}
		""",
		editorConfig: "max_line_length = 120\ncsharp_keep_existing_linebreaks = true");

	[Test]
	public Task Each_constraint_keeps_its_own_place() => Unchanged(
		// dotnet format reads each break on its own, so one clause moving down says nothing about another.
		"""
		public class A<T, U>
		    where T : class where U : class
		{
		}

		public class B<T, U> where T : class
		    where U : class
		{
		}
		""");

	[Test]
	public Task A_constraint_below_a_broken_parameter_list_stays_below_it() => Unchanged(
		"""
		public class C
		{
		    void M<T>(
		        int a,
		        int b)
		        where T : class
		    {
		    }
		}
		""");

	[Test]
	public Task A_constraint_on_every_declaration_that_takes_one() => Unchanged(
		"""
		public delegate void D<T>()
		    where T : class;

		public record R<T>(int X)
		    where T : class;

		public abstract class C
		{
		    protected abstract void P<T>()
		        where T : class;

		    void O<T>()
		        where T : class => P<T>();

		    void S()
		    {
		        T L<T>()
		            where T : class => default;
		    }
		}
		""");

	[Test]
	public Task A_clause_stays_as_far_in_as_the_author_put_it() => Unchanged(
		// dotnet format leaves these lines at whatever indent they have, however odd.
		"""
		public class Base(string name);

		public class C<T>(string name)
		: Base(name)
		              where T : class
		{
		    public C(int id)
		    : this(id.ToString())
		    {
		    }

		    public C(long id)
		                : this(id.ToString())
		    {
		    }

		    public static void M<T2>()
		    where T2 : new()
		    {
		    }
		}
		""");

	[Test]
	public Task A_clause_moves_as_far_as_its_declaration_does() => Formats(
		"""
		namespace N
		{
		public class C<T>
		    : B
		    where T : class
		{
		public C()
		    : base(1)
		{
		}
		}
		}
		""",
		"""
		namespace N
		{
		    public class C<T>
		        : B
		        where T : class
		    {
		        public C()
		            : base(1)
		        {
		        }
		    }
		}
		""");

	[Test]
	public Task A_clause_after_a_trailing_comment_is_indented_as_the_author_put_it() => Unchanged(
		"""
		public interface I // note
		    : System.IDisposable
		{
		}

		public class C : B
		{
		    public C(int a) // note
		        : base(a)
		    {
		    }

		    void M<T>() // note
		        where T : class
		    {
		    }
		}
		""");

	[Test]
	public Task A_comment_above_a_clause_stays_on_its_column() => Unchanged(
		"""
		public class C : B
		{
		    public C(int a)
		        // note
		        : base(a)
		    {
		    }

		    void M<T>()
		        // note
		        where T : class
		    {
		    }
		}
		""");

	[Test]
	public Task A_comment_ahead_of_a_clause_keeps_it_on_its_own_line_under_a_width() => Formats(
		// A width joins a clause the author broke, but a comment that ends the line ahead of it has
		// already broken it, so the clause takes a line of its own one level in (issue #310).
		"""
		public class C<T>
		    // c
		    where T : class
		{
		    void M<U>()
		        // c
		        where U : struct
		    {
		    }

		    void N<U>() // c
		        where U : struct
		    {
		    }

		    void O<U>()
		        /* c */
		        where U : struct
		    {
		    }
		}

		public class D
		    // c
		    : System.Object
		{
		}
		""",
		"""
		public class C<T>
		    // c
		    where T : class
		{
		    void M<U>()
		        // c
		        where U : struct
		    { }

		    void N<U>() // c
		        where U : struct
		    { }

		    void O<U>()
		        /* c */
		        where U : struct
		    { }
		}

		public class D
		    // c
		    : System.Object
		{
		}
		""",
		editorConfig: "max_line_length = 120");

	[Test]
	public Task A_one_line_body_follows_a_clause_a_comment_broke() => Formats(
		// The `{ }` goes below the clause, as below one the author broke: dotnet format moves it there.
		"""
		public class C
		{
		    void M<T>()
		        // c
		        where T : class
		    {
		    }

		    void N<T, U>()
		        where T : class
		        // c
		        where U : struct
		    {
		    }
		}
		""",
		"""
		public class C
		{
		    void M<T>()
		        // c
		        where T : class
		    { }

		    void N<T, U>() where T : class
		        // c
		        where U : struct
		    { }
		}
		""",
		editorConfig: "max_line_length = 120\ncsharp_empty_block_style = together_same_line");

	[Test]
	public Task A_directive_ahead_of_a_clause_stays_at_the_margin() => Unchanged(
		"""
		public class C
		#if X
		    : B
		#endif
		{
		}
		""");

	[Test]
	public Task A_one_line_type_body_follows_a_clause_the_author_broke() => Formats(
		// The brace follows the clause down, as it does when the option moved it: measured.
		"""
		public class C<T>
		    where T : class { }

		public class D
		    : B { }

		public enum E
		    : byte { A }
		""",
		"""
		public class C<T>
		    where T : class
		{ }

		public class D
		    : B
		{ }

		public enum E
		    : byte
		{ A }
		""");

	[Test]
	public Task The_key_gives_it_a_line_of_its_own() => WithAndWithout(
		"""
		public class C
		{
		    public T M<T>() where T : class
		    {
		        return default;
		    }
		}
		""",
		"""
		public class C
		{
		    public T M<T>() where T : class
		    {
		        return default;
		    }
		}
		""",
		"""
		public class C
		{
		    public T M<T>()
		        where T : class
		    {
		        return default;
		    }
		}
		""",
		Constraint);

	[Test]
	public Task Several_clauses_each_take_their_own_line() => Formats(
		// The key names the *first* constraint, but a break before one `where` and a space before the
		// next is a rendering nobody asks for, so the placement applies to each clause.
		"""
		public class C
		{
		    public T M<T, U>() where T : class where U : struct
		    {
		        return default;
		    }
		}
		""",
		"""
		public class C
		{
		    public T M<T, U>()
		        where T : class
		        where U : struct
		    {
		        return default;
		    }
		}
		""",
		editorConfig: Constraint);

	[Test]
	public Task It_reaches_types_and_local_functions_too() => Formats(
		"""
		public class C<T> where T : class
		{
		    void M()
		    {
		        T Local<U>() where U : T => default;
		    }
		}
		""",
		"""
		public class C<T>
		    where T : class
		{
		    void M()
		    {
		        T Local<U>()
		            where U : T => default;
		    }
		}
		""",
		editorConfig: Constraint);

	[Test]
	public Task A_one_line_method_body_follows_a_constraint_the_author_broke() => Formats(
		// Measured, and unlike a constructor's below its initializer.
		"""
		public class C
		{
		    void M<T>()
		        where T : class { }

		    void N()
		    {
		        void L<T>()
		            where T : class { }
		    }
		}
		""",
		"""
		public class C
		{
		    void M<T>()
		        where T : class
		    { }

		    void N()
		    {
		        void L<T>()
		            where T : class
		        { }
		    }
		}
		""");

	[Test]
	public Task An_empty_body_collapsed_by_its_option_follows_the_constraint_too() => Formats(
		// together_same_line joins an empty pair to its header, and dotnet format moves it back below a
		// `where` line, so the two would undo each other on every run.
		"""
		public class C
		{
		    void M<T>()
		        where T : class
		    {
		    }
		}
		""",
		"""
		public class C
		{
		    void M<T>()
		        where T : class
		    { }
		}
		""",
		editorConfig: "csharp_empty_block_style = together_same_line");

	// ---- the base list ---------------------------------------------------------------------------------

	[Test]
	public Task A_base_list_the_author_broke_stays_broken() => Unchanged(
		"""
		public class C
		    : System.Object
		{
		}

		public class D<T>(string name)
		    : B(name)
		    where T : class
		{
		}

		public enum E
		    : byte
		{
		    A,
		}

		public record R(int X)
		    : System.IDisposable;
		""");

	[Test]
	public Task Setting_the_key_to_false_joins_a_base_list_the_author_broke() => Formats(
		"""
		public class C
		    : System.Object
		{
		}
		""",
		"""
		public class C : System.Object
		{
		}
		""",
		editorConfig: "csharp_wrap_before_extends_colon = false");

	[Test]
	public Task The_key_puts_the_colon_on_the_next_line() => WithAndWithout(
		"""
		public class C : System.Object
		{
		}
		""",
		"""
		public class C : System.Object
		{
		}
		""",
		"""
		public class C
		    : System.Object
		{
		}
		""",
		Extends);

	[Test]
	public Task An_interface_list_moves_as_one_clause() => Formats(
		// The colon leads, and the list itself is untouched — this option decides where the clause
		// starts, not how a list too wide for the line comes apart.
		"""
		public class C : System.Object, System.IDisposable
		{
		}
		""",
		"""
		public class C
		    : System.Object, System.IDisposable
		{
		}
		""",
		editorConfig: Extends);

	[Test]
	public Task A_record_and_an_enum_take_it_as_well() => Formats(
		"""
		public enum E : byte
		{
		    A,
		}

		public record R(int X) : System.IDisposable;
		""",
		"""
		public enum E
		    : byte
		{
		    A,
		}

		public record R(int X)
		    : System.IDisposable;
		""",
		editorConfig: Extends);

	// ---- the constructor initializer ---------------------------------------------------------------------

	private const string Initializer = "csharp_place_constructor_initializer_on_same_line = false";

	[Test]
	public Task An_initializer_the_author_broke_stays_broken() => Unchanged(
		"""
		public class C : B
		{
		    public C()
		        : base(1)
		    {
		    }

		    public C(int a)
		        : base(a) { }

		    public C(long a)
		        : base(
		            1,
		            2)
		    {
		    }
		}
		""");

	[Test]
	public Task Setting_the_key_to_true_joins_an_initializer_the_author_broke() => Formats(
		"""
		public class C : B
		{
		    public C()
		        : base(1)
		    {
		    }
		}
		""",
		"""
		public class C : B
		{
		    public C() : base(1)
		    {
		    }
		}
		""",
		editorConfig: "csharp_place_constructor_initializer_on_same_line = true");

	[Test]
	public Task The_key_gives_the_initializer_its_own_line() => WithAndWithout(
		"""
		public class C : B
		{
		    public C(int x) : this()
		    {
		    }
		}
		""",
		"""
		public class C : B
		{
		    public C(int x) : this()
		    {
		    }
		}
		""",
		"""
		public class C : B
		{
		    public C(int x)
		        : this()
		    {
		    }
		}
		""",
		Initializer);

	[Test]
	public Task A_one_line_body_stays_on_the_initializer_line() => Formats(
		// Where a type's brace has to follow a clause down, a constructor's does not — measured, and
		// the asymmetry is dotnet format's rather than Curb's. Asserted so a later change to the type
		// rule does not quietly get copied here.
		"""
		public class C : B
		{
		    public C() : base(1) { }
		}
		""",
		"""
		public class C : B
		{
		    public C()
		        : base(1) { }
		}
		""",
		editorConfig: Initializer);

	// ---- the arrow -----------------------------------------------------------------------------------

	private const string Arrow = "csharp_wrap_before_arrow_with_expressions = true\nmax_line_length = 70";

	[Test]
	public Task A_body_that_fits_prints_the_same_either_way() => Formats(
		// The option moves the arrow across a break; where there is no break there is nothing to move,
		// so it can never be what causes a wrap.
		"""
		public class C
		{
		    int M() => 1;
		}
		""",
		"""
		public class C
		{
		    int M() => 1;
		}
		""",
		editorConfig: Arrow);

	[Test]
	public Task A_body_that_wraps_takes_the_arrow_with_it() => WithAndWithout(
		"""
		public class C
		{
		    int M() => Something(aaaaaaaaaaaaaaaaa, bbbbbbbbbbbbbbbbb, ccccccccc);
		}
		""",
		"""
		public class C
		{
		    int M() =>
		        Something(aaaaaaaaaaaaaaaaa, bbbbbbbbbbbbbbbbb, ccccccccc);
		}
		""",
		"""
		public class C
		{
		    int M()
		        => Something(aaaaaaaaaaaaaaaaa, bbbbbbbbbbbbbbbbb, ccccccccc);
		}
		""",
		Arrow,
		"max_line_length = 70");

	[Test]
	public Task A_body_converted_from_a_block_takes_the_arrow_too() => Formats(
		// The arrow is emitted from three places, and a body converted from a block reaches none of
		// the same code as one written with an arrow already. Missing two of the three made a
		// converted member print the arrow trailing on the run that converted it and leading on the
		// next — thirteen corpus files, and nothing in this class, because every other case here
		// starts from a body that already had its arrow.
		"""
		public class C
		{
		    int M()
		    {
		        return Something(aaaaaaaaaaaaaaaaa, bbbbbbbbbbbbbbbbb, ccccccccc);
		    }

		    int P
		    {
		        get { return Something(aaaaaaaaaaaaaaaaa, bbbbbbbbbbbbbbbbb, ccccccccc); }
		    }
		}
		""",
		"""
		public class C
		{
		    int M()
		        => Something(aaaaaaaaaaaaaaaaa, bbbbbbbbbbbbbbbbb, ccccccccc);

		    int P
		        => Something(aaaaaaaaaaaaaaaaa, bbbbbbbbbbbbbbbbb, ccccccccc);
		}
		""",
		editorConfig: Arrow
			+ "\ncsharp_style_expression_bodied_methods = true"
			+ "\ncsharp_style_expression_bodied_properties = true");

	// ---- together ---------------------------------------------------------------------------------------

	[Test]
	public Task Both_clauses_stack_under_the_declaration() => Formats(
		"""
		public class C<T> : System.Object where T : class
		{
		}
		""",
		"""
		public class C<T>
		    : System.Object
		    where T : class
		{
		}
		""",
		editorConfig: Constraint + "\n" + Extends);
}
