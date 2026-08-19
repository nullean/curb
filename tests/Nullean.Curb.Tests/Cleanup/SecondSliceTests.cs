using AwesomeAssertions;
using Microsoft.CodeAnalysis.Text;
using Nullean.Curb.Cleanup;

namespace Nullean.Curb.Tests.Cleanup;

/// <summary>
/// The second slice: IDE0250, IDE0251, IDE0034, IDE0071 and IDE0240.
/// </summary>
/// <remarks>
/// All five reuse the two deltas the first slice built, which is why they are one change rather than five.
/// Positions are the measured ones: the readonly rules report at the declaration's name, IDE0034 at the
/// <c>default</c> keyword, IDE0071 at the dot, and IDE0240 at the <c>#</c>.
/// </remarks>
public class SecondSliceTests
{
	private const string Path = "/repo/Widget.cs";

	private static CleanupResult Clean(string source, params CleanupDiagnostic[] diagnostics) =>
		new CSharpCleaner().Clean(source, diagnostics);

	private static CleanupDiagnostic At(string ruleId, string source, string at)
	{
		var text = SourceText.From(source);
		var offset = source.IndexOf(at, StringComparison.Ordinal);
		offset.Should().BeGreaterThanOrEqualTo(0, $"the fixture has to contain '{at}'");

		var line = text.Lines.GetLineFromPosition(offset);
		return new CleanupDiagnostic(ruleId, Path, new LinePosition(line.LineNumber, offset - line.Start));
	}

	// ---- IDE0250, readonly struct -----------------------------------------------------------------

	[Test]
	public async Task A_struct_is_made_readonly()
	{
		const string source = """
			namespace N;

			public struct Point
			{
				public int X { get; init; }
			}

			""";

		var result = Clean(source, At("IDE0250", source, "Point"));

		result.Applied.Should().Be(1);
		result.Text.Should().Contain("public readonly struct Point");

		await Task.CompletedTask;
	}

	[Test]
	public async Task A_record_struct_is_made_readonly()
	{
		const string source = """
			namespace N;

			public record struct Point(int X);

			""";

		Clean(source, At("IDE0250", source, "Point(")).Text.Should().Contain("public readonly record struct Point");
		await Task.CompletedTask;
	}

	[Test]
	public async Task A_class_is_refused()
	{
		const string source = """
			namespace N;

			public class Widget
			{
			}

			""";

		var result = Clean(source, At("IDE0250", source, "Widget"));

		result.Changed.Should().BeFalse();
		result.Refusals.Should().ContainSingle().Which.Should().Contain("only a struct");

		await Task.CompletedTask;
	}

	[Test]
	public async Task A_struct_that_is_already_readonly_is_refused()
	{
		const string source = """
			namespace N;

			public readonly struct Point
			{
			}

			""";

		Clean(source, At("IDE0250", source, "Point")).Refusals.Should().ContainSingle().Which.Should().Contain("already");
		await Task.CompletedTask;
	}

	// ---- IDE0251, readonly member -----------------------------------------------------------------

	[Test]
	public async Task A_struct_member_is_made_readonly()
	{
		const string source = """
			namespace N;

			public struct Point
			{
				public int X { get; init; }

				public int Doubled() => X * 2;
			}

			""";

		var result = Clean(source, At("IDE0251", source, "Doubled"));

		result.Applied.Should().Be(1);
		result.Text.Should().Contain("public readonly int Doubled()");

		await Task.CompletedTask;
	}

	[Test]
	public async Task A_class_member_is_refused()
	{
		// `readonly` on a class member does not compile, so a log that drifted onto one must not be applied.
		const string source = """
			namespace N;

			public class Widget
			{
				public int Doubled() => 2;
			}

			""";

		var result = Clean(source, At("IDE0251", source, "Doubled"));

		result.Changed.Should().BeFalse();
		result.Refusals.Should().ContainSingle().Which.Should().Contain("not declared in a struct");

		await Task.CompletedTask;
	}

	[Test]
	public async Task A_static_member_is_refused()
	{
		const string source = """
			namespace N;

			public struct Point
			{
				public static int Zero() => 0;
			}

			""";

		var result = Clean(source, At("IDE0251", source, "Zero"));

		result.Changed.Should().BeFalse();
		result.Refusals.Should().ContainSingle().Which.Should().Contain("static");

		await Task.CompletedTask;
	}

	[Test]
	public async Task Both_readonly_rules_can_apply_to_one_struct()
	{
		// Checked against the compiler: `readonly` on a member of a readonly struct compiles, so the pair
		// arriving together is not a conflict.
		const string source = """
			namespace N;

			public struct Point
			{
				public int X { get; init; }

				public int Doubled() => X * 2;
			}

			""";

		var result = Clean(source, At("IDE0250", source, "Point"), At("IDE0251", source, "Doubled"));

		result.Applied.Should().Be(2);
		result.Text.Should().Contain("public readonly struct Point").And.Contain("public readonly int Doubled()");

		await Task.CompletedTask;
	}

	// ---- IDE0034, default(T) ----------------------------------------------------------------------

	[Test]
	public async Task A_default_expression_loses_its_type()
	{
		const string source = """
			namespace N;

			public sealed class Widget
			{
				public bool Flag()
				{
					bool flag = default(bool);
					return flag;
				}
			}

			""";

		var result = Clean(source, At("IDE0034", source, "default(bool)"));

		result.Applied.Should().Be(1);
		result.Text.Should().Contain("bool flag = default;");

		await Task.CompletedTask;
	}

	[Test]
	public async Task A_default_with_a_comment_in_it_is_refused()
	{
		const string source = """
			namespace N;

			public sealed class Widget
			{
				public bool Flag()
				{
					bool flag = default(/* why */ bool);
					return flag;
				}
			}

			""";

		var result = Clean(source, At("IDE0034", source, "default(/*"));

		result.Changed.Should().BeFalse();
		result.Refusals.Should().ContainSingle().Which.Should().Contain("comment");

		await Task.CompletedTask;
	}

	// ---- IDE0071, interpolation ------------------------------------------------------------------

	[Test]
	public async Task A_redundant_tostring_is_dropped_from_an_interpolation()
	{
		const string source = """"
			namespace N;

			public sealed class Widget
			{
				public string Describe(int n) => $"{n.ToString()}";
			}

			"""";

		var result = Clean(source, At("IDE0071", source, ".ToString()"));

		result.Applied.Should().Be(1);
		result.Text.Should().Contain("""$"{n}";""");

		await Task.CompletedTask;
	}

	[Test]
	public async Task A_tostring_with_a_format_argument_is_refused()
	{
		// The real fix moves the argument into the interpolation's format clause — `{n:N0}` — which is two
		// places, not one. Deleting the call alone would silently lose the format.
		const string source = """"
			namespace N;

			public sealed class Widget
			{
				public string Describe(int n) => $"{n.ToString("N0")}";
			}

			"""";

		var result = Clean(source, At("IDE0071", source, ".ToString("));

		result.Changed.Should().BeFalse();
		result.Refusals.Should().ContainSingle().Which.Should().Contain("format clause");

		await Task.CompletedTask;
	}

	[Test]
	public async Task A_tostring_outside_an_interpolation_is_refused()
	{
		// Outside an interpolation the call is not redundant; it is what produces the string.
		const string source = """
			namespace N;

			public sealed class Widget
			{
				public string Describe(int n) => n.ToString();
			}

			""";

		var result = Clean(source, At("IDE0071", source, ".ToString()"));

		result.Changed.Should().BeFalse();
		result.Refusals.Should().ContainSingle().Which.Should().Contain("not the whole of an interpolation");

		await Task.CompletedTask;
	}

	[Test]
	public async Task A_different_call_at_the_position_is_refused()
	{
		const string source = """"
			namespace N;

			public sealed class Widget
			{
				public string Describe(string s) => $"{s.Trim()}";
			}

			"""";

		var result = Clean(source, At("IDE0071", source, ".Trim()"));

		result.Changed.Should().BeFalse();
		result.Refusals.Should().ContainSingle().Which.Should().Contain("not ToString");

		await Task.CompletedTask;
	}

	// ---- IDE0240, nullable directive -------------------------------------------------------------

	[Test]
	public async Task A_redundant_nullable_directive_is_removed_with_its_line()
	{
		const string source = """
			#nullable enable

			namespace N;

			public sealed class Widget
			{
			}

			""";

		var result = Clean(source, At("IDE0240", source, "#nullable enable"));

		result.Applied.Should().Be(1);
		result.Text.Should().NotContain("#nullable");
		result.Text.Should().StartWith("\nnamespace N;", "the line goes, not just the directive");

		await Task.CompletedTask;
	}

	[Test]
	public async Task A_nullable_directive_sharing_its_line_with_a_comment_is_refused()
	{
		const string source = """
			#nullable enable // load-bearing, see #1234

			namespace N;

			public sealed class Widget
			{
			}

			""";

		var result = Clean(source, At("IDE0240", source, "#nullable enable"));

		result.Changed.Should().BeFalse();
		result.Refusals.Should().ContainSingle().Which.Should().Contain("shares its line with a comment");

		await Task.CompletedTask;
	}

	[Test]
	public async Task A_nullable_directive_in_a_file_with_a_conditional_is_refused()
	{
		const string source = """
			#nullable enable

			namespace N;

			public sealed class Widget
			{
			#if NET
				public string? Name { get; init; }
			#endif
			}

			""";

		var result = Clean(source, At("IDE0240", source, "#nullable enable"));

		result.Changed.Should().BeFalse();
		result.Refusals.Should().ContainSingle().Which.Should().Contain("#if");

		await Task.CompletedTask;
	}

	[Test]
	public async Task A_position_that_is_not_a_nullable_directive_is_refused()
	{
		const string source = """
			#region Widgets

			namespace N;

			public sealed class Widget
			{
			}

			#endregion

			""";

		var result = Clean(source, At("IDE0240", source, "#region"));

		result.Changed.Should().BeFalse();
		result.Refusals.Should().ContainSingle().Which.Should().Contain("not the start of a #nullable directive");

		await Task.CompletedTask;
	}

	// ---- Idempotency -----------------------------------------------------------------------------

	[Test]
	public async Task Cleaning_the_output_again_changes_nothing()
	{
		const string source = """"
			#nullable enable

			namespace N;

			public struct Point
			{
				public int X { get; init; }

				public int Doubled() => X * 2;

				public string Show() => $"{X.ToString()}";
			}

			"""";

		var diagnostics = new[]
		{
			At("IDE0240", source, "#nullable enable"),
			At("IDE0250", source, "Point"),
			At("IDE0251", source, "Doubled"),
			At("IDE0071", source, ".ToString()"),
		};

		var once = Clean(source, diagnostics);
		once.Applied.Should().Be(4);

		Clean(once.Text!, diagnostics).Changed.Should().BeFalse("the positions no longer hold what the log described");
		await Task.CompletedTask;
	}
}
