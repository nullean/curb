using System.IO.Abstractions;
using Nullean.Kerf;
using Nullean.Kerf.Cli;
using Nullean.Kerf.EditorConfig;
using Nullean.Kerf.Options;

// M1 command surface. Nullean.Argh replaces this hand-rolled parsing once the shape settles.

if (args.Length == 0 || args[0] is "-h" or "--help")
{
	Console.WriteLine("kerf — a C# formatter driven by your .editorconfig");
	Console.WriteLine();
	Console.WriteLine("  kerf format <path>         format files in place");
	Console.WriteLine("  kerf check <path>          exit non-zero if anything would change");
	Console.WriteLine("  kerf print-config <file>   show the resolved options and any diagnostics");
	Console.WriteLine("  kerf doc-tree <file>       dump the document IR for a file");
	Console.WriteLine("  kerf --version");
	Console.WriteLine();
	Console.WriteLine("  --no-verify   skip re-parsing output to prove the token stream is unchanged");
	return 0;
}

if (args[0] is "--version" or "-v")
{
	Console.WriteLine(typeof(CSharpFormatter).Assembly.GetName().Version?.ToString() ?? "0.0.0");
	return 0;
}

var fileSystem = new FileSystem();

switch (args[0])
{
	case "format" when args.Length > 1:
		return FormattingRun.Execute(fileSystem, args[1], write: true,
			verify: !args.Contains("--no-verify"));

	case "check" when args.Length > 1:
		return FormattingRun.Execute(fileSystem, args[1], write: false,
			expandUnhandled: args.Contains("--expand-unhandled"),
			verify: !args.Contains("--no-verify"),
			coverageReport: args.Contains("--coverage"));

	case "doc-tree" when args.Length > 1:
		{
			var path = fileSystem.Path.GetFullPath(args[1]);
			var options = EditorConfigOptionsBinder.Bind(new KerfEditorConfig(fileSystem).For(path));
			using var formatter = new CSharpFormatter();
			Console.WriteLine(formatter.DumpDocumentTree(fileSystem.File.ReadAllText(path), options));
			return 0;
		}

	case "print-config" when args.Length > 1:
		{
			var path = fileSystem.Path.GetFullPath(args[1]);
			var config = new KerfEditorConfig(fileSystem).For(path);

			var diagnostics = new List<KerfDiagnostic>();
			var options = EditorConfigOptionsBinder.Bind(config, diagnostics);

			// Driven by the catalog rather than a hand-written list, so the command reports every
			// option Kerf implements and cannot quietly fall behind as more are onboarded.
			var keys = OptionCatalog.ImplementedKeys.Order(StringComparer.Ordinal).ToArray();
			var width = keys.Max(key => key.Length);

			Console.WriteLine($"# {path}");
			Console.WriteLine();
			var implementedFormatting = OptionCatalog.FormattingKeys.Count(OptionCatalog.IsImplemented);
			Console.WriteLine(
				$"# resolved — {implementedFormatting} of {OptionCatalog.FormattingKeys.Count} IDE0055 options "
				+ $"implemented, plus the {OptionCatalog.CoreKeys.Count} core keys");
			Console.WriteLine();

			// Capped, so one long list value does not push every comment off to the right.
			var valueWidth = Math.Min(22, keys.Max(key => (OptionValues.Of(options, key) ?? "").Length));
			foreach (var key in keys)
			{
				var value = OptionValues.Of(options, key) ?? "";
				// A value someone actually set is the one worth looking at, so mark the rest.
				var origin = config.Properties.ContainsKey(key) ? "" : "  # default";
				Console.WriteLine($"{key.PadRight(width)} = {value.PadRight(origin.Length > 0 ? valueWidth : 0)}{origin}");
			}

			var unimplemented = OptionCatalog.FormattingKeys
				.Where(key => !OptionCatalog.IsImplemented(key))
				.Order(StringComparer.Ordinal)
				.ToArray();

			if (unimplemented.Length > 0)
			{
				Console.WriteLine();
				Console.WriteLine($"# {unimplemented.Length} known option(s) not implemented yet");
				foreach (var key in unimplemented)
					Console.WriteLine($"# {key}");
			}

			if (diagnostics.Count > 0)
			{
				Console.WriteLine();
				Console.WriteLine($"# {diagnostics.Count} diagnostic(s)");
				foreach (var diagnostic in diagnostics.OrderBy(d => d.Id, StringComparer.Ordinal))
					Console.WriteLine(diagnostic.ToString());
			}

			return 0;
		}

	default:
		Console.Error.WriteLine($"unknown command '{args[0]}' — try 'kerf --help'");
		return 2;
}
