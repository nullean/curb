namespace Nullean.Curb.Tests.Formatting.Declarations;

/// <summary>Classes, structs, interfaces, records and their headers.</summary>
public class TypeDeclarationTests : FormattingTest
{
	[Test]
	public Task Class_with_a_member() => Unchanged(
		"""
		public class C
		{
		    public int Value;
		}
		""");

	[Test]
	public Task Empty_class() => Unchanged(
		"""
		public class C
		{
		}
		""");

	[Test]
	public Task Brace_moves_to_its_own_line() => Formats(
		"""
		public class C {
		    public int Value;
		}
		""",
		"""
		public class C
		{
		    public int Value;
		}
		""");

	[Test]
	public Task Modifiers_are_separated_by_single_spaces() => Formats(
		"""
		public    sealed     class C
		{
		}
		""",
		"""
		public sealed class C
		{
		}
		""");

	[Test]
	public Task Struct() => Unchanged(
		"""
		public struct Point
		{
		    public int X;
		}
		""");

	[Test]
	public Task Readonly_struct() => Unchanged(
		"""
		public readonly struct Point
		{
		    public readonly int X;
		}
		""");

	[Test]
	public Task Interface() => Unchanged(
		"""
		public interface IThing
		{
		    void Do();
		}
		""");

	[Test]
	public Task Record() => Unchanged(
		"""
		public record Person(string Name, int Age);
		""");

	[Test]
	public Task Record_struct_keeps_both_keywords() => Unchanged(
		"""
		public readonly record struct Point(int X, int Y);
		""");

	[Test]
	public Task Record_with_a_body() => Unchanged(
		"""
		public record Person(string Name)
		{
		    public int Age;
		}
		""");

	[Test]
	public Task Static_class() => Unchanged(
		"""
		public static class Helpers
		{
		    public static int Value;
		}
		""");

	[Test]
	public Task Abstract_class() => Unchanged(
		"""
		public abstract class Base
		{
		    public abstract void Do();
		}
		""");

	[Test]
	public Task Partial_class() => Unchanged(
		"""
		public partial class C
		{
		    public int Value;
		}
		""");

	[Test]
	public Task Nested_types_are_indented() => Unchanged(
		"""
		public class Outer
		{
		    public class Inner
		    {
		        public int Value;
		    }
		}
		""");

	[Test]
	public Task Nested_types_are_re_indented() => Formats(
		"""
		public class Outer
		{
		public class Inner
		{
		public int Value;
		}
		}
		""",
		"""
		public class Outer
		{
		    public class Inner
		    {
		        public int Value;
		    }
		}
		""");

	[Test]
	public Task Base_type() => Unchanged(
		"""
		public class C : Base
		{
		    public int Value;
		}
		""");

	[Test]
	public Task Base_type_and_interfaces_are_comma_separated() => Formats(
		"""
		public class C:Base,IFirst,ISecond
		{
		}
		""",
		"""
		public class C : Base, IFirst, ISecond
		{
		}
		""");

	[Test]
	public Task Generic_type() => Unchanged(
		"""
		public class Box<T>
		{
		    public T Value;
		}
		""");

	[Test]
	public Task Generic_type_with_several_parameters() => Formats(
		"""
		public class Map<TKey,TValue>
		{
		}
		""",
		"""
		public class Map<TKey, TValue>
		{
		}
		""");

	[Test]
	public Task Generic_variance_keywords_keep_their_space() => Unchanged(
		"""
		public interface IReader<out T, in TKey>
		{
		    T Read(TKey key);
		}
		""");

	[Test]
	public Task Type_parameter_constraint() => Unchanged(
		"""
		public class Box<T> where T : class, new()
		{
		    public T Value;
		}
		""");

	[Test]
	public Task Several_type_parameter_constraints() => Unchanged(
		"""
		public class Map<TKey, TValue> where TKey : notnull where TValue : class
		{
		}
		""");

	[Test]
	public Task Attribute_above_a_type() => Unchanged(
		"""
		[Serializable]
		public class C
		{
		}
		""");

	[Test]
	public Task Primary_constructor_parameters() => Unchanged(
		"""
		public class C(int value)
		{
		    public int Value = value;
		}
		""");

	[Test]
	public Task Primary_constructor_with_a_base_call() => Unchanged(
		"""
		public class C(int value) : Base(value)
		{
		}
		""");

	// ---- unions (C# preview) -----------------------------------------------------------------------
	//
	// Issue #90. A union is a type declaration whose primary constructor lists case types, so it prints
	// as a record does. It used to print verbatim, which left a mis-indented one for dotnet format to
	// move. Every expectation here was checked against dotnet format whitespace (SDK 10.0.403, whose
	// Roslyn parses `union`) under LangVersion=preview.

	[Test]
	public Task Union_without_a_body() => Unchanged(
		"""
		public union Outcome(Started, Orphaned);
		""");

	[Test]
	public Task Union_with_a_body_and_a_switch_expression_property() => Unchanged(
		"""
		public union RaftMessage(VoteRequest, VoteResponse, AppendRequest, AppendResponse)
		{
		    public readonly int Term => this switch
		    {
		        VoteRequest r => r.Term,
		        VoteResponse r => r.Term,
		        AppendRequest r => r.Term,
		        AppendResponse r => r.Term,
		        _ => 0,
		    };
		}
		""");

	[Test]
	public Task Union_with_type_parameters_and_a_constraint() => Unchanged(
		"""
		public union Result<T>(T, Error) where T : class;
		""");

	[Test]
	public Task Union_is_formatted_like_a_record() => Formats(
		"""
		public   union  Messy( VoteRequest ,VoteResponse ){
		public int Term => 1;
		}
		""",
		"""
		public union Messy(VoteRequest, VoteResponse)
		{
		    public int Term => 1;
		}
		""");

	[Test]
	public Task Union_keeps_its_case_types_where_the_author_broke_them() => Unchanged(
		"""
		public union Command(
		    ParseCommand,
		    CheckCommand,
		    VerifyCommand);
		""");

	[Test]
	public Task Union_with_tabs_and_allman_braces() => Unchanged(
		"public union RaftMessage(VoteRequest, VoteResponse)\n{\n\tpublic readonly int Term => this switch\n\t{\n\t\tVoteRequest r => r.Term,\n\t\t_ => 0,\n\t};\n}",
		editorConfig: "indent_style = tab\ncsharp_new_line_before_open_brace = all");

	[Test]
	public Task Union_that_does_not_fit_wraps_its_case_types_like_a_record() => Formats(
		"""
		public union RaftMessage(VoteRequest, VoteResponse, AppendRequest, AppendResponse)
		{
		    public int Term => 1;
		}
		""",
		"""
		public union RaftMessage(
		    VoteRequest,
		    VoteResponse,
		    AppendRequest,
		    AppendResponse
		)
		{
		    public int Term => 1;
		}
		""",
		editorConfig: "max_line_length = 60");

	[Test]
	public Task Enum_is_not_a_type_declaration_but_indents_the_same() => Unchanged(
		"""
		public class C
		{
		    public enum Colour
		    {
		        Red,
		        Green,
		    }
		}
		""");
}
