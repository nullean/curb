using System.Globalization;
using System.IO.Abstractions;
using Nullean.Kerf;
using Nullean.Kerf.EditorConfig;
using Nullean.Kerf.Options;

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

			var diagnostics = new List<KerfDiagnostic>();
			var options = EditorConfigOptionsBinder.Bind(config, diagnostics);

			Console.WriteLine($"# {path}");
			Console.WriteLine();
			Console.WriteLine("# resolved");
			Console.WriteLine($"indent_style             = {(options.UseTabs ? "tab" : "space")}");
			Console.WriteLine($"indent_size              = {options.IndentSize}");
			Console.WriteLine($"tab_width                = {options.TabWidth}");
			Console.WriteLine($"max_line_length          = {(options.ReflowDisabled ? "off" : options.MaxLineLength.ToString(CultureInfo.InvariantCulture))}");
			Console.WriteLine($"end_of_line              = {options.EndOfLine.ToString().ToLowerInvariant()}");
			Console.WriteLine($"insert_final_newline     = {options.InsertFinalNewLine.ToString().ToLowerInvariant()}");
			Console.WriteLine($"trim_trailing_whitespace = {options.TrimTrailingWhitespace.ToString().ToLowerInvariant()}");

			if (diagnostics.Count > 0)
			{
				Console.WriteLine();
				Console.WriteLine($"# {diagnostics.Count} diagnostic(s)");
				foreach (var diagnostic in diagnostics.OrderBy(d => d.Id, StringComparer.Ordinal))
					Console.WriteLine(diagnostic.ToString());
			}

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
