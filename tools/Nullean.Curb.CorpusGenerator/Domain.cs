namespace Nullean.Curb.CorpusGenerator;

/// <summary>
/// The hand-written model every generated file compiles against, in the multi-targeted project. Kept to what
/// netstandard2.0 can compile with an <c>IsExternalInit</c> polyfill.
/// </summary>
internal static class Domain
{
	public static IEnumerable<(string Name, string Source)> Files()
	{
		yield return ("Polyfills.cs", """
			#if NETSTANDARD2_0
			namespace System.Runtime.CompilerServices
			{
				/// <summary>Lets records and init accessors compile for netstandard2.0.</summary>
				internal static class IsExternalInit
				{
				}
			}
			#endif
			""");

		yield return ("Status.cs", """
			namespace Corpus.Abstractions;

			/// <summary>Where an account is in its lifecycle.</summary>
			public enum Status
			{
				Pending,
				Active,
				Suspended,
				Closed,
			}

			/// <summary>What a caller may do.</summary>
			[System.Flags]
			public enum Permissions
			{
				None = 0,
				Read = 1,
				Write = 2,
				Delete = 4,
				Admin = Read | Write | Delete
			}
			""");

		yield return ("Customer.cs", """
			namespace Corpus.Abstractions;

			/// <summary>Someone who places orders.</summary>
			public sealed record Customer(int Id, string Name, string? Email, Status Status)
			{
				public bool IsActive => Status == Status.Active;

				public Permissions Permissions { get; init; } = Permissions.Read;
			}
			""");

		yield return ("Order.cs", """
			using System;
			using System.Collections.Generic;
			using System.Linq;

			namespace Corpus.Abstractions;

			/// <summary>One line of an order.</summary>
			public sealed record OrderLine(string Sku, int Quantity, decimal UnitPrice)
			{
				public decimal Total => Quantity * UnitPrice;
			}

			/// <summary>An order and its lines.</summary>
			public sealed record Order(int Id, int CustomerId, DateTime PlacedAt, IReadOnlyList<OrderLine> Lines)
			{
				public decimal Total => Lines.Sum(l => l.Total);

				public bool IsEmpty => Lines.Count == 0;
			}
			""");

		yield return ("Money.cs", """
			using System;

			namespace Corpus.Abstractions;

			/// <summary>An amount in a currency.</summary>
			public readonly record struct Money(decimal Amount, string Currency)
			{
				public static Money Zero(string currency) => new Money(0m, currency);

				public static Money operator +(Money left, Money right)
				{
					if (left.Currency != right.Currency)
						throw new InvalidOperationException("currencies differ");
					return new Money(left.Amount + right.Amount, left.Currency);
				}

				public override string ToString() => Amount.ToString("0.00") + " " + Currency;
			}
			""");

		yield return ("Repository.cs", """
			using System;
			using System.Collections.Generic;
			using System.Linq;

			namespace Corpus.Abstractions;

			/// <summary>Somewhere entities live.</summary>
			public interface IRepository<T> where T : class
			{
				T? Find(int id);

				IEnumerable<T> All();

				void Add(T item);

				bool Remove(int id);
			}

			/// <summary>A repository in a dictionary, for tests and samples.</summary>
			public sealed class InMemoryRepository<T> : IRepository<T> where T : class
			{
				private readonly Dictionary<int, T> _items = new Dictionary<int, T>();
				private readonly Func<T, int> _idOf;

				public InMemoryRepository(Func<T, int> idOf) => _idOf = idOf;

				public T? Find(int id) => _items.TryGetValue(id, out var item) ? item : null;

				public IEnumerable<T> All() => _items.Values.ToList();

				public void Add(T item) => _items[_idOf(item)] = item;

				public bool Remove(int id) => _items.Remove(id);
			}
			""");

		yield return ("Clock.cs", """
			using System;

			namespace Corpus.Abstractions;

			/// <summary>The current time, injectable.</summary>
			public interface IClock
			{
				DateTime UtcNow { get; }
			}

			/// <summary>The real clock.</summary>
			public sealed class SystemClock : IClock
			{
				public static readonly SystemClock Instance = new SystemClock();

				public DateTime UtcNow => DateTime.UtcNow;
			}

			/// <summary>A clock that only moves when told to.</summary>
			public sealed class FixedClock : IClock
			{
				public FixedClock(DateTime now) => UtcNow = now;

				public DateTime UtcNow { get; private set; }

				public void Advance(TimeSpan by) => UtcNow = UtcNow.Add(by);
			}
			""");

		yield return ("Result.cs", """
			using System;

			namespace Corpus.Abstractions;

			/// <summary>A value or the reason there is none.</summary>
			public sealed class Result<T>
			{
				private Result(T? value, string? error)
				{
					Value = value;
					Error = error;
				}

				public T? Value { get; }

				public string? Error { get; }

				public bool IsOk => Error is null;

				public static Result<T> Ok(T value) => new Result<T>(value, null);

				public static Result<T> Fail(string error) => new Result<T>(default, error);

				public TOut Match<TOut>(Func<T, TOut> ok, Func<string, TOut> fail) => IsOk ? ok(Value!) : fail(Error!);
			}

			/// <summary>Argument checks that read as one line.</summary>
			public static class Guard
			{
				public static T NotNull<T>(T? value, string name) where T : class =>
					value ?? throw new ArgumentNullException(name);

				public static string NotEmpty(string? value, string name) =>
					string.IsNullOrEmpty(value) ? throw new ArgumentException("must not be empty", name) : value!;
			}
			""");

		yield return ("Events.cs", """
			using System;

			namespace Corpus.Abstractions;

			/// <summary>Where notable things are reported.</summary>
			public interface IEventSink
			{
				void Publish(string name, object? payload = null);
			}

			/// <summary>Raised when something changed.</summary>
			public sealed class ChangedEventArgs : EventArgs
			{
				public ChangedEventArgs(string what) => What = what;

				public string What { get; }
			}
			""");
	}
}
