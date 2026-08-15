using BenchmarkDotNet.Attributes;
using Nullean.Kerf;

namespace Nullean.Kerf.Benchmarks;

/// <summary>
/// The Roslyn floor: the work Kerf cannot avoid, and therefore the yardstick everything else is
/// measured against.
/// </summary>
/// <remarks>
/// Measured on elastic/docs-builder (1,196 files, 6.5 MB) during M0, single-threaded:
/// parse only 235 ms / 52.6 MB allocated; parse + full red-tree token walk 307 ms / 107 MB.
/// For context, CSharpier spends ~14,000 ms of CPU and dotnet format ~12,000 ms on that corpus —
/// so parsing is roughly 2.5% of the budget and ~97% is formatter work. Gate on allocated
/// bytes/op rather than time: time is noise on hosted runners, allocations are deterministic.
///
/// Note: the AOT binary carries a ~35% penalty on red-tree walks (no tiered re-JIT or dynamic
/// PGO), so tune against the AOT publish, not this JIT build.
/// </remarks>
[MemoryDiagnoser]
public class ParseBenchmarks
{
	private string _source = null!;

	[GlobalSetup]
	public void Setup() => _source = SampleSource.Typical;

	[Benchmark(Baseline = true)]
	public int Parse()
	{
		if (!CSharpSource.TryParse(_source, out var source, out _))
			throw new InvalidOperationException("benchmark sample must parse");
		return source.Root.RawKind;
	}

	[Benchmark]
	public int ParseAndWalkTokens()
	{
		if (!CSharpSource.TryParse(_source, out var source, out _))
			throw new InvalidOperationException("benchmark sample must parse");

		var n = 0;
		foreach (var token in source.Root.DescendantTokens())
			n += token.Span.Length;
		return n;
	}
}
