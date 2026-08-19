using BenchmarkDotNet.Running;

BenchmarkSwitcher.FromAssembly(typeof(Nullean.Curb.Benchmarks.ParseBenchmarks).Assembly).Run(args);
