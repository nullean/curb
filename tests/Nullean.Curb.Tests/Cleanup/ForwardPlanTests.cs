using AwesomeAssertions;
using Microsoft.CodeAnalysis.Text;
using Nullean.Curb.Cleanup;

namespace Nullean.Curb.Tests.Cleanup;

/// <summary>
/// What <c>--forward</c> hands to <c>dotnet format</c>, and what it deliberately does not.
/// </summary>
/// <remarks>
/// The plan is pure so this can be asserted without starting a process, which is the whole reason it is a
/// separate type. Two of these cases exist because the real thing was wrong first: absolute paths in
/// <c>--include</c> match nothing and exit zero, and the .NET analysers report at note level by default, so
/// without a gate forwarding quietly applied CA suggestions nobody had seen.
/// </remarks>
public class ForwardPlanTests
{
	private const string Root = "/repo";

	private static CleanupDiagnostic Diagnostic(
		string ruleId,
		string path = "/repo/src/Widget.cs",
		DiagnosticLevel level = DiagnosticLevel.Warning) =>
		new(ruleId, path, new LinePosition(0, 0), null, level);

	private static ForwardResult Plan(params CleanupDiagnostic[] diagnostics) =>
		ForwardPlan.For(diagnostics, Root);

	// ---- which subcommand ------------------------------------------------------------------------

	[Test]
	public async Task The_ide_series_goes_to_style()
	{
		var plan = Plan(Diagnostic("IDE0017"), Diagnostic("IDE0028"));

		plan.Invocations.Should().ContainSingle();
		plan.Invocations[0].Subcommand.Should().Be("style");
		plan.Invocations[0].RuleIds.Should().Equal("IDE0017", "IDE0028");

		await Task.CompletedTask;
	}

	[Test]
	public async Task A_third_party_analyser_goes_to_analyzers()
	{
		var plan = Plan(Diagnostic("CA1822"), Diagnostic("SA1200"));

		plan.Invocations.Should().ContainSingle();
		plan.Invocations[0].Subcommand.Should().Be("analyzers");
		plan.Invocations[0].RuleIds.Should().Equal("CA1822", "SA1200");

		await Task.CompletedTask;
	}

	[Test]
	public async Task Both_kinds_produce_one_invocation_each()
	{
		var plan = Plan(Diagnostic("IDE0017"), Diagnostic("CA1822"));

		plan.Invocations.Should().HaveCount(2);
		plan.Invocations.Select(i => i.Subcommand).Should().Equal("style", "analyzers");

		await Task.CompletedTask;
	}

	[Test]
	public async Task A_compiler_warning_is_not_forwarded()
	{
		// `dotnet format` cannot fix a compiler diagnostic, so passing one asks for a no-op that looks
		// like a failure.
		Plan(Diagnostic("CS0168")).Invocations.Should().BeEmpty();
		await Task.CompletedTask;
	}

	// ---- what is withheld ------------------------------------------------------------------------

	[Test]
	public async Task A_rule_curb_never_fixes_is_not_forwarded_either()
	{
		// Curb's position on these is not "we cannot" but "no tool should do this unattended", so routing
		// around it through another tool would be Curb ignoring its own judgement.
		var plan = Plan(Diagnostic("IDE1006"));

		plan.Invocations.Should().BeEmpty();
		plan.Withheld.Should().ContainSingle();
		plan.Withheld[0].Id.Should().Be("IDE1006");
		plan.Withheld[0].Reason.Should().NotBeNullOrWhiteSpace("a refusal has to say why");

		await Task.CompletedTask;
	}

	[Test]
	public async Task A_refusal_that_is_only_curbs_is_still_forwarded()
	{
		// IDE0051 deletes a declaration, which Curb will not do. `dotnet format style` will, and someone
		// who escalated the rule and asked to forward has said they want it — so Curb's own constraint is
		// not imposed on another tool.
		var plan = Plan(Diagnostic("IDE0051"));

		plan.Withheld.Should().BeEmpty();
		plan.Invocations.Should().ContainSingle();
		plan.Invocations[0].RuleIds.Should().Equal("IDE0051");

		await Task.CompletedTask;
	}

	[Test]
	public async Task A_withheld_rule_is_named_once_however_many_sites_it_has()
	{
		var plan = Plan(
			Diagnostic("IDE1006", "/repo/src/A.cs"),
			Diagnostic("IDE1006", "/repo/src/B.cs"),
			Diagnostic("IDE1006", "/repo/src/C.cs"));

		plan.Withheld.Should().ContainSingle();
		await Task.CompletedTask;
	}

	[Test]
	public async Task A_diagnostic_below_warning_is_not_forwarded()
	{
		// The .NET analysers are on by default at this level, and it is invisible at normal build
		// verbosity. Forwarding would fix every occurrence of the rule in the file on the strength of
		// something nobody saw.
		var plan = Plan(Diagnostic("CA1822", level: DiagnosticLevel.Note));

		plan.Invocations.Should().BeEmpty();
		plan.Quiet.Should().Be(1, "and it is counted rather than passed over in silence");

		await Task.CompletedTask;
	}

	[Test]
	public async Task An_error_is_forwarded()
	{
		Plan(Diagnostic("IDE0017", level: DiagnosticLevel.Error)).Invocations.Should().ContainSingle();
		await Task.CompletedTask;
	}

	[Test]
	public async Task An_unknown_level_is_forwarded()
	{
		// What MSBuild's console output leaves us with, since the severity word is localised. A saved log
		// someone is looking at is one they saw.
		Plan(Diagnostic("IDE0017", level: DiagnosticLevel.Unknown)).Invocations.Should().ContainSingle();
		await Task.CompletedTask;
	}

	// ---- paths -----------------------------------------------------------------------------------

	[Test]
	public async Task Files_are_relative_to_the_working_directory()
	{
		// Measured: an absolute path in --include matches nothing and dotnet format exits zero having done
		// nothing — a silent no-op, which looks exactly like success.
		var plan = Plan(Diagnostic("IDE0017", "/repo/src/Widget.cs"));

		plan.Invocations[0].Files.Should().Equal(System.IO.Path.Combine("src", "Widget.cs"));
		plan.Invocations[0].Arguments.Should().NotContain("/repo/src/Widget.cs");

		await Task.CompletedTask;
	}

	[Test]
	public async Task No_argument_is_an_absolute_path()
	{
		var plan = Plan(Diagnostic("IDE0017", "/repo/a/b/Widget.cs"), Diagnostic("IDE0028", "/repo/Other.cs"));

		foreach (var argument in plan.Invocations[0].Arguments)
			System.IO.Path.IsPathRooted(argument).Should().BeFalse($"'{argument}' would match nothing");

		await Task.CompletedTask;
	}

	[Test]
	public async Task A_file_outside_the_working_directory_widens_the_invocation()
	{
		// It cannot be named relatively, and a partial --include would silently skip it while looking like
		// a complete run. Widening is the honest direction.
		var plan = Plan(Diagnostic("IDE0017", "/elsewhere/Widget.cs"));

		plan.Invocations.Should().ContainSingle();
		plan.Invocations[0].Files.Should().BeEmpty();
		plan.Invocations[0].Arguments.Should().NotContain("--include");

		await Task.CompletedTask;
	}

	[Test]
	public async Task Files_and_rules_are_deduplicated_and_sorted()
	{
		var plan = Plan(
			Diagnostic("IDE0028", "/repo/b.cs"),
			Diagnostic("IDE0017", "/repo/a.cs"),
			Diagnostic("IDE0017", "/repo/a.cs"),
			Diagnostic("IDE0017", "/repo/b.cs"));

		plan.Invocations[0].RuleIds.Should().Equal("IDE0017", "IDE0028");
		plan.Invocations[0].Files.Should().Equal("a.cs", "b.cs");

		await Task.CompletedTask;
	}

	// ---- the invocation itself -------------------------------------------------------------------

	[Test]
	public async Task The_arguments_carry_the_flags_that_make_it_cheap_and_narrow()
	{
		var arguments = Plan(Diagnostic("IDE0017")).Invocations[0].Arguments;

		arguments.Should().StartWith(["format", "style", "--diagnostics", "IDE0017"]);
		arguments.Should().ContainInOrder("--severity", "info");
		arguments.Should().Contain("--no-restore", "the build that produced the log already restored");
		arguments.Should().NotContain("--no-build", "dotnet format has no such flag; it loads a workspace");

		await Task.CompletedTask;
	}

	[Test]
	public async Task Nothing_unfixed_means_nothing_to_run()
	{
		var plan = ForwardPlan.For([], Root);

		plan.Invocations.Should().BeEmpty();
		plan.Withheld.Should().BeEmpty();
		plan.Quiet.Should().Be(0);

		await Task.CompletedTask;
	}

	[Test]
	public async Task The_command_line_is_printable()
	{
		var plan = Plan(Diagnostic("IDE0017", "/repo/my code/Widget.cs"));

		plan.Invocations[0].CommandLine.Should().StartWith("dotnet format style --diagnostics IDE0017");
		plan.Invocations[0].CommandLine.Should().Contain("\"my code", "a path with a space has to survive being read back");

		await Task.CompletedTask;
	}
}
