using System.Text;

namespace Nullean.Curb.CorpusGenerator;

/// <summary>Types, and the files that hold them.</summary>
public static partial class Templates
{
	/// <summary>A class with the service fields, a constructor, and <paramref name="members"/> members drawn from the pool.</summary>
	public static string ServiceClass(Ctx c, int members, bool netStandard)
	{
		var name = c.Type(c.R.Pick("Service", "Processor", "Coordinator", "Builder", "Store", "Manager", ""));
		c.TypeName = name;
		c.HasServiceFields = true;
		var primary = !netStandard && c.R.Chance(0.3);
		var disposable = c.R.Chance(0.2);
		var b = new StringBuilder();

		if (c.R.Chance(0.5))
		{
			b.Append("/// <summary>Coordinates ").Append(c.Noun().ToLowerInvariant()).Append(" work for one tenant.</summary>\n");
		}
		if (c.R.Chance(0.15))
		{
			c.Require("System.Diagnostics");
			b.Append("[DebuggerDisplay(\"{_name,nq}\")]\n");
		}

		b.Append(c.R.Pick("public sealed ", "public ", "internal sealed ", "public sealed partial ")).Append("class ").Append(name);
		if (primary)
		{
			b.Append("(IClock clock, IRepository<Customer> customers, string name)");
		}
		if (disposable)
		{
			b.Append(" : IDisposable");
		}
		b.Append("\n{\n");

		// No IDE0040 site in the multi-targeted project: KnownDefects, item 9.
		var modifier = netStandard ? "private " : c.Either("", "private ", 0.15);
		if (primary)
		{
			b.Append(modifier).Append("readonly IClock _clock = clock;\n");
			b.Append("private readonly IRepository<Customer> _customers = customers;\n");
			b.Append("private ").Append(c.Either("", "readonly ")).Append("string _name = name;\n");
		}
		else
		{
			b.Append(modifier).Append("readonly IClock _clock;\n");
			b.Append("private readonly IRepository<Customer> _customers;\n");
			b.Append("private ").Append(netStandard ? "readonly " : c.Either("", "readonly ")).Append("string _name;\n");
		}

		// No IDE0044 site in the multi-targeted project: KnownDefects, item 9.
		b
			.Append("private ")
			.Append(netStandard ? "readonly " : c.Either("", "readonly ", 0.3))
			.Append("List<string> _log = ")
			.Append(c.Either("new List<string>()", "new()", 0.5))
			.Append(";\n");
		b.Append("private int _version;\n\n");

		if (!primary)
		{
			b.Append("public ").Append(name).Append("(IClock clock, IRepository<Customer> customers, string name)\n{\n");
			b.Append("_clock = clock ?? throw new ArgumentNullException(nameof(clock));\n");
			b.Append("_customers = customers;\n_name = name;\n}\n\n");
		}

		b.Append("public DateTime Now => _clock.UtcNow;\n\n");
		b.Append("public void Touch() => _version++;\n\n");

		var pool = netStandard ? Members.Where(m => m.NetStandard).ToArray() : Members;
		for (var i = 0; i < members; i++)
		{
			b.Append(c.R.Pick(pool).Write(c)).Append("\n\n");
		}

		if (c.R.Chance(0.2))
		{
			b.Append("public override string ToString() => $\"").Append(name).Append(" {_name} v{_version}\";\n\n");
		}

		if (disposable)
		{
			b.Append("public void Dispose()\n{\n_log.Clear();\nGC.SuppressFinalize(this);\n}\n\n");
		}

		if (c.R.Chance(0.1))
		{
			b.Append("private sealed class Entry").Append(c.N()).Append("\n{\npublic string Key { get; set; } = \"\";\npublic int Hits;\n}\n");
		}

		b.Append("}\n");
		c.HasServiceFields = false;
		return b.ToString();
	}

	/// <summary>A type that does not need the service fields, picked at random.</summary>
	public static string OtherType(Ctx c, bool netStandard)
	{
		var pick = netStandard ? c.R.Pick(StandardTypes) : c.R.Pick(AllTypes);
		return pick(c);
	}

	private static readonly Func<Ctx, string>[] StandardTypes =
	[
		// Neither struct candidate: their IDE0250 and IDE0251 sites would hit KnownDefects, item 9.
		RecordType,
		EnumType,
		ExceptionType,
		DelegatesType,
		AbstractHierarchy,
		GenericCache,
		StaticDefaultsStandard,
		PartialType,
		OperatorStruct,
	];

	private static readonly Func<Ctx, string>[] AllTypes =
	[
		RecordType,
		EnumType,
		ExceptionType,
		DelegatesType,
		AbstractHierarchy,
		GenericCache,
		StaticDefaults,
		PartialType,
		StructReadonlyCandidate,
		StructMemberCandidate,
		OperatorStruct,
		InterfaceType,
		ExtensionsType,
		FileLocalType,
		DisposableType,
		RecordStructType,
	];

	private static string RecordType(Ctx c)
	{
		var name = c.Type("Summary");
		return $$"""
			/// <summary>A read model for one {{c.Noun().ToLowerInvariant()}}.</summary>
			public sealed record {{name}}(int Id, string Name, decimal Total, IReadOnlyList<string> Tags)
			{
				public static {{name}} Empty { get; } = new {{name}}(0, "", 0m, Array.Empty<string>());

				public string Display => $"{Name} ({Total:N2})";

				public bool HasTag(string tag) => Tags.Contains(tag, StringComparer.OrdinalIgnoreCase);

				public {{name}} WithTotal(decimal total) => this with { Total = total };
			}
			""";
	}

	private static string RecordStructType(Ctx c) =>
		$$"""
		public record struct {{c.Type("Pair")}}(int Left, int Right)
		{
			public void Swap() => (Left, Right) = (Right, Left);
		}

		public readonly record struct {{c.Type("Key")}}(string Scope, int Id)
		{
			public override string ToString() => $"{Scope}/{Id}";
		}
		""";

	private static string EnumType(Ctx c)
	{
		var flags = c.R.Chance(0.4);
		var names = new[] { "None", "First", "Second", "Third", "Fourth", "Fifth", "Sixth" }.Take(c.R.Next(3, 7)).ToArray();
		var b = new StringBuilder();
		if (flags)
		{
			b.Append("[Flags]\n");
		}
		b.Append("public enum ").Append(c.Type(flags ? "Options" : "Kind")).Append(c.R.Chance(0.2) ? " : byte" : "").Append("\n{\n");
		for (var i = 0; i < names.Length; i++)
		{
			if (c.R.Chance(0.2))
			{
				b.Append("/// <summary>The ").Append(names[i].ToLowerInvariant()).Append(" one.</summary>\n");
			}
			b.Append(names[i]);
			if (flags)
			{
				b.Append(" = ").Append(i == 0 ? "0" : (1 << (i - 1)).ToString(System.Globalization.CultureInfo.InvariantCulture));
			}
			else if (c.R.Chance(0.2))
			{
				b.Append(" = ").Append(i * 10);
			}
			if (i < names.Length - 1 || c.R.Chance(0.5))
			{
				b.Append(',');
			}
			b.Append('\n');
		}

		return b.Append("}\n").ToString();
	}

	private static string ExceptionType(Ctx c)
	{
		var name = c.Type("Exception");
		return $$"""
			public sealed class {{name}} : Exception
			{
				public {{name}}() { }

				public {{name}}(string message) : base(message) { }

				public {{name}}(string message, Exception inner)
					: base(message, inner)
				{
				}

				public int Code { get; set; }
			}
			""";
	}

	private static string DelegatesType(Ctx c) =>
		$$"""
		public delegate void {{c.Type("Handler")}}(object sender, ChangedEventArgs e);

		public delegate TResult {{c.Type("Projector")}}<in T, out TResult>(T input) where T : notnull;
		""";

	private static string AbstractHierarchy(Ctx c)
	{
		var shape = c.Type("Shape");
		var circle = c.Type("Circle");
		var square = c.Type("Square");
		return $$"""
			public abstract class {{shape}}
			{
				protected {{shape}}(string name) => Name = name;

				public string Name { get; }

				public abstract double Area();

				public virtual string Describe() => $"{Name}: {Area():F2}";
			}

			public sealed class {{circle}} : {{shape}}
			{
				private readonly double _radius;

				public {{circle}}(double radius) : base("circle") => _radius = radius;

				public override double Area() => Math.PI * _radius * _radius;

				public override string Describe() => base.Describe() + " (round)";
			}

			public class {{square}} : {{shape}}
			{
				public {{square}}(double side)
					: base("square")
				{
					Side = side;
				}

				public double Side { get; }

				public override double Area() => Side * Side;

				public new string ToString() => Describe();
			}
			""";
	}

	private static string GenericCache(Ctx c)
	{
		c.Require("System.Collections");
		var name = c.Type("Cache");
		return $$"""
			public sealed class {{name}}<TKey, TValue> : IEnumerable<KeyValuePair<TKey, TValue>>
				where TKey : notnull
			{
				private readonly Dictionary<TKey, TValue> _items = new Dictionary<TKey, TValue>();
				private readonly int _capacity;

				public {{name}}(int capacity = {{c.R.Next(4, 256)}}) => _capacity = capacity;

				public int Count => _items.Count;

				public TValue this[TKey key]
				{
					get => _items[key];
					set
					{
						if (_items.Count >= _capacity && !_items.ContainsKey(key))
							_items.Remove(_items.Keys.First());
						_items[key] = value;
					}
				}

				public bool TryGet(TKey key, out TValue? value)
				{
					if (_items.TryGetValue(key, out var found))
					{
						value = found;
						return true;
					}
					value = default;
					return false;
				}

				public IEnumerator<KeyValuePair<TKey, TValue>> GetEnumerator() => _items.GetEnumerator();

				IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

				private sealed class Node
				{
					public TKey? Key { get; set; }

					public Node? Next { get; set; }
				}
			}
			""";
	}

	private static string StaticDefaults(Ctx c) => StaticDefaults(c, netStandard: false);

	private static string StaticDefaultsStandard(Ctx c) => StaticDefaults(c, netStandard: true);

	private static string StaticDefaults(Ctx c, bool netStandard) =>
		$$"""
		public static class {{c.Type("Defaults")}}
		{
			public const int MaxItems = {{c.R.Next(10, 1000)}};
			public const string Prefix = "{{c.Text()}}";
			{{(netStandard ? "internal const" : c.Either("const", "internal const"))}} int Hidden = 7;
			public static readonly TimeSpan Timeout = TimeSpan.FromSeconds({{c.R.Next(1, 120)}});
			private static int _counter;

			public static int Next() => Interlocked.Increment(ref _counter);

			public static string Key(string name) => Prefix + ":" + name + ":" + MaxItems;
		}
		""";

	private static string PartialType(Ctx c)
	{
		var name = c.Type("Model");
		return $$"""
			public partial class {{name}}
			{
				private int _changes;

				public string Name { get; private set; } = "";

				partial void OnRenamed(string previous);

				public void Rename(string name)
				{
					var previous = Name;
					Name = name;
					_changes++;
					OnRenamed(previous);
				}
			}

			public partial class {{name}}
			{
				partial void OnRenamed(string previous)
				{
					if (previous.Length == 0 && _changes > {{c.R.Next(1, 5)}}) Name = Name.Trim();
				}
			}
			""";
	}

	/// <summary>Every field readonly but the struct is not, so IDE0250 reports it.</summary>
	private static string StructReadonlyCandidate(Ctx c)
	{
		var name = c.Type("Point");
		return $$"""
			public struct {{name}}
			{
				public readonly int X;
				public readonly int Y;

				public {{name}}(int x, int y)
				{
					X = x;
					Y = y;
				}

				public int LengthSquared() => X * X + Y * Y;
			}
			""";
	}

	/// <summary>A mutable struct with a member that mutates nothing, so IDE0251 reports that member.</summary>
	private static string StructMemberCandidate(Ctx c) =>
		$$"""
		public struct {{c.Type("Counter")}}
		{
			private int _count;

			public void Increment() => _count++;

			public int Peek() => _count + {{c.R.Next(0, 3)}};
		}
		""";

	private static string OperatorStruct(Ctx c)
	{
		var name = c.Type("Fraction");
		return $$"""
			public readonly struct {{name}} : IEquatable<{{name}}>
			{
				public {{name}}(long numerator, long denominator)
				{
					if (denominator == 0) throw new DivideByZeroException();
					Numerator = numerator;
					Denominator = denominator;
				}

				public long Numerator { get; }

				public long Denominator { get; }

				public static {{name}} operator +({{name}} a, {{name}} b) => new {{name}}(a.Numerator * b.Denominator + b.Numerator * a.Denominator, a.Denominator * b.Denominator);

				public static {{name}} operator -({{name}} a) => new {{name}}(-a.Numerator, a.Denominator);

				public static bool operator ==({{name}} a, {{name}} b) => a.Equals(b);

				public static bool operator !=({{name}} a, {{name}} b) => !a.Equals(b);

				public static implicit operator double({{name}} f) => (double)f.Numerator / f.Denominator;

				public static explicit operator {{name}}(long value) => new {{name}}(value, 1);

				public bool Equals({{name}} other) => Numerator * other.Denominator == other.Numerator * Denominator;

				public override bool Equals(object? obj) => obj is {{name}} other && Equals(other);

				public override int GetHashCode() => ((double)this).GetHashCode();
			}
			""";
	}

	private static string InterfaceType(Ctx c)
	{
		var name = "I" + c.Type("Source");
		var zero = "I" + c.Type("Zero");
		return $$"""
			public interface {{name}}
			{
				string Name { get; }

				Task<int> CountAsync(CancellationToken cancellationToken = default);

				bool IsEnabled() => Name.Length > 0;

				string Describe()
				{
					return $"{Name} ({(IsEnabled() ? "on" : "off")})";
				}
			}

			public interface {{zero}}<TSelf> where TSelf : {{zero}}<TSelf>
			{
				static abstract TSelf Zero { get; }

				static virtual bool IsZero(TSelf value) => value.Equals(TSelf.Zero);
			}
			""";
	}

	private static string ExtensionsType(Ctx c)
	{
		var n = c.N();
		return $$"""
			public static class {{c.Noun()}}Extensions{{n}}
			{
				public static bool IsVip{{n}}(this Customer customer, decimal threshold = {{c.R.Next(100, 10000)}}m) =>
					customer.IsActive && customer.Permissions.HasFlag(Permissions.Write) && threshold > 0;

				public static IEnumerable<T> WhereNotNull{{n}}<T>(this IEnumerable<T?> source) where T : class
				{
					foreach (var item in source)
						if (item is not null) yield return item;
				}

				extension(Customer customer)
				{
					public bool HasEmail{{n}} => customer.Email is not null;

					public string Display{{n}}() => $"{customer.Name} <{customer.Email ?? "?"}>";
				}

				extension<T>(IReadOnlyCollection<T> source)
				{
					public bool IsEmpty{{n}} => source.Count == 0;
				}
			}
			""";
	}

	private static string FileLocalType(Ctx c) =>
		$$"""
		file sealed class {{c.Type("Helper")}}
		{
			public static int Twice(int value) => value * {{c.R.Next(2, 9)}};
		}
		""";

	private static string DisposableType(Ctx c)
	{
		var name = c.Type("Lease");
		return $$"""
			public sealed class {{name}} : IDisposable, IAsyncDisposable
			{
				private readonly Action _release;
				private bool _disposed;

				public {{name}}(Action release) => _release = release;

				public void Dispose()
				{
					if (_disposed) return;
					_disposed = true;
					_release();
				}

				public ValueTask DisposeAsync()
				{
					Dispose();
					return ValueTask.CompletedTask;
				}
			}
			""";
	}
}
