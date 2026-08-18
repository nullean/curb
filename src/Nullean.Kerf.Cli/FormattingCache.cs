using System.Collections.Concurrent;
using System.Globalization;
using System.IO.Abstractions;
using System.Reflection;
using System.Text;

namespace Nullean.Kerf.Cli;

/// <summary>Remembers which files a previous run watched the formatter leave alone.</summary>
/// <remarks>
/// <para>
/// An entry means one thing: Kerf ran the formatter over exactly these bytes under exactly these resolved
/// options and got them back unchanged. An observation, never an inference — which is why a file Kerf
/// <em>rewrote</em> is not recorded even though its output would almost certainly be a fixed point. That
/// would bake idempotency into the cache, and idempotency bugs are a thing that happens; every one found
/// so far grew a blank line or an indent level per run. Today a second <c>kerf format</c> changes such a
/// file again and someone notices. A cache that trusted its own output would report nothing changed.
/// The cost of not doing it is one extra pass, once, after a bulk first format.
/// </para>
/// <para>
/// Nothing here may throw. A cache that is missing, corrupt, truncated, or a directory where a file was
/// expected degrades to no cache; a line that does not parse is dropped without poisoning the rest; a
/// write that fails does not change the exit code. The cache can only ever skip work that was provably
/// unnecessary, so failing a build over it would be trading the one thing it must not cost for the one
/// thing it was never worth.
/// </para>
/// <para>
/// There is no ambient location under a user profile directory, and that is the design rather than an
/// omission. The caller names the path or there is no cache: one nobody named is one nobody can find,
/// clear or reason about, and it outlives every repository that fed it.
/// </para>
/// </remarks>
internal sealed class FormattingCache
{
	/// <summary>Bumped when the line format changes. A mismatch discards the file rather than migrating it.</summary>
	private const int FormatVersion = 1;

	private const string Marker = "kerf-cache";

	/// <summary>
	/// How long an entry survives without being seen. This is the whole of the bounding story: no stat
	/// calls, no directory walk, and it reaches the case nothing else would — a pre-commit hook caching
	/// into a gitignored folder, where no `dotnet clean` ever runs. A file that is deleted or renamed
	/// simply ages out, and the cost of being wrong is one cold run on a formatter that is fast cold.
	/// </summary>
	private static readonly TimeSpan MaxAge = TimeSpan.FromDays(7);

	/// <summary>UTF-8 without a byte-order mark, matching how the run writes source.</summary>
	private static readonly UTF8Encoding Utf8 = new(encoderShouldEmitUTF8Identifier: false);

	/// <summary>
	/// The informational version, not <c>GetName().Version</c>: MinVer pins the assembly version to
	/// Major.Minor, so that one reads 0.1.0.0 for every 0.1.x build ever produced and would hold a cache
	/// valid across releases that format differently. This one carries the height and the commit.
	/// </summary>
	/// <remarks>
	/// Two builds of <em>uncommitted</em> formatter changes do share it, so a contributor iterating on a
	/// printer while passing <c>--cache</c> can read stale results. The cache is opt-in, Kerf's own build
	/// never passes it, and `dotnet clean` removes it, which is as far as this is worth defending.
	/// </remarks>
	private static readonly string ToolVersion =
		typeof(FormattingCache).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
		?? typeof(FormattingCache).Assembly.GetName().Version?.ToString()
		?? "unknown";

	private readonly IFileSystem _fileSystem;
	private readonly string _path;
	private readonly long _now;
	private readonly ConcurrentDictionary<string, Entry> _entries = new(StringComparer.Ordinal);

	private FormattingCache(IFileSystem fileSystem, string path, DateTimeOffset now)
	{
		_fileSystem = fileSystem;
		_path = path;
		_now = now.ToUnixTimeSeconds();
	}

	/// <summary>What a run watched happen to one file.</summary>
	private readonly record struct Entry(UInt128 Content, UInt128 Options, long LastSeen);

	/// <summary>How many entries are held. For tests and for the summary line.</summary>
	internal int Count => _entries.Count;

	/// <summary>Reads the cache at <paramref name="path"/>, or hands back an empty one.</summary>
	internal static FormattingCache Load(IFileSystem fileSystem, string path, DateTimeOffset now)
	{
		var cache = new FormattingCache(fileSystem, path, now);

		string[] lines;
		try
		{
			if (!fileSystem.File.Exists(path))
				return cache;
			lines = fileSystem.File.ReadAllLines(path);
		}
		catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or NotSupportedException)
		{
			// Including the case where something else is sitting at that path. Nothing here is worth a
			// message: the run is about to do the work anyway, and the only thing lost is speed.
			return cache;
		}

		// The header pins both the line format and the formatter. Either moving invalidates everything,
		// because an entry records a verdict only that formatter could have reached.
		if (lines.Length == 0 || lines[0] != Header())
			return cache;

		var oldest = now.ToUnixTimeSeconds() - (long)MaxAge.TotalSeconds;
		for (var i = 1; i < lines.Length; i++)
		{
			if (TryParse(lines[i], out var path_, out var entry) && entry.LastSeen >= oldest)
				cache._entries[path_] = entry;
		}

		return cache;
	}

	/// <summary>Whether a previous run watched these exact bytes format to themselves under these options.</summary>
	internal bool IsFixedPoint(string path, UInt128 content, UInt128 options)
	{
		if (!_entries.TryGetValue(path, out var entry) || entry.Content != content || entry.Options != options)
			return false;

		// Seen, so it survives the next expiry. Without this a file nobody edits would be re-parsed once
		// a week for no reason.
		_entries[path] = entry with { LastSeen = _now };
		return true;
	}

	/// <summary>Records that the formatter produced these bytes back unchanged. Called from the worker threads.</summary>
	internal void Record(string path, UInt128 content, UInt128 options) =>
		_entries[path] = new Entry(content, options, _now);

	/// <summary>Writes the cache back, merging what this run saw over what it loaded.</summary>
	internal void Save()
	{
		try
		{
			var directory = _fileSystem.Path.GetDirectoryName(_path);
			if (!string.IsNullOrEmpty(directory))
				_fileSystem.Directory.CreateDirectory(directory);

			var builder = new StringBuilder();
			builder.Append(Header()).Append('\n');

			// Ordinal by path, so two runs over an unchanged tree produce the same bytes and the file
			// diffs like anything else someone might want to read.
			foreach (var (path, entry) in _entries.OrderBy(pair => pair.Key, StringComparer.Ordinal))
			{
				builder.Append(entry.Content.ToString("x32", CultureInfo.InvariantCulture)).Append('\t')
					.Append(entry.Options.ToString("x32", CultureInfo.InvariantCulture)).Append('\t')
					.Append(entry.LastSeen.ToString(CultureInfo.InvariantCulture)).Append('\t')
					.Append(path).Append('\n');
			}

			// Through a temporary file, so a run that dies half way through leaves the previous cache
			// intact rather than a truncated one the next run has to reject.
			var temporary = _path + ".tmp";
			_fileSystem.File.WriteAllText(temporary, builder.ToString(), Utf8);
			_fileSystem.File.Move(temporary, _path, overwrite: true);
		}
		catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or NotSupportedException or ArgumentException)
		{
			// Two runs writing one cache path is the usual cause, and the loser simply loses its entries.
			// Said out loud because a cache that silently never persists looks exactly like one that is
			// working, but not raised — nothing here is worth failing a build over.
			Console.Error.WriteLine($"{_path}: could not write the formatting cache — {exception.Message}");
		}
	}

	private static string Header() =>
		string.Create(CultureInfo.InvariantCulture, $"{Marker} {FormatVersion} {ToolVersion}");

	/// <summary>
	/// Tab separated with the path last and split on the first three tabs, so a path containing a tab
	/// survives. One containing a newline arrives as a fragment with too few fields and is dropped, which
	/// costs that file a cache hit and nothing else.
	/// </summary>
	private static bool TryParse(string line, out string path, out Entry entry)
	{
		path = "";
		entry = default;

		var fields = line.Split('\t', 4);
		if (fields.Length != 4 || fields[3].Length == 0)
			return false;

		if (!UInt128.TryParse(fields[0], NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var content)
			|| !UInt128.TryParse(fields[1], NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var options)
			|| !long.TryParse(fields[2], NumberStyles.Integer, CultureInfo.InvariantCulture, out var lastSeen))
		{
			return false;
		}

		path = fields[3];
		entry = new Entry(content, options, lastSeen);
		return true;
	}
}
