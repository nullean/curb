using System.Text;

namespace Nullean.Curb.CorpusGenerator;

/// <summary>The test-like project and the console app: a hand-rolled test framework, test classes, top-level statements.</summary>
public static class TestTemplates
{
	/// <summary>Attributes and assertions, so the test project needs no package and builds offline.</summary>
	public const string Framework =
		"""
		using System.Reflection;

		namespace Corpus.Tests;

		[AttributeUsage(AttributeTargets.Method)]
		public sealed class FactAttribute : Attribute
		{
			public string? Skip { get; set; }
		}

		[AttributeUsage(AttributeTargets.Method)]
		public sealed class TheoryAttribute : Attribute
		{
		}

		[AttributeUsage(AttributeTargets.Method, AllowMultiple = true)]
		public sealed class InlineDataAttribute(params object?[] data) : Attribute
		{
			public object?[] Data { get; } = data;
		}

		public static class Assert
		{
			public static void True(bool condition, string? message = null)
			{
				if (!condition) throw new AssertionException(message ?? "expected true");
			}

			public static void False(bool condition) => True(!condition, "expected false");

			public static void Equal<T>(T expected, T actual)
			{
				if (!EqualityComparer<T>.Default.Equals(expected, actual))
					throw new AssertionException($"expected {expected}, got {actual}");
			}

			public static void NotNull(object? value) => True(value is not null, "expected a value");

			public static void Null(object? value) => True(value is null, "expected null");

			public static T Throws<T>(Action action) where T : Exception
			{
				try
				{
					action();
				}
				catch (T expected)
				{
					return expected;
				}
				throw new AssertionException($"expected {typeof(T).Name}");
			}
		}

		public sealed class AssertionException(string message) : Exception(message);

		public static class Runner
		{
			public static int Run(Assembly assembly)
			{
				var failures = 0;
				foreach (var type in assembly.GetTypes())
				foreach (var method in type.GetMethods())
				{
					if (method.GetCustomAttribute<FactAttribute>() is not { Skip: null }) continue;
					try
					{
						var instance = Activator.CreateInstance(type);
						var result = method.Invoke(instance, null);
						if (result is Task task) task.GetAwaiter().GetResult();
					}
					catch (Exception e)
					{
						failures++;
						Console.Error.WriteLine($"{type.Name}.{method.Name}: {e.InnerException?.Message ?? e.Message}");
					}
				}
				return failures;
			}
		}
		""";

	public const string TestProgram =
		"""
		using System.Reflection;
		using Corpus.Tests;

		return Runner.Run(Assembly.GetExecutingAssembly());
		""";

	/// <summary>The console app's entry point, as top-level statements.</summary>
	public static string CliProgram(Rng r) =>
		$$"""
		using Corpus.Abstractions;

		var repository = new InMemoryRepository<Customer>(c => c.Id);
		IClock clock = args.Contains("--fixed") ? new FixedClock(new DateTime(2024, 1, 1)) : SystemClock.Instance;

		if (args.Length == 0)
		{
			Console.Error.WriteLine("usage: corpus <list|add|stats> [options]");
			return 1;
		}

		for (var i = 0; i < {{r.Next(3, 30)}}; i++)
			repository.Add(new Customer(i, $"customer {i}", i % 3 == 0 ? null : $"c{i}@example.com", (Status)(i % 4)));

		var exitCode = args[0] switch
		{
			"list" => List(repository),
			"add" when args.Length > 1 => Add(repository, args[1..]),
			"stats" => await StatsAsync(repository, clock),
			var unknown => Unknown(unknown),
		};

		return exitCode;

		static int List(IRepository<Customer> customers)
		{
			foreach (var customer in customers.All().OrderBy(c => c.Name))
				Console.WriteLine($"{customer.Id,4} {customer.Name,-20} {customer.Status}");
			return 0;
		}

		static int Add(IRepository<Customer> customers, string[] names)
		{
			var next = customers.All().Select(c => c.Id).DefaultIfEmpty().Max() + 1;
			foreach (var name in names) customers.Add(new Customer(next++, name, null, Status.Pending));
			return 0;
		}

		static async Task<int> StatsAsync(IRepository<Customer> customers, IClock clock)
		{
			await Task.Yield();
			var groups = customers.All().GroupBy(c => c.Status).Select(g => (Status: g.Key, Count: g.Count()));
			foreach (var (status, count) in groups) Console.WriteLine($"{clock.UtcNow:u} {status}: {count}");
			return 0;
		}

		static int Unknown(string command)
		{
			Console.Error.WriteLine($"unknown command '{command}'");
			return 2;
		}
		""";

	public static string TestClass(Ctx c, int tests)
	{
		var b = new StringBuilder();
		b.Append("public sealed class ").Append(c.Type("Tests")).Append("\n{\n");
		b.Append("private readonly InMemoryRepository<Customer> _repository = new InMemoryRepository<Customer>(c => c.Id);\n\n");
		for (var i = 0; i < tests; i++)
		{
			b.Append(c.R.Pick(Tests)(c)).Append("\n\n");
		}
		return b.Append("}\n").ToString();
	}

	private static readonly Func<Ctx, string>[] Tests =
	[
		c =>
			$$"""
			[Fact]
			public void {{c.Verb()}}_finds_an_added_customer_{{c.N()}}()
			{
				// Arrange
				_repository.Add(new Customer({{c.Int()}}, "{{c.Text()}}", null, Status.Active));

				// Act
				var found = _repository.All().FirstOrDefault();

				// Assert
				Assert.NotNull(found);
				Assert.True(found!.IsActive);
			}
			""",
		c =>
			$$"""
			[Theory]
			[InlineData(1, 2, 3)]
			[InlineData(-1, 1, 0)]
			[InlineData({{c.Int()}}, 0, {{c.Int()}})]
			public void Adds_{{c.N()}}(int a, int b, int expected) => Assert.True(a + b == expected || expected > 0);
			""",
		c =>
			$$"""
			[Fact]
			public async Task {{c.Verb()}}_completes_asynchronously_{{c.N()}}()
			{
				var clock = new FixedClock(new DateTime(2024, {{c.R.Next(1, 13)}}, 1, 0, 0, 0, DateTimeKind.Utc));
				await Task.Delay(1);
				clock.Advance(TimeSpan.FromHours(1));
				Assert.Equal(1, clock.UtcNow.Hour);
			}
			""",
		c =>
			$$"""
			[Fact]
			public void Rejects_an_empty_name_{{c.N()}}()
			{
				var exception = Assert.Throws<ArgumentException>(() => Guard.NotEmpty("", "name"));
				Assert.Equal("name", exception.ParamName);
			}
			""",
		c =>
			$$"""
			[Fact(Skip = "flaky on CI")]
			public void Totals_an_order_{{c.N()}}()
			{
				var order = new Order(1, 2, DateTime.UtcNow, new[]
				{
					new OrderLine("a", 2, 1.5m),
					new OrderLine("b", 1, {{c.Int()}}m),
				});
				Assert.Equal(order.Lines.Sum(l => l.Total), order.Total);
				Assert.False(order.IsEmpty);
			}
			""",
		c =>
			$$"""
			[Fact]
			public void Result_maps_both_ways_{{c.N()}}()
			{
				var ok = Result<int>.Ok({{c.Int()}});
				var failed = Result<int>.Fail("{{c.Text()}}");
				Assert.Equal("ok", ok.Match(_ => "ok", e => e));
				Assert.Equal("{{c.Text()}}".Length > 0, !failed.IsOk);
			}
			""",
	];
}
