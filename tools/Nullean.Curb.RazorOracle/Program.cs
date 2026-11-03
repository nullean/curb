using System.Reflection;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Razor.Formatting;
using Microsoft.CodeAnalysis.Text;

// Formats every Expected.razor under a dump directory with Microsoft's Razor formatter, the engine
// `dotnet format` is getting for Razor (dotnet/sdk#56563), and writes what it makes of it to Reference.razor
// beside it. ./build.sh verifyrazorexpectations then compares the two: an expectation the engine changes
// is not a fixed point of it.
//
// Each case is a project of its own: one Razor file, the case's .editorconfig, the .NET and ASP.NET Core
// reference packs, and the Razor source generator, the way the SDK compiles a component. A case the
// engine cannot format writes no Reference.razor and is reported.
//
// usage: Nullean.Curb.RazorOracle <dump-directory>
if (args.Length != 1 || !Directory.Exists(args[0]))
{
	Console.Error.WriteLine("usage: Nullean.Curb.RazorOracle <dump-directory>");
	return 2;
}

var dump = Path.GetFullPath(args[0]);
var references = ReferencePacks();
var generator = GeneratorReference();

var cases = Directory.GetDirectories(dump).Order(StringComparer.Ordinal).ToArray();
var failed = 0;
foreach (var directory in cases)
{
	var expected = Path.Combine(directory, "Expected.razor");
	if (!File.Exists(expected))
	{
		continue;
	}

	var reference = Path.Combine(directory, "Reference.razor");
	try
	{
		var (project, document) = CaseProject(directory, expected);
		var result = await RazorFormatter.TryFormatAsync(project, document, CancellationToken.None);
		if (result is null)
		{
			failed++;
			Console.WriteLine($"no result: {Path.GetFileName(directory)}");
			continue;
		}

		File.WriteAllText(reference, result.ToString(), new UTF8Encoding(false));
	}
	catch (Exception e)
	{
		failed++;
		Console.WriteLine($"failed: {Path.GetFileName(directory)}: {e.GetType().Name}: {e.Message.Split('\n')[0]}");
	}
}

Console.WriteLine($"formatted {cases.Length - failed} of {cases.Length} case(s) with the Razor engine");
return failed == 0 ? 0 : 1;

(Project, DocumentId) CaseProject(string directory, string razorFile)
{
	var workspace = new AdhocWorkspace();
	var projectId = ProjectId.CreateNewId();
	var info = ProjectInfo
		.Create(
			projectId,
			VersionStamp.Create(),
			"Case",
			"Case",
			LanguageNames.CSharp,
			Path.Combine(directory, "Case.csproj"),
			parseOptions: CSharpParseOptions.Default.WithLanguageVersion(LanguageVersion.Preview).WithFeatures([new("use-roslyn-tokenizer", "true")]),
			compilationOptions: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, nullableContextOptions: NullableContextOptions.Enable)
		)
		.WithMetadataReferences(references)
		.WithAnalyzerReferences([generator])
		.WithDefaultNamespace("Case");

	var solution = workspace.CurrentSolution.AddProject(info);
	var documentId = DocumentId.CreateNewId(projectId);
	solution = solution.AddAdditionalDocument(
		documentId,
		Path.GetFileName(razorFile),
		SourceText.From(File.ReadAllText(razorFile), Encoding.UTF8),
		filePath: razorFile
	);

	// What the Razor SDK hands its source generator for a component.
	var globalConfig =
		$"""
		is_global = true
		build_property.RazorLangVersion = Latest
		build_property.RazorConfiguration = Default
		build_property.RootNamespace = Case
		build_property.SuppressRazorSourceGenerator = true
		build_property.MSBuildProjectDirectory = {directory}

		[{razorFile}]
		build_metadata.AdditionalFiles.TargetPath = {Convert.ToBase64String(Encoding.UTF8.GetBytes(Path.GetFileName(razorFile)))}
		""";
	solution = solution.AddAnalyzerConfigDocument(
		DocumentId.CreateNewId(projectId),
		".globalconfig",
		SourceText.From(globalConfig),
		filePath: Path.Combine(directory, ".globalconfig")
	);

	var editorConfig = Path.Combine(directory, ".editorconfig");
	if (File.Exists(editorConfig))
	{
		solution = solution.AddAnalyzerConfigDocument(
			DocumentId.CreateNewId(projectId),
			".editorconfig",
			SourceText.From(File.ReadAllText(editorConfig)),
			filePath: editorConfig
		);
	}

	return (solution.GetProject(projectId)!, documentId);
}

// The .NET and ASP.NET Core reference packs of the SDK running this process.
static List<MetadataReference> ReferencePacks()
{
	var dotnetRoot = Path.GetDirectoryName(Path.GetDirectoryName(Path.GetDirectoryName(Path.GetDirectoryName(typeof(object).Assembly.Location))))!;
	var references = new List<MetadataReference>();
	foreach (var pack in new[] { "Microsoft.NETCore.App.Ref", "Microsoft.AspNetCore.App.Ref" })
	{
		var versions = Directory
			.GetDirectories(Path.Combine(dotnetRoot, "packs", pack))
			.Where(d => Path.GetFileName(d).StartsWith("10.", StringComparison.Ordinal))
			.ToArray();
		if (versions.Length == 0)
		{
			throw new InvalidOperationException($"no 10.x {pack} under {dotnetRoot}/packs");
		}

		foreach (var dll in Directory.GetFiles(Path.Combine(versions.Max(StringComparer.Ordinal)!, "ref", "net10.0"), "*.dll"))
		{
			references.Add(MetadataReference.CreateFromFile(dll));
		}
	}

	return references;
}

static AnalyzerReference GeneratorReference()
{
	var assembly = typeof(Microsoft.NET.Sdk.Razor.SourceGenerators.RazorSourceGenerator).Assembly;
	return new AnalyzerFileReference(assembly.Location, new SameAssemblyLoader(assembly));
}

/// <summary>Hands Roslyn the generator assembly already loaded in this process.</summary>
internal sealed class SameAssemblyLoader(Assembly assembly) : IAnalyzerAssemblyLoader
{
	public void AddDependencyLocation(string fullPath)
	{ }

	public Assembly LoadFromPath(string fullPath) => Path.GetFileName(fullPath) == Path.GetFileName(assembly.Location) ? assembly : Assembly.LoadFrom(fullPath);
}
