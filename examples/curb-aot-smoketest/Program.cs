using Nullean.Curb;

// Proves the engine links and runs under native AOT on every shipped RID. CI publishes this per
// RID on a matching runner and asserts the output below. It is never packed or released.

const string source = """
	using System;

	namespace Smoke;

	public class Greeter
	{
		public string Greet(string name) => $"hello {name}";
	}
	""";

if (!CSharpSource.TryParse(source, out var parsed, out var errors))
{
	Console.Error.WriteLine($"FAIL: could not parse smoke-test source: {errors[0].GetMessage()}");
	return 1;
}

var tokens = parsed.Root.DescendantTokens().Count();
if (tokens == 0)
{
	Console.Error.WriteLine("FAIL: parsed tree produced no tokens");
	return 1;
}

Console.WriteLine($"OK: parsed {tokens} tokens under native AOT");
return 0;
