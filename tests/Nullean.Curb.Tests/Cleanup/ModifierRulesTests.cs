using AwesomeAssertions;
using Microsoft.CodeAnalysis.Text;
using Nullean.Curb.Cleanup;

namespace Nullean.Curb.Tests.Cleanup;

/// <summary>
/// IDE0044 and IDE0040, the two rules that add a modifier.
/// </summary>
/// <remarks>
/// <para>
/// Both are reported at the declaration's <em>name</em> rather than at the declaration — measured, not
/// assumed — so the diagnostics here are built by finding an identifier in the source, which is what the
/// compiler would have pointed at.
/// </para>
/// <para>
/// Neither needs the diagnostic's end, so both work from MSBuild's console output as well as from a log.
/// The `Start_alone_is_enough` cases assert that, because it is the difference between these rules and
/// IDE0005.
/// </para>
/// </remarks>
public class ModifierRulesTests
{
	private const string Path = "/repo/Widget.cs";

	private static CleanupResult Clean(string source, params CleanupDiagnostic[] diagnostics)
	{
		var result = new CSharpCleaner().Clean(source, diagnostics);
		CleanupExpectationDump.Record(result, diagnostics.Select(d => d.RuleId));
		return result;
	}

	/// <summary>Points at <paramref name="name"/> where it is declared, as the compiler does.</summary>
	private static CleanupDiagnostic At(string ruleId, string source, string name)
	{
		var text = SourceText.From(source);
		var offset = source.IndexOf(name, StringComparison.Ordinal);
		offset.Should().BeGreaterThanOrEqualTo(0, $"the fixture has to contain '{name}'");

		var line = text.Lines.GetLineFromPosition(offset);
		return new CleanupDiagnostic(ruleId, Path, new LinePosition(line.LineNumber, offset - line.Start));
	}

	// ---- IDE0044, readonly ------------------------------------------------------------------------

	[Test]
	public async Task Readonly_is_added_after_the_accessibility()
	{
		const string source = """
			namespace N;

			public sealed class Widget
			{
				private string _name;

				public Widget() => _name = "w";
			}

			""";

		var result = Clean(source, At("IDE0044", source, "_name;"));

		result.Status.Should().Be(CleanupStatus.Cleaned);
		result.Applied.Should().Be(1);
		result.Text.Should().Contain("private readonly string _name;");

		await Task.CompletedTask;
	}

	[Test]
	public async Task Readonly_goes_after_static_and_before_unsafe()
	{
		// Roslyn's order is `static` then `readonly` then `unsafe`. Appending to the modifier list would
		// give `private unsafe readonly`, which compiles and is then reported by IDE0036 — fixing one rule
		// by breaking another.
		const string source = """
			namespace N;

			public sealed class Widget
			{
				private static int _count;
				private unsafe int* _pointer;
			}

			""";

		var result = Clean(source, At("IDE0044", source, "_count;"), At("IDE0044", source, "_pointer;"));

		result.Applied.Should().Be(2);
		result.Text.Should().Contain("private static readonly int _count;");
		result.Text.Should().Contain("private readonly unsafe int* _pointer;");

		await Task.CompletedTask;
	}

	[Test]
	public async Task Readonly_lands_after_an_attribute_not_before_it()
	{
		const string source = """
			using System;

			namespace N;

			public sealed class Widget
			{
				[Obsolete]
				string _name = "w";
			}

			""";

		var result = Clean(source, At("IDE0044", source, "_name ="));

		result.Applied.Should().Be(1);
		result.Text.Should().Contain("[Obsolete]\n\treadonly string _name",
			"writing the keyword before the attribute would not compile");

		await Task.CompletedTask;
	}

	[Test]
	public async Task Start_alone_is_enough_for_readonly()
	{
		// No end position, which is all MSBuild's console output carries.
		const string source = """
			namespace N;

			public sealed class Widget
			{
				private string _name = "w";
			}

			""";

		var diagnostic = At("IDE0044", source, "_name =");
		diagnostic.HasSpan.Should().BeFalse();

		Clean(source, diagnostic).Applied.Should().Be(1);
		await Task.CompletedTask;
	}

	[Test]
	public async Task A_field_that_is_already_readonly_is_refused()
	{
		const string source = """
			namespace N;

			public sealed class Widget
			{
				private readonly string _name = "w";
			}

			""";

		var result = Clean(source, At("IDE0044", source, "_name ="));

		result.Changed.Should().BeFalse();
		result.Refusals.Should().ContainSingle().Which.Should().Contain("already");

		await Task.CompletedTask;
	}

	[Test]
	public async Task A_declaration_of_several_fields_is_refused()
	{
		// `int a, b;` is one declaration and two fields. readonly would apply to both, and the compiler
		// only said one of them qualifies.
		const string source = """
			namespace N;

			public sealed class Widget
			{
				private int _a, _b;

				public Widget() => _a = 1;
			}

			""";

		var result = Clean(source, At("IDE0044", source, "_a,"));

		result.Changed.Should().BeFalse();
		result.Refusals.Should().ContainSingle().Which.Should().Contain("more than one field");

		await Task.CompletedTask;
	}

	[Test]
	public async Task A_position_naming_something_that_is_not_a_field_is_refused()
	{
		const string source = """
			namespace N;

			public sealed class Widget
			{
				private string Name() => "w";
			}

			""";

		var result = Clean(source, At("IDE0044", source, "Name()"));

		result.Changed.Should().BeFalse();
		result.Refusals.Should().ContainSingle().Which.Should().Contain("only a field");

		await Task.CompletedTask;
	}

	[Test]
	public async Task A_position_on_an_identifier_that_is_not_a_declaration_name_is_refused()
	{
		// What a stale log looks like: the position still lands on an identifier, but one inside a body
		// rather than a member's name. Without this gate the modifier would go on whatever encloses it.
		const string source = """
			namespace N;

			public sealed class Widget
			{
				private string _name = "w";

				public string Describe() => _name.Trim();
			}

			""";

		var result = Clean(source, At("IDE0044", source, "_name.Trim"));

		result.Changed.Should().BeFalse();
		result.Refusals.Should().ContainSingle().Which.Should().Contain("names something other than the member");

		await Task.CompletedTask;
	}

	// ---- IDE0040, accessibility -------------------------------------------------------------------

	[Test]
	public async Task A_type_member_gets_private()
	{
		const string source = """
			namespace N;

			public sealed class Widget
			{
				int _count;

				void Bump() => _count++;
			}

			""";

		var result = Clean(source, At("IDE0040", source, "_count;"), At("IDE0040", source, "Bump()"));

		result.Applied.Should().Be(2);
		result.Text.Should().Contain("private int _count;").And.Contain("private void Bump()");

		await Task.CompletedTask;
	}

	[Test]
	public async Task A_top_level_type_gets_internal()
	{
		const string source = """
			namespace N;

			class Widget
			{
			}

			""";

		var result = Clean(source, At("IDE0040", source, "Widget"));

		result.Applied.Should().Be(1);
		result.Text.Should().Contain("internal class Widget", "that is the accessibility C# already applied");

		await Task.CompletedTask;
	}

	[Test]
	public async Task An_interface_member_gets_public()
	{
		const string source = """
			namespace N;

			public interface IWidget
			{
				string Describe();
			}

			""";

		var result = Clean(source, At("IDE0040", source, "Describe()"));

		result.Applied.Should().Be(1);
		result.Text.Should().Contain("public string Describe();");

		await Task.CompletedTask;
	}

	[Test]
	public async Task Accessibility_goes_first_in_the_modifier_list()
	{
		const string source = """
			namespace N;

			public sealed class Widget
			{
				static async Task Work() => await Task.Delay(1);
			}

			""";

		var result = Clean(source, At("IDE0040", source, "Work()"));

		result.Applied.Should().Be(1);
		result.Text.Should().Contain("private static async Task Work()");

		await Task.CompletedTask;
	}

	[Test]
	public async Task A_declaration_that_already_states_its_accessibility_is_refused()
	{
		const string source = """
			namespace N;

			public sealed class Widget
			{
				internal int _count;
			}

			""";

		var result = Clean(source, At("IDE0040", source, "_count;"));

		result.Changed.Should().BeFalse();
		result.Refusals.Should().ContainSingle().Which.Should().Contain("already states");

		await Task.CompletedTask;
	}

	[Test]
	public async Task A_partial_member_is_refused()
	{
		// The other part may carry the accessibility, and two would not compile. Deciding it needs the
		// whole type, which is more than this rule is allowed to know.
		const string source = """
			namespace N;

			public partial class Widget
			{
				partial void OnChanged();
			}

			""";

		var result = Clean(source, At("IDE0040", source, "OnChanged()"));

		result.Changed.Should().BeFalse();
		result.Refusals.Should().ContainSingle().Which.Should().Contain("partial");

		await Task.CompletedTask;
	}

	[Test]
	public async Task An_enum_member_is_refused()
	{
		const string source = """
			namespace N;

			public enum Colour
			{
				Red,
			}

			""";

		var result = Clean(source, At("IDE0040", source, "Red,"));

		result.Changed.Should().BeFalse();
		result.Refusals.Should().ContainSingle();

		await Task.CompletedTask;
	}

	[Test]
	public async Task An_explicit_interface_implementation_is_refused()
	{
		const string source = """
			namespace N;

			public interface IWidget
			{
				string Describe();
			}

			public sealed class Widget : IWidget
			{
				string IWidget.Describe() => "w";
			}

			""";

		var result = Clean(source, At("IDE0040", source, "Describe() => "));

		result.Changed.Should().BeFalse();
		result.Refusals.Should().ContainSingle();

		await Task.CompletedTask;
	}

	// ---- Both together, and idempotency ----------------------------------------------------------

	[Test]
	public async Task Both_rules_on_the_same_field_apply_together()
	{
		const string source = """
			namespace N;

			public sealed class Widget
			{
				string _name;

				public Widget() => _name = "w";
			}

			""";

		var result = Clean(source, At("IDE0040", source, "_name;"), At("IDE0044", source, "_name;"));

		result.Applied.Should().Be(2);
		result.Text.Should().Contain("private readonly string _name;",
			"accessibility sorts first and readonly after it, so the two insertions do not fight");

		await Task.CompletedTask;
	}

	[Test]
	public async Task Cleaning_the_output_again_changes_nothing()
	{
		const string source = """
			namespace N;

			public sealed class Widget
			{
				string _name;

				public Widget() => _name = "w";
			}

			""";

		var once = Clean(source, At("IDE0040", source, "_name;"), At("IDE0044", source, "_name;"));
		once.Changed.Should().BeTrue();

		// The same diagnostics no longer apply: the modifiers are there, so both rules refuse.
		var twice = Clean(once.Text!, At("IDE0040", source, "_name;"), At("IDE0044", source, "_name;"));
		twice.Changed.Should().BeFalse();
		twice.Refusals.Should().HaveCount(2);

		await Task.CompletedTask;
	}
}
