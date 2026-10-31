using System.Security.Cryptography;
using System.Text;

namespace Nullean.Curb.CorpusGenerator;

/// <summary>
/// SplitMix64, written out rather than <see cref="Random"/>: the corpus has to be byte-identical for a seed on every
/// machine and every runtime version, and <see cref="Random"/>'s seeded sequence is only promised within one.
/// </summary>
public sealed class Rng(ulong state)
{
	private ulong _state = state;

	/// <summary>
	/// A generator for one file, from the run's seed and the file's corpus-relative path. Keyed by path rather than
	/// drawn in sequence, so adding a test case or a template changes the files it adds and no others.
	/// </summary>
	public static Rng For(int seed, string key)
	{
		var hash = SHA256.HashData(Encoding.UTF8.GetBytes($"{seed}:{key}"));
		return new Rng(BitConverter.ToUInt64(hash, 0));
	}

	public ulong NextUInt64()
	{
		var z = _state += 0x9E3779B97F4A7C15UL;
		z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
		z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
		return z ^ (z >> 31);
	}

	/// <summary>A double in [0, 1).</summary>
	public double NextDouble() => (NextUInt64() >> 11) * (1.0 / (1UL << 53));

	/// <summary>An integer in [0, max).</summary>
	public int Next(int max) => max <= 1 ? 0 : (int)(NextUInt64() % (ulong)max);

	/// <summary>An integer in [min, max).</summary>
	public int Next(int min, int max) => min + Next(max - min);

	public bool Chance(double probability) => probability > 0 && NextDouble() < probability;

	public T Pick<T>(IReadOnlyList<T> items) => items[Next(items.Count)];

	public T Pick<T>(params T[] items) => items[Next(items.Length)];
}
