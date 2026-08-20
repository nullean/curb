using System.IO.Abstractions.TestingHelpers;
using AwesomeAssertions;
using Nullean.Curb;
using Nullean.Curb.EditorConfig;
using Nullean.Curb.Options;

namespace Nullean.Curb.Tests.Options;

public class OptionsBindingTests
{
	private const string Root = "/repo";

	private static FormatOptions Bind(string editorConfig, out List<CurbDiagnostic> diagnostics)
	{
		var fs = new MockFileSystem();
		fs.AddFile($"{Root}/.editorconfig", new MockFileData(editorConfig));
		fs.AddFile($"{Root}/Foo.cs", new MockFileData("class Foo { }"));

		var configuration = new CurbEditorConfig(fs).For($"{Root}/Foo.cs");
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
			"reflow is opt-in so Curb is a no-op on an IDE0055-clean repository");
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
			.Which.Id.Should().Be("CURB1001");
		diagnostics[0].Message.Should().Contain("'spaces'").And.Contain("tab or space");
		await Task.CompletedTask;
	}

	[Test]
	public async Task Every_catalogued_option_is_implemented()
	{
		// CURB1003 — "Curb knows this key but does not act on it yet" — has nothing left to report.
		// The diagnostic stays wired up for whenever Microsoft adds an option and the catalog grows
		// ahead of the printers; this asserts the gap is currently empty rather than untested.
		var outstanding = OptionCatalog.FormattingKeys
			.Where(key => !OptionCatalog.IsImplemented(key))
			.Order(StringComparer.Ordinal)
			.ToArray();

		outstanding.Should().BeEmpty();
		await Task.CompletedTask;
	}

	[Test]
	public async Task An_unknown_option_suggests_the_closest_real_one()
	{
		Bind("root = true\n\n[*.cs]\ncsharp_space_after_casts = true\n", out var diagnostics);

		diagnostics.Should().ContainSingle().Which.Id.Should().Be("CURB1002");
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
			csharp_max_initializer_elements_on_line = 5
			csharp_place_simple_initializer_on_single_line = true
			csharp_wrap_chained_method_calls = chop_always
			""", out var diagnostics);

		diagnostics.Should().BeEmpty("those belong to ReSharper, not to Curb");
		await Task.CompletedTask;
	}

	[Test]
	public async Task A_wrap_style_that_needs_deterministic_layout_says_so()
	{
		// Not silence, and not a plain "unimplemented" either: Curb does implement this key, just not in
		// the mode this configuration asked for. Forcing an initializer to wrap moves what is nested
		// inside it, and preservation mode has other rules reading that nesting's indentation from the
		// source — 140 corpus files stopped settling. Saying which mode it needs is the useful answer.
		var options = Bind("""
			root = true

			[*.cs]
			csharp_wrap_object_and_collection_initializer_style = chop_always
			""", out var diagnostics);

		diagnostics.Should().ContainSingle().Which.Id.Should().Be("CURB1005");
		diagnostics[0].Message.Should().Contain("csharp_keep_existing_linebreaks = false");
		options.WrapObjectAndCollectionInitializerStyle.Should().BeNull("the key was dropped, not applied");
		await Task.CompletedTask;
	}

	[Test]
	public async Task The_same_wrap_style_binds_under_deterministic_layout()
	{
		var options = Bind("""
			root = true

			[*.cs]
			max_line_length = 120
			csharp_wrap_arguments_style = chop_always
			csharp_wrap_object_and_collection_initializer_style = chop_always
			""", out var diagnostics);

		diagnostics.Should().BeEmpty();
		options.KeepExistingLinebreaks.Should().BeFalse("a width was asked for, so layout is deterministic");
		options.WrapArgumentsStyle.Should().Be(WrapStyle.ChopAlways);
		options.WrapObjectAndCollectionInitializerStyle.Should().Be(WrapStyle.ChopAlways);
		await Task.CompletedTask;
	}

	// ---- the mode resolves from the width -----------------------------------------------------------

	/// <summary>
	/// <c>max_line_length</c> selects the layout mode as well as the width.
	/// </summary>
	/// <remarks>
	/// Asking for reflow is asking Curb to decide layout, so it is one opt-in rather than two. The
	/// alternative — deterministic everywhere, with an implied width — would hand a repository that never
	/// named a width a 46,907-line diff instead of a 17,035-line one on the corpus. The file counts barely
	/// differ (894 against 742), which is exactly why file count is the wrong metric for this decision.
	/// </remarks>
	[Test]
	public async Task No_width_means_preservation()
	{
		var options = Bind("root = true\n\n[*.cs]\nindent_style = tab\n", out var diagnostics);

		diagnostics.Should().BeEmpty();
		options.KeepExistingLinebreaksOption.Should().BeNull("the key was not mentioned");
		options.KeepExistingLinebreaks.Should().BeTrue();
		options.ReflowDisabled.Should().BeTrue();
		await Task.CompletedTask;
	}

	[Test]
	public async Task A_width_means_deterministic()
	{
		var options = Bind("root = true\n\n[*.cs]\nmax_line_length = 120\n", out var diagnostics);

		diagnostics.Should().BeEmpty();
		options.KeepExistingLinebreaksOption.Should().BeNull();
		options.KeepExistingLinebreaks.Should().BeFalse();
		options.ReflowDisabled.Should().BeFalse();
		await Task.CompletedTask;
	}

	[Test]
	public async Task An_explicit_true_opts_back_out_of_it()
	{
		var options = Bind("""
			root = true

			[*.cs]
			max_line_length = 120
			csharp_keep_existing_linebreaks = true
			""", out var diagnostics);

		diagnostics.Should().BeEmpty();
		options.KeepExistingLinebreaksOption.Should().BeTrue();
		options.KeepExistingLinebreaks.Should().BeTrue("reflow on, but the author's breaks are kept");
		options.ReflowDisabled.Should().BeFalse();
		await Task.CompletedTask;
	}

	/// <summary>Deterministic layout with no width would join every construct that fits, so it is refused.</summary>
	[Test]
	public async Task Deterministic_layout_without_a_width_is_refused()
	{
		var options = Bind("""
			root = true

			[*.cs]
			csharp_keep_existing_linebreaks = false
			""", out var diagnostics);

		diagnostics.Should().ContainSingle().Which.Id.Should().Be("CURB1007");
		diagnostics[0].Message.Should().Contain("max_line_length");
		options.KeepExistingLinebreaks.Should().BeTrue("refused, so preservation stands");
		await Task.CompletedTask;
	}

	/// <summary>The order the binder reads these two in is load-bearing, so assert it rather than trust it.</summary>
	/// <remarks>
	/// The resolved mode reads <c>MaxLineLength</c>, and the binder's own diagnostic helpers read the
	/// resolved mode while binding is still in progress. Put the width after the keys that ask and every one
	/// of them sees the wrong mode — this is the cheapest possible guard against that regressing.
	/// </remarks>
	[Test]
	public async Task A_deterministic_only_key_sees_the_width_whichever_order_it_is_written_in()
	{
		var before = Bind("""
			root = true

			[*.cs]
			csharp_wrap_arguments_style = chop_always
			max_line_length = 120
			""", out var beforeDiagnostics);

		var after = Bind("""
			root = true

			[*.cs]
			max_line_length = 120
			csharp_wrap_arguments_style = chop_always
			""", out var afterDiagnostics);

		beforeDiagnostics.Should().BeEmpty();
		afterDiagnostics.Should().BeEmpty();
		before.WrapArgumentsStyle.Should().Be(WrapStyle.ChopAlways);
		after.WrapArgumentsStyle.Should().Be(WrapStyle.ChopAlways);
		await Task.CompletedTask;
	}

	/// <summary>
	/// The one class of failure Curb cannot fix from the inside: a value it can express, and produce
	/// idempotently, that <c>dotnet format</c> reverts anyway.
	/// </summary>
	/// <remarks>
	/// Measured rather than reasoned: joining every attribute unconditionally came back changed on 347 of
	/// 1,196 corpus files, because dotnet format moves an attribute off a member that spans lines. Its own
	/// rule is <c>if_owner_is_single_line</c>, which is why that value is clean at 100%.
	/// </remarks>
	[Test]
	public async Task An_attribute_placement_dotnet_format_would_undo_is_refused()
	{
		var options = Bind("""
			root = true

			[*.cs]
			max_line_length = 120
			csharp_place_method_attribute_on_same_line = true
			""", out var diagnostics);

		diagnostics.Should().ContainSingle().Which.Id.Should().Be("CURB1006");
		diagnostics[0].Message.Should().Contain("if_owner_is_single_line");
		options.PlaceMethodAttributeOnSameLine.Should().Be(AttributePlacement.OwnLine);
		options.MeasuresWholeMembers.Should().BeFalse("nothing needs a member measured as one unit");
		await Task.CompletedTask;
	}

	[Test]
	public async Task The_conditional_attribute_placement_binds_and_asks_for_a_member_group()
	{
		var options = Bind("""
			root = true

			[*.cs]
			max_line_length = 120
			csharp_place_property_attribute_on_same_line = if_owner_is_single_line
			""", out var diagnostics);

		diagnostics.Should().BeEmpty();
		options.PlacePropertyAttributeOnSameLine.Should().Be(AttributePlacement.IfOwnerIsSingleLine);
		options.PlaceMethodAttributeOnSameLine.Should().Be(AttributePlacement.OwnLine);
		options.MeasuresWholeMembers.Should().BeTrue();
		await Task.CompletedTask;
	}

	[Test]
	public async Task Preservation_keys_report_that_deterministic_layout_ignores_them()
	{
		Bind("""
			root = true

			[*.cs]
			max_line_length = 120
			csharp_preserve_single_line_blocks = true
			""", out var diagnostics);

		diagnostics.Should().ContainSingle().Which.Id.Should().Be("CURB1004");
		await Task.CompletedTask;
	}

	[Test]
	public async Task Empty_block_style_binds_under_all_four_resharper_spellings()
	{
		foreach (var key in new[]
		{
			"resharper_csharp_empty_block_style",
			"csharp_empty_block_style",
			"resharper_empty_block_style",
			"empty_block_style",
		})
		{
			var options = Bind($"root = true\n\n[*.cs]\n{key} = together_same_line\n", out var diagnostics);

			diagnostics.Should().BeEmpty(key);
			options.EmptyBlockStyle.Should().Be(EmptyBlockStyle.TogetherSameLine, key);
		}

		await Task.CompletedTask;
	}

	[Test]
	public async Task An_unrecognised_empty_block_style_value_is_reported()
	{
		var options = Bind("root = true\n\n[*.cs]\ncsharp_empty_block_style = sometimes\n", out var diagnostics);

		diagnostics.Should().ContainSingle().Which.Id.Should().Be("CURB1001");
		options.EmptyBlockStyle.Should().BeNull();
		await Task.CompletedTask;
	}

	[Test]
	public async Task Empty_block_style_is_unset_by_default()
	{
		var options = Bind("root = true\n\n[*.cs]\n", out var diagnostics);

		diagnostics.Should().BeEmpty();
		options.EmptyBlockStyle.Should().BeNull();
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
	public async Task Every_implemented_option_can_be_printed()
	{
		// print-config is driven by ImplementedKeys, so an option onboarded without teaching
		// OptionValues about it would be reported as blank rather than left out. Fail here instead.
		var options = new FormatOptions();

		var missing = OptionCatalog.ImplementedKeys
			.Where(key => OptionValues.Of(options, key) is null)
			.Order(StringComparer.Ordinal)
			.ToArray();

		missing.Should().BeEmpty("print-config would report these as blank");
		await Task.CompletedTask;
	}

	[Test]
	public async Task Nothing_unimplemented_claims_to_have_a_value()
	{
		var options = new FormatOptions();

		var claimed = OptionCatalog.FormattingKeys
			.Where(key => !OptionCatalog.IsImplemented(key))
			.Where(key => OptionValues.Of(options, key) is not null)
			.Order(StringComparer.Ordinal)
			.ToArray();

		claimed.Should().BeEmpty("reporting a resolved value for an option Curb ignores is the failure mode the catalog exists to prevent");
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
