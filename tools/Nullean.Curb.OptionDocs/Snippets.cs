namespace Nullean.Curb.OptionDocs;

/// <summary>Shared C# snippet fixtures used when generating option reference examples.</summary>
public static class Snippets
{
    public static IReadOnlyDictionary<string, string> All { get; } = new Dictionary<string, string>
    {
        ["basic_method"] = """
            namespace N;

            public class Widget
            {
                public int Value { get; set; }

                public int Double()
                {
                    return Value * 2;
                }
            }

            """,

        ["class_with_method"] = """
            namespace N;

            public class Widget : Base, IWidget
            {
                public int X { get; set; }

                public void Run()
                {
                    X = X + 1;
                }
            }

            """,

        ["if_else"] = """
            namespace N;

            public class Widget
            {
                public string Classify(int n)
                {
                    if (n > 0)
                    {
                        return "positive";
                    }
                    else if (n < 0)
                    {
                        return "negative";
                    }
                    else
                    {
                        return "zero";
                    }
                }
            }

            """,

        ["try_catch"] = """
            namespace N;

            public class Widget
            {
                public int Parse(string s)
                {
                    try
                    {
                        return int.Parse(s);
                    }
                    catch (FormatException)
                    {
                        return 0;
                    }
                    finally
                    {
                        Console.WriteLine("done");
                    }
                }
            }

            """,

        ["object_init"] = """
            namespace N;

            public class Widget
            {
                public object Make()
                {
                    return new Point { X = 1, Y = 2, Z = 3 };
                }
            }

            """,

        ["anonymous_type"] = """
            namespace N;

            public class Widget
            {
                public object Describe()
                {
                    return new { Name = "curb", Version = 1, Active = true };
                }
            }

            """,

        ["query"] = """
            using System.Collections.Generic;
            using System.Linq;

            namespace N;

            public class Widget
            {
                public IEnumerable<int> Even(List<int> nums)
                {
                    return from n in nums where n % 2 == 0 orderby n select n;
                }
            }

            """,

        ["switch_stmt"] = """
            namespace N;

            public class Widget
            {
                public string Name(int code)
                {
                    switch (code)
                    {
                        case 1:
                            return "one";
                        case 2:
                        {
                            var msg = "two";
                            return msg;
                        }
                        default:
                            return "other";
                    }
                }
            }

            """,

        ["goto_label"] = """
            namespace N;

            public class Widget
            {
                public void Run(bool skip)
                {
                    if (skip)
                        goto end;
                    Console.WriteLine("working");
                end:
                    Console.WriteLine("done");
                }
            }

            """,

        ["cast"] = """
            namespace N;

            public class Widget
            {
                public int Narrow(object o)
                {
                    return (int)o;
                }
            }

            """,

        ["binary_ops"] = """
            namespace N;

            public class Widget
            {
                public bool Check(int a, int b, int c)
                {
                    return a > 0 && b > 0 && c > 0 && a + b > c && a + c > b && b + c > a;
                }
            }

            """,

        ["declaration"] = """
            namespace N;

            public class Widget
            {
                public void Run()
                {
                    int x = 1;
                    string name = "curb";
                    bool active = true;
                }
            }

            """,

        ["call_args"] = """
            namespace N;

            public class Widget
            {
                public void Register(string name, int value, bool active, string tag)
                {
                }

                public void Run()
                {
                    Register("curb", 42, true, "formatter");
                }
            }

            """,

        ["method_decl"] = """
            namespace N;

            public class Widget
            {
                public void Register(string name, int value, bool active, string tag)
                {
                    Console.WriteLine(name);
                }
            }

            """,

        ["lambda_block"] = """
            namespace N;

            public class Widget
            {
                public void Run()
                {
                    Action<int> log = x =>
                    {
                        Console.WriteLine(x);
                    };
                    log(1);
                }
            }

            """,

        ["unary"] = """
            namespace N;

            public class Widget
            {
                public int Negate(int x)
                {
                    int a = -x;
                    bool b = !(x > 0);
                    int c = ~x;
                    return a + c;
                }
            }

            """,

        ["ternary"] = """
            namespace N;

            public class Widget
            {
                public int Clamp(int x)
                {
                    return x > 0 ? x : 0;
                }
            }

            """,

        ["for_loop"] = """
            namespace N;

            public class Widget
            {
                public int Sum(int n)
                {
                    int total = 0;
                    for (int i = 0; i < n; i++)
                    {
                        total += i;
                    }
                    return total;
                }
            }

            """,

        ["single_line_block"] = """
            namespace N;

            public class Widget
            {
                public int X { get; set; }
                public string Name { get; set; } = "";
                public void Empty() { }
            }

            """,

        ["long_chain"] = """
            using System.Text;

            namespace N;

            public class Widget
            {
                public string Build()
                {
                    return new StringBuilder().Append("Hello").Append(", ").Append("World").Append("!").ToString();
                }
            }

            """,

        ["indexer"] = """
            namespace N;

            public class Widget
            {
                private int[] _data = new int[10];

                public int this[int index]
                {
                    get { return _data[index]; }
                    set { _data[index] = value; }
                }

                public int First => _data[0];
            }

            """,

        ["generic_constraint"] = """
            namespace N;

            public class Widget
            {
                public T Max<T>(T a, T b) where T : IComparable<T>
                {
                    return a.CompareTo(b) >= 0 ? a : b;
                }
            }

            """,

        ["constructor_init"] = """
            namespace N;

            public class Base
            {
                public Base(string name) { }
            }

            public class Widget : Base
            {
                public Widget(string name, int value) : base(name)
                {
                    Value = value;
                }

                public int Value { get; }
            }

            """,

        ["expression_body"] = """
            namespace N;

            public class Widget
            {
                public int Value { get; set; }

                public int Double()
                {
                    return Value * 2;
                }

                public string Describe()
                {
                    return $"Widget({Value})";
                }
            }

            """,

        ["constructor_expr"] = """
            namespace N;

            public class Widget
            {
                public int Value { get; }

                public Widget(int value)
                {
                    Value = value;
                }
            }

            """,

        ["property"] = """
            namespace N;

            public class Widget
            {
                private int _value;

                public int Value
                {
                    get { return _value; }
                    set { _value = value; }
                }

                public string Name
                {
                    get { return "widget"; }
                }
            }

            """,

        ["simple_enum"] = """
            namespace N;

            public enum Status { Active, Inactive, Pending }

            """,

        ["attributed_method"] = """
            namespace N;

            public class Widget
            {
                [Obsolete]
                public void OldWay() { }

                [Obsolete("use NewWay")]
                public string OldName { get; set; } = "";
            }

            """,

        ["members"] = """
            namespace N;

            public class Widget
            {
                private int _x;

                private string _name = "";


                public int X
                {
                    get { return _x; }
                    set { _x = value; }
                }

                public void Run()
                {
                    int a = 1;


                    int b = 2;
                    Console.WriteLine(a + b);
                }

                public void Stop()
                {
                    Console.WriteLine("stop");
                }
            }

            """,

        ["near_braces"] = """
            namespace N;

            public class Widget
            {

                private int _x;

                public void Run()
                {

                    int a = 1;
                    Console.WriteLine(a);
                }
            }

            """,

        ["usings"] = """
            using System;
            using Foo.Bar;
            using System.Collections.Generic;

            namespace N;

            public class Widget { }

            """,

        ["modifiers"] = """
            namespace N;

            public class Widget
            {
                public static readonly int Max = 100;
                static public int Count;
                private protected virtual void Run() { }
            }

            """,

        ["namespace_block"] = """
            using System;

            namespace N
            {
                public class Widget
                {
                    public void Run() { }
                }
            }

            """,

        ["array_init"] = """
            namespace N;

            public class Widget
            {
                public int[] Make()
                {
                    return new int[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 };
                }
            }

            """,
    };

    public static IReadOnlyDictionary<string, string> BadSnippets { get; } = new Dictionary<string, string>
    {
        // ---- Core -----------------------------------------------------------------------
        ["indent_style"] = """
            namespace N;

            public class Widget
            {
            	public int Value { get; set; }

            	public int Double()
            	{
            		return Value * 2;
            	}
            }

            """,

        // ---- Spacing --------------------------------------------------------------------
        ["csharp_space_after_cast"] = """
            namespace N;

            public class Widget
            {
                public int Narrow(object o, double d)
                {
                    int x = (int) o;
                    float y = (float) d;
                    return x + (int) y;
                }
            }

            """,

        ["csharp_space_after_keywords_in_control_flow_statements"] = """
            namespace N;

            public class Widget
            {
                public int Count(int n)
                {
                    int total = 0;
                    for(int i = 0; i < n; i++)
                    {
                        if(i % 2 == 0)
                            total++;
                    }
                    while(total > 100)
                        total /= 2;
                    return total;
                }
            }

            """,

        ["csharp_space_before_colon_in_inheritance_clause"] = """
            namespace N;

            public interface IWidget { }

            public class Widget: IWidget
            {
                public void Run() { }
            }

            """,

        ["csharp_space_after_colon_in_inheritance_clause"] = """
            namespace N;

            public interface IWidget { }

            public class Widget :IWidget
            {
                public void Run() { }
            }

            """,

        ["csharp_space_around_binary_operators"] = """
            namespace N;

            public class Widget
            {
                public bool Check(int a, int b, int c)
                {
                    return a>0&&b>0&&c>0&&a+b>c;
                }
            }

            """,

        ["csharp_space_around_declaration_statements"] = """
            namespace N;

            public class Widget
            {
                public void Run()
                {
                    int  x=1;
                    string  name="curb";
                    bool  active=true;
                }
            }

            """,

        ["csharp_space_after_comma"] = """
            namespace N;

            public class Widget
            {
                public void Register(string name,int value,bool active)
                {
                    Console.WriteLine(name,value,active);
                }
            }

            """,

        ["csharp_space_before_comma"] = """
            namespace N;

            public class Widget
            {
                public void Register(string name ,int value ,bool active)
                {
                    Console.WriteLine(name ,value ,active);
                }
            }

            """,

        ["csharp_space_between_parentheses"] = """
            namespace N;

            public class Widget
            {
                public string Classify(int n)
                {
                    if ( n > 0 )
                        return "positive";
                    else if ( n < 0 )
                        return "negative";
                    return "zero";
                }
            }

            """,

        ["csharp_space_between_method_declaration_parameter_list_parentheses"] = """
            namespace N;

            public class Widget
            {
                public void Register( string name, int value, bool active )
                {
                    Console.WriteLine(name);
                }
            }

            """,

        ["csharp_space_between_method_declaration_empty_parameter_list_parentheses"] = """
            namespace N;

            public class Widget
            {
                public void Reset( )
                {
                    Console.WriteLine("reset");
                }
            }

            """,

        ["csharp_space_between_method_declaration_name_and_open_parenthesis"] = """
            namespace N;

            public class Widget
            {
                public void Register (string name, int value)
                {
                    Console.WriteLine (name);
                }
            }

            """,

        ["csharp_space_between_method_call_parameter_list_parentheses"] = """
            namespace N;

            public class Widget
            {
                public void Run()
                {
                    Console.WriteLine( "hello" );
                    Math.Max( 1, 2 );
                }
            }

            """,

        ["csharp_space_between_method_call_empty_parameter_list_parentheses"] = """
            namespace N;

            public class Widget
            {
                public void Run()
                {
                    var s = ToString( );
                    var h = GetHashCode( );
                }
            }

            """,

        ["csharp_space_between_method_call_name_and_opening_parenthesis"] = """
            namespace N;

            public class Widget
            {
                public void Run()
                {
                    Console.WriteLine ("hello");
                    Math.Max (1, 2);
                }
            }

            """,

        ["csharp_space_before_open_square_brackets"] = """
            namespace N;

            public class Widget
            {
                public int Get()
                {
                    int [] arr = new int [10];
                    arr [0] = 1;
                    return arr [0];
                }
            }

            """,

        ["csharp_space_between_empty_square_brackets"] = """
            namespace N;

            public class Widget
            {
                private int[ ] _data = new int[ ] { 1, 2, 3 };
            }

            """,

        ["csharp_space_between_square_brackets"] = """
            namespace N;

            public class Widget
            {
                private int[] _data = new int[10];

                public int Get(int i) => _data[ i ];

                public void Set(int i, int v) { _data[ i ] = v; }
            }

            """,

        ["csharp_space_before_dot"] = """
            using System.Text;

            namespace N;

            public class Widget
            {
                public string Build()
                {
                    return new StringBuilder() .Append("Hello") .Append(", ") .ToString();
                }
            }

            """,

        ["csharp_space_after_dot"] = """
            using System.Text;

            namespace N;

            public class Widget
            {
                public string Build()
                {
                    return new StringBuilder(). Append("Hello"). Append(", "). ToString();
                }
            }

            """,

        ["csharp_space_before_semicolon_in_for_statement"] = """
            namespace N;

            public class Widget
            {
                public int Sum(int n)
                {
                    int total = 0;
                    for (int i = 0 ;i < n ;i++)
                        total += i;
                    return total;
                }
            }

            """,

        ["csharp_space_after_semicolon_in_for_statement"] = """
            namespace N;

            public class Widget
            {
                public int Sum(int n)
                {
                    int total = 0;
                    for (int i = 0;i < n;i++)
                        total += i;
                    return total;
                }
            }

            """,

        // ---- NewLines -------------------------------------------------------------------
        ["csharp_new_line_before_else"] = """
            namespace N;

            public class Widget
            {
                public string Classify(int n)
                {
                    if (n > 0) {
                        return "positive";
                    } else if (n < 0) {
                        return "negative";
                    } else {
                        return "zero";
                    }
                }
            }

            """,

        ["csharp_new_line_before_catch"] = """
            namespace N;

            public class Widget
            {
                public int Parse(string s)
                {
                    try {
                        return int.Parse(s);
                    } catch (FormatException) {
                        return 0;
                    }
                }
            }

            """,

        ["csharp_new_line_before_finally"] = """
            namespace N;

            public class Widget
            {
                public int Parse(string s)
                {
                    try {
                        return int.Parse(s);
                    } catch (FormatException) {
                        return 0;
                    } finally {
                        Console.WriteLine("done");
                    }
                }
            }

            """,

        ["csharp_new_line_before_members_in_object_initializers"] = """
            namespace N;

            public class Point { public int X; public int Y; public int Z; }

            public class Widget
            {
                public Point Make() => new Point { X = 1, Y = 2, Z = 3 };
            }

            """,

        ["csharp_new_line_before_members_in_anonymous_types"] = """
            namespace N;

            public class Widget
            {
                public object Describe()
                {
                    return new { Name = "curb", Version = 1, Active = true };
                }
            }

            """,

        ["csharp_new_line_between_query_expression_clauses"] = """
            using System.Collections.Generic;
            using System.Linq;

            namespace N;

            public class Widget
            {
                public IEnumerable<int> Even(List<int> nums)
                {
                    return from n in nums where n % 2 == 0 orderby n select n;
                }
            }

            """,

        ["csharp_new_line_before_open_brace"] = """
            namespace N;

            public class Widget {
                public int X { get; set; }

                public void Run() {
                    if (X > 0) {
                        X--;
                    }
                }
            }

            """,

        // ---- Indentation ---------------------------------------------------------------
        ["csharp_indent_case_contents"] = """
            namespace N;

            public class Widget
            {
                public string Name(int code)
                {
                    switch (code)
                    {
                        case 1:
                        return "one";
                        case 2:
                        {
                        var msg = "two";
                        return msg;
                        }
                        default:
                        return "other";
                    }
                }
            }

            """,

        ["csharp_indent_switch_labels"] = """
            namespace N;

            public class Widget
            {
                public string Name(int code)
                {
                    switch (code)
                    {
                    case 1:
                        return "one";
                    case 2:
                    {
                        var msg = "two";
                        return msg;
                    }
                    default:
                        return "other";
                    }
                }
            }

            """,

        ["csharp_indent_block_contents"] = """
            namespace N;

            public class Widget
            {
                public void Run()
                {
            int x = 1;
            int y = 2;
            Console.WriteLine(x + y);
                }
            }

            """,

        ["csharp_indent_braces"] = """
            namespace N;

            public class Widget
            {
                public void Run()
                {
                    Console.WriteLine("hello");
                }
            }

            """,

        ["csharp_indent_case_contents_when_block"] = """
            namespace N;

            public class Widget
            {
                public string Name(int code)
                {
                    switch (code)
                    {
                        case 2:
                        {
                        var msg = "two";
                        return msg;
                        }
                        default:
                            return "other";
                    }
                }
            }

            """,

        ["csharp_indent_labels"] = """
            namespace N;

            public class Widget
            {
                public void Run(bool skip)
                {
                    if (skip)
                        goto end;
                    Console.WriteLine("working");
                        end:
                    Console.WriteLine("done");
                }
            }

            """,

        // ---- ExpressionBodies ----------------------------------------------------------
        ["csharp_style_expression_bodied_methods"] = """
            namespace N;

            public class Widget
            {
                public int Value { get; set; }

                public int Double() { return Value * 2; }

                public string Describe() { return $"Widget({Value})"; }
            }

            """,

        ["csharp_style_expression_bodied_constructors"] = """
            namespace N;

            public class Widget
            {
                public int Value { get; }

                public Widget(int value) { Value = value; }
            }

            """,

        ["csharp_style_expression_bodied_operators"] = """
            namespace N;

            public class Widget
            {
                public int Value { get; }

                public Widget(int value) { Value = value; }

                public static Widget operator+(Widget a, Widget b) { return new Widget(a.Value + b.Value); }
            }

            """,

        ["csharp_style_expression_bodied_local_functions"] = """
            namespace N;

            public class Widget
            {
                public int Compute(int a, int b)
                {
                    int Add(int x, int y) { return x + y; }
                    return Add(a, b);
                }
            }

            """,

        ["csharp_style_expression_bodied_accessors"] = """
            namespace N;

            public class Widget
            {
                private int _value;

                public int Value
                {
                    get { return _value; }
                    set { _value = value; }
                }
            }

            """,

        ["csharp_style_expression_bodied_properties"] = """
            namespace N;

            public class Widget
            {
                private int _value;

                public int Value
                {
                    get { return _value; }
                }

                public string Name
                {
                    get { return "widget"; }
                }
            }

            """,

        ["csharp_style_expression_bodied_indexers"] = """
            namespace N;

            public class Widget
            {
                private int[] _data = new int[10];

                public int this[int index]
                {
                    get { return _data[index]; }
                }
            }

            """,

        // ---- Usings --------------------------------------------------------------------
        ["dotnet_sort_system_directives_first"] = """
            using Foo.Bar;
            using System;
            using Baz.Qux;
            using System.Collections.Generic;

            namespace N;

            public class Widget { }

            """,

        ["dotnet_separate_import_directive_groups"] = """
            using System;
            using System.Collections.Generic;
            using Foo.Bar;
            using Baz.Qux;

            namespace N;

            public class Widget { }

            """,

        ["csharp_using_directive_placement"] = """
            using System;
            using System.Collections.Generic;

            namespace N
            {
                public class Widget { }
            }

            """,

        // ---- ModifiersAndBraces --------------------------------------------------------
        ["csharp_prefer_braces"] = """
            namespace N;

            public class Widget
            {
                public string Classify(int n)
                {
                    if (n > 0)
                        return "positive";
                    else if (n < 0)
                        return "negative";
                    else
                        return "zero";
                }
            }

            """,

        ["csharp_style_namespace_declarations"] = """
            using System;

            namespace N
            {
                public class Widget
                {
                    public void Run() { }
                }
            }

            """,

        // ---- TrailingCommas ------------------------------------------------------------
        ["csharp_trailing_comma_in_multiline_lists"] = """
            namespace N;

            public class Widget
            {
                public int[] Make()
                {
                    return new int[]
                    {
                        1,
                        2,
                        3
                    };
                }
            }

            """,

        ["csharp_trailing_comma_in_singleline_lists"] = """
            namespace N;

            public class Widget
            {
                public void Run()
                {
                    var list = new List<int> { 1, 2, 3 };
                    Register("curb", 42, true, "formatter");
                }

                private void Register(string a, int b, bool c, string d) { }
            }

            """,

        // ---- BlankLines ----------------------------------------------------------------
        ["csharp_keep_blank_lines_in_declarations"] = """
            namespace N;

            public class Widget
            {
                private int _x;



                private string _name = "";



                public int X { get; set; }
            }

            """,

        ["csharp_keep_blank_lines_in_code"] = """
            namespace N;

            public class Widget
            {
                public void Run()
                {
                    int a = 1;



                    int b = 2;



                    Console.WriteLine(a + b);
                }
            }

            """,

        ["csharp_blank_lines_around_invocable"] = """
            namespace N;

            public class Widget
            {
                public void Run()
                {
                    Console.WriteLine("run");
                }
                public void Stop()
                {
                    Console.WriteLine("stop");
                }
                public void Reset()
                {
                    Console.WriteLine("reset");
                }
            }

            """,

        ["csharp_blank_lines_around_type"] = """
            namespace N;

            public class Outer
            {
                public class Inner1 { }
                public class Inner2 { }
                public class Inner3 { }
            }

            """,

        ["csharp_blank_lines_around_property"] = """
            namespace N;

            public class Widget
            {
                public int X { get; set; }
                public int Y { get; set; }
                public int Z { get; set; }
            }

            """,

        ["csharp_blank_lines_around_field"] = """
            namespace N;

            public class Widget
            {
                private int _x;
                private int _y;
                private int _z;
            }

            """,

        ["csharp_blank_lines_inside_type"] = """
            namespace N;

            public class Widget
            {
                public int X { get; set; }
                public void Run() { }
            }

            """,

        ["csharp_blank_lines_around_namespace"] = """
            using System;

            namespace N;

            public class Widget { }

            """,

        ["csharp_blank_lines_after_using_list"] = """
            using System;
            namespace N;

            public class Widget { }

            """,

        ["csharp_blank_lines_after_file_scoped_namespace_directive"] = """
            using System;

            namespace N;
            public class Widget { }

            """,

        // ---- Wrapping ------------------------------------------------------------------
        ["csharp_preserve_single_line_blocks"] = """
            namespace N;

            public class Widget
            {
                public int X { get; set; }
                public string Name { get; set; } = "";
                public void Empty() { }
            }

            """,

        ["csharp_preserve_single_line_statements"] = """
            namespace N;

            public class Widget
            {
                public int Clamp(int n)
                {
                    if (n < 0) return 0;
                    if (n > 100) return 100;
                    return n;
                }
            }

            """,

        ["csharp_place_simple_accessorholder_on_single_line"] = """
            namespace N;

            public class Widget
            {
                public int X
                {
                    get;
                    set;
                }

                public string Name
                {
                    get;
                    set;
                }
            }

            """,

        ["csharp_place_simple_enum_on_single_line"] = """
            namespace N;

            public enum Status
            {
                Active,
                Inactive,
                Pending,
            }

            """,
    };

    public static IReadOnlyDictionary<string, string> DemoValues { get; } = new Dictionary<string, string>
    {
        ["csharp_blank_lines_around_invocable"] = "1",
        ["csharp_blank_lines_around_type"] = "1",
        ["csharp_blank_lines_around_property"] = "1",
        ["csharp_blank_lines_around_field"] = "1",
        ["csharp_blank_lines_inside_type"] = "1",
        ["csharp_blank_lines_around_namespace"] = "1",
        ["csharp_blank_lines_after_using_list"] = "1",
        ["file_header_template"] = "Copyright (c) Example Corp.\\nLicensed under the MIT license.",
    };
}
