using System.Buffers;
using System.IO.Hashing;
using System.Runtime.InteropServices;
using EditorConfig.Core;
using Nullean.Kerf.EditorConfig;

namespace Nullean.Kerf.Cli;

/// <summary>The two hashes a <see cref="FormattingCache"/> entry is keyed on.</summary>
/// <remarks>
/// 128 bits of XxHash, not a cryptographic digest. The threat model is a build artefact in a directory
/// the caller named: anyone who can write the cache can write the <c>.cs</c> file next to it, so there is
/// no adversary to resist. See <c>Directory.Packages.props</c> for why the package rather than
/// <c>System.Security.Cryptography</c>.
/// </remarks>
internal static class Fingerprint
{
	/// <summary>Separates a key from its value. Never appears inside an <c>.editorconfig</c> key.</summary>
	private static ReadOnlySpan<char> KeyValueSeparator => "=";

	/// <summary>Separates one pair from the next. Never appears inside a value.</summary>
	private static ReadOnlySpan<char> PairSeparator => "\n";

	/// <summary>Hashes the bytes exactly as they sit on disk, byte-order mark included.</summary>
	/// <remarks>
	/// Raw bytes rather than decoded text, because a mark added or removed is a change to the file even
	/// when every byte after it is identical — which is how the run counts it, so it is how the cache has
	/// to key it.
	/// </remarks>
	internal static UInt128 OfContent(ReadOnlySpan<byte> bytes)
	{
		Span<byte> digest = stackalloc byte[16];
		XxHash128.Hash(bytes, digest);
		return ReadDigest(digest);
	}

	/// <summary>Hashes the resolved <c>.editorconfig</c> properties a file's options were bound from.</summary>
	/// <remarks>
	/// The raw properties, deliberately, and not the bound <see cref="FormatOptions"/>. Two reasons, and
	/// the second is the one that matters.
	/// <para>
	/// <see cref="FormatOptions"/> is a record struct whose <c>PreferredModifierOrder</c> member is a
	/// <c>string[]</c>, so the synthesised <c>GetHashCode</c> is reference-based over it and gives a
	/// different answer for every bind of the same configuration.
	/// </para>
	/// <para>
	/// More importantly, <see cref="EditorConfigOptionsBinder.Bind"/> reads nothing off the configuration
	/// except <c>Properties</c>. Options are therefore a pure function of the properties and the version
	/// of Kerf doing the binding, and the version is already in the cache header — so this fingerprint
	/// <em>cannot</em> miss a change that would alter formatting. It can move when a key Kerf never reads
	/// changes, which costs one run and only when someone edits an <c>.editorconfig</c>. That is the safe
	/// direction to be wrong in, and it is why this is not driven off the option catalog: that would be
	/// complete by test rather than by construction, and a member with no catalog key would format stale
	/// files silently.
	/// </para>
	/// </remarks>
	internal static UInt128 OfOptions(FileConfiguration configuration)
	{
		var properties = configuration.Properties;
		var count = properties.Count;

		// Sorted, because a dictionary hands its keys back in whatever order it happens to hold them and
		// two runs must agree. Rented rather than allocated: this runs once per file.
		var keys = ArrayPool<string>.Shared.Rent(count);
		try
		{
			var index = 0;
			foreach (var key in properties.Keys)
				keys[index++] = key;

			Array.Sort(keys, 0, count, StringComparer.Ordinal);

			var hasher = new XxHash128();
			for (var i = 0; i < count; i++)
			{
				// The UTF-16 bytes of the string itself, so there is nothing to encode and nothing to
				// allocate. Every RID Kerf publishes is little-endian, and the cache never travels
				// between machines — it lives in obj/ or a folder the caller gitignored.
				hasher.Append(MemoryMarshal.AsBytes(keys[i].AsSpan()));
				hasher.Append(MemoryMarshal.AsBytes(KeyValueSeparator));
				hasher.Append(MemoryMarshal.AsBytes(properties[keys[i]].AsSpan()));
				hasher.Append(MemoryMarshal.AsBytes(PairSeparator));
			}

			Span<byte> digest = stackalloc byte[16];
			hasher.GetHashAndReset(digest);
			return ReadDigest(digest);
		}
		finally
		{
			// Cleared, because the pool hands these strings to the next renter and holding a reference to
			// every key of the last file read is a leak nobody would look for.
			ArrayPool<string>.Shared.Return(keys, clearArray: true);
		}
	}

	private static UInt128 ReadDigest(ReadOnlySpan<byte> digest) => MemoryMarshal.Read<UInt128>(digest);
}

/// <summary>Hashes a file's resolved options, reusing the answer while it does not move.</summary>
/// <remarks>
/// Options resolve per file rather than per directory, because a section can discriminate on the file
/// name. In practice that changes the answer rarely: every file in a folder usually resolves the same
/// properties, and comparing two dictionaries is a handful of probes against sorting and hashing a few
/// hundred keys. Not thread-safe, and it does not need to be — it is used only by the serial pre-pass,
/// for the same reason the <c>.editorconfig</c> parser is.
/// </remarks>
internal sealed class OptionsFingerprinter
{
	private IReadOnlyDictionary<string, string>? _previous;
	private UInt128 _hash;

	internal UInt128 Of(FileConfiguration configuration)
	{
		var properties = configuration.Properties;
		if (_previous is not null && Matches(_previous, properties))
			return _hash;

		_hash = Fingerprint.OfOptions(configuration);
		_previous = properties;
		return _hash;
	}

	private static bool Matches(IReadOnlyDictionary<string, string> previous, IReadOnlyDictionary<string, string> current)
	{
		if (ReferenceEquals(previous, current))
			return true;
		if (previous.Count != current.Count)
			return false;

		foreach (var (key, value) in current)
		{
			if (!previous.TryGetValue(key, out var existing) || !string.Equals(existing, value, StringComparison.Ordinal))
				return false;
		}

		return true;
	}
}
