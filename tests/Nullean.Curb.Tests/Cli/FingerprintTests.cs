using System.IO.Abstractions.TestingHelpers;
using System.Text;
using AwesomeAssertions;
using Nullean.Curb.Cli;
using Nullean.Curb.EditorConfig;

namespace Nullean.Curb.Tests.Cli;

/// <summary>The two hashes an entry is keyed on, and the one thing neither may do: miss a real change.</summary>
public class FingerprintTests
{
	private static MockFileSystem WithEditorConfig(string contents)
	{
		var fs = new MockFileSystem();
		fs.AddFile("/repo/.editorconfig", new MockFileData(contents));
		fs.AddFile("/repo/src/Foo.cs", new MockFileData("class Foo { }"));
		fs.AddFile("/repo/src/Bar.cs", new MockFileData("class Bar { }"));
		return fs;
	}

	private static UInt128 OptionsOf(string editorConfig, string file = "/repo/src/Foo.cs") =>
		Fingerprint.OfOptions(new CurbEditorConfig(WithEditorConfig(editorConfig)).For(file));

	[Test]
	public async Task The_same_configuration_always_hashes_the_same()
	{
		const string config = """
			root = true

			[*.cs]
			indent_style = tab
			max_line_length = 160
			""";

		OptionsOf(config).Should().Be(OptionsOf(config));
		await Task.CompletedTask;
	}

	[Test]
	public async Task A_changed_value_moves_the_hash()
	{
		var before = OptionsOf("""
			root = true

			[*.cs]
			indent_style = tab
			""");

		var after = OptionsOf("""
			root = true

			[*.cs]
			indent_style = space
			""");

		before.Should().NotBe(after);
		await Task.CompletedTask;
	}

	[Test]
	public async Task An_added_key_moves_the_hash()
	{
		var before = OptionsOf("""
			root = true

			[*.cs]
			indent_style = tab
			""");

		var after = OptionsOf("""
			root = true

			[*.cs]
			indent_style = tab
			max_line_length = 120
			""");

		before.Should().NotBe(after);
		await Task.CompletedTask;
	}

	[Test]
	public async Task The_order_keys_are_written_in_does_not_move_the_hash()
	{
		// A dictionary hands its keys back in whatever order it holds them, so without sorting two runs
		// over the same repository could disagree and the cache would never hit.
		var first = OptionsOf("""
			root = true

			[*.cs]
			indent_style = tab
			max_line_length = 160
			csharp_space_after_cast = false
			""");

		var second = OptionsOf("""
			root = true

			[*.cs]
			csharp_space_after_cast = false
			max_line_length = 160
			indent_style = tab
			""");

		first.Should().Be(second);
		await Task.CompletedTask;
	}

	[Test]
	public async Task A_section_that_discriminates_on_file_name_is_seen()
	{
		// The reason options resolve per file rather than per directory, and therefore the reason the
		// fingerprint has to be taken per file too.
		const string config = """
			root = true

			[*.cs]
			indent_style = tab

			[Foo.cs]
			indent_style = space
			""";

		OptionsOf(config).Should().NotBe(OptionsOf(config, "/repo/src/Bar.cs"));
		await Task.CompletedTask;
	}

	[Test]
	public async Task Reusing_the_answer_agrees_with_computing_it()
	{
		// The memo exists so a folder resolves its properties once; it must not be able to hand back a
		// stale answer when a file-scoped section changes the result mid-directory.
		var fs = WithEditorConfig("""
			root = true

			[*.cs]
			indent_style = tab

			[Foo.cs]
			indent_style = space
			""");

		var editorConfig = new CurbEditorConfig(fs);
		var fingerprinter = new OptionsFingerprinter();

		foreach (var path in new[] { "/repo/src/Bar.cs", "/repo/src/Foo.cs", "/repo/src/Bar.cs" })
		{
			var configuration = editorConfig.For(path);
			fingerprinter.Of(configuration).Should().Be(Fingerprint.OfOptions(configuration));
		}

		await Task.CompletedTask;
	}

	[Test]
	public async Task A_byte_order_mark_is_part_of_the_content()
	{
		// Adding or removing a mark is a change to the file even when every byte after it is identical,
		// and the run counts it as one — so the cache has to key on it or it would serve a file back as
		// unchanged that Curb is about to rewrite.
		var without = Encoding.UTF8.GetBytes("class Foo { }");
		var with = (byte[])[0xEF, 0xBB, 0xBF, .. without];

		Fingerprint.OfContent(without).Should().NotBe(Fingerprint.OfContent(with));
		await Task.CompletedTask;
	}

	[Test]
	public async Task Content_hashes_are_stable_and_distinguish_files()
	{
		var a = Encoding.UTF8.GetBytes("class A { }");
		var b = Encoding.UTF8.GetBytes("class B { }");

		Fingerprint.OfContent(a).Should().Be(Fingerprint.OfContent(a));
		Fingerprint.OfContent(a).Should().NotBe(Fingerprint.OfContent(b));
		Fingerprint.OfContent([]).Should().Be(Fingerprint.OfContent([]));
		await Task.CompletedTask;
	}
}
