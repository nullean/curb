using BenchmarkDotNet.Running;

BenchmarkSwitcher.FromAssembly(typeof(Nullean.Kerf.Benchmarks.ParseBenchmarks).Assembly).Run(args);
