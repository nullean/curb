namespace Nullean.Kerf.Tests.Formatting.Options;

/// <summary>
/// <c>csharp_wrap_before_first_type_parameter_constraint</c> and <c>csharp_wrap_before_extends_colon</c>.
/// </summary>
/// <remarks>
/// <para>
/// Two clauses Kerf joins onto the signature line whatever the author wrote, which makes them the
/// places a Rider-formatted repository differs from Kerf by a whole line rather than by a column.
/// Rider puts both on their own line by default.
/// </para>
/// <para>
/// Free ground, measured in both directions: <c>dotnet format</c> neither joins a clause the author
/// broke nor breaks one they joined, so either rendering is a fixed point. And both are forced rather
/// than width-driven — the break is always there or never — so neither asks a question about layout
/// that this run's own output would change the answer to. That is what separates them from the
/// single-line blank-line family, which had to be dropped for exactly that reason.
/// </para>
/// </remarks>
public class WrapBeforeClauseTests : FormattingTest
{
	private const string Constraint = "csharp_wrap_before_first_type_parameter_constraint = true";
	private const string Extends = "csharp_wrap_before_extends_colon = true";

	// ---- type parameter constraints ------------------------------------------------------------------

	[Test]
	public Task A_constraint_joins_the_signature_by_default() => Formats(
		// Including one the author had already put on its own line: the default joins regardless, which
		// is the behaviour this option exists to let a repository turn off.
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

	// ---- the base list ---------------------------------------------------------------------------------

	[Test]
	public Task A_base_list_joins_the_declaration_by_default() => Formats(
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
		""");

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
	public Task An_initializer_joins_the_signature_by_default() => Formats(
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
		""");

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
		// the asymmetry is dotnet format's rather than Kerf's. Asserted so a later change to the type
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
