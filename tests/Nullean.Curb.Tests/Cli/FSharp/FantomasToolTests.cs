using System.IO.Abstractions.TestingHelpers;
using AwesomeAssertions;
using Nullean.Curb.Cli.Languages.FSharp;

namespace Nullean.Curb.Tests.Cli.FSharp;

/// <summary>
/// Finding the Fantomas a repository pinned, and the files it runs from, the way the SDK would, without
/// asking the SDK.
/// </summary>
public class FantomasToolTests
{
	private const string Home = "/home";

	internal static string Manifest(string version = "8.1.0", string id = "fantomas", bool isRoot = false, bool rollForward = false) =>
		$$"""
		{
		  "version": 1,
		  "isRoot": {{(isRoot ? "true" : "false")}},
		  "tools": {
		    "{{id}}": { "version": "{{version}}", "commands": [ "fantomas" ], "rollForward": {{(rollForward ? "true" : "false")}} }
		  }
		}
		""";

	internal static string ResolverCache(string version, string dll) =>
		$$"""[{"Version":"{{version}}","TargetFramework":"net10.0","RuntimeIdentifier":"any","Name":"fantomas","Runner":"dotnet","PathToExecutable":"{{dll}}"}]""";

	private const string OtherTools = """{ "version": 1, "isRoot": false, "tools": { "minver-cli": { "version": "7.0.0", "commands": ["minver"] } } }""";

	private const string RootWithoutFantomas = """{ "version": 1, "isRoot": true, "tools": { "minver-cli": { "version": "7.0.0", "commands": ["minver"] } } }""";

	[Test]
	public async Task A_manifest_at_the_root_is_found_from_a_nested_folder()
	{
		var fs = new MockFileSystem();
		fs.AddFile("/repo/dotnet-tools.json", new MockFileData(Manifest()));

		var found = new ToolManifestLocator(fs).Find("/repo/src/deep/er");

		found.Should().NotBeNull();
		found.ManifestPath.Should().Be("/repo/dotnet-tools.json");
		found.Version.Should().Be("8.1.0");
		found.Directory.Should().Be("/repo");
		await Task.CompletedTask;
	}

	[Test]
	public async Task A_manifest_under_dot_config_is_found_and_governs_its_parent()
	{
		var fs = new MockFileSystem();
		fs.AddFile("/repo/.config/dotnet-tools.json", new MockFileData(Manifest(rollForward: true)));

		var found = new ToolManifestLocator(fs).Find("/repo/src");

		found!.ManifestPath.Should().Be("/repo/.config/dotnet-tools.json");
		found.Directory.Should().Be("/repo", "dotnet tool run starts from the folder the .config folder sits in");
		found.RollForward.Should().BeTrue();
		await Task.CompletedTask;
	}

	[Test]
	public async Task A_nearer_manifest_without_fantomas_does_not_hide_a_parent_one()
	{
		var fs = new MockFileSystem();
		fs.AddFile("/repo/dotnet-tools.json", new MockFileData(Manifest("8.1.0")));
		fs.AddFile("/repo/sub/dotnet-tools.json", new MockFileData(OtherTools));

		new ToolManifestLocator(fs).Find("/repo/sub/x")!.ManifestPath.Should().Be("/repo/dotnet-tools.json");
		await Task.CompletedTask;
	}

	[Test]
	public async Task The_nearest_manifest_declaring_fantomas_wins()
	{
		var fs = new MockFileSystem();
		fs.AddFile("/repo/dotnet-tools.json", new MockFileData(Manifest("8.1.0")));
		fs.AddFile("/repo/sub/.config/dotnet-tools.json", new MockFileData(Manifest("7.0.3")));
		var locator = new ToolManifestLocator(fs);

		locator.Find("/repo/sub/x")!.Version.Should().Be("7.0.3");
		locator.Find("/repo/other")!.Version.Should().Be("8.1.0");
		await Task.CompletedTask;
	}

	[Test]
	public async Task A_root_manifest_ends_the_walk_even_without_fantomas()
	{
		var fs = new MockFileSystem();
		fs.AddFile("/repo/dotnet-tools.json", new MockFileData(Manifest()));
		fs.AddFile("/repo/sub/dotnet-tools.json", new MockFileData(RootWithoutFantomas));

		new ToolManifestLocator(fs).Find("/repo/sub/x").Should().BeNull();
		await Task.CompletedTask;
	}

	[Test]
	public async Task No_manifest_finds_nothing()
	{
		var fs = new MockFileSystem();
		fs.AddDirectory("/repo/src");

		new ToolManifestLocator(fs).Find("/repo/src").Should().BeNull();
		await Task.CompletedTask;
	}

	[Test]
	public async Task The_legacy_tool_id_is_recognised()
	{
		var fs = new MockFileSystem();
		fs.AddFile("/repo/dotnet-tools.json", new MockFileData(Manifest("4.7.9", id: "fantomas-tool")));

		var found = new ToolManifestLocator(fs).Find("/repo");

		found!.ToolId.Should().Be("fantomas-tool");
		await Task.CompletedTask;
	}

	[Test]
	public async Task An_unreadable_manifest_is_an_answer_carrying_its_problem()
	{
		var fs = new MockFileSystem();
		fs.AddFile("/repo/dotnet-tools.json", new MockFileData(Manifest()));
		fs.AddFile("/repo/sub/dotnet-tools.json", new MockFileData("{ not json"));
		fs.AddFile("/repo/other/dotnet-tools.json", new MockFileData("""{ "tools": { "fantomas": { "commands": ["fantomas"] } } }"""));
		var locator = new ToolManifestLocator(fs);

		locator.Find("/repo/sub")!.Problem.Should().Contain("/repo/sub/dotnet-tools.json").And.Contain("could not be read");
		locator.Find("/repo/other")!.Problem.Should().Contain("without a version");
		locator.Find("/repo")!.Problem.Should().BeNull();
		await Task.CompletedTask;
	}

	private static FantomasManifest Pinned(string version = "8.1.0", bool rollForward = false) =>
		new("/repo/.config/dotnet-tools.json", "fantomas", version, rollForward);

	private static FantomasResolver Resolver(MockFileSystem fs, Dictionary<string, string>? variables = null) =>
		new(fs, new ToolEnvironment(name => variables?.GetValueOrDefault(name), Home));

	[Test]
	public async Task The_sdk_resolver_cache_comes_first()
	{
		var fs = new MockFileSystem();
		const string dll = "/somewhere/fantomas.dll";
		fs.AddFile(dll, new MockFileData(""));
		fs.AddFile($"{Home}/.dotnet/toolResolverCache/1/fantomas", new MockFileData(
			$"[{{\"Version\":\"9.0.0\",\"PathToExecutable\":\"/nine/fantomas.dll\"}},{{\"Version\":\"8.1.0\",\"PathToExecutable\":\"{dll}\"}}]"));

		var tool = Resolver(fs).Resolve(Pinned()).Tool;

		tool!.EntryPoint.Should().Be(dll);
		await Task.CompletedTask;
	}

	[Test]
	public async Task Dotnet_cli_home_moves_the_resolver_cache()
	{
		var fs = new MockFileSystem();
		const string dll = "/cli/fantomas.dll";
		fs.AddFile(dll, new MockFileData(""));
		fs.AddFile("/cli-home/.dotnet/toolResolverCache/1/fantomas", new MockFileData(ResolverCache("8.1.0", dll)));

		Resolver(fs).Resolve(Pinned()).Tool.Should().BeNull("without DOTNET_CLI_HOME the cache is not where Curb looks");
		Resolver(fs, new() { ["DOTNET_CLI_HOME"] = "/cli-home" }).Resolve(Pinned()).Tool!.EntryPoint.Should().Be(dll);
		await Task.CompletedTask;
	}

	[Test]
	public async Task A_stale_resolver_cache_entry_falls_through_to_the_packages_folder()
	{
		var fs = new MockFileSystem();
		fs.AddFile($"{Home}/.dotnet/toolResolverCache/1/fantomas", new MockFileData(ResolverCache("8.1.0", "/gone/fantomas.dll")));
		AddVersion1(fs, $"{Home}/.nuget/packages/fantomas/8.1.0/tools/net10.0/any");

		Resolver(fs).Resolve(Pinned()).Tool!.EntryPoint.Should().Be($"{Home}/.nuget/packages/fantomas/8.1.0/tools/net10.0/any/fantomas.dll");
		await Task.CompletedTask;
	}

	[Test]
	public async Task Nuget_packages_moves_the_packages_folder_and_the_newest_framework_wins()
	{
		var fs = new MockFileSystem();
		AddVersion1(fs, "/packages/fantomas/8.1.0/tools/net8.0/any");
		AddVersion1(fs, "/packages/fantomas/8.1.0/tools/net10.0/any");

		var tool = Resolver(fs, new() { ["NUGET_PACKAGES"] = "/packages" }).Resolve(Pinned()).Tool;

		tool!.EntryPoint.Should().Be("/packages/fantomas/8.1.0/tools/net10.0/any/fantomas.dll");
		await Task.CompletedTask;
	}

	[Test]
	public async Task A_version_2_package_points_at_its_any_runtime_package()
	{
		var fs = new MockFileSystem();
		fs.AddFile($"{Home}/.nuget/packages/fantomas/9.0.0-alpha-002/tools/any/any/DotnetToolSettings.xml", new MockFileData("""
			<?xml version="1.0" encoding="utf-8"?>
			<DotNetCliTool Version="2">
			  <Commands><Command Name="fantomas" /></Commands>
			  <RuntimeIdentifierPackages>
			    <RuntimeIdentifierPackage RuntimeIdentifier="linux-x64" Id="fantomas.linux-x64" />
			    <RuntimeIdentifierPackage RuntimeIdentifier="any" Id="fantomas.any" />
			  </RuntimeIdentifierPackages>
			</DotNetCliTool>
			"""));
		AddVersion1(fs, $"{Home}/.nuget/packages/fantomas.any/9.0.0-alpha-002/tools/net10.0/any");

		var tool = Resolver(fs).Resolve(Pinned("9.0.0-alpha-002")).Tool;

		tool!.EntryPoint.Should().Be($"{Home}/.nuget/packages/fantomas.any/9.0.0-alpha-002/tools/net10.0/any/fantomas.dll");
		await Task.CompletedTask;
	}

	[Test]
	public async Task A_package_curb_cannot_read_is_left_to_dotnet_tool_run()
	{
		var fs = new MockFileSystem();
		fs.AddFile($"{Home}/.nuget/packages/fantomas/8.1.0/tools/net10.0/any/DotnetToolSettings.xml", new MockFileData(
			"""<DotNetCliTool Version="1"><Commands><Command Name="fantomas" EntryPoint="fantomas" Runner="executable" /></Commands></DotNetCliTool>"""));

		var tool = Resolver(fs).Resolve(Pinned()).Tool;

		tool!.EntryPoint.Should().BeNull();
		var command = tool.Command("dotnet", ["daemon"]);
		command.Arguments.Should().Equal("tool", "run", "fantomas", "--", "daemon");
		command.WorkingDirectory.Should().Be("/repo");
		await Task.CompletedTask;
	}

	[Test]
	public async Task Nothing_restored_is_a_problem_found_without_starting_anything()
	{
		var resolution = Resolver(new MockFileSystem()).Resolve(Pinned());

		resolution.Tool.Should().BeNull();
		resolution.Problem.Should().Contain("dotnet tool restore").And.Contain("8.1.0");
		await Task.CompletedTask;
	}

	[Test]
	public async Task Roll_forward_goes_before_the_entry_point()
	{
		var tool = new FantomasTool(Pinned(rollForward: true), "/f/fantomas.dll");

		tool.Command("/usr/bin/dotnet", ["daemon"]).Arguments.Should().Equal("exec", "--roll-forward", "Major", "/f/fantomas.dll", "daemon");
		new FantomasTool(Pinned(), "/f/fantomas.dll").Command("dotnet", ["daemon"]).Arguments.Should().Equal("exec", "/f/fantomas.dll", "daemon");
		await Task.CompletedTask;
	}

	[Test]
	[Arguments("8.1.0", "daemon")]
	[Arguments("9.0.0-alpha-002", "daemon")]
	[Arguments("8.0.0", "daemon")]
	[Arguments("8.0.0-alpha-016", "daemon")]
	[Arguments("8.0.0-alpha-015", "--daemon")]
	[Arguments("8.0.0-beta-001", "daemon")]
	[Arguments("7.0.3", "--daemon")]
	[Arguments("4.7.9", "--daemon")]
	public async Task The_daemon_command_follows_the_version(string version, string expected)
	{
		FantomasRunner.DaemonArgument(version).Should().Be(expected);
		await Task.CompletedTask;
	}

	[Test]
	[Arguments("8.1.0+9b5cde1f5f9d944e", "8.1.0", true)]
	[Arguments("8.1.0", "8.1.0", true)]
	[Arguments("v8.1.0", "8.1.0", true)]
	[Arguments("Fantomas v8.1.0+abc", "8.1.0", true)]
	[Arguments("8.1.1+abc", "8.1.0", false)]
	[Arguments("8.1.0-alpha-001", "8.1.0", false)]
	public async Task The_daemon_version_is_compared_without_build_metadata(string reported, string pinned, bool matches)
	{
		FantomasRunner.VersionMatches(reported, pinned).Should().Be(matches);
		await Task.CompletedTask;
	}

	[Test]
	[Arguments("8.1.0", "8.0.0-alpha-016", 1)]
	[Arguments("8.0.0", "8.0.0-alpha-016", 1)]
	[Arguments("8.0.0-alpha-016", "8.0.0-alpha-016", 0)]
	[Arguments("8.0.0-alpha-015", "8.0.0-alpha-016", -1)]
	[Arguments("8.0.0-alpha-100", "8.0.0-alpha-016", 1)]
	[Arguments("8.0.0-beta-001", "8.0.0-alpha-016", 1)]
	[Arguments("7.0.3", "8.0.0-alpha-016", -1)]
	[Arguments("8.1.0+abc", "8.1.0", 0)]
	[Arguments("10.0.0", "9.0.0", 1)]
	public async Task Versions_order_by_release_then_prerelease(string left, string right, int expected)
	{
		Math.Sign(FantomasVersion.Compare(left, right)).Should().Be(expected);
		Math.Sign(FantomasVersion.Compare(right, left)).Should().Be(-expected);
		await Task.CompletedTask;
	}

	[Test]
	public async Task Timeouts_scale_with_the_size_of_the_input()
	{
		var minute = TimeSpan.FromSeconds(60);
		FantomasRunner.Scaled(minute, 0, 1000).Should().Be(minute);
		FantomasRunner.Scaled(minute, 1000, 1000).Should().Be(minute);
		FantomasRunner.Scaled(minute, 1001, 1000).Should().Be(minute * 2);
		await Task.CompletedTask;
	}

	[Test]
	public async Task Long_file_lists_are_split_to_fit_a_command_line()
	{
		var paths = Enumerable.Range(0, 10).Select(i => $"/repo/{i:0000000}.fs").ToArray(); // 16 characters each, 19 budgeted

		var chunks = FantomasRunner.Chunks(paths, budget: 60).ToArray();

		chunks.Select(c => c.Length).Should().Equal(3, 3, 3, 1);
		chunks.SelectMany(c => c).Should().Equal(paths);
		FantomasRunner.Chunks(paths).Should().ContainSingle();
		await Task.CompletedTask;
	}

	[Test]
	public async Task Only_a_leading_comment_marks_a_file_generated()
	{
		FSharpLanguage.HasGeneratedHeader("// <auto-generated>\nnamespace X\n").Should().BeTrue();
		FSharpLanguage.HasGeneratedHeader("\n\n//------\n// <auto-generated />\n").Should().BeTrue();
		FSharpLanguage.HasGeneratedHeader("(*\n  <auto-generated>\n*)\nmodule X\n").Should().BeTrue();
		FSharpLanguage.HasGeneratedHeader("module X\n// <auto-generated>\n").Should().BeFalse();
		FSharpLanguage.HasGeneratedHeader("(* licence *)\nmodule X\nlet s = \"<auto-generated>\"\n").Should().BeFalse();
		FSharpLanguage.HasGeneratedHeader("").Should().BeFalse();
		await Task.CompletedTask;
	}

	[Test]
	public async Task Errors_are_shortened_to_their_first_diagnostic_without_the_path()
	{
		const string path = "/repo/A.fs";
		FantomasRunner.DescribeError($"{path} could not be parsed by Fantomas:\n\n{path}(1,9): error FS0583: Unmatched '('\n\n1 | let z = (\n", path)
			.Should().Be("(1,9): error FS0583: Unmatched '('");
		FantomasRunner.DescribeError("something odd\nmore", path).Should().Be("something odd");
		FantomasRunner.DescribeError(null, path).Should().Be("Fantomas gave no reason");
		await Task.CompletedTask;
	}

	private static void AddVersion1(MockFileSystem fs, string folder)
	{
		fs.AddFile($"{folder}/DotnetToolSettings.xml", new MockFileData(
			"""<DotNetCliTool Version="1"><Commands><Command Name="fantomas" EntryPoint="fantomas.dll" Runner="dotnet" /></Commands></DotNetCliTool>"""));
		fs.AddFile($"{folder}/fantomas.dll", new MockFileData(""));
	}
}
