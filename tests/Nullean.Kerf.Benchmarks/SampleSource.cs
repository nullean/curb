namespace Nullean.Kerf.Benchmarks;

internal static class SampleSource
{
	/// <summary>A representative file: usings, namespace, type, members, control flow, LINQ, comments.</summary>
	public const string Typical = """
		using System;
		using System.Collections.Generic;
		using System.Linq;

		namespace Sample.Library;

		/// <summary>Aggregates orders for a customer.</summary>
		public sealed class OrderAggregator(IReadOnlyList<Order> orders)
		{
			private readonly Dictionary<string, decimal> _totals = new(StringComparer.Ordinal);

			public decimal TotalFor(string customer)
			{
				if (string.IsNullOrWhiteSpace(customer))
					throw new ArgumentException("customer must be provided", nameof(customer));

				if (_totals.TryGetValue(customer, out var cached))
					return cached;

				var total = orders
					.Where(o => o.Customer == customer && o.Status != OrderStatus.Cancelled)
					.Sum(o => o.Lines.Sum(l => l.Quantity * l.UnitPrice));

				_totals[customer] = total;
				return total;
			}

			public IEnumerable<(string Customer, decimal Total)> Ranked() =>
				orders.Select(o => o.Customer)
					.Distinct(StringComparer.Ordinal)
					.Select(c => (Customer: c, Total: TotalFor(c)))
					.OrderByDescending(x => x.Total);

			public string Describe(OrderStatus status) => status switch
			{
				OrderStatus.Pending => "pending",
				OrderStatus.Shipped => "shipped",
				OrderStatus.Cancelled => "cancelled",
				_ => throw new ArgumentOutOfRangeException(nameof(status), status, null),
			};

			// Deliberately verbose so the layout engine has something to reflow.
			public void Report(Action<string> write)
			{
				foreach (var (customer, total) in Ranked())
				{
					try
					{
						write($"{customer,-30} {total,12:C} across {orders.Count(o => o.Customer == customer)} order(s)");
					}
					catch (Exception ex) when (ex is not OutOfMemoryException)
					{
						write($"failed to report {customer}: {ex.Message}");
					}
					finally
					{
						_totals.Remove(customer);
					}
				}
			}
		}

		public enum OrderStatus { Pending, Shipped, Cancelled }

		public record Order(string Customer, OrderStatus Status, IReadOnlyList<OrderLine> Lines);

		public record OrderLine(int Quantity, decimal UnitPrice);
		""";
}
