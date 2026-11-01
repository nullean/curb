namespace Nullean.Curb.CorpusGenerator;

/// <summary>A member, or a few that belong together, that compiles inside any class the synthesiser writes.</summary>
/// <param name="Service">Needs the service fields (<c>_clock</c>, <c>_customers</c>, <c>_log</c>, <c>_version</c>, <c>_name</c>).</param>
/// <param name="NetStandard">Compiles for netstandard2.0 too.</param>
public sealed record MemberTemplate(string Name, Func<Ctx, string> Write, bool Service = false, bool NetStandard = false);

/// <summary>
/// Members covering the C# the option catalog formats: every statement kind, queries, patterns, lambdas, initializers,
/// strings of every kind, generics with constraints, attributes and directives. Each is written the way people write
/// it, and some on purpose the way an analyser would flag, so the cleanup gate has sites to fix.
/// </summary>
public static partial class Templates
{
	public static readonly MemberTemplate[] Members =
	[
		new(
			"guarded-lookup",
			c =>
				$$"""
			public Customer? {{c.Method()}}(int id, string? fallbackName = null)
			{
				if (id <= 0)
					return null;
				var customer = _customers.Find(id);
				if (customer is null)
				{
					_log.Add("missing " + id);
					return fallbackName is null ? null : new Customer(id, fallbackName, null, Status.Pending);
				}
				else if (customer.Status == Status.Closed) return null;
				return customer.IsActive ? customer : customer with { Status = Status.Active };
			}
			""",
			Service: true
		),
		new(
			"query-join",
			c =>
				$$"""
			public IReadOnlyList<string> {{c.Method()}}(IEnumerable<Order> orders, IEnumerable<Customer> customers)
			{
				var rows = from o in orders
						   join cu in customers on o.CustomerId equals cu.Id
						   where o.Total > {{c.Int()}}m && cu.Status != Status.Closed
						   orderby o.PlacedAt descending, cu.Name
						   select new { cu.Name, o.Id, Total = o.Total };
				return rows.Select(r => $"{r.Name}: #{r.Id} {r.Total:N2}").ToList();
			}
			""",
			NetStandard: true
		),
		new(
			"query-group",
			c =>
				$$"""
			public Dictionary<Status, int> {{c.Method()}}(IEnumerable<Customer> customers)
			{
				var groups =
					from customer in customers
					let length = customer.Name.Length
					where length > {{c.R.Next(0, 5)}}
					group customer by customer.Status into byStatus
					orderby byStatus.Key
					select new { Status = byStatus.Key, Count = byStatus.Count() };
				return groups.ToDictionary(g => g.Status, g => g.Count);
			}
			""",
			NetStandard: true
		),
		new(
			"method-chain",
			c =>
				$$"""
			public IEnumerable<KeyValuePair<string, decimal>> {{c.Method()}}(IEnumerable<Order> orders, int top = {{c.R.Next(3, 20)}})
			{
				return orders.Where(o => !o.IsEmpty).SelectMany(o => o.Lines.Select(l => new { o.CustomerId, l.Sku, l.Total })).GroupBy(x => x.Sku).Select(g => new KeyValuePair<string, decimal>(g.Key, g.Sum(x => x.Total))).OrderByDescending(p => p.Value).ThenBy(p => p.Key, StringComparer.Ordinal).Take(top);
			}
			""",
			NetStandard: true
		),
		new(
			"method-chain-broken",
			c =>
				$$"""
			public int {{c.Method()}}(IReadOnlyList<Customer> customers) =>
				customers
					.Where(c => c.Email != null)
					.Select(c => c.Email!.Trim().ToLowerInvariant())
					.Distinct()
					.Count();
			""",
			NetStandard: true
		),
		new(
			"async-method",
			c =>
				$$"""
			public async Task<int> {{c.Method()}}(IEnumerable<int> ids, CancellationToken cancellationToken = default)
			{
				var processed = 0;
				try
				{
					foreach (var id in ids)
					{
						cancellationToken.ThrowIfCancellationRequested();
						await Task.Delay(TimeSpan.FromMilliseconds({{c.R.Next(1, 50)}}), cancellationToken).ConfigureAwait(false);
						if (_customers.Find(id) is { } found && found.IsActive)
							processed++;
					}
				}
				catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
				{
					_log.Add("timed out");
					throw;
				}
				finally
				{
					_version++;
				}

				return processed;
			}
			""",
			Service: true
		),
		new(
			"async-stream",
			c =>
			{
				c.Require("System.Runtime.CompilerServices");
				var name = c.Method();
				return $$"""
				public async IAsyncEnumerable<Customer> {{name}}([EnumeratorCancellation] CancellationToken cancellationToken = default)
				{
					foreach (var customer in _customers.All())
					{
						await Task.Yield();
						if (cancellationToken.IsCancellationRequested) yield break;
						yield return customer;
					}
				}

				public async Task<int> Count{{name}}()
				{
					var count = 0;
					await foreach (var customer in {{name}}().ConfigureAwait(false))
						if (customer.IsActive) count += 1;
					return count;
				}
				""";
			},
			Service: true
		),
		new(
			"switch-statement",
			c =>
				$$"""
			public static string {{c.Method()}}(Status status, bool verbose)
			{
				string label;
				switch (status)
				{
					case Status.Pending:
					case Status.Suspended:
						label = verbose ? "waiting for review" : "waiting";
						break;
					case Status.Active:
					{
						label = "active";
						break;
					}
					case Status.Closed when verbose:
						label = "closed for good";
						break;
					case Status.Closed:
						goto default;
					default:
						label = "unknown";
						break;
				}

				return label;
			}
			""",
			NetStandard: true
		),
		new(
			"switch-expression",
			c =>
				$$"""
			public static decimal {{c.Method()}}(Order order, Customer customer) => (order, customer) switch
			{
				({ Total: > 1000m }, { Status: Status.Active }) => order.Total * 0.9m,
				({ Lines.Count: 0 }, _) => 0m,
				(_, { Permissions: var p }) when (p & Permissions.Admin) == Permissions.Admin => order.Total * 0.5m,
				({ Total: >= 100m and < 1000m }, not null) => order.Total - {{c.R.Next(1, 20)}}m,
				_ => order.Total,
			};
			"""
		),
		new(
			"relational-patterns",
			c =>
				$$"""
			public static string {{c.Method()}}(int value)
			{
				return value switch
				{
					< 0 => "negative",
					0 => "zero",
					> 0 and <= {{c.R.Next(5, 15)}} => "small",
					> 100 and < 10_000 or 50_000 => "large",
					_ => "medium"
				};
			}
			""",
			NetStandard: true
		),
		new(
			"list-patterns",
			c =>
				$$"""
			public static string {{c.Method()}}(int[] values) => values switch
			{
				[] => "empty",
				[var only] => $"one: {only}",
				[var first, .., var last] when first == last => "bookends",
				[0, 1, ..] => "counting",
				[_, _, .. var rest] => $"and {rest.Length} more",
			};
			"""
		),
		new(
			"for-loops",
			c =>
				$$"""
			public static int[,] {{c.Method()}}(int rows, int columns)
			{
				var grid = new int[rows, columns];
				for (int i = 0, n = rows; i < n; i++)
				{
					for (var j = 0; j < columns; ++j)
					{
						if (j % {{c.R.Next(2, 5)}} == 0) continue;
						if (i * j > 10_000) break;
						grid[i, j] = i * columns + j;
					}
				}
				for (;;)
				{
					rows--;
					if (rows < 0) break;
				}
				return grid;
			}
			""",
			NetStandard: true
		),
		new(
			"while-loops",
			c =>
				$$"""
			public static long {{c.Method()}}(long seed)
			{
				var value = seed;
				var steps = 0L;
				while (value != 1 && steps < {{c.R.Next(100, 1000)}})
				{
					value = value % 2 == 0 ? value / 2 : 3 * value + 1;
					steps++;
				}
				do
				{
					steps--;
				} while (steps > 0 && steps % 7 != 0);
				return steps;
			}
			""",
			NetStandard: true
		),
		new(
			"try-catch",
			c =>
				$$"""
			public string {{c.Method()}}(string path)
			{
				try
				{
					using var reader = new StringReader(path);
					var first = reader.ReadLine() ?? throw new InvalidDataException("empty");
					return first.Trim();
				}
				catch (InvalidDataException ex) when (ex.Message.Length > 0)
				{
					_log.Add(ex.Message);
					return string.Empty;
				}
				catch (Exception ex) when (ex is ArgumentException or FormatException)
				{
					throw new InvalidOperationException("could not read " + path, ex);
				}
				finally { _version++; }
			}
			""",
			Service: true
		),
		new(
			"using-statements",
			c =>
			{
				c.Require("System.Text");
				return $$"""
				public static byte[] {{c.Method()}}(string text)
				{
					using (var stream = new MemoryStream())
					using (var writer = new StreamWriter(stream, Encoding.UTF8, {{c.R.Next(256, 4096)}}, leaveOpen: true))
					{
						writer.Write(text);
						writer.Flush();
						return stream.ToArray();
					}
				}
				""";
			},
			NetStandard: true
		),
		new(
			"lock",
			c =>
				$$"""
			public int {{c.Method()}}(string entry)
			{
				lock (_log)
				{
					if (_log.Count > {{c.R.Next(100, 10000)}}) _log.RemoveAt(0);
					_log.Add(entry);
					return _log.Count;
				}
			}
			""",
			Service: true
		),
		new(
			"string-builder",
			c =>
			{
				c.Require("System.Text");
				return $$"""
				public static string {{c.Method()}}(IEnumerable<OrderLine> lines, string title)
				{
					var builder = new StringBuilder();
					builder.AppendLine($"== {title} ==");
					foreach (var line in lines)
						builder.Append($"{line.Sku,-12}|{line.Quantity,5}|{line.UnitPrice,10:N2}").AppendLine();
					builder.Append(@"C:\reports\").Append(title).Append(@"\summary.txt");
					return builder.ToString();
				}
				""";
			},
			NetStandard: true
		),
		new(
			"raw-strings",
			c =>
				$$$""""
			public static string {{{c.Method()}}}(Customer customer, int indent)
			{
				var plain = """
					{
					  "kind": "customer",
					  "version": {{{c.R.Next(1, 9)}}}
					}
					""";
				var json = $$"""
					{
					  "id": {{customer.Id}},
					  "name": "{{customer.Name}}",
					  "indent": {{indent}}
					}
					""";
				var single = """He said "hi" and left""";
				return plain.Length > json.Length ? single : json;
			}
			""""
		),
		new(
			"utf8-and-verbatim",
			c =>
				$$"""
			public static int {{c.Method()}}()
			{
				ReadOnlySpan<byte> header = "CURB"u8;
				var path = @"C:\data\""quoted""\file.txt";
				var multi = @"first line
			second line";
				return header.Length + path.Length + multi.Length;
			}
			"""
		),
		new(
			"iterator",
			c =>
				$$"""
			public static IEnumerable<int> {{c.Method()}}(int start, int count)
			{
				if (count <= 0) yield break;
				for (var i = 0; i < count; i++)
				{
					if (i == {{c.R.Next(10, 100)}})
						yield break;
					yield return start + i;
				}
			}
			""",
			NetStandard: true
		),
		new(
			"generic-constraints",
			c =>
				$$"""
			public static TResult {{c.Method()}}<TSource, TKey, TResult>(IEnumerable<TSource> source, Func<TSource, TKey> keySelector, Func<TKey, IEnumerable<TSource>, TResult> resultSelector, TResult empty)
				where TSource : class
				where TKey : notnull, IComparable<TKey>
			{
				var first = source.FirstOrDefault();
				if (first is null) return empty;
				var key = keySelector(first);
				return resultSelector(key, source.Where(s => keySelector(s).CompareTo(key) == 0));
			}
			""",
			NetStandard: true
		),
		new(
			"generic-new",
			c =>
				$$"""
			public static List<T> {{c.Method()}}<T>(int count) where T : class, new()
			{
				var items = new List<T>(capacity: count);
				for (var i = 0; i < count; i++) items.Add(new T());
				return items;
			}
			""",
			NetStandard: true
		),
		new(
			"local-functions",
			c =>
				$$"""
			public static int {{c.Method()}}(int n)
			{
				return Fib(n) + Twice(n);

				static int Fib(int k) => k < 2 ? k : Fib(k - 1) + Fib(k - 2);

				int Twice(int value)
				{
					var doubled = value * 2;
					return doubled > {{c.R.Next(10, 1000)}} ? doubled : value;
				}
			}
			""",
			NetStandard: true
		),
		new(
			"lambdas",
			c =>
				$$"""
			public static Func<int, int> {{c.Method()}}(IEnumerable<int> values)
			{
				var square = (int x) => x * x;
				Func<int, int, int> add = static (a, b) => a + b;
				var parse = int (string s) => int.TryParse(s, out var v) ? v : -1;
				Action<string> log = message =>
				{
					if (message.Length > {{c.R.Next(5, 80)}})
						Console.WriteLine(message.Substring(0, 10));
				};
				var total = values.Aggregate(0, (acc, v) => add(acc, square(v)));
				values.ToList().ForEach(v => log(v.ToString()));
				return x => x + total + parse("1");
			}
			"""
		),
		new(
			"discards",
			c =>
				$$"""
			public static int {{c.Method()}}(IDictionary<string, int> map)
			{
				_ = map.TryGetValue("{{c.Text()}}", out var found);
				Func<int, int, int> first = (a, _) => a;
				var (_, second) = (1, found);
				return first(second, 0);
			}
			""",
			NetStandard: true
		),
		new(
			"object-initializers",
			c =>
				$$"""
			public static object {{c.Method()}}(int id)
			{
				var customer = new Customer(id, "{{c.Text()}}", null, Status.Active) { Permissions = Permissions.Read | Permissions.Write };
				var renamed = customer with { Name = customer.Name.ToUpperInvariant(), Email = "someone@example.com" };
				var lookup = new Dictionary<string, int>
				{
					["one"] = 1,
					["two"] = 2,
					[renamed.Name] = id
				};
				var anonymous = new { customer.Id, renamed.Name, Count = lookup.Count, Nested = new { Flag = true } };
				return anonymous;
			}
			""",
			NetStandard: true
		),
		new(
			"collection-initializers",
			c =>
				$$"""
			public static IReadOnlyList<OrderLine> {{c.Method()}}()
			{
				var lines = new List<OrderLine>
				{
					new OrderLine("A-{{c.Int()}}", 1, 9.99m),
					new OrderLine("B-{{c.Int()}}", 2, 19.5m),
					new("C-{{c.Int()}}", 3, 0.25m),
				};
				var matrix = new[] { new[] { 1, 2 }, new[] { 3, 4, 5 } };
				var grid = new int[,] { { 1, 2 }, { 3, 4 } };
				return matrix.Length + grid.Length > 0 ? lines : Array.Empty<OrderLine>();
			}
			""",
			NetStandard: true
		),
		new(
			"collection-expressions",
			c =>
				$$"""
			public static int[] {{c.Method()}}(int[] extra)
			{
				int[] first = [1, 2, 3];
				List<int> second = [.. first, {{c.Int()}}, .. extra];
				IEnumerable<int> empty = [];
				int[] combined = [.. second.Where(x => x % 2 == 0), .. empty];
				return combined;
			}
			"""
		),
		new(
			"tuples",
			c =>
				$$"""
			public static (int Count, decimal Sum, decimal? Max) {{c.Method()}}(IReadOnlyCollection<OrderLine> lines)
			{
				var (count, sum) = (lines.Count, lines.Sum(l => l.Total));
				decimal? max = count == 0 ? null : lines.Max(l => l.Total);
				var a = 1;
				var b = 2;
				(a, b) = (b, a);
				return (count + a - b + 1, sum, max);
			}
			""",
			NetStandard: true
		),
		new(
			"null-operators",
			c =>
				$$"""
			public string {{c.Method()}}(Customer? customer, string? overrideName)
			{
				var email = customer?.Email?.Trim() ?? "none";
				overrideName ??= customer?.Name;
				var length = overrideName?.Length ?? 0;
				if (customer is not null && customer.Email is { Length: > {{c.R.Next(3, 10)}} })
					_log.Add(customer.Email!);
				return $"{overrideName ?? _name}:{email}:{length}";
			}
			""",
			Service: true
		),
		new("null-conditional-assignment", c =>
		{
			c.Require("System.Text");
			return $$"""
				public static string {{c.Method()}}(StringBuilder? builder)
				{
					builder?.Capacity = {{c.R.Next(16, 512)}};
					return builder?.ToString() ?? "";
				}
				""";
		}),
		new(
			"expression-bodies",
			c =>
				$$"""
			public int Count{{c.N()}} => _log.Count;

			public bool IsEmpty{{c.N()}} => _log.Count == 0 && _version == 0;

			public string Describe{{c.N()}}() => $"{_name} v{_version} ({_log.Count} entries)";
			""",
			Service: true
		),
		new("properties", c =>
		{
			var n = c.N();
			return $$"""
				private decimal _threshold{{n}};

				public string Title{{n}} { get; set; } = "{{c.Text()}}";

				public int Retries{{n}} { get; private set; }

				public DateTime Created{{n}} { get; init; } = DateTime.UtcNow;

				public decimal Threshold{{n}}
				{
					get { return _threshold{{n}}; }
					set
					{
						if (value < 0) throw new ArgumentOutOfRangeException(nameof(value));
						_threshold{{n}} = value;
					}
				}
				""";
		}),
		new(
			"backing-field-property",
			c =>
			{
				var n = c.N();
				return $$"""
				private decimal _limit{{n}} = {{c.Int()}}m;

				public decimal Limit{{n}}
				{
					get => _limit{{n}};
					set
					{
						if (value == _limit{{n}}) return;
						_limit{{n}} = value;
						Changed{{n}}?.Invoke(this, new ChangedEventArgs(nameof(Limit{{n}})));
					}
				}

				public event EventHandler<ChangedEventArgs>? Changed{{n}};
				""";
			},
			NetStandard: true
		),
		new(
			"field-keyword",
			c =>
				$$"""
			public string Label{{c.N()}}
			{
				get;
				set => field = value?.Trim() ?? throw new ArgumentNullException(nameof(value));
			} = "";
			"""
		),
		new(
			"custom-event",
			c =>
			{
				var n = c.N();
				return $$"""
				private EventHandler? _handlers{{n}};

				public event EventHandler Updated{{n}}
				{
					add { _handlers{{n}} += value; }
					remove { _handlers{{n}} -= value; }
				}

				protected void OnUpdated{{n}}() => _handlers{{n}}?.Invoke(this, EventArgs.Empty);
				""";
			},
			NetStandard: true
		),
		new("attributes", c =>
		{
			c.Require("System.Runtime.CompilerServices");
			c.Require("System.Diagnostics.CodeAnalysis");
			c.Require("System.ComponentModel");
			return $$"""
				[Obsolete("Use the overload that takes a culture")]
				[EditorBrowsable(EditorBrowsableState.Never)]
				[return: NotNullIfNotNull(nameof(value))]
				public static string? {{c.Method()}}(string? value) => value?.ToUpperInvariant();

				[MethodImpl(MethodImplOptions.AggressiveInlining)][return: NotNull]
				public static string {{c.Method()}}(string value, [CallerMemberName] string caller = "") => value + caller;
				""";
		}),
		new(
			"preprocessor",
			c =>
				$$"""
			#region {{c.Noun()}} helpers

			public static string {{c.Method()}}(int level)
			{
			#if DEBUG
				var mode = "debug";
			#else
				var mode = "release";
			#endif
			#pragma warning disable CS0618
				var text = level.ToString();
			#pragma warning restore CS0618
				return mode + text;
			}

			#endregion
			""",
			NetStandard: true
		),
		new(
			"arithmetic",
			c =>
				$$"""
			public static int {{c.Method()}}(int a, int b, uint mask)
			{
				var sum = checked(a + b);
				var wrapped = unchecked(a * {{c.R.Next(2, 100000)}});
				var shifted = (a << 3) | (b >> 2);
				var logical = (int)(mask >>> 1);
				sum += wrapped;
				sum ^= shifted;
				sum &= ~logical;
				return sum % 7 == 0 ? -sum : +sum;
			}
			""",
			NetStandard: true
		),
		new(
			"type-tests",
			c =>
				$$"""
			public static string {{c.Method()}}(object? value)
			{
				if (value is int number && number > {{c.R.Next(0, 10)}}) return "int " + number;
				var text = value as string;
				if (text != null) return text;
				switch (value)
				{
					case Customer { IsActive: true } active:
						return active.Name;
					case IEnumerable<int> numbers when numbers.Any():
						return nameof(numbers);
					case null:
						return "null";
				}
				return typeof(object).Name + sizeof(int) + default(int);
			}
			""",
			NetStandard: true
		),
		new(
			"ref-out-params",
			c =>
				$$"""
			public static bool {{c.Method()}}(string text, out int value, ref int attempts, in Money budget, params int[] fallbacks)
			{
				attempts++;
				if (int.TryParse(text, out value)) return budget.Amount > value;
				foreach (var fallback in fallbacks)
				{
					value = fallback;
					if (fallback > {{c.R.Next(0, 50)}}) return true;
				}
				return false;
			}
			""",
			NetStandard: true
		),
		new(
			"spans",
			c =>
				$$"""
			public static int {{c.Method()}}(ReadOnlySpan<char> text, params ReadOnlySpan<int> weights)
			{
				Span<int> buffer = stackalloc int[{{c.R.Next(4, 64)}}];
				var trimmed = text.Trim();
				for (var i = 0; i < buffer.Length && i < trimmed.Length; i++)
					buffer[i] = trimmed[i] * (i < weights.Length ? weights[i] : 1);
				var head = trimmed[..Math.Min(3, trimmed.Length)];
				return head.Length + buffer[0];
			}
			"""
		),
		new(
			"throw-expressions",
			c =>
				$$"""
			public string {{c.Method()}}(string? input)
			{
				var value = input ?? throw new ArgumentNullException(nameof(input));
				ArgumentException.ThrowIfNullOrEmpty(value);
				return value.Length > {{c.R.Next(10, 100)}} ? throw new ArgumentOutOfRangeException(nameof(input), "too long") : value + _name;
			}
			""",
			Service: true
		),
		new(
			"long-signature",
			c =>
				$$"""
			public static string {{c.Method()}}(string name, int count, decimal price, bool includeTax, DateTime? since = null, string currency = "EUR", int precision = 2, IFormatProvider? provider = null)
			{
				var total = price * count * (includeTax ? 1.21m : 1m);
				return string.Format(provider, "{0}: {1} x {2} = {3} {4} since {5}", name, count, Math.Round(price, precision), Math.Round(total, precision), currency, since?.ToString("yyyy-MM-dd") ?? "always");
			}
			""",
			NetStandard: true
		),
		new(
			"named-arguments",
			c => $$"""
			public static string {{c.Method()}}(Customer customer) =>
				string.Join(separator: ", ", value: new[] { customer.Name, customer.Email ?? "n/a", customer.Status.ToString() }) + Pad(text: customer.Name, width: {{c.R.Next(5, 40)}}, padding: '.');

			private static string Pad(string text, int width, char padding) => text.PadRight(width, padding);
			""".Replace("Pad(", "Pad" + c.N() + "("),
			NetStandard: true
		),
		new(
			"long-condition",
			c =>
				$$"""
			public static bool {{c.Method()}}(Customer customer, Order order, DateTime now)
			{
				return customer.IsActive && order.CustomerId == customer.Id && order.Total > {{c.R.Next(0, 500)}}m && (now - order.PlacedAt).TotalDays < 30 && !order.IsEmpty && (customer.Permissions & Permissions.Write) != 0 || customer.Status == Status.Pending && order.Lines.Count > 3;
			}
			""",
			NetStandard: true
		),
		new(
			"nested-ternary",
			c =>
				$$"""
			public static string {{c.Method()}}(int score) =>
				score >= 90 ? "excellent" : score >= 75 ? "good" : score >= 50 ? "fair" : score >= {{c.R.Next(10, 40)}} ? "poor" : "failing";
			""",
			NetStandard: true
		),
		new(
			"string-concat",
			c =>
				$$"""
			public static string {{c.Method()}}(Customer customer, Order order)
			{
				return "Customer " + customer.Name + " (" + customer.Id + ") placed order " + order.Id + " on " + order.PlacedAt.ToString("u") + " for a total of " + order.Total + " across " + order.Lines.Count + " line(s).";
			}
			""",
			NetStandard: true
		),
		new(
			"goto",
			c =>
				$$"""
			public static int {{c.Method()}}(int[] values)
			{
				var index = 0;
			retry:
				if (index >= values.Length) goto done;
				if (values[index] < 0)
				{
					index++;
					goto retry;
				}
				return values[index];
			done:
				return -{{c.R.Next(1, 9)}};
			}
			""",
			NetStandard: true
		),
		new(
			"dictionary",
			c =>
				$$"""
			public static int {{c.Method()}}(IReadOnlyDictionary<string, int> counts, string key)
			{
				if (counts.TryGetValue(key, out var count)) return count;
				var total = 0;
				foreach (var (name, value) in counts)
				{
					if (name.StartsWith("{{c.Text()}}", StringComparison.Ordinal)) total += value;
				}
				return counts.ContainsKey(key.ToUpperInvariant()) ? -1 : total;
			}
			"""
		),
		new(
			"regex",
			c =>
			{
				c.Require("System.Text.RegularExpressions");
				return $$"""
				private static readonly Regex Pattern{{c.N()}} = new Regex(@"^(?<key>[a-z_]+)\s*=\s*(?<value>.*)$", RegexOptions.Compiled | RegexOptions.IgnoreCase);

				public static string? {{c.Method()}}(string line)
				{
					var match = Regex.Match(line, @"^\s*(\w+)\s*:\s*(\d+)\s*$");
					return match.Success && match.Groups[2].Length < {{c.R.Next(2, 6)}} ? match.Groups[1].Value : null;
				}
				""";
			},
			NetStandard: true
		),
		new(
			"culture",
			c =>
			{
				c.Require("System.Globalization");
				return $$"""
				public static string {{c.Method()}}(decimal amount, string cultureName = "nl-NL")
				{
					var culture = CultureInfo.GetCultureInfo(cultureName);
					var parsed = decimal.Parse("{{c.R.Next(1, 999)}}.50", NumberStyles.Number, CultureInfo.InvariantCulture);
					return (amount + parsed).ToString("C", culture) + " " + DateTime.UtcNow.ToString("d", CultureInfo.InvariantCulture);
				}
				""";
			},
			NetStandard: true
		),
		new(
			"task-combinators",
			c =>
				$$"""
			public async Task<IReadOnlyList<int>> {{c.Method()}}(IEnumerable<int> ids)
			{
				var tasks = ids.Select(async id =>
				{
					await Task.Delay({{c.R.Next(1, 20)}}).ConfigureAwait(false);
					return _customers.Find(id)?.Id ?? -1;
				});
				var results = await Task.WhenAll(tasks).ConfigureAwait(false);
				return results.Where(r => r >= 0).ToArray();
			}
			""",
			Service: true
		),
		new(
			"explicit-locals",
			c =>
				$$"""
			public static string {{c.Method()}}(IEnumerable<Customer> customers)
			{
				int total = 0;
				string label = "{{c.Text()}}";
				List<string> names = new List<string>();
				Dictionary<int, Customer> byId = new Dictionary<int, Customer>();
				foreach (Customer customer in customers)
				{
					bool active = customer.IsActive;
					if (active) names.Add(customer.Name);
					byId[customer.Id] = customer;
					total++;
				}
				return label + total + names.Count + byId.Count;
			}
			""",
			NetStandard: true
		),
		new(
			"default-literal",
			c =>
				$$"""
			public static int {{c.Method()}}(int[] values)
			{
				if (values.Length == 0) return default(int);
				var first = values[0];
				return first > {{c.R.Next(0, 100)}} ? first : default;
			}
			""",
			NetStandard: true
		),
		new(
			"interpolation-tostring",
			c =>
				$$"""
			public string {{c.Method()}}(int count, decimal amount)
			{
				return $"{_name}: {count.ToString()} item(s), {amount} total, version {_version.ToString()}";
			}
			""",
			Service: true
		),
		new(
			"missing-modifiers",
			c =>
				$$"""
			void Reset{{c.N()}}()
			{
				_version = 0;
				_log.Clear();
			}

			static int Clamp{{c.N()}}(int value) => Math.Max(0, Math.Min({{c.R.Next(10, 1000)}}, value));
			""",
			Service: true
		),
		new(
			"conditional-attribute",
			c =>
			{
				c.Require("System.Diagnostics");
				return $$"""
				[Conditional("DEBUG")]
				public static void {{c.Method()}}(string message, [System.Runtime.CompilerServices.CallerLineNumber] int line = 0)
				{
					Debug.WriteLine($"{line}: {message}");
					Trace.TraceInformation(message);
				}
				""";
			},
			NetStandard: true
		),
		new(
			"doc-comments",
			c =>
				$$"""
			/// <summary>
			/// Works out the <see cref="Order.Total"/> for every order of <paramref name="customerId"/>.
			/// </summary>
			/// <param name="orders">The orders to look through.</param>
			/// <param name="customerId">Whose orders.</param>
			/// <returns>The sum, or <c>0</c> when there are none.</returns>
			/// <exception cref="ArgumentNullException"><paramref name="orders"/> is <see langword="null"/>.</exception>
			public static decimal {{c.Method()}}(IEnumerable<Order> orders, int customerId)
			{
				if (orders == null) throw new ArgumentNullException(nameof(orders));
				return orders.Where(o => o.CustomerId == customerId).Sum(o => o.Total) + {{c.R.Next(0, 9)}}m;
			}
			""",
			NetStandard: true
		),
		new(
			"nested-lambdas",
			c =>
				$$"""
			public static IEnumerable<IGrouping<int, string>> {{c.Method()}}(IEnumerable<Order> orders)
			{
				return orders
					.Where(order =>
					{
						if (order.IsEmpty) return false;
						var lines = order.Lines.Where(line => line.Quantity > {{c.R.Next(0, 5)}}).ToList();
						return lines.Count > 0;
					})
					.SelectMany(order => order.Lines, (order, line) => new { order.CustomerId, line.Sku })
					.GroupBy(x => x.CustomerId, x => x.Sku);
			}
			""",
			NetStandard: true
		),
		new(
			"comments",
			c =>
				$$"""
			// Kept for callers that still pass the legacy flag.
			public static int {{c.Method()}}(int value /* raw */, bool legacy = false)
			{
				/* The legacy path rounds down;
				   the new one rounds to even. */
				if (legacy)
					return value / {{c.R.Next(2, 9)}}; // integer division on purpose

				// Fall through to the new behaviour.
				return (int)Math.Round(value / 2.0, MidpointRounding.ToEven);
			}
			""",
			NetStandard: true
		),
		new(
			"unsafe-free-pointers",
			c =>
				$$"""
			public static int {{c.Method()}}(Customer[] customers)
			{
				ref var first = ref customers[0];
				var copy = first;
				ref readonly var view = ref customers[^1];
				return copy.Id + view.Id + customers[1..].Length;
			}
			"""
		),
		new(
			"checked-casts",
			c =>
				$$"""
			public static long {{c.Method()}}(double value, object boxed)
			{
				var truncated = (long)value;
				var narrowed = (short)(truncated & 0x7FFF);
				var unboxed = boxed is long l ? l : Convert.ToInt64(boxed);
				var hex = 0xFF_FF;
				var binary = 0b1010_1010;
				return truncated + narrowed + unboxed + hex + binary + {{c.R.Next(1, 9)}}L;
			}
			""",
			NetStandard: true
		),
	];
}
