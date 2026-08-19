using System.IO.Abstractions.TestingHelpers;
using AwesomeAssertions;
using Nullean.Curb.EditorConfig;

namespace Nullean.Curb.Tests;

public class EditorConfigBindingTests
{
	private const string Root = "/repo";

	private static MockFileSystem WithEditorConfig(string contents)
	{
		var fs = new MockFileSystem();
		fs.AddDirectory(Root);
		fs.AddFile($"{Root}/.editorconfig", new MockFileData(contents));
		fs.AddFile($"{Root}/src/Foo.cs", new MockFileData("class Foo { }"));
		return fs;
	}

	[Test]
	public async Task Reads_arbitrary_csharp_formatting_keys()
	{
		// The whole premise of Curb: the csharp_* / dotnet_* surface is available verbatim, not just
		// the handful of keys editorconfig models as typed properties.
		var fs = WithEditorConfig("""
			root = true

			[*.cs]
			indent_style = tab
			max_line_length = 160
			csharp_new_line_before_open_brace = all
			csharp_space_after_cast = false
			csharp_preserve_single_line_blocks = true
			dotnet_sort_system_directives_first = true
			""");

		var config = new CurbEditorConfig(fs).For($"{Root}/src/Foo.cs");

		config.Properties["csharp_new_line_before_open_brace"].Should().Be("all");
		config.Properties["csharp_space_after_cast"].Should().Be("false");
		config.Properties["csharp_preserve_single_line_blocks"].Should().Be("true");
		config.Properties["dotnet_sort_system_directives_first"].Should().Be("true");
		await Task.CompletedTask;
	}

	[Test]
	public async Task Later_sections_win()
	{
		var fs = WithEditorConfig("""
			root = true

			[*.cs]
			csharp_space_after_cast = false

			[Foo.cs]
			csharp_space_after_cast = true
			""");

		var config = new CurbEditorConfig(fs).For($"{Root}/src/Foo.cs");

		config.Properties["csharp_space_after_cast"].Should().Be("true");
		await Task.CompletedTask;
	}

	[Test]
	public async Task Resolves_typed_core_properties()
	{
		var fs = WithEditorConfig("""
			root = true

			[*.cs]
			indent_style = tab
			indent_size = 4
			max_line_length = 120
			""");

		var config = new CurbEditorConfig(fs).For($"{Root}/src/Foo.cs");

		config.IndentStyle.Should().Be(global::EditorConfig.Core.IndentStyle.Tab);
		config.MaxLineLength.Should().Be(120);
		await Task.CompletedTask;
	}
}
