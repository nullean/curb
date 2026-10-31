using System.Globalization;

namespace Nullean.Curb.CorpusGenerator;

/// <summary>Everything a template needs while one file is being written: its generator, fresh names, and usings.</summary>
public sealed class Ctx(Rng r, string ns, string tag = "")
{
	private static readonly string[] Nouns =
	[
		"Account",
		"Archive",
		"Batch",
		"Bucket",
		"Catalog",
		"Channel",
		"Cursor",
		"Digest",
		"Document",
		"Envelope",
		"Filter",
		"Gateway",
		"Handler",
		"Index",
		"Inventory",
		"Invoice",
		"Journal",
		"Kernel",
		"Lease",
		"Ledger",
		"Manifest",
		"Mapping",
		"Notice",
		"Outline",
		"Payload",
		"Pipeline",
		"Policy",
		"Query",
		"Quota",
		"Registry",
		"Report",
		"Schedule",
		"Segment",
		"Session",
		"Shipment",
		"Snapshot",
		"Template",
		"Tenant",
		"Ticket",
		"Token",
		"Upload",
		"Usage",
		"Variant",
		"Version",
		"Widget",
		"Window",
		"Workflow",
		"Profile",
		"Route",
		"Signal",
	];

	private static readonly string[] Verbs =
	[
		"Apply",
		"Build",
		"Collect",
		"Compute",
		"Export",
		"Format",
		"Group",
		"Import",
		"Index",
		"Load",
		"Merge",
		"Normalize",
		"Parse",
		"Prune",
		"Publish",
		"Rebuild",
		"Refresh",
		"Render",
		"Resolve",
		"Save",
		"Scan",
		"Schedule",
		"Sort",
		"Split",
		"Summarize",
		"Translate",
		"Validate",
		"Project",
		"Reconcile",
		"Measure",
	];

	private readonly HashSet<string> _usings = new(StringComparer.Ordinal);
	private int _next;

	public Rng R { get; } = r;

	public string Namespace { get; } = ns;

	/// <summary>The type whose members are being written; constructors and operators need it.</summary>
	public string TypeName { get; set; } = "";

	/// <summary>Whether the enclosing type has the instance fields the service templates use.</summary>
	public bool HasServiceFields { get; set; }

	/// <summary>The usings the templates written so far need, sorted.</summary>
	public IReadOnlyCollection<string> Usings => _usings;

	/// <summary>A suffix no other name in this file has.</summary>
	public string N() => (++_next).ToString(CultureInfo.InvariantCulture);

	public string Noun() => R.Pick(Nouns);

	public string Verb() => R.Pick(Verbs);

	/// <summary>A method name, unique in the file.</summary>
	public string Method() => Verb() + Noun() + N();

	/// <summary>
	/// A type name, unique in its namespace: the file's own name goes in front, since two files in one folder never
	/// share a name and every folder is its own namespace.
	/// </summary>
	public string Type(string suffix = "") => tag + Noun() + suffix + N();

	public string Int() => R.Next(0, 1000).ToString(CultureInfo.InvariantCulture);

	public string Text() => R.Pick("alpha", "beta", "gamma", "delta", "release", "draft", "main", "edge", "pending", "archived");

	public void Require(string ns) => _usings.Add(ns);

	/// <summary>Picks one of two spellings; the first is the one an analyser would ask to change.</summary>
	public string Either(string flagged, string clean, double flaggedChance = 0.35) => R.Chance(flaggedChance) ? flagged : clean;
}
