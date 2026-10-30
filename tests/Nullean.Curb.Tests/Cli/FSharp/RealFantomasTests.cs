using System.IO.Abstractions;
using AwesomeAssertions;
using Nullean.Curb.Cli;
using Nullean.Curb.Cli.Languages;
using Nullean.Curb.Cli.Languages.FSharp;

namespace Nullean.Curb.Tests.Cli.FSharp;

/// <summary>
/// The one place the real Fantomas is run: the wire format and the CLI report are Fantomas's to change, and
/// only this notices when they do. Skipped, with the reason, where Fantomas 8.1.0 is not restored
/// (<c>dotnet tool restore</c> in a folder whose manifest pins it, or <c>./build.sh fsharpsmoketest</c>).
/// </summary>
public class RealFantomasTests
{
	private const string Version = "8.1.0";

	private static (FileSystem Fs, string Root) Workspace()
	{
		var fs = new FileSystem();
		var tool = new FantomasResolver(fs, ToolEnvironment.Current)
			.Resolve(new FantomasManifest("/dotnet-tools.json", "fantomas", Version, false)).Tool;
		Skip.When(tool?.EntryPoint is null, $"Fantomas {Version} is not restored on this machine");

		var root = fs.Directory.CreateTempSubdirectory("curb-fantomas-").FullName;
		fs.File.WriteAllText(fs.Path.Combine(root, "dotnet-tools.json"), FantomasToolTests.Manifest(Version, isRoot: true));
		fs.File.WriteAllText(fs.Path.Combine(root, ".editorconfig"), "root = true\n\n[*]\nend_of_line = lf\n");
		fs.File.WriteAllText(fs.Path.Combine(root, "Clean.fs"), "module Clean\n\nlet x = 1\n");
		fs.File.WriteAllText(fs.Path.Combine(root, "Messy.fs"), "module Messy\nlet  f  x =   x+1\n");
		fs.File.WriteAllText(fs.Path.Combine(root, "Broken.fs"), "let z = (\n");
		fs.File.WriteAllText(fs.Path.Combine(root, "Sig.fsi"), "module Sig\nval  f : int -> int\n");
		fs.File.WriteAllText(fs.Path.Combine(root, "Ignored.fs"), "let  ignored  = 1\n");
		fs.File.WriteAllText(fs.Path.Combine(root, ".fantomasignore"), "Ignored.fs\n");
		return (fs, root);
	}

	private static FormattingRunSummary Run(FileSystem fs, string root, bool write, bool daemon = true) =>
		FormattingRun.Execute(fs, root, write, languages: LanguageRegistry.FSharpOnly(fs, ProcessLauncher.Instance,
			new ToolEnvironment(name => name == FantomasRunner.DaemonVariable ? (daemon ? null : "false") : Environment.GetEnvironmentVariable(name),
				ToolEnvironment.Current.HomeDirectory)));

	[Test]
	[Arguments(true)]
	[Arguments(false)]
	public async Task Check_then_format_then_check_again(bool daemon)
	{
		var (fs, root) = Workspace();
		try
		{
			var check = Run(fs, root, write: false, daemon);
			check.Files.Should().Be(5);
			check.Changed.Should().Be(2);
			check.Unparsable.Should().Be(1);
			check.Skipped.Should().Be(1);
			check.ExitCode.Should().Be(1);

			var format = Run(fs, root, write: true, daemon);
			format.Changed.Should().Be(2);
			format.Failed.Should().Be(0);
			fs.File.ReadAllText(fs.Path.Combine(root, "Messy.fs")).Should().Be("module Messy\n\nlet f x = x + 1\n");
			fs.File.ReadAllText(fs.Path.Combine(root, "Sig.fsi")).Should().Be("module Sig\n\nval f: int -> int\n");
			fs.File.ReadAllText(fs.Path.Combine(root, "Ignored.fs")).Should().Be("let  ignored  = 1\n");
			fs.File.ReadAllText(fs.Path.Combine(root, "Broken.fs")).Should().Be("let z = (\n");

			var again = Run(fs, root, write: false, daemon);
			again.Changed.Should().Be(0);
			again.ExitCode.Should().Be(0);
		}
		finally
		{
			fs.Directory.Delete(root, recursive: true);
		}

		await Task.CompletedTask;
	}
}
