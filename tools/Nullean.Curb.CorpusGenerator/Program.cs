using System.Globalization;
using Nullean.Curb.CorpusGenerator;

// curb-corpus --output <dir> [--expectations <dump>] [--seed N] [--size-mb N] [--global-json <file>]
//             [--divergences <conformance-divergences.json>] [--no-snippet-settings]
string? output = null, expectations = null, globalJson = null, divergences = null;
var seed = 1;
var sizeMb = 10.0;
var snippetSettings = true;

for (var i = 0; i < args.Length; i++)
{
	string Value() => i + 1 < args.Length ? args[++i] : throw new ArgumentException($"{args[i]} needs a value");

	switch (args[i])
	{
		case "--output":
			output = Value();
			break;
		case "--expectations":
			expectations = Value();
			break;
		case "--seed":
			seed = int.Parse(Value(), CultureInfo.InvariantCulture);
			break;
		case "--size-mb":
			sizeMb = double.Parse(Value(), CultureInfo.InvariantCulture);
			break;
		case "--global-json":
			globalJson = Value();
			break;
		case "--divergences":
			divergences = Value();
			break;
		case "--no-snippet-settings":
			snippetSettings = false;
			break;
		default:
			Console.Error.WriteLine($"unknown argument {args[i]}");
			return 2;
	}
}

if (output is null)
{
	Console.Error.WriteLine("usage: --output <dir> [--expectations <dump>] [--seed N] [--size-mb N] [--global-json <file>] [--divergences <json>]");
	return 2;
}

var report = CorpusGenerator.Generate(new GeneratorOptions
{
	Output = output,
	Expectations = expectations,
	Seed = seed,
	SizeMb = sizeMb,
	GlobalJson = globalJson is null ? null : File.ReadAllText(globalJson),
	SnippetSettings = snippetSettings,
	DivergentCases = divergences is null ? new HashSet<string>() : Divergences.Read(divergences),
});

Console.WriteLine(
	$"generated corpus (seed {seed}) in {Path.GetFullPath(output)}: {report.CSharpFiles} C# file(s), {report.CSharpBytes / 1024.0 / 1024.0:F2} MB " +
		$"({report.TemplateFiles} generated, {report.SnippetFiles} snippet(s) from the test suite), {report.OtherFiles} other file(s)"
);
Console.WriteLine($"layout rungs: {string.Join(", ", report.Rungs.Select(p => $"{p.Key}={p.Value}"))}");
return 0;
