using System.Globalization;
using System.IO.Abstractions;
using Nullean.Kerf;
using Nullean.Kerf.EditorConfig;

// M0 scaffolding. The real command surface (format / check / print-config / doc-tree) arrives in M1
// on top of Nullean.Argh; this exists so the AOT publish path is exercised end to end from day one.

if (args.Length == 0 || args[0] is "-h" or "--help")
{
	Console.WriteLine("kerf — a C# formatter driven by your .editorconfig");
	Console.WriteLine();
	Console.WriteLine("  kerf print-config <file>   show the resolved .editorconfig settings for a file");
	Console.WriteLine("  kerf parse <path>          parse C# files and report token counts (M0 smoke command)");
	Console.WriteLine("  kerf --version             print the version");
	return 0;
}

if (args[0] is "--version" or "-v")
{
	var version = typeof(CSharpSource).Assembly.GetName().Version;
	Console.WriteLine(version?.ToString() ?? "0.0.0");
	return 0;
}

var fileSystem = new FileSystem();

switch (args[0])
{
	case "print-config" when args.Length > 1:
		{
			var path = fileSystem.Path.GetFullPath(args[1]);
			var config = new KerfEditorConfig(fileSystem).For(path);
			Console.WriteLine($"# resolved .editorconfig for {path}");
			foreach (var (key, value) in config.Properties.OrderBy(p => p.Key, StringComparer.Ordinal))
				Console.WriteLine($"{key} = {value}");
			return 0;
		}

	case "parse" when args.Length > 1:
		{
			var target = fileSystem.Path.GetFullPath(args[1]);
			var files = fileSystem.Directory.Exists(target)
				? fileSystem.Directory.EnumerateFiles(target, "*.cs", SearchOption.AllDirectories)
				: [target];

			long tokens = 0;
			var parsed = 0;
			var failed = 0;

			foreach (var file in files)
			{
				var source = fileSystem.File.ReadAllText(file);
				if (!CSharpSource.TryParse(source, out var csharp, out var errors))
				{
					failed++;
					Console.Error.WriteLine($"{file}: {errors[0].GetMessage(CultureInfo.InvariantCulture)}");
					continue;
				}

				parsed++;
				foreach (var _ in csharp.Root.DescendantTokens())
					tokens++;
			}

			Console.WriteLine($"parsed {parsed} file(s), {failed} failed, {tokens:N0} tokens");
			return failed > 0 ? 1 : 0;
		}

	default:
		Console.Error.WriteLine($"unknown command '{args[0]}' — try 'kerf --help'");
		return 2;
}
