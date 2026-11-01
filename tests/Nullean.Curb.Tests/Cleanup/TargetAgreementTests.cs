using AwesomeAssertions;
using Microsoft.CodeAnalysis.Text;
using Nullean.Curb.Cleanup;

namespace Nullean.Curb.Tests.Cleanup;

/// <summary>
/// A site is applied only when every target framework that compiled its file reported it (#189), and once
/// however many reported it (#178).
/// </summary>
public class TargetAgreementTests
{
	private const string Widget = "/repo/Widget.cs";

	private static readonly Compilation Net10 = Compilation.ForLog("/repo/obj/Debug/net10.0/curb.sarif");
	private static readonly Compilation NetStandard = Compilation.ForLog("/repo/obj/Debug/netstandard2.0/curb.sarif");

	private static CleanupDiagnostic Using(string file = Widget, int endLine = 1, DiagnosticLevel level = DiagnosticLevel.Warning) =>
		new("IDE0005", file, new LinePosition(0, 0), new LinePosition(endLine, 20), level);

	private static CleanupDiagnostic Field(string file = Widget) =>
		new("IDE0044", file, new LinePosition(7, 16), new LinePosition(7, 21), DiagnosticLevel.Warning);

	[Test]
	public async Task A_site_both_frameworks_reported_is_applied_once()
	{
		var agreement = new TargetAgreement();
		agreement.Report(Net10, Using(level: DiagnosticLevel.Warning));
		agreement.Report(NetStandard, Using(level: DiagnosticLevel.Note));
		agreement.Report(NetStandard, Using() with { RuleId = "ide0005" });

		var verdict = agreement.Resolve();

		verdict.Agreed[Widget].Should().ContainSingle("a severity or casing difference is still the same site");
		verdict.Declined.Should().BeEmpty();

		await Task.CompletedTask;
	}

	[Test]
	public async Task A_site_one_framework_reported_is_declined_and_says_which()
	{
		var agreement = new TargetAgreement();
		agreement.Report(Net10, Using());
		agreement.Report(Net10, Field());
		agreement.Report(NetStandard, Field());

		var verdict = agreement.Resolve();

		verdict.Agreed[Widget].Should().ContainSingle().Which.RuleId.Should().Be("IDE0044");
		var declined = verdict.Declined.Should().ContainSingle().Subject;
		declined.Diagnostic.RuleId.Should().Be("IDE0005");
		declined.Reason.Should().Be(
			"IDE0005: reported for net10.0 but not for netstandard2.0, which compiles this file too, "
			+ "so fixing it could break that build");

		await Task.CompletedTask;
	}

	[Test]
	public async Task A_framework_silent_about_the_file_is_assumed_to_have_compiled_it()
	{
		// netstandard2.0 says nothing about Widget.cs. It may have compiled it and found nothing, which is the
		// using-needed-on-one-framework case, so without a compile list its silence is disagreement.
		var agreement = new TargetAgreement();
		agreement.Report(Net10, Using());
		agreement.Report(Net10, Field("/repo/Other.cs"));
		agreement.Report(NetStandard, Field("/repo/Other.cs"));

		var verdict = agreement.Resolve();

		verdict.Agreed.Should().ContainKey("/repo/Other.cs").And.NotContainKey(Widget);
		verdict.Declined.Should().ContainSingle().Which.Diagnostic.FilePath.Should().Be(Widget);

		await Task.CompletedTask;
	}

	[Test]
	public async Task A_file_only_one_framework_compiles_keeps_its_fixes()
	{
		// <Compile Remove="Modern.cs" Condition="'$(TargetFramework)' == 'netstandard2.0'" />: the compile lists
		// say netstandard2.0 never saw the file, so net10.0's verdict is the only one there is.
		var agreement = new TargetAgreement();
		agreement.Compiled(Net10, [Widget, "/repo/Modern.cs"]);
		agreement.Compiled(NetStandard, [Widget]);
		agreement.Report(Net10, Field("/repo/Modern.cs"));

		var verdict = agreement.Resolve();

		verdict.Agreed["/repo/Modern.cs"].Should().ContainSingle();
		verdict.Declined.Should().BeEmpty();

		await Task.CompletedTask;
	}

	[Test]
	public async Task A_compile_list_naming_the_file_makes_silence_disagreement()
	{
		var agreement = new TargetAgreement();
		agreement.Compiled(Net10, [Widget]);
		agreement.Compiled(NetStandard, [Widget]);
		agreement.Report(Net10, Using());

		agreement.Resolve().Declined.Should().ContainSingle();
		await Task.CompletedTask;
	}

	[Test]
	public async Task A_compile_list_that_misses_a_file_its_own_log_reported_is_not_believed()
	{
		// netstandard2.0's list spells paths differently from its log (or is stale), so it cannot be trusted to
		// say Widget.cs was not compiled there either.
		var agreement = new TargetAgreement();
		agreement.Compiled(Net10, [Widget]);
		agreement.Compiled(NetStandard, ["/REPO/OTHER.CS"]);
		agreement.Report(Net10, Using());
		agreement.Report(Net10, Field("/repo/Other.cs"));
		agreement.Report(NetStandard, Field("/repo/Other.cs"));

		var verdict = agreement.Resolve();

		verdict.Agreed["/repo/Other.cs"].Should().ContainSingle();
		verdict.Declined.Should().ContainSingle().Which.Diagnostic.FilePath.Should().Be(Widget);

		await Task.CompletedTask;
	}

	[Test]
	public async Task A_single_target_project_is_unchanged()
	{
		var agreement = new TargetAgreement();
		agreement.Report(Net10, Using());
		agreement.Report(Net10, Field());

		var verdict = agreement.Resolve();

		verdict.Agreed[Widget].Should().HaveCount(2);
		verdict.Declined.Should().BeEmpty();

		await Task.CompletedTask;
	}

	[Test]
	public async Task Two_projects_are_never_one_projects_frameworks()
	{
		var core = Compilation.ForLog("/repo/src/Core/obj/Debug/net10.0/curb.sarif");
		var cli = Compilation.ForLog("/repo/src/Cli/obj/Debug/net10.0/curb.sarif");
		var custom = Compilation.ForLog("/repo/logs/build.sarif");
		var agreement = new TargetAgreement();
		agreement.Declare(cli);
		agreement.Declare(custom);
		agreement.Report(core, Using());

		agreement.Resolve().Declined.Should().BeEmpty();
		await Task.CompletedTask;
	}

	[Test]
	public async Task Two_frameworks_disagreeing_about_a_runs_length_decline_both()
	{
		// IDE0005 reports a maximal run of directives; net10.0 found two unnecessary, netstandard2.0 one. Neither
		// span is a site both agreed on.
		var agreement = new TargetAgreement();
		agreement.Report(Net10, Using(endLine: 1));
		agreement.Report(NetStandard, Using(endLine: 0));

		var verdict = agreement.Resolve();

		verdict.Agreed.Should().BeEmpty();
		verdict.Declined.Should().HaveCount(2);

		await Task.CompletedTask;
	}

	[Test]
	public async Task A_console_log_compares_frameworks_by_its_suffix()
	{
		const string log = "/repo/build.log";
		var net10 = Compilation.ForConsole(log, "/repo/e1.csproj::TargetFramework=net10.0");
		var netStandard = Compilation.ForConsole(log, "/repo/e1.csproj::TargetFramework=netstandard2.0");
		var other = Compilation.ForConsole(log, "/repo/e2.csproj::TargetFramework=net10.0");

		var agreement = new TargetAgreement();
		agreement.Declare(netStandard);
		agreement.Declare(other);
		agreement.Report(net10, Field());

		var declined = agreement.Resolve().Declined.Should().ContainSingle().Subject;
		declined.Reason.Should().Contain("reported for net10.0 but not for netstandard2.0,", "e2 is another project");

		await Task.CompletedTask;
	}

	[Test]
	[Arguments("/repo/obj/Debug/net10.0/curb.sarif", "/repo/obj/Debug/*/curb.sarif", "net10.0")]
	[Arguments("/repo/obj/Release/netstandard2.0/curb.sarif", "/repo/obj/Release/*/curb.sarif", "netstandard2.0")]
	[Arguments("/repo/artifacts/obj/Proj/debug_net8.0-windows/curb.sarif", "/repo/artifacts/obj/Proj/debug_*/curb.sarif", "net8.0-windows")]
	[Arguments("/repo/obj/Debug/net48/linux-x64/curb.sarif", "/repo/obj/Debug/*/linux-x64/curb.sarif", "net48")]
	[Arguments("C:\\repo\\obj\\Debug\\netcoreapp3.1\\curb.sarif", "C:/repo/obj/Debug/*/curb.sarif", "netcoreapp3.1")]
	[Arguments("/repo/logs/build.sarif", "/repo/logs/build.sarif", "/repo/logs/build.sarif")]
	[Arguments("/repo/network/curb.sarif", "/repo/network/curb.sarif", "/repo/network/curb.sarif")]
	public async Task A_logs_framework_is_read_from_its_path(string log, string group, string label)
	{
		var compilation = Compilation.ForLog(log);

		compilation.Group.Should().Be(group);
		compilation.Label.Should().Be(label);

		await Task.CompletedTask;
	}
}
