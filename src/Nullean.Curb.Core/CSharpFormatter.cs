using Nullean.Curb.Documents;
using Nullean.Curb.Printing;
using Nullean.Curb.Printing.CSharp;
using Nullean.Curb.Verification;

namespace Nullean.Curb;

/// <summary>Why a file was not formatted.</summary>
public enum FormatStatus
{
	/// <summary>Formatted successfully.</summary>
	Formatted,

	/// <summary>The source does not parse. Curb never re-prints from a recovered tree.</summary>
	SyntaxError,

	/// <summary>Formatting produced output that lost or altered content, so it was discarded.</summary>
	VerificationFailed,

	/// <summary>The syntax nests deeper than Curb will follow.</summary>
	TooDeep,
}

/// <summary>The outcome of formatting one file.</summary>
/// <param name="Status">Whether it worked, and if not, why.</param>
/// <param name="Changed">True when the output differs from the input.</param>
/// <param name="Text">The formatted text, or null when it was not requested or not produced.</param>
/// <param name="Coverage">Share of tokens emitted by a real printer rather than verbatim.</param>
/// <param name="Message">Detail for a non-successful status.</param>
public readonly record struct FormatResult(
	FormatStatus Status,
	bool Changed,
	string? Text,
	double Coverage,
	string? Message)
{
	public bool Success => Status == FormatStatus.Formatted;
}

/// <summary>
/// Formats C# source.
/// </summary>
/// <remarks>
/// <para>
/// One instance holds the pooled document arena and output buffer, so formatting a whole repository
/// reuses the same memory rather than allocating per file. Instances are <b>not</b> thread-safe —
/// give each worker its own.
/// </para>
/// <para>
/// Nothing here touches the filesystem: the caller supplies text and decides what to do with the
/// result. That keeps the engine testable without any IO at all.
/// </para>
/// </remarks>
public sealed class CSharpFormatter : IDisposable
{
	private readonly DocArena _arena = new();
	private readonly DocPrinter _printer = new();
	private readonly OutputBuffer _output = new();

	/// <summary>How many files this formatter actually re-parsed, for reporting how often it was needed.</summary>
	public int RoundTripsChecked { get; private set; }

	/// <summary>Set to collect which syntax kinds are still falling through to verbatim emission.</summary>
	public Dictionary<int, int>? UnhandledByKind { get; set; }

	/// <param name="source">The C# text to format.</param>
	/// <param name="options">Layout settings, normally bound from <c>.editorconfig</c>.</param>
	/// <param name="fileName">
	/// The file's name, without its directory, for the <c>{fileName}</c> placeholder in
	/// <c>file_header_template</c>. Null when the caller has no path to give — the placeholder is
	/// then replaced with an empty string, the same as Roslyn does for a path-less document.
	/// </param>
	/// <param name="expandUnhandled">Benchmark-only cost model; see <c>PrintContext.ExpandUnhandled</c>.</param>
	/// <param name="forceRoundTrip">
	/// Re-parse even where the printer proved no boundary moved. The conditional check is trusted in
	/// normal use; this exists so the test suite can re-establish that the risk detector is not
	/// letting damage through, and for consumers who want the check unconditionally.
	/// </param>
	/// <param name="verifyRoundTrip">
	/// Re-parse the output and compare token streams, catching the damage the content check cannot
	/// see. Costs one extra parse — but only where the printer actually put a token boundary at
	/// risk, which it tracks while laying the document out. On code that never closes a gap between
	/// tokens this is free. Pass <c>true</c> to force it regardless.
	/// </param>
	/// <param name="produceText">
	/// False for <c>check</c>, which only needs to know whether anything would change. Skipping the
	/// string keeps an unchanged file allocation-free on the output side.
	/// </param>
	public FormatResult Format(
		string source,
		in FormatOptions options,
		string? fileName = null,
		bool produceText = true,
		bool expandUnhandled = false,
		bool verifyRoundTrip = false,
		bool forceRoundTrip = false)
	{
		if (!CSharpSource.TryParse(source, out var parsed, out var errors))
		{
			return new FormatResult(
				FormatStatus.SyntaxError, false, null, 0,
				errors.Count > 0 ? errors[0].GetMessage(System.Globalization.CultureInfo.InvariantCulture) : "source does not parse");
		}

		_arena.Reset(source.Length);
		_output.Reset(source.Length + 64);

		var context = new PrintContext(_arena, parsed.Text, options)
		{
			FileName = fileName,
			ExpandUnhandled = expandUnhandled,
			UnhandledByKind = UnhandledByKind,
			Suppressed = FormattingSuppression.Scan(parsed.Root, source.AsSpan()),
		};

		try
		{
			Node.Print(parsed.Root, context);
		}
		catch (PrintTooDeepException exception)
		{
			return new FormatResult(FormatStatus.TooDeep, false, null, context.Coverage, exception.Message);
		}

#if DEBUG
		DocValidator.Validate(_arena, source.Length);
#endif

		_printer.Print(_arena, source.AsMemory(), options, _output);

		var written = _output.Written;

		if (!ContentVerifier.Verify(
			source.AsSpan(), written, out var failure, context.ReorderedSpans, options.RewritesTrailingCommas,
			context.BracesAdded, context.NamespaceUnwrapped,
			context.DroppedSpans, context.ArrowsAdded, HeaderFor(context, options)))
			return new FormatResult(FormatStatus.VerificationFailed, false, null, context.Coverage, failure);

		// The second parse only ever finds a moved token boundary, and the printer already knows
		// whether it created that risk. Where it did not, the check is provably redundant.
		//
		// ExpressionBodyAdded is intentionally absent: when a block body is converted to an expression
		// body the dropped tokens are recorded in DroppedSpans (caught by ContentVerifier) and any
		// adjacency risk from closing the gap is covered by _printer.RoundTripAtRisk. Running
		// TokenStreamComparer unconditionally for every expression-body rewrite is redundant.
		var reordered = context.ReorderedSpans is not null || context.BracesAdded || context.NamespaceUnwrapped;
		verifyRoundTrip = verifyRoundTrip && (forceRoundTrip || reordered || _printer.RoundTripAtRisk);

		var changed = !written.SequenceEqual(source.AsSpan());

		// Only materialise the formatted text when the caller needs to write it; for verify-only
		// runs (check mode) the span is passed directly and the string is created inside Verify
		// only when TryParse needs it, so check runs that do not reach the re-parse never allocate.
		var text = produceText ? _output.ToString() : null;

		if (verifyRoundTrip)
		{
			RoundTripsChecked++;
			// Pass the span and the already-materialised string (null when produceText is off).
			// Verify will call ToString() on the span only if formattedStr is null, so the string
			// is never created twice when both produceText and verifyRoundTrip are true.
			if (!TokenStreamComparer.Verify(
				parsed.Root, source.AsSpan(), text is not null ? text.AsSpan() : _output.Written, text,
				out var roundTripFailure,
				context.UsingsReordered, options.RewritesTrailingCommas, context.ModifiersReordered,
				context.BracesAdded, context.NamespaceUnwrapped, context.DroppedSpans, context.ArrowsAdded))
				return new FormatResult(FormatStatus.VerificationFailed, false, null, context.Coverage, roundTripFailure);
		}

		return new FormatResult(
			FormatStatus.Formatted,
			changed,
			text,
			context.Coverage,
			null);
	}

	/// <summary>The header text the printer wrote, or null when it did not touch the top of the file.</summary>
	/// <remarks>
	/// The comment markers and the newlines the printer put between the template's lines, since the
	/// verifier compares what was written rather than what was configured. Used for both an
	/// inserted header and one that replaced a mismatched <c>//</c> block — either way, the output
	/// starts with exactly this.
	/// </remarks>
	private static string? HeaderFor(PrintContext context, in FormatOptions options) =>
		context.FileHeaderAdded && options.FileHeaderTemplate is { } template
			? FileHeaderText.Rendered(template, context.FileName)
			: null;

	/// <summary>Returns the pooled buffers this formatter holds.</summary>
	public void Dispose() => _output.Dispose();

	/// <summary>Renders the document tree for a file, for debugging why it laid out the way it did.</summary>
	public string DumpDocumentTree(string source, in FormatOptions options)
	{
		if (!CSharpSource.TryParse(source, out var parsed, out var errors))
			return $"# does not parse: {errors[0].GetMessage(System.Globalization.CultureInfo.InvariantCulture)}";

		_arena.Reset(source.Length);
		Node.Print(parsed.Root, new PrintContext(_arena, parsed.Text, options));
		return DocDumper.Dump(_arena, source);
	}
}
