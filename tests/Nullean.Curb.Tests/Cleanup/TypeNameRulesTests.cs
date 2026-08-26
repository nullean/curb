using AwesomeAssertions;
using Microsoft.CodeAnalysis.Text;
using Nullean.Curb.Cleanup;

namespace Nullean.Curb.Tests.Cleanup;

/// <summary>
/// IDE0090 (<c>new()</c>) and IDE0007 (<c>var</c>), the two rules that take a type name out.
/// </summary>
/// <remarks>
/// IDE0090 is reported at the <c>new</c> keyword and IDE0007 at the type — both measured. They are the
/// two ends of the risk order in the slice: a wrong <c>new()</c> does not compile, and a wrong <c>var</c>
/// does. The refusal cases for <c>var</c> are therefore the most load-bearing tests in this file.
/// </remarks>
public class TypeNameRulesTests
{
	private const string Path = "/repo/Widget.cs";

	private static CleanupResult Clean(string source, params CleanupDiagnostic[] diagnostics)
	{
		var result = new CSharpCleaner().Clean(source, diagnostics);
		CleanupExpectationDump.Record(result, diagnostics.Select(d => d.RuleId));
		return result;
	}

	private static CleanupDiagnostic At(string ruleId, string source, string at)
	{
		var text = SourceText.From(source);
		var offset = source.IndexOf(at, StringComparison.Ordinal);
		offset.Should().BeGreaterThanOrEqualTo(0, $"the fixture has to contain '{at}'");

		var line = text.Lines.GetLineFromPosition(offset);
		return new CleanupDiagnostic(ruleId, Path, new LinePosition(line.LineNumber, offset - line.Start));
	}

	// ---- IDE0090, new() ---------------------------------------------------------------------------

	[Test]
	public async Task The_type_name_is_dropped_from_a_creation()
	{
		const string source = """
			namespace N;

			public sealed class Widget
			{
				private static Widget Create() => new Widget();
			}

			""";

		var result = Clean(source, At("IDE0090", source, "new Widget()"));

		result.Status.Should().Be(CleanupStatus.Cleaned);
		result.Applied.Should().Be(1);
		result.Text.Should().Contain("=> new();");

		await Task.CompletedTask;
	}

	[Test]
	public async Task A_generic_type_name_is_dropped_whole()
	{
		const string source = """
			using System.Collections.Generic;

			namespace N;

			public sealed class Widget
			{
				private static Dictionary<string, int> Make() => new Dictionary<string, int>();
			}

			""";

		var result = Clean(source, At("IDE0090", source, "new Dictionary"));

		result.Applied.Should().Be(1);
		result.Text.Should().Contain("=> new();");

		await Task.CompletedTask;
	}

	[Test]
	public async Task Arguments_are_kept()
	{
		const string source = """
			using System.Text.RegularExpressions;

			namespace N;

			public sealed class Widget
			{
				public Regex Pattern { get; } = new Regex("[a-z]+");
			}

			""";

		var result = Clean(source, At("IDE0090", source, "new Regex"));

		result.Applied.Should().Be(1);
		result.Text.Should().Contain("""= new("[a-z]+");""");

		await Task.CompletedTask;
	}

	[Test]
	public async Task A_creation_without_an_argument_list_is_refused()
	{
		// `new Widget { X = 1 }` has no parentheses, so dropping the type would leave `new { X = 1 }` —
		// an anonymous object. It compiles, and it is a different program. The one quiet mistake this
		// rule could make.
		const string source = """
			namespace N;

			public sealed class Widget
			{
				public int X { get; set; }

				private static Widget Create() => new Widget { X = 1 };
			}

			""";

		var result = Clean(source, At("IDE0090", source, "new Widget {"));

		result.Changed.Should().BeFalse();
		result.Refusals.Should().ContainSingle().Which.Should().Contain("anonymous object");

		await Task.CompletedTask;
	}

	[Test]
	public async Task A_position_that_is_not_a_new_keyword_is_refused()
	{
		const string source = """
			namespace N;

			public sealed class Widget
			{
				private static Widget Create() => new Widget();
			}

			""";

		var result = Clean(source, At("IDE0090", source, "Widget Create"));

		result.Changed.Should().BeFalse();
		result.Refusals.Should().ContainSingle().Which.Should().Contain("not a `new` keyword");

		await Task.CompletedTask;
	}

	// ---- IDE0007, var -----------------------------------------------------------------------------

	[Test]
	public async Task An_explicit_local_type_becomes_var()
	{
		const string source = """
			namespace N;

			public sealed class Widget
			{
				public string Describe()
				{
					string text = "hello";
					return text;
				}
			}

			""";

		var result = Clean(source, At("IDE0007", source, "string text"));

		result.Status.Should().Be(CleanupStatus.Cleaned);
		result.Applied.Should().Be(1);
		result.Text.Should().Contain("var text = \"hello\";");

		await Task.CompletedTask;
	}

	[Test]
	public async Task A_generic_local_type_becomes_var()
	{
		const string source = """
			using System.Collections.Generic;

			namespace N;

			public sealed class Widget
			{
				public int Count()
				{
					List<string> items = [];
					return items.Count;
				}
			}

			""";

		var result = Clean(source, At("IDE0007", source, "List<string> items"));

		result.Applied.Should().Be(1);
		result.Text.Should().Contain("var items = [];");

		await Task.CompletedTask;
	}

	[Test]
	public async Task A_foreach_variable_becomes_var()
	{
		const string source = """
			using System.Collections.Generic;

			namespace N;

			public sealed class Widget
			{
				public int Total(List<int> items)
				{
					var sum = 0;
					foreach (int item in items)
						sum += item;

					return sum;
				}
			}

			""";

		var result = Clean(source, At("IDE0007", source, "int item in"));

		result.Applied.Should().Be(1);
		result.Text.Should().Contain("foreach (var item in items)");

		await Task.CompletedTask;
	}

	[Test]
	public async Task A_declaration_of_several_variables_is_refused()
	{
		// `int a = 1, b = 2;` cannot be written with var at all.
		const string source = """
			namespace N;

			public sealed class Widget
			{
				public int Sum()
				{
					int a = 1, b = 2;
					return a + b;
				}
			}

			""";

		var result = Clean(source, At("IDE0007", source, "int a = 1"));

		result.Changed.Should().BeFalse();
		result.Refusals.Should().ContainSingle().Which.Should().Contain("more than one variable");

		await Task.CompletedTask;
	}

	[Test]
	public async Task A_declaration_without_an_initialiser_is_refused()
	{
		const string source = """
			namespace N;

			public sealed class Widget
			{
				public string Describe()
				{
					string text;
					text = "hello";
					return text;
				}
			}

			""";

		var result = Clean(source, At("IDE0007", source, "string text;"));

		result.Changed.Should().BeFalse();
		result.Refusals.Should().ContainSingle().Which.Should().Contain("no initialiser");

		await Task.CompletedTask;
	}

	[Test]
	public async Task A_const_declaration_is_refused()
	{
		const string source = """
			namespace N;

			public sealed class Widget
			{
				public string Describe()
				{
					const string text = "hello";
					return text;
				}
			}

			""";

		var result = Clean(source, At("IDE0007", source, "string text"));

		result.Changed.Should().BeFalse();
		result.Refusals.Should().ContainSingle().Which.Should().Contain("const");

		await Task.CompletedTask;
	}

	[Test]
	public async Task A_field_type_is_refused()
	{
		// IDE0007 is about locals. A field cannot be var, and a stale position landing on one must not
		// produce source that does not compile.
		const string source = """
			namespace N;

			public sealed class Widget
			{
				private string _name = "w";
			}

			""";

		var result = Clean(source, At("IDE0007", source, "string _name"));

		result.Changed.Should().BeFalse();
		result.Refusals.Should().ContainSingle().Which.Should().Contain("not a local declaration");

		await Task.CompletedTask;
	}

	[Test]
	public async Task A_declaration_that_is_already_var_is_refused()
	{
		const string source = """
			namespace N;

			public sealed class Widget
			{
				public string Describe()
				{
					var text = "hello";
					return text;
				}
			}

			""";

		var result = Clean(source, At("IDE0007", source, "var text"));

		result.Changed.Should().BeFalse();
		result.Refusals.Should().ContainSingle().Which.Should().Contain("already uses var");

		await Task.CompletedTask;
	}

	[Test]
	public async Task A_nullable_local_type_becomes_var()
	{
		// `string?` is a NullableTypeSyntax wrapping a PredefinedTypeSyntax. Stopping at the inner one made
		// the type's parent the wrapper rather than the declaration, and every nullable local was refused.
		const string source = """
			namespace N;

			public sealed class Widget
			{
				public string Describe(string? given)
				{
					string? text = given;
					return text ?? "";
				}
			}

			""";

		var result = Clean(source, At("IDE0007", source, "string? text"));

		result.Applied.Should().Be(1);
		result.Text.Should().Contain("var text = given;");

		await Task.CompletedTask;
	}

	[Test]
	public async Task An_initialiser_that_infers_nothing_is_refused()
	{
		// `var x = default;` and `var x = null;` do not compile. A syntactic invariant of var rather than a
		// judgement about the verdict, and owning the rewrite means owning it.
		const string source = """
			namespace N;

			public sealed class Widget
			{
				public bool Flag()
				{
					bool flag = default(bool);
					string? name = null;
					return flag && name is null;
				}
			}

			""";

		var result = Clean(source, At("IDE0007", source, "bool flag"), At("IDE0007", source, "string? name"));

		result.Changed.Should().BeFalse();
		result.Refusals.Should().HaveCount(2);
		result.Refusals.Should().AllSatisfy(r => r.Should().Contain("infers no type"));

		await Task.CompletedTask;
	}

	[Test]
	public async Task Var_and_a_simplified_default_cannot_both_apply()
	{
		// The interaction a real build caught, as CS8716. Each fix is valid alone; together they produce
		// `var flag = default`, which has no target type. IDE0007 stands down so the pair cannot arise.
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

		var result = Clean(source, At("IDE0007", source, "bool flag"), At("IDE0034", source, "default(bool)"));

		result.Applied.Should().Be(1, "only the default is simplified");
		result.Text.Should().Contain("bool flag = default;").And.NotContain("var flag");

		await Task.CompletedTask;
	}

	// ---- Idempotency ------------------------------------------------------------------------------

	[Test]
	public async Task Cleaning_the_output_again_changes_nothing()
	{
		const string source = """
			namespace N;

			public sealed class Widget
			{
				public string Describe()
				{
					string text = "hello";
					return text;
				}

				private static Widget Create() => new Widget();
			}

			""";

		var diagnostics = new[] { At("IDE0007", source, "string text"), At("IDE0090", source, "new Widget()") };

		var once = Clean(source, diagnostics);
		once.Applied.Should().Be(2);

		var twice = Clean(once.Text!, diagnostics);
		twice.Changed.Should().BeFalse("the positions no longer hold what the log described");

		await Task.CompletedTask;
	}
}
