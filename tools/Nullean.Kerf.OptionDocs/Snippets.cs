namespace Nullean.Kerf.OptionDocs;

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
                    return new { Name = "kerf", Version = 1, Active = true };
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
                    string name = "kerf";
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
                    Register("kerf", 42, true, "formatter");
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
}
