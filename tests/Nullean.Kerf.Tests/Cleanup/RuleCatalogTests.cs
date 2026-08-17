using AwesomeAssertions;
using Nullean.Kerf.Cleanup;
using Nullean.Kerf.Options;

namespace Nullean.Kerf.Tests.Cleanup;

/// <summary>
/// The catalog's own invariants.
/// </summary>
/// <remarks>
/// These exist for the same reason <c>OptionsBindingTests</c> asserts the 39 IDE0055 keys: a
/// hand-maintained table falls behind silently, and the failure mode is Kerf reporting a rule it
/// cannot fix as if it could, or refusing one without saying why.
/// </remarks>
public class RuleCatalogTests
{
	/// <summary>
	/// The count in the SDK's <c>analysislevelstyle_all.globalconfig</c>, which is where the list came
	/// from. A new SDK adding rules should fail this and be looked at, not absorbed quietly.
	/// </summary>
	private const int RulesInTheSdk = 116;

	[Test]
	public async Task Every_rule_the_sdk_can_report_has_a_row()
	{
		RuleCatalog.All.Should().HaveCount(RulesInTheSdk);
		await Task.CompletedTask;
	}

	[Test]
	public async Task Ids_are_unique()
	{
		RuleCatalog.All.Select(entry => entry.Id).Should().OnlyHaveUniqueItems();
		await Task.CompletedTask;
	}

	[Test]
	public async Task Every_id_is_shaped_like_a_rule_id()
	{
		foreach (var entry in RuleCatalog.All)
		{
			entry.Id.Should().MatchRegex("^IDE[0-9]{4}$");
			entry.Title.Should().NotBeNullOrWhiteSpace();
		}

		await Task.CompletedTask;
	}

	[Test]
	public async Task A_permanent_refusal_carries_its_reason()
	{
		// The rule that keeps `Never` from reading as a backlog item, which is the same discipline the
		// not-implemented records in FormatOptions follow.
		foreach (var entry in RuleCatalog.All.Where(entry => entry.Owner == RuleOwner.Never))
			entry.Refusal.Should().NotBeNullOrWhiteSpace($"{entry.Id} is refused, so it has to say why");

		await Task.CompletedTask;
	}

	[Test]
	public async Task Only_a_refused_rule_carries_a_reason()
	{
		foreach (var entry in RuleCatalog.All.Where(entry => entry.Owner != RuleOwner.Never))
			entry.Refusal.Should().BeNull($"{entry.Id} is not refused");

		await Task.CompletedTask;
	}

	[Test]
	public async Task Every_cleanup_rule_declares_a_token_delta()
	{
		// A fix that changes tokens without declaring it is exactly what the verifiers exist to stop.
		RuleCatalog.CleanupKeys.Should().NotBeEmpty();

		foreach (var entry in RuleCatalog.All.Where(entry => entry.Owner == RuleOwner.Cleanup))
			entry.Delta.Should().NotBe(TokenDelta.None, $"{entry.Id} rewrites source, so it moves tokens");

		await Task.CompletedTask;
	}

	[Test]
	public async Task Only_a_cleanup_rule_declares_a_token_delta()
	{
		foreach (var entry in RuleCatalog.All.Where(entry => entry.Owner != RuleOwner.Cleanup))
			entry.Delta.Should().Be(TokenDelta.None, $"{entry.Id} is not a cleanup rewrite");

		await Task.CompletedTask;
	}

	[Test]
	public async Task Every_rule_claimed_as_cleanup_has_a_fixer_behind_it()
	{
		// The invariant that keeps the catalog honest. Without it a row can claim a rule is fixed while no
		// fixer implements it, and the symptom is a diagnostic silently skipped — which reads as a clean
		// run. Onboarding a rule means flipping its row and adding its fixer in the same change.
		RuleCatalog.CleanupKeys.Should().BeEquivalentTo(CSharpCleaner.ImplementedRuleIds);
		await Task.CompletedTask;
	}

	[Test]
	public async Task The_rules_cleanup_owns_are_what_they_say()
	{
		RuleCatalog.CleanupKeys.Should().BeEquivalentTo([
			"IDE0005", "IDE0007", "IDE0034", "IDE0040", "IDE0044",
			"IDE0071", "IDE0090", "IDE0240", "IDE0250", "IDE0251",
		]);

		await Task.CompletedTask;
	}

	[Test]
	public async Task Lookup_is_case_insensitive_and_misses_cleanly()
	{
		RuleCatalog.Find("ide0005").Should().NotBeNull("editorconfig keys are written in lower case");
		RuleCatalog.Find("IDE0005")!.Value.Owner.Should().Be(RuleOwner.Cleanup);
		RuleCatalog.Find("CA1822").Should().BeNull("the catalog is the IDE series, not every analyser");
		RuleCatalog.IsCleanupRule("IDE0051").Should().BeFalse();

		await Task.CompletedTask;
	}

	[Test]
	public async Task The_rules_kerf_already_formats_are_not_claimed_as_cleanup()
	{
		// IDE0055 and the syntax-only rules are the formatter's, and it fixes them before the compiler
		// looks. Claiming them here would mean cleanup trying to fix what is already fixed.
		foreach (var id in new[] { "IDE0055", "IDE0011", "IDE0036", "IDE0065", "IDE0161", "IDE2000" })
			RuleCatalog.Find(id)!.Value.Owner.Should().Be(RuleOwner.Formatting, id);

		await Task.CompletedTask;
	}
}
