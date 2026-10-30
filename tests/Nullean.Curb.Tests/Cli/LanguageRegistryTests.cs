using System.IO.Abstractions.TestingHelpers;
using AwesomeAssertions;
using EditorConfig.Core;
using Nullean.Curb.Cli;
using Nullean.Curb.Cli.Languages;
using Nullean.Curb.Cli.Languages.CSharp;
using Nullean.Curb.EditorConfig;

namespace Nullean.Curb.Tests.Cli;

/// <summary>
/// The registry decides which language a file is, and the run has to route each file to its own
/// language's worker while keeping the cache, the write and the summary shared. Only C# is registered
/// for real, so a second, trivial language stands in for the ones still to come.
/// </summary>
public class LanguageRegistryTests
{
	private const string Root = "/repo";

	[Test]
	public async Task The_csharp_registry_walks_only_cs_files()
	{
		var registry = LanguageRegistry.CSharpOnly;

		registry.Count.Should().Be(1);
		registry[0].Should().BeSameAs(CSharpLanguage.Instance);
		registry.SearchPattern.Should().Be("*.cs", "a single language keeps the walk as narrow as it always was");
		registry.IndexOf("/repo/A.cs").Should().Be(0);
		registry.IndexOf("/repo/A.CS").Should().Be(0);
		registry.IndexOf("/repo/A.fs").Should().Be(-1);
		registry.IndexOf("/repo/A.csproj").Should().Be(-1);
		await Task.CompletedTask;
	}

	[Test]
	public async Task The_default_registry_adds_xml_fsharp_and_razor()
	{
		var registry = LanguageRegistry.Default(new MockFileSystem());

		registry.Count.Should().Be(4);
		registry.SearchPattern.Should().Be("*");
		registry.IndexOf("/repo/A.cs").Should().Be(0);
		registry.IndexOf("/repo/A.csproj").Should().Be(1);
		registry.IndexOf("/repo/Directory.Build.TARGETS").Should().Be(1);
		registry.IndexOf("/repo/A.slnx").Should().Be(1);
		registry.IndexOf("/repo/nuget.config").Should().Be(1);
		registry.IndexOf("/repo/A.fsproj").Should().Be(1);
		registry.IndexOf("/repo/A.fs").Should().Be(2);
		registry.IndexOf("/repo/A.fsi").Should().Be(2);
		registry.IndexOf("/repo/build.fsx").Should().Be(2);
		registry.IndexOf("/repo/A.sln").Should().Be(-1);
		registry.IndexOf("/repo/A.ml").Should().Be(-1, "OCaml shares the extension, so Curb does not claim it");
		registry.IndexOf("/repo/Counter.razor").Should().Be(3);
		registry.IndexOf("/repo/Views/Index.CSHTML").Should().Be(3);
		registry.IndexOf("/repo/A.razor.cs").Should().Be(0, "a code-behind file is C#");
		await Task.CompletedTask;
	}

	[Test]
	public async Task More_than_one_language_widens_the_walk_and_routes_by_extension()
	{
		var registry = new LanguageRegistry(CSharpLanguage.Instance, Shout.Instance);

		registry.SearchPattern.Should().Be("*");
		registry.IndexOf("/repo/A.cs").Should().Be(0);
		registry.IndexOf("/repo/A.shout").Should().Be(1);
		registry.IndexOf("/repo/A.txt").Should().Be(-1);
		await Task.CompletedTask;
	}

	[Test]
	public async Task An_extension_claimed_twice_is_refused()
	{
		var register = () => new LanguageRegistry(CSharpLanguage.Instance, CSharpLanguage.Instance);

		register.Should().Throw<ArgumentException>();
		await Task.CompletedTask;
	}

	[Test]
	public async Task A_run_formats_each_file_with_its_own_language_and_shares_everything_else()
	{
		var fs = new MockFileSystem();
		fs.AddFile($"{Root}/.editorconfig", new MockFileData("root = true\n\n[*.shout]\ncharset = utf-8-bom\n"));
		fs.AddFile($"{Root}/A.cs", new MockFileData("class A{}\n"));
		fs.AddFile($"{Root}/B.shout", new MockFileData("quiet\n"));
		fs.AddFile($"{Root}/C.txt", new MockFileData("left alone\n"));

		var summary = FormattingRun.Execute(fs, Root, write: true, languages: new LanguageRegistry(CSharpLanguage.Instance, Shout.Instance));

		summary.Files.Should().Be(2, "the .txt file belongs to no registered language");
		summary.Changed.Should().Be(2);
		using var formatter = new CSharpFormatter();
		fs.File.ReadAllText($"{Root}/A.cs").Should().Be(formatter.Format("class A{}\n", new FormatOptions()).Text);
		fs.File.ReadAllBytes($"{Root}/B.shout").Should().Equal([0xEF, 0xBB, 0xBF, .. "QUIET\n"u8.ToArray()],
			"the byte-order mark comes from the shared write path, driven by the language's own layout options");
		fs.File.ReadAllText($"{Root}/C.txt").Should().Be("left alone\n");
		await Task.CompletedTask;
	}

	/// <summary>Upper-cases <c>.shout</c> files. Binds the core keys with defaults of its own.</summary>
	private sealed class Shout : IInProcessLanguage
	{
		public static Shout Instance { get; } = new();

		public string Id => "shout";

		public string[] Extensions { get; } = [".shout"];

		public LanguageOptions BindOptions(FileConfiguration configuration, CurbEditorConfig editorConfig) =>
			new ShoutOptions(LayoutOptionsBinder.Bind(configuration.Properties, null, new LayoutOptions { IndentSize = 2 }));

		public bool SkipSource(string source) => false;

		public ILanguageWorker CreateWorker(bool collectUnhandled) => new Worker();

		public string DescribeUnhandledKind(int kind) => kind.ToString(System.Globalization.CultureInfo.InvariantCulture);

		private sealed class ShoutOptions(LayoutOptions layout) : LanguageOptions
		{
			public override LayoutOptions Layout => layout;

			public override bool Excluded => false;
		}

		private sealed class Worker : ILanguageWorker
		{
			public FormatResult Format(string source, LanguageOptions options, string fileName, bool produceText, bool expandUnhandled, bool verifyRoundTrip)
			{
				var text = source.ToUpperInvariant();
				return new FormatResult(FormatStatus.Formatted, text != source, produceText ? text : null, 1, null);
			}

			public int RoundTripsChecked => 0;

			public Dictionary<int, int>? UnhandledByKind => null;

			public void Dispose() { }
		}
	}
}
