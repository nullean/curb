using EditorConfig.Core;

namespace Nullean.Curb.Cli.Languages;

/// <summary>
/// One kind of file Curb formats: which files are its, how their options bind, and which ones ask to be
/// left alone.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="FormattingRun"/> owns everything a language does not: the walk, the read, the cache, the
/// byte-order mark, the write, the summary and the exit code. A language only turns source into a
/// <see cref="FormatResult"/>, so those stay identical however many languages a run covers.
/// </para>
/// <para>
/// A language formats either in process, through <see cref="IInProcessLanguage"/>, or by handing a batch
/// of files to another tool, through <see cref="IExternalLanguage"/>.
/// </para>
/// </remarks>
internal interface IFormattingLanguage
{
	/// <summary>A short, stable name, such as <c>csharp</c>.</summary>
	string Id { get; }

	/// <summary>The file extensions this language claims, each with its leading dot. Matched case-insensitively.</summary>
	string[] Extensions { get; }

	/// <summary>Binds one file's resolved <c>.editorconfig</c> settings.</summary>
	/// <remarks>
	/// Called for every file before any is formatted, on the run's one thread, because the configuration
	/// resolver it follows is not thread-safe. Nothing it returns may be mutated afterwards: workers on
	/// every thread read it.
	/// </remarks>
	LanguageOptions BindOptions(FileConfiguration configuration);

	/// <summary>
	/// True when the file says, in its own text, not to format it, such as C#'s <c>&lt;auto-generated&gt;</c>
	/// header. Counted as skipped, like a file the configuration excluded.
	/// </summary>
	bool SkipSource(string source);
}

/// <summary>What a language bound for one file, and the parts of it the shared run reads.</summary>
/// <remarks>
/// A class rather than a struct so each language can carry its own options type through the run's
/// shared queue without the run knowing it. One per file, bound before the run's clock starts.
/// </remarks>
internal abstract class LanguageOptions
{
	/// <summary>The core keys. The run reads <see cref="LayoutOptions.Charset"/> from it to write the byte-order mark.</summary>
	public abstract LayoutOptions Layout { get; }

	/// <summary>The configuration asked for the file not to be formatted at all.</summary>
	public abstract bool Excluded { get; }
}

/// <summary>A language Curb formats itself, with one <see cref="ILanguageWorker"/> per thread.</summary>
internal interface IInProcessLanguage : IFormattingLanguage
{
	/// <param name="collectUnhandled">Record what fell through to verbatim emission, for <c>check --coverage</c>.</param>
	ILanguageWorker CreateWorker(bool collectUnhandled);

	/// <summary>Names a kind a worker reported in <see cref="ILanguageWorker.UnhandledByKind"/>.</summary>
	string DescribeUnhandledKind(int kind);
}

/// <summary>
/// Formats one file at a time for one thread. Owns pooled buffers, so it is never shared between threads.
/// </summary>
internal interface ILanguageWorker : IDisposable
{
	/// <param name="source">The decoded text, without its byte-order mark.</param>
	/// <param name="options">What <see cref="IFormattingLanguage.BindOptions"/> returned for this file.</param>
	/// <param name="fileName">The file's name without its directory.</param>
	/// <param name="produceText">False for <c>check</c>, which only needs to know whether anything would change.</param>
	/// <param name="expandUnhandled">Benchmark-only cost model; a language without one ignores it.</param>
	/// <param name="verifyRoundTrip">Re-parse the output where the printer put a token boundary at risk.</param>
	FormatResult Format(
		string source,
		LanguageOptions options,
		string fileName,
		bool produceText,
		bool expandUnhandled,
		bool verifyRoundTrip);

	/// <summary>How many files this worker re-parsed to verify, for the summary.</summary>
	int RoundTripsChecked { get; }

	/// <summary>Tokens emitted verbatim, by language-specific kind; null unless asked for.</summary>
	Dictionary<int, int>? UnhandledByKind { get; }
}

/// <summary>
/// A language another tool formats, handed every file that missed the cache in one batch, so the tool
/// starts once per run rather than once per file.
/// </summary>
/// <remarks>
/// Declared so the seam has its shape before anything implements it. <see cref="LanguageRegistry"/>
/// refuses one until <see cref="FormattingRun"/> routes files to it.
/// </remarks>
internal interface IExternalLanguage : IFormattingLanguage
{
	/// <summary>Formats or checks <paramref name="files"/>, returning one result per file, in the same order.</summary>
	/// <param name="files">The files that missed the cache and were not skipped.</param>
	/// <param name="produceText">False for <c>check</c>.</param>
	/// <param name="cancellationToken">Stops the tool.</param>
	Task<FormatResult[]> FormatBatchAsync(IReadOnlyList<ExternalFile> files, bool produceText, CancellationToken cancellationToken);
}

/// <summary>One file handed to an <see cref="IExternalLanguage"/>.</summary>
/// <param name="Path">Where it lives, for a tool that resolves its own configuration from it.</param>
/// <param name="Source">The decoded text, without its byte-order mark.</param>
/// <param name="Options">What <see cref="IFormattingLanguage.BindOptions"/> returned for it.</param>
internal readonly record struct ExternalFile(string Path, string Source, LanguageOptions Options);
