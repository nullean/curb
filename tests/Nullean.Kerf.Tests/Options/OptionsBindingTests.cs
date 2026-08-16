using System.IO.Abstractions.TestingHelpers;
using AwesomeAssertions;
using Nullean.Kerf;
using Nullean.Kerf.EditorConfig;
using Nullean.Kerf.Options;

namespace Nullean.Kerf.Tests.Options;

public class OptionsBindingTests
{
	private const string Root = "/repo";

	private static FormatOptions Bind(string editorConfig, out List<KerfDiagnostic> diagnostics)
	{
		var fs = new MockFileSystem();
		fs.AddFile($"{Root}/.editorconfig", new MockFileData(editorConfig));
		fs.AddFile($"{Root}/Foo.cs", new MockFileData("class Foo { }"));

		var configuration = new KerfEditorConfig(fs).For($"{Root}/Foo.cs");
		diagnostics = [];
		return EditorConfigOptionsBinder.Bind(configuration, diagnostics);
	}

	[Test]
	public async Task Defaults_match_roslyn_and_leave_reflow_off()
	{
		var options = Bind("root = true\n\n[*.cs]\n", out _);

		options.IndentSize.Should().Be(4);
		options.UseTabs.Should().BeFalse();
		options.InsertFinalNewLine.Should().BeTrue();
		options.TrimTrailingWhitespace.Should().BeTrue();
		options.MaxLineLength.Should().Be(FormatOptions.Off,
			"reflow is opt-in so Kerf is a no-op on an IDE0055-clean repository");
		await Task.CompletedTask;
	}

	[Test]
	public async Task Binds_the_core_keys()
	{
		var options = Bind("""
			root = true

			[*.cs]
			indent_style = tab
			indent_size = 2
			max_line_length = 120
			end_of_line = crlf
			insert_final_newline = false
			trim_trailing_whitespace = false
			""", out var diagnostics);

		options.UseTabs.Should().BeTrue();
		options.IndentSize.Should().Be(2);
		options.MaxLineLength.Should().Be(120);
		options.EndOfLine.Should().Be(EndOfLine.CrLf);
		options.InsertFinalNewLine.Should().BeFalse();
		options.TrimTrailingWhitespace.Should().BeFalse();
		diagnostics.Should().BeEmpty();
		await Task.CompletedTask;
	}

	[Test]
	public async Task max_line_length_off_disables_reflow()
	{
		var options = Bind("root = true\n\n[*.cs]\nmax_line_length = off\n", out _);

		options.MaxLineLength.Should().Be(FormatOptions.Off);
		options.ReflowDisabled.Should().BeTrue();
		await Task.CompletedTask;
	}

	[Test]
	public async Task tab_width_falls_back_to_indent_size()
	{
		var options = Bind("root = true\n\n[*.cs]\nindent_size = 3\n", out _);

		options.TabWidth.Should().Be(3);
		await Task.CompletedTask;
	}

	[Test]
	public async Task A_bad_value_warns_and_keeps_the_default()
	{
		var options = Bind("root = true\n\n[*.cs]\nindent_style = spaces\n", out var diagnostics);

		options.UseTabs.Should().BeFalse("the default survives");
		diagnostics.Should().ContainSingle()
			.Which.Id.Should().Be("KERF1001");
		diagnostics[0].Message.Should().Contain("'spaces'").And.Contain("tab or space");
		await Task.CompletedTask;
	}

	[Test]
	public async Task A_known_but_unimplemented_option_is_reported_not_ignored()
	{
		// Whichever key is still outstanding, rather than a name that has to be swapped every time
		// an option is onboarded. Once the catalog is fully implemented this test has nothing left
		// to assert and should be deleted along with the KERF1003 diagnostic.
		var outstanding = OptionCatalog.FormattingKeys
			.Where(key => !OptionCatalog.IsImplemented(key))
			.Order(StringComparer.Ordinal)
			.FirstOrDefault();

		outstanding.Should().NotBeNull("every catalogued option is implemented — delete this test and KERF1003");

		var options = Bind($"root = true\n\n[*.cs]\n{outstanding} = true\n", out var diagnostics);

		options.Should().NotBeNull();
		diagnostics.Should().ContainSingle().Which.Id.Should().Be("KERF1003");
		diagnostics[0].Message.Should().Contain(outstanding).And.Contain("does not implement it yet");
		await Task.CompletedTask;
	}

	[Test]
	public async Task An_unknown_option_suggests_the_closest_real_one()
	{
		Bind("root = true\n\n[*.cs]\ncsharp_space_after_casts = true\n", out var diagnostics);

		diagnostics.Should().ContainSingle().Which.Id.Should().Be("KERF1002");
		diagnostics[0].Message.Should().Contain("Did you mean 'csharp_space_after_cast'?");
		await Task.CompletedTask;
	}

	[Test]
	public async Task Resharper_formatting_keys_are_left_alone()
	{
		// The csharp_* namespace is shared with ReSharper and Rider. These are real keys taken from
		// a real repository's .editorconfig; warning about them would fire nearly everywhere and
		// would be wrong.
		Bind("""
			root = true

			[*.cs]
			csharp_align_multiline_parameter = false
			csharp_align_multiline_array_and_object_initializer = false
			csharp_alignment_tab_fill_style = optimal_fill
			csharp_preferred_modifier_order = public, private, protected
			csharp_wrap_object_and_collection_initializer_style = chop_always
			csharp_max_initializer_elements_on_line = 5
			csharp_place_simple_initializer_on_single_line = true
			""", out var diagnostics);

		diagnostics.Should().BeEmpty("those belong to ReSharper, not to Kerf");
		await Task.CompletedTask;
	}

	[Test]
	public async Task Code_style_and_naming_keys_are_not_our_business()
	{
		Bind("""
			root = true

			[*.cs]
			csharp_style_var_elsewhere = true:suggestion
			dotnet_style_readonly_field = true
			dotnet_naming_rule.x.severity = warning
			dotnet_diagnostic.IDE0055.severity = warning
			""", out var diagnostics);

		diagnostics.Should().BeEmpty("those belong to dotnet format style, not to a formatter");
		await Task.CompletedTask;
	}

	[Test]
	public async Task Cr_line_endings_are_rejected_explicitly()
	{
		Bind("root = true\n\n[*.cs]\nend_of_line = cr\n", out var diagnostics);

		diagnostics.Should().ContainSingle().Which.Message.Should().Contain("cr is not supported");
		await Task.CompletedTask;
	}

	[Test]
	public async Task The_catalog_covers_all_39_ide0055_options()
	{
		// If this number moves, either Microsoft added an option or someone mistyped one.
		OptionCatalog.FormattingKeys.Should().HaveCount(39);
		OptionCatalog.CoreKeys.Should().HaveCount(8);
		await Task.CompletedTask;
	}
}
