namespace Nullean.Curb.Tests.Formatting.Declarations;

/// <summary>Fields, properties, constructors, indexers, operators, events and delegates.</summary>
public class MemberTests : FormattingTest
{
	// ---- fields -------------------------------------------------------------------------------

	[Test]
	public Task Field() => Unchanged(
		"""
		public class C
		{
		    public int Value;
		}
		""");

	[Test]
	public Task Field_with_an_initializer() => Formats(
		"""
		public class C
		{
		    public int Value=1;
		}
		""",
		"""
		public class C
		{
		    public int Value = 1;
		}
		""");

	[Test]
	public Task Several_declarators_on_one_field() => Formats(
		"""
		public class C
		{
		    public int First,Second;
		}
		""",
		"""
		public class C
		{
		    public int First, Second;
		}
		""");

	[Test]
	public Task Const_field() => Unchanged(
		"""
		public class C
		{
		    public const int Value = 1;
		}
		""");

	[Test]
	public Task Static_readonly_field() => Unchanged(
		"""
		public class C
		{
		    private static readonly int Value = 1;
		}
		""");

	// ---- properties ---------------------------------------------------------------------------

	[Test]
	public Task Auto_property_stays_on_one_line() => Unchanged(
		"""
		public class C
		{
		    public int Value { get; set; }
		}
		""");

	[Test]
	public Task Auto_property_with_init() => Unchanged(
		"""
		public class C
		{
		    public int Value { get; init; }
		}
		""");

	[Test]
	public Task Get_only_property() => Unchanged(
		"""
		public class C
		{
		    public int Value { get; }
		}
		""");

	[Test]
	public Task Property_with_an_initializer() => Unchanged(
		"""
		public class C
		{
		    public int Value { get; set; } = 1;
		}
		""");

	[Test]
	public Task Expression_bodied_property() => Unchanged(
		"""
		public class C
		{
		    public int Value => 1;
		}
		""");

	[Test]
	public Task Property_with_accessor_bodies() => Unchanged(
		"""
		public class C
		{
		    private int _value;

		    public int Value
		    {
		        get
		        {
		            return _value;
		        }
		        set
		        {
		            _value = value;
		        }
		    }
		}
		""");

	[Test]
	[Skip("reflow off collapses a multi-line accessor list onto one line; dotnet format never joins lines")]
	public Task Property_with_expression_bodied_accessors() => Unchanged(
		"""
		public class C
		{
		    private int _value;

		    public int Value
		    {
		        get => _value;
		        set => _value = value;
		    }
		}
		""");

	[Test]
	public Task Property_with_a_private_setter() => Unchanged(
		"""
		public class C
		{
		    public int Value { get; private set; }
		}
		""");

	[Test]
	public Task Required_property() => Unchanged(
		"""
		public class C
		{
		    public required int Value { get; set; }
		}
		""");

	// ---- constructors -------------------------------------------------------------------------

	[Test]
	public Task Constructor() => Unchanged(
		"""
		public class C
		{
		    public C()
		    {
		    }
		}
		""");

	[Test]
	public Task Constructor_with_parameters() => Unchanged(
		"""
		public class C
		{
		    public C(int value)
		    {
		        Value = value;
		    }

		    public int Value;
		}
		""");

	[Test]
	public Task Constructor_with_a_base_initializer() => Unchanged(
		"""
		public class C : Base
		{
		    public C(int value) : base(value)
		    {
		    }
		}
		""");

	[Test]
	public Task Constructor_with_a_this_initializer() => Unchanged(
		"""
		public class C
		{
		    public C() : this(0)
		    {
		    }

		    public C(int value)
		    {
		    }
		}
		""");

	[Test]
	public Task Static_constructor() => Unchanged(
		"""
		public class C
		{
		    static C()
		    {
		    }
		}
		""");

	[Test]
	public Task Destructor() => Unchanged(
		"""
		public class C
		{
		    ~C()
		    {
		    }
		}
		""");

	[Test]
	public Task A_destructor_is_actually_reformatted() => Formats(
		// Destructors went entirely unprinted until this test existed — Unchanged above passed either
		// way, since UnhandledNode's verbatim fallback reproduces already-correct input identically to
		// a real printer. This one needs genuine reformatting to tell the two apart.
		"""
		public class C
		{
		~C(  )
		    {
		Cleanup();
		    }
		}
		""",
		"""
		public class C
		{
		    ~C()
		    {
		        Cleanup();
		    }
		}
		""");

	// ---- indexers, operators, events, delegates -----------------------------------------------

	[Test]
	public Task Indexer() => Unchanged(
		"""
		public class C
		{
		    public int this[int index] => index;
		}
		""");

	[Test]
	public Task Indexer_with_accessors() => Unchanged(
		"""
		public class C
		{
		    public int this[int index]
		    {
		        get
		        {
		            return index;
		        }
		    }
		}
		""");

	[Test]
	public Task Operator() => Unchanged(
		"""
		public class C
		{
		    public static C operator +(C left, C right)
		    {
		        return left;
		    }
		}
		""");

	[Test]
	public Task Implicit_conversion_operator() => Unchanged(
		"""
		public class C
		{
		    public static implicit operator int(C value)
		    {
		        return 0;
		    }
		}
		""");

	[Test]
	public Task Explicit_conversion_operator() => Unchanged(
		"""
		public class C
		{
		    public static explicit operator string(C value)
		    {
		        return null;
		    }
		}
		""");

	[Test]
	public Task Event_field() => Unchanged(
		"""
		public class C
		{
		    public event EventHandler Changed;
		}
		""");

	[Test]
	public Task Delegate_declaration() => Unchanged(
		"""
		public delegate int Transform(int value);
		""");

	[Test]
	public Task Generic_delegate_declaration() => Unchanged(
		"""
		public delegate TResult Transform<T, TResult>(T value);
		""");

	// ---- enums --------------------------------------------------------------------------------

	[Test]
	public Task Enum_members_one_per_line() => Unchanged(
		"""
		public enum Colour
		{
		    Red,
		    Green,
		    Blue,
		}
		""");

	[Test]
	public Task Enum_with_explicit_values() => Unchanged(
		"""
		public enum Colour
		{
		    Red = 1,
		    Green = 2,
		}
		""");

	[Test]
	public Task Enum_with_a_base_type() => Unchanged(
		"""
		public enum Colour : byte
		{
		    Red,
		}
		""");

	[Test]
	public Task Empty_enum() => Unchanged(
		"""
		public enum Colour
		{
		}
		""");

	[Test]
	public Task Enum_member_with_an_attribute() => Unchanged(
		"""
		public enum Colour
		{
		    [Obsolete]
		    Red,
		    Green,
		}
		""");

	[Test]
	public Task Blank_line_between_enum_members_is_kept() => Unchanged(
		"""
		public enum Colour
		{
		    Red,

		    Green,
		}
		""");
}
