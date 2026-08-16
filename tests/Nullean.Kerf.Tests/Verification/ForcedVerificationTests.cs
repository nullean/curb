using AwesomeAssertions;
using Nullean.Kerf;

namespace Nullean.Kerf.Tests.Verification;

/// <summary>
/// Formats a battery of constructs with round-trip verification <b>forced</b>, bypassing the risk
/// detector entirely.
/// </summary>
/// <remarks>
/// In normal use the second parse only happens where the printer decided a token boundary was at
/// risk, which on a real repository is well under 1% of files. That is the right trade at runtime,
/// but it means the detector is load-bearing: if it ever under-reports, damage would ship unchecked.
/// These tests re-establish the guarantee independently of it, which is the job the removed
/// <c>--verify-all</c> flag used to do by hand.
/// </remarks>
public class ForcedVerificationTests
{
	private static readonly FormatOptions Options = new() { EndOfLine = EndOfLine.Lf, IndentSize = 4 };

	private static void ShouldSurviveForcedVerification(string source)
	{
		using var formatter = new CSharpFormatter();

		var result = formatter.Format(source, Options, produceText: true, forceRoundTrip: true, verifyRoundTrip: true);
		result.Status.Should().Be(FormatStatus.Formatted, result.Message ?? "no message");
		formatter.RoundTripsChecked.Should().Be(1, "verification must actually have run");

		// And again on the output: formatting a formatted file must also survive the check.
		var second = formatter.Format(result.Text!, Options, produceText: true, forceRoundTrip: true, verifyRoundTrip: true);
		second.Status.Should().Be(FormatStatus.Formatted, second.Message ?? "no message");
		second.Text.Should().Be(result.Text, "formatting must be idempotent");
	}

	[Test]
	[Arguments("""
		global using System;
		using Alias = System.Collections.Generic.List<string>;
		using static System.Math;
		#if DEBUG
		using System.Diagnostics;
		#endif

		[assembly: CLSCompliant(true)]

		namespace Sample;

		/// <summary>Docs.</summary>
		public readonly record struct Point(int X, int Y) : IComparable<Point>
		{
		    public int CompareTo(Point other) => X.CompareTo(other.X);
		}
		""")]
	[Arguments("""
		public interface IReader<out T, in TKey> where T : class, new()
		{
		    T? Read(TKey key);
		}
		""")]
	[Arguments("""
		public class C
		{
		    public void M(IDictionary<string, int> d)
		    {
		        if (d.TryGetValue("k", out var value) && value is > 0 and < 10)
		            Console.WriteLine(value);
		        else if (d is { Count: 0 })
		            return;
		        else
		        {
		            foreach (var (key, item) in d)
		                Console.WriteLine($"{key}={item}");
		        }

		        try
		        {
		            using var stream = File.OpenRead("x");
		        }
		        catch (IOException ex) when (ex.HResult != 0)
		        {
		            throw new InvalidOperationException("bad", ex);
		        }
		        finally
		        {
		            Console.WriteLine("done");
		        }
		    }
		}
		""")]
	[Arguments("""
		public class C
		{
		    public string Describe(object o) => o switch
		    {
		        int i when i > 0 => "positive",
		        string { Length: 0 } => "empty",
		        [var first, .. var rest] => $"{first} plus {rest.Length}",
		        null => "null",
		        _ => "other",
		    };

		    public int[] Numbers { get; init; } = [1, 2, 3];

		    public void Query(IEnumerable<int> source)
		    {
		        var result = from x in source
		                     where x > 0
		                     orderby x descending
		                     select x * 2;
		    }
		}
		""")]
	[Arguments("""
		public class C
		{
		    private static readonly Func<int, int> Double = static x => x * 2;

		    public async Task<int> M(CancellationToken ct)
		    {
		        await Task.Delay(1, ct).ConfigureAwait(false);
		        var anon = new { Name = "x", Value = 1 };
		        var arr = new[] { 1, 2, 3 };
		        return arr[^1] + (int)Double(arr[0]) + anon.Value;
		    }
		}
		""")]
	public async Task Survives_forced_round_trip_verification(string source)
	{
		ShouldSurviveForcedVerification(source);
		await Task.CompletedTask;
	}

	[Test]
	public async Task Verification_runs_even_where_the_detector_sees_no_risk()
	{
		// A file with nothing at risk: the detector would skip it, forcing must not.
		const string source = "public class C { }";

		using var formatter = new CSharpFormatter();
		formatter.Format(source, Options, verifyRoundTrip: true);
		formatter.RoundTripsChecked.Should().Be(0, "the detector found nothing to check");

		using var forced = new CSharpFormatter();
		forced.Format(source, Options, verifyRoundTrip: true, forceRoundTrip: true);
		forced.RoundTripsChecked.Should().Be(1);
		await Task.CompletedTask;
	}
}
