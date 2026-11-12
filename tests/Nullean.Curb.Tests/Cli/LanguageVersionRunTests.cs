using System.IO.Abstractions.TestingHelpers;
using AwesomeAssertions;
using Microsoft.CodeAnalysis.CSharp;
using Nullean.Curb.Cli;
using Nullean.Curb.Cli.Languages;
using Nullean.Curb.Cli.Languages.CSharp;

namespace Nullean.Curb.Tests.Cli;

/// <summary>
/// <c>--lang-version</c>, which the MSBuild integration passes once per target framework (issue #298).
/// </summary>
public class LanguageVersionRunTests
{
	private const string Root = "/repo";

	private const string Block = "namespace Alpha\n{\n    class C\n    {\n    }\n}\n";

	private static MockFileSystem Repo()
	{
		var fs = new MockFileSystem();
		fs.AddFile($"{Root}/.editorconfig", new MockFileData("root = true\n\n[*.cs]\ncsharp_style_namespace_declarations = file_scoped\n"));
		fs.AddFile($"{Root}/A.cs", new MockFileData(Block));
		return fs;
	}

	private static LanguageVersion Resolve(params string[] values)
	{
		CSharpLanguage.TryResolveLanguageVersion(values, out var version).Should().BeTrue();
		return version;
	}

	[Test]
	public async Task The_lowest_of_several_versions_applies()
	{
		// net10.0;netstandard2.0 passes 14.0 and 7.3, and the file has to compile in both.
		Resolve("14.0", "7.3").Should().Be(LanguageVersion.CSharp7_3);
		Resolve("10", "latest", "8.0").Should().Be(LanguageVersion.CSharp8);
		await Task.CompletedTask;
	}

	[Test]
	[Arguments("latest")]
	[Arguments("latestMajor")]
	[Arguments("preview")]
	[Arguments("default")]
	[Arguments("99.0")]
	[Arguments("")]
	public async Task A_version_at_least_as_new_as_the_parser_caps_nothing(string value)
	{
		// 99.0 stands in for an SDK newer than the Roslyn Curb ships, whose default LangVersion the
		// parser does not know yet: every rewrite Curb has is older than it.
		Resolve(value).Should().Be(LanguageVersion.Default);
		await Task.CompletedTask;
	}

	[Test]
	public async Task No_version_caps_nothing()
	{
		CSharpLanguage.TryResolveLanguageVersion(null, out var version).Should().BeTrue();
		version.Should().Be(LanguageVersion.Default);
		CSharpLanguage.For(version).Should().BeSameAs(CSharpLanguage.Instance);
		await Task.CompletedTask;
	}

	[Test]
	public async Task A_value_that_is_not_a_version_is_refused()
	{
		CSharpLanguage.TryResolveLanguageVersion(["seven"], out _).Should().BeFalse();
		await Task.CompletedTask;
	}

	[Test]
	public async Task A_capped_run_leaves_the_block_namespace()
	{
		var fs = Repo();

		FormattingRun.Execute(fs, Root, write: true, languages: LanguageRegistry.CSharpOnlyFor(CSharpLanguage.For(LanguageVersion.CSharp7_3)));

		fs.File.ReadAllText($"{Root}/A.cs").Should().Be(Block);
		await Task.CompletedTask;
	}

	[Test]
	public async Task An_uncapped_run_still_converts()
	{
		var fs = Repo();

		FormattingRun.Execute(fs, Root, write: true, languages: LanguageRegistry.CSharpOnly);

		fs.File.ReadAllText($"{Root}/A.cs").Should().Be("namespace Alpha;\n\nclass C\n{\n}\n");
		await Task.CompletedTask;
	}

	[Test]
	public async Task The_cache_does_not_carry_a_capped_verdict_to_an_uncapped_run()
	{
		// A capped run finds the block namespace already formatted. Keyed on the .editorconfig alone, the
		// next run without the cap would read that verdict back and leave the file unconverted.
		var fs = Repo();
		const string cache = "/repo/obj/curb.cache";

		var capped = FormattingRun.Execute(
			fs,
			Root,
			write: false,
			cachePath: cache,
			languages: LanguageRegistry.CSharpOnlyFor(CSharpLanguage.For(LanguageVersion.CSharp7_3))
		);
		capped.ExitCode.Should().Be(0);

		var uncapped = FormattingRun.Execute(fs, Root, write: false, cachePath: cache, languages: LanguageRegistry.CSharpOnly);
		uncapped.Cached.Should().Be(0);
		uncapped.ExitCode.Should().Be(1, "without the cap the namespace is unformatted");
		await Task.CompletedTask;
	}
}
