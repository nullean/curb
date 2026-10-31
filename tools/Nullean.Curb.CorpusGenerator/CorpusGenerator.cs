using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace Nullean.Curb.CorpusGenerator;

public sealed record GeneratorOptions
{
	/// <summary>The directory to write. Must not exist, or be empty.</summary>
	public required string Output { get; init; }

	/// <summary>An <c>ExpectationDump</c> directory (one sub-directory per case), or null for templates only.</summary>
	public string? Expectations { get; init; }

	public int Seed { get; init; } = 1;

	/// <summary>Roughly how many megabytes of generated C# to write, on top of the snippets.</summary>
	public double SizeMb { get; init; } = 10;

	/// <summary>The <c>global.json</c> to pin the corpus's SDK with, or null for none.</summary>
	public string? GlobalJson { get; init; }

	/// <summary>Give each snippet its own test case's settings in an editorconfig section.</summary>
	public bool SnippetSettings { get; init; } = true;

	/// <summary>Test cases whose settings are a documented non-fixed-point; their snippets keep the corpus settings.</summary>
	public IReadOnlySet<string> DivergentCases { get; init; } = new HashSet<string>();
}

/// <summary>What one run wrote, for the report line and for tests.</summary>
public sealed record GeneratorReport(
	int CSharpFiles,
	long CSharpBytes,
	int SnippetFiles,
	int TemplateFiles,
	int OtherFiles,
	IReadOnlyDictionary<string, int> Rungs
);

/// <summary>
/// Writes the corpus: a solution that builds, generated C# that compiles, the test suite's expectation sources as
/// snippets nothing compiles, and project files, F# and Razor samples. Every C# file goes through <see cref="Permuter"/>.
/// </summary>
public static class CorpusGenerator
{
	/// <summary>
	/// Keys left out of a snippet's own settings. Without a width the binder refuses deterministic layout (CURB1007),
	/// and the conformance gate takes every width away; the file would then be refused rather than formatted.
	/// </summary>
	private static readonly string[] DroppedSnippetKeys = ["csharp_keep_existing_linebreaks", "root"];

	public static GeneratorReport Generate(GeneratorOptions options)
	{
		var root = Path.GetFullPath(options.Output);
		if (Directory.Exists(root) && Directory.EnumerateFileSystemEntries(root).Any())
		{
			throw new InvalidOperationException($"{root} is not empty; the corpus needs a fresh directory");
		}
		Directory.CreateDirectory(root);

		var rungs = new SortedDictionary<string, int>(StringComparer.Ordinal);
		var csharpFiles = 0;
		var csharpBytes = 0L;
		var otherFiles = 0;

		void WriteRaw(string relative, string text)
		{
			var path = Path.Combine(root, relative.Replace('/', Path.DirectorySeparatorChar));
			Directory.CreateDirectory(Path.GetDirectoryName(path)!);
			var bom = text.StartsWith('\uFEFF');
			var bytes = new UTF8Encoding(false).GetBytes(bom ? text[1..] : text);
			using var stream = File.Create(path);
			if (bom)
			{
				stream.Write([0xEF, 0xBB, 0xBF]);
			}
			stream.Write(bytes);
		}

		void WriteOther(string relative, string text)
		{
			WriteRaw(relative, text.Replace("\r\n", "\n").Replace("\n", text.Contains("\r\n") ? "\r\n" : "\n"));
			otherFiles++;
		}

		void WriteCSharp(string relative, string source, Func<Rng, LayoutProfile>? profile = null)
		{
			var r = Rng.For(options.Seed, relative);
			var result = Permuter.Apply(source, (profile ?? LayoutProfile.Draw)(r), r);
			rungs[result.Rung] = rungs.GetValueOrDefault(result.Rung) + 1;
			WriteRaw(relative, result.Text);
			csharpFiles++;
			csharpBytes += Encoding.UTF8.GetByteCount(result.Text);
		}

		// The solution around the code.
		WriteOther(".editorconfig", Scaffold.EditorConfig + "\n");
		WriteOther("Directory.Build.props", Scaffold.DirectoryBuildProps + "\n");
		WriteOther("Directory.Packages.props", Scaffold.DirectoryPackagesProps + "\n");
		WriteOther("nuget.config", Scaffold.NuGetConfig + "\n");
		WriteOther("Corpus.slnx", Scaffold.Solution + "\n");
		WriteOther("tests/Directory.Build.props", Scaffold.TestsDirectoryBuildProps + "\n");
		if (options.GlobalJson is not null)
		{
			WriteOther("global.json", options.GlobalJson);
		}

		var plan = new Rng((ulong)options.Seed * 0x9E3779B97F4A7C15UL + 1);
		WriteProjects(plan, WriteOther);

		foreach (var (path, text) in Scaffold.XmlSamples().Concat(Scaffold.FSharpSamples()).Concat(Scaffold.RazorSamples(plan)))
		{
			WriteOther(path, text + "\n");
		}

		// The hand-written model, then the generated code, until the size is reached.
		foreach (var (name, source) in Domain.Files())
		{
			WriteCSharp("src/Corpus.Abstractions/" + name, source + "\n");
		}
		WriteCSharp("src/Corpus.Cli/Program.cs", TestTemplates.CliProgram(plan));
		WriteCSharp("tests/Corpus.Tests/Framework.cs", TestTemplates.Framework + "\n");
		WriteCSharp("tests/Corpus.Tests/Program.cs", TestTemplates.TestProgram + "\n");

		var target = (long)(options.SizeMb * 1024 * 1024);
		var templateFiles = 0;
		var used = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
		while (csharpBytes < target)
		{
			var kind = PickKind(plan);
			var folder = plan.Chance(0.7) ? plan.Pick(Folders) : "";
			string name;
			do
			{
				name = plan.Pick(FileStems) + plan.Pick(FileSuffixes);
			}
			while (!used.Add(kind + "/" + folder + "/" + name));

			var members = MemberCount(plan);
			var project = kind == ProjectKind.Tests ? "tests/Corpus.Tests" : "src/Corpus." + kind;
			var relative = $"{project}/{(folder.Length > 0 ? folder + "/" : "")}{name}.cs";
			var fileRng = Rng.For(options.Seed, "content:" + relative);
			var source = Synthesizer.File(fileRng, kind, Synthesizer.NamespaceFor(kind, folder), members, name);
			WriteCSharp(relative, source, plan.Chance(0.08) ? LayoutProfile.Quiet : null);
			templateFiles++;
		}

		var snippets = options.Expectations is null ? 0 : WriteSnippets(options, WriteCSharp, WriteOther);

		return new GeneratorReport(csharpFiles, csharpBytes, snippets, templateFiles, otherFiles, rungs);
	}

	private static readonly string[] Folders =
	[
		"Billing",
		"Catalog",
		"Indexing",
		"Routing",
		"Storage",
		"Reporting",
		"Search",
		"Security",
		"Scheduling",
		"Sync",
		"Text",
		"Diagnostics"
	];

	private static readonly string[] FileStems =
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

	private static readonly string[] FileSuffixes =
	[
		"Service",
		"Processor",
		"Builder",
		"Store",
		"Extensions",
		"Models",
		"Handlers",
		"Rules",
		"Reader",
		"Writer",
		"Planner",
		"Cache",
		"Client",
		"Tests",
		"Options",
		"Resolver"
	];

	private static ProjectKind PickKind(Rng r) => r.NextDouble() switch
	{
		< 0.08 => ProjectKind.Abstractions,
		< 0.55 => ProjectKind.Core,
		< 0.80 => ProjectKind.Services,
		< 0.88 => ProjectKind.Cli,
		_ => ProjectKind.Tests,
	};

	/// <summary>Mostly small and medium files with a long tail of large ones, the shape a real repository has.</summary>
	private static int MemberCount(Rng r) => r.NextDouble() switch
	{
		< 0.25 => r.Next(3, 15),
		< 0.70 => r.Next(15, 45),
		< 0.95 => r.Next(45, 100),
		_ => r.Next(100, 220),
	};

	private static void WriteProjects(Rng r, Action<string, string> write)
	{
		(string, string)[] P(params (string, string)[] p) => p;

		(string, string, string?)[] I(params (string, string, string?)[] i) => i;

		write(
			"src/Corpus.Abstractions/Corpus.Abstractions.csproj",
			Scaffold.Project(
				r,
				"Microsoft.NET.Sdk",
				P(
					("TargetFrameworks", "netstandard2.0;net10.0"),
					("ImplicitUsings", "disable"),
					("RootNamespace", "Corpus.Abstractions"),
					("PackageId", "Corpus.Abstractions"),
					("Version", "1.2.3")
				),
				I(("InternalsVisibleTo", "Corpus.Core", null), ("InternalsVisibleTo", "Corpus.Tests", null))
			)
		);

		write(
			"src/Corpus.Core/Corpus.Core.csproj",
			Scaffold.Project(
				r,
				"Microsoft.NET.Sdk",
				P(("TargetFramework", "net10.0"), ("RootNamespace", "Corpus.Core"), ("DefineConstants", "$(DefineConstants);CORPUS_CORE")),
				I(
					("ProjectReference", "..\\Corpus.Abstractions\\Corpus.Abstractions.csproj", null),
					("InternalsVisibleTo", "Corpus.Tests", null),
					("None", "README.md", "<CopyToOutputDirectory>Never</CopyToOutputDirectory>")
				)
			)
		);

		write(
			"src/Corpus.Services/Corpus.Services.csproj",
			Scaffold.Project(
				r,
				"Microsoft.NET.Sdk",
				P(("TargetFramework", "net10.0"), ("AssemblyName", "Corpus.Services"), ("NoWarn", "$(NoWarn);CS0618")),
				I(
					("ProjectReference", "../Corpus.Core/Corpus.Core.csproj", null),
					("ProjectReference", "../Corpus.Abstractions/Corpus.Abstractions.csproj", null),
					("Using", "System.Text.Json", null)
				)
			)
		);

		write(
			"src/Corpus.Cli/Corpus.Cli.csproj",
			Scaffold.Project(
				r,
				"Microsoft.NET.Sdk",
				P(("OutputType", "Exe"), ("TargetFramework", "net10.0"), ("AssemblyName", "corpus")),
				I(("ProjectReference", "../Corpus.Services/Corpus.Services.csproj", null))
			)
		);

		write(
			"tests/Corpus.Tests/Corpus.Tests.csproj",
			Scaffold.Project(
				r,
				"Microsoft.NET.Sdk",
				P(("TargetFramework", "net10.0"), ("IsPackable", "false")),
				I(
					("ProjectReference", "../../src/Corpus.Core/Corpus.Core.csproj", null),
					("ProjectReference", "../../src/Corpus.Services/Corpus.Services.csproj", null)
				)
			)
		);

		write("src/Corpus.Core/README.md", "# Corpus.Core\n\nGenerated. Do not edit.\n");
	}

	/// <summary>
	/// The test suite's expectation sources, one file per distinct source, named after the test that asserts it.
	/// Not built: most are fragments that reference types nobody declared. The formatting gates read them anyway.
	/// </summary>
	private static int WriteSnippets(GeneratorOptions options, Action<string, string, Func<Rng, LayoutProfile>?> writeCSharp, Action<string, string> writeOther)
	{
		var cases = Directory
			.GetDirectories(options.Expectations!)
			.Select(
				d =>
					new
					{
						TestCase = File.Exists(Path.Combine(d, "TestCase.txt")) ? File.ReadAllText(Path.Combine(d, "TestCase.txt")).Trim() : "Unknown.Case",
						Source = File.ReadAllText(Path.Combine(d, "Source.cs")),
						Settings = File.Exists(Path.Combine(d, ".editorconfig")) ? File.ReadAllText(Path.Combine(d, ".editorconfig")) : "",
					}
			)
			// The dump's directory numbers come from tests finishing in parallel; the content is what is stable.
			.GroupBy(c => c.Source + "\0" + c.Settings, StringComparer.Ordinal)
			.Select(g => g.OrderBy(c => c.TestCase, StringComparer.Ordinal).First())
			.OrderBy(c => c.TestCase, StringComparer.Ordinal)
			.ThenBy(c => Hash(c.Source + "\0" + c.Settings), StringComparer.Ordinal)
			.ToList();

		var written = 0;
		foreach (var byClass in cases.GroupBy(c => c.TestCase.Split('.')[0], StringComparer.Ordinal))
		{
			var sections = new StringBuilder();
			var seen = new Dictionary<string, int>(StringComparer.Ordinal);
			foreach (var item in byClass)
			{
				var method = item.TestCase.Contains('.') ? item.TestCase[(item.TestCase.IndexOf('.') + 1)..] : item.TestCase;
				var index = seen[method] = seen.GetValueOrDefault(method) + 1;
				var file = (index == 1 ? method : $"{method}_{index.ToString(CultureInfo.InvariantCulture)}") + ".cs";
				writeCSharp($"snippets/{byClass.Key}/{file}", item.Source, null);
				written++;

				if (!options.SnippetSettings || options.DivergentCases.Contains(item.TestCase) || KnownDefects.SettingsConflicts.Contains(item.TestCase))
				{
					continue;
				}
				var settings = item
					.Settings
					.Split('\n')
					.Select(l => l.Trim())
					.Where(l => l.Length > 0 && !l.StartsWith('[') && l.Contains('='))
					.Where(l => !DroppedSnippetKeys.Any(k => l.StartsWith(k, StringComparison.OrdinalIgnoreCase)))
					.ToList();
				if (settings.Count == 0)
				{
					continue;
				}
				sections.Append('[').Append(file).Append("]\n");
				foreach (var line in settings)
				{
					sections.Append(line).Append('\n');
				}
				sections.Append('\n');
			}

			if (sections.Length > 0)
			{
				writeOther($"snippets/{byClass.Key}/.editorconfig", sections.ToString());
			}
		}

		return written;
	}

	private static string Hash(string text) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text)));
}
