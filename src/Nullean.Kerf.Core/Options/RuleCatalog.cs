using System.Collections.Frozen;

namespace Nullean.Kerf.Options;

/// <summary>Who, if anyone, fixes a code style rule.</summary>
public enum RuleOwner
{
	/// <summary>
	/// <c>dotnet format style</c>'s territory. Kerf reports these and points there; it does not
	/// attempt them.
	/// </summary>
	DotnetFormatStyle,

	/// <summary>Kerf's formatter already satisfies it from syntax alone, in the same pass as IDE0055.</summary>
	Formatting,

	/// <summary><c>kerf cleanup</c> fixes it, from a diagnostic a build already reported.</summary>
	Cleanup,

	/// <summary>Kerf will never fix it. <see cref="RuleEntry.Refusal"/> carries the reason.</summary>
	Never,
}

/// <summary>How a fix changes the token stream, so the verifier can be told what to expect.</summary>
[Flags]
public enum TokenDelta
{
	/// <summary>The token stream is unchanged.</summary>
	None = 0,

	/// <summary>Tokens are removed, at declared spans.</summary>
	Dropped = 1,

	/// <summary>Tokens are added, each declared by its exact text.</summary>
	Inserted = 2,

	// There is deliberately no `Replaced`. Swapping a type name for `var` is
	// `Dropped | Inserted` — the type's tokens go, the keyword arrives — and that is also exactly what the
	// verifiers are told, so a third value would be a second way to say the same thing that they would
	// then have to be taught to translate.
}

/// <summary>One code style rule, and Kerf's position on it.</summary>
/// <param name="Id">The rule id, for example <c>IDE0005</c>.</param>
/// <param name="Title">Roslyn's own title, taken from the SDK's severity configuration.</param>
/// <param name="Owner">Who fixes it.</param>
/// <param name="Delta">What a fix does to the token stream.</param>
/// <param name="Refusal">Why Kerf will not fix it, when <paramref name="Owner"/> is <see cref="RuleOwner.Never"/>.</param>
public readonly record struct RuleEntry(
	string Id,
	string Title,
	RuleOwner Owner,
	TokenDelta Delta,
	string? Refusal = null);

/// <summary>
/// Every code style rule the .NET SDK can report, and whether Kerf fixes it.
/// </summary>
/// <remarks>
/// <para>
/// The list is the 116 <c>IDE</c> rules named in the SDK's
/// <c>codestyle/cs/build/config/analysislevelstyle_all.globalconfig</c>, which is the authoritative
/// set and ships on every machine that has the SDK. Titles are Roslyn's own rather than reworded, so
/// a message Kerf prints matches the one the build printed.
/// </para>
/// <para>
/// The point of naming all 116 rather than only the ones Kerf touches is the same honesty
/// <see cref="OptionCatalog"/> exists for: a user looking at a diagnostic wants to know whether Kerf
/// will deal with it, will never deal with it, or has simply not reached it — and those are three
/// different answers. <see cref="RuleOwner.Never"/> carries a reason, so a permanent refusal does not
/// read as a backlog item.
/// </para>
/// <para>
/// Which syntax a rule applies to is deliberately not recorded here. That is the fixer's node-kind
/// gate, it is often more than one kind, and putting it in a data table would mean maintaining it in
/// two places.
/// </para>
/// <para>
/// A row says <see cref="RuleOwner.Cleanup"/> only once a fixer for it exists, never because one is
/// planned. <c>RuleCatalogTests</c> holds the two together, so a rule cannot claim to be fixed
/// by a fixer nobody wrote — which would show up as a diagnostic quietly skipped rather than as a
/// failure.
/// </para>
/// <para>
/// Hand-maintained, like <see cref="OptionCatalog"/>, and for the same reason.
/// </para>
/// </remarks>
public static class RuleCatalog
{
	/// <summary>Every rule, ordered by id.</summary>
	public static readonly IReadOnlyList<RuleEntry> All =
	[
		new("IDE0004", "Remove Unnecessary Cast",                                          RuleOwner.DotnetFormatStyle, TokenDelta.None),
		new("IDE0005", "Using directive is unnecessary.",                                  RuleOwner.Cleanup, TokenDelta.Dropped),
		new("IDE0007", "Use implicit type",                                                RuleOwner.Cleanup, TokenDelta.Dropped | TokenDelta.Inserted),
		new("IDE0008", "Use explicit type",                                                RuleOwner.Never, TokenDelta.None, "The diagnostic does not carry the type name, so the fix is not derivable from the span."),
		new("IDE0010", "Add missing cases",                                                RuleOwner.DotnetFormatStyle, TokenDelta.None),
		new("IDE0011", "Add braces",                                                       RuleOwner.Formatting, TokenDelta.None),
		new("IDE0016", "Use 'throw' expression",                                           RuleOwner.DotnetFormatStyle, TokenDelta.None),
		new("IDE0017", "Simplify object initialization",                                   RuleOwner.DotnetFormatStyle, TokenDelta.None),
		new("IDE0018", "Inline variable declaration",                                      RuleOwner.DotnetFormatStyle, TokenDelta.None),
		new("IDE0019", "Use pattern matching",                                             RuleOwner.DotnetFormatStyle, TokenDelta.None),
		new("IDE0020", "Use pattern matching",                                             RuleOwner.DotnetFormatStyle, TokenDelta.None),
		new("IDE0021", "Use expression body for constructor",                              RuleOwner.Formatting, TokenDelta.None),
		new("IDE0022", "Use expression body for method",                                   RuleOwner.Formatting, TokenDelta.None),
		new("IDE0023", "Use expression body for conversion operator",                      RuleOwner.Formatting, TokenDelta.None),
		new("IDE0024", "Use expression body for operator",                                 RuleOwner.Formatting, TokenDelta.None),
		new("IDE0025", "Use expression body for property",                                 RuleOwner.Formatting, TokenDelta.None),
		new("IDE0026", "Use expression body for indexer",                                  RuleOwner.Formatting, TokenDelta.None),
		new("IDE0027", "Use expression body for accessor",                                 RuleOwner.Formatting, TokenDelta.None),
		new("IDE0028", "Simplify collection initialization",                               RuleOwner.DotnetFormatStyle, TokenDelta.None),
		new("IDE0029", "Use coalesce expression",                                          RuleOwner.DotnetFormatStyle, TokenDelta.None),
		new("IDE0030", "Use coalesce expression",                                          RuleOwner.DotnetFormatStyle, TokenDelta.None),
		new("IDE0031", "Use null propagation",                                             RuleOwner.DotnetFormatStyle, TokenDelta.None),
		new("IDE0032", "Use auto property",                                                RuleOwner.DotnetFormatStyle, TokenDelta.None),
		new("IDE0033", "Use explicitly provided tuple name",                               RuleOwner.DotnetFormatStyle, TokenDelta.None),
		new("IDE0034", "Simplify 'default' expression",                                    RuleOwner.DotnetFormatStyle, TokenDelta.None),
		new("IDE0036", "Order modifiers",                                                  RuleOwner.Formatting, TokenDelta.None),
		new("IDE0037", "Use inferred member name",                                         RuleOwner.DotnetFormatStyle, TokenDelta.None),
		new("IDE0039", "Use local function",                                               RuleOwner.DotnetFormatStyle, TokenDelta.None),
		new("IDE0040", "Add accessibility modifiers",                                      RuleOwner.Cleanup, TokenDelta.Inserted),
		new("IDE0041", "Use 'is null' check",                                              RuleOwner.DotnetFormatStyle, TokenDelta.None),
		new("IDE0042", "Deconstruct variable declaration",                                 RuleOwner.DotnetFormatStyle, TokenDelta.None),
		new("IDE0043", "Invalid format string",                                            RuleOwner.DotnetFormatStyle, TokenDelta.None),
		new("IDE0044", "Add readonly modifier",                                            RuleOwner.Cleanup, TokenDelta.Inserted),
		new("IDE0045", "Convert to conditional expression",                                RuleOwner.DotnetFormatStyle, TokenDelta.None),
		new("IDE0046", "Convert to conditional expression",                                RuleOwner.DotnetFormatStyle, TokenDelta.None),
		new("IDE0047", "Remove unnecessary parentheses",                                   RuleOwner.DotnetFormatStyle, TokenDelta.None),
		new("IDE0048", "Add parentheses for clarity",                                      RuleOwner.DotnetFormatStyle, TokenDelta.None),
		new("IDE0051", "Remove unused private members",                                    RuleOwner.Never, TokenDelta.None, "The fix deletes a declaration."),
		new("IDE0052", "Remove unread private members",                                    RuleOwner.Never, TokenDelta.None, "The fix deletes a declaration."),
		new("IDE0053", "Use block body for lambda expression",                             RuleOwner.DotnetFormatStyle, TokenDelta.None),
		new("IDE0054", "Use compound assignment",                                          RuleOwner.DotnetFormatStyle, TokenDelta.None),
		new("IDE0055", "Fix formatting",                                                   RuleOwner.Formatting, TokenDelta.None),
		new("IDE0056", "Use index operator",                                               RuleOwner.DotnetFormatStyle, TokenDelta.None),
		new("IDE0057", "Use range operator",                                               RuleOwner.DotnetFormatStyle, TokenDelta.None),
		new("IDE0058", "Expression value is never used",                                   RuleOwner.DotnetFormatStyle, TokenDelta.None),
		new("IDE0059", "Unnecessary assignment of a value",                                RuleOwner.Never, TokenDelta.None, "The fix deletes an assignment."),
		new("IDE0060", "Remove unused parameter",                                          RuleOwner.Never, TokenDelta.None, "The fix changes a signature."),
		new("IDE0061", "Use expression body for local function",                           RuleOwner.Formatting, TokenDelta.None),
		new("IDE0062", "Make local function 'static'",                                     RuleOwner.DotnetFormatStyle, TokenDelta.None),
		new("IDE0063", "Use simple 'using' statement",                                     RuleOwner.DotnetFormatStyle, TokenDelta.None),
		new("IDE0064", "Make readonly fields writable",                                    RuleOwner.DotnetFormatStyle, TokenDelta.None),
		new("IDE0065", "Misplaced using directive",                                        RuleOwner.Formatting, TokenDelta.None),
		new("IDE0066", "Convert switch statement to expression",                           RuleOwner.DotnetFormatStyle, TokenDelta.None),
		new("IDE0070", "Use 'System.HashCode'",                                            RuleOwner.DotnetFormatStyle, TokenDelta.None),
		new("IDE0071", "Simplify interpolation",                                           RuleOwner.DotnetFormatStyle, TokenDelta.None),
		new("IDE0072", "Add missing cases",                                                RuleOwner.DotnetFormatStyle, TokenDelta.None),
		new("IDE0073", "The file header is missing or not located at the top of the file", RuleOwner.Formatting, TokenDelta.None),
		new("IDE0074", "Use compound assignment",                                          RuleOwner.DotnetFormatStyle, TokenDelta.None),
		new("IDE0075", "Simplify conditional expression",                                  RuleOwner.DotnetFormatStyle, TokenDelta.None),
		new("IDE0076", "Invalid global 'SuppressMessageAttribute'",                        RuleOwner.DotnetFormatStyle, TokenDelta.None),
		new("IDE0077", "Avoid legacy format target in 'SuppressMessageAttribute'",         RuleOwner.DotnetFormatStyle, TokenDelta.None),
		new("IDE0078", "Use pattern matching",                                             RuleOwner.DotnetFormatStyle, TokenDelta.None),
		new("IDE0080", "Remove unnecessary suppression operator",                          RuleOwner.DotnetFormatStyle, TokenDelta.None),
		new("IDE0082", "'typeof' can be converted to 'nameof'",                            RuleOwner.DotnetFormatStyle, TokenDelta.None),
		new("IDE0083", "Use pattern matching",                                             RuleOwner.DotnetFormatStyle, TokenDelta.None),
		new("IDE0090", "Use 'new(...)'",                                                   RuleOwner.Cleanup, TokenDelta.Dropped),
		new("IDE0100", "Remove redundant equality",                                        RuleOwner.DotnetFormatStyle, TokenDelta.None),
		new("IDE0110", "Remove unnecessary discard",                                       RuleOwner.DotnetFormatStyle, TokenDelta.None),
		new("IDE0120", "Simplify LINQ expression",                                         RuleOwner.DotnetFormatStyle, TokenDelta.None),
		new("IDE0121", "Simplify LINQ expression",                                         RuleOwner.DotnetFormatStyle, TokenDelta.None),
		new("IDE0130", "Namespace does not match folder structure",                        RuleOwner.Never, TokenDelta.None, "A namespace rename touches every reference site."),
		new("IDE0150", "Prefer 'null' check over type check",                              RuleOwner.DotnetFormatStyle, TokenDelta.None),
		new("IDE0160", "Convert to block scoped namespace",                                RuleOwner.Never, TokenDelta.None, "Kerf converts to a file-scoped namespace and never back; removing braces can change what a name resolves to."),
		new("IDE0161", "Convert to file-scoped namespace",                                 RuleOwner.Formatting, TokenDelta.None),
		new("IDE0170", "Property pattern can be simplified",                               RuleOwner.DotnetFormatStyle, TokenDelta.None),
		new("IDE0180", "Use tuple to swap values",                                         RuleOwner.DotnetFormatStyle, TokenDelta.None),
		new("IDE0200", "Remove unnecessary lambda expression",                             RuleOwner.DotnetFormatStyle, TokenDelta.None),
		new("IDE0210", "Convert to top-level statements",                                  RuleOwner.DotnetFormatStyle, TokenDelta.None),
		new("IDE0211", "Convert to 'Program.Main' style program",                          RuleOwner.DotnetFormatStyle, TokenDelta.None),
		new("IDE0220", "Add explicit cast",                                                RuleOwner.DotnetFormatStyle, TokenDelta.None),
		new("IDE0221", "Add explicit cast",                                                RuleOwner.DotnetFormatStyle, TokenDelta.None),
		new("IDE0230", "Use UTF-8 string literal",                                         RuleOwner.DotnetFormatStyle, TokenDelta.None),
		new("IDE0240", "Remove redundant nullable directive",                              RuleOwner.DotnetFormatStyle, TokenDelta.None),
		new("IDE0241", "Remove unnecessary nullable directive",                            RuleOwner.DotnetFormatStyle, TokenDelta.None),
		new("IDE0250", "Make struct 'readonly'",                                           RuleOwner.DotnetFormatStyle, TokenDelta.None),
		new("IDE0251", "Make member 'readonly'",                                           RuleOwner.DotnetFormatStyle, TokenDelta.None),
		new("IDE0260", "Use pattern matching",                                             RuleOwner.DotnetFormatStyle, TokenDelta.None),
		new("IDE0270", "Use coalesce expression",                                          RuleOwner.DotnetFormatStyle, TokenDelta.None),
		new("IDE0280", "Use 'nameof'",                                                     RuleOwner.DotnetFormatStyle, TokenDelta.None),
		new("IDE0290", "Use primary constructor",                                          RuleOwner.DotnetFormatStyle, TokenDelta.None),
		new("IDE0300", "Simplify collection initialization",                               RuleOwner.DotnetFormatStyle, TokenDelta.None),
		new("IDE0301", "Simplify collection initialization",                               RuleOwner.DotnetFormatStyle, TokenDelta.None),
		new("IDE0302", "Simplify collection initialization",                               RuleOwner.DotnetFormatStyle, TokenDelta.None),
		new("IDE0303", "Simplify collection initialization",                               RuleOwner.DotnetFormatStyle, TokenDelta.None),
		new("IDE0304", "Simplify collection initialization",                               RuleOwner.DotnetFormatStyle, TokenDelta.None),
		new("IDE0305", "Simplify collection initialization",                               RuleOwner.DotnetFormatStyle, TokenDelta.None),
		new("IDE0306", "Simplify collection initialization",                               RuleOwner.DotnetFormatStyle, TokenDelta.None),
		new("IDE0320", "Make anonymous function static",                                   RuleOwner.DotnetFormatStyle, TokenDelta.None),
		new("IDE0330", "Use 'System.Threading.Lock'",                                      RuleOwner.DotnetFormatStyle, TokenDelta.None),
		new("IDE0340", "Use unbound generic type",                                         RuleOwner.DotnetFormatStyle, TokenDelta.None),
		new("IDE0350", "Use implicitly typed lambda",                                      RuleOwner.DotnetFormatStyle, TokenDelta.None),
		new("IDE0360", "Simplify property accessor",                                       RuleOwner.DotnetFormatStyle, TokenDelta.None),
		new("IDE0370", "Remove unnecessary suppression",                                   RuleOwner.DotnetFormatStyle, TokenDelta.None),
		new("IDE0380", "Remove unnecessary 'unsafe' modifier",                             RuleOwner.DotnetFormatStyle, TokenDelta.None),
		new("IDE0390", "Make method synchronous",                                          RuleOwner.DotnetFormatStyle, TokenDelta.None),
		new("IDE0391", "Make method synchronous",                                          RuleOwner.DotnetFormatStyle, TokenDelta.None),
		new("IDE0410", "Use labeled jump statement",                                       RuleOwner.DotnetFormatStyle, TokenDelta.None),
		new("IDE1005", "Delegate invocation can be simplified.",                           RuleOwner.DotnetFormatStyle, TokenDelta.None),
		new("IDE1006", "Naming Styles",                                                    RuleOwner.Never, TokenDelta.None, "A rename touches every reference site, and no compiler check catches a reflection or serialisation string that named the old one."),
		new("IDE2000", "Avoid multiple blank lines",                                       RuleOwner.Formatting, TokenDelta.None),
		new("IDE2001", "Embedded statements must be on their own line",                    RuleOwner.Formatting, TokenDelta.None),
		new("IDE2002", "Consecutive braces must not have blank line between them",         RuleOwner.Formatting, TokenDelta.None),
		new("IDE2003", "Blank line required between block and subsequent statement",       RuleOwner.Formatting, TokenDelta.None),
		new("IDE2004", "Blank line not allowed after constructor initializer colon",       RuleOwner.Formatting, TokenDelta.None),
		new("IDE2005", "Blank line not allowed after conditional expression token",        RuleOwner.Formatting, TokenDelta.None),
		new("IDE2006", "Blank line not allowed after arrow expression clause token",       RuleOwner.Formatting, TokenDelta.None),
	];

	private static readonly FrozenDictionary<string, RuleEntry> ById =
		All.ToFrozenDictionary(entry => entry.Id, StringComparer.OrdinalIgnoreCase);

	/// <summary>The rules <c>kerf cleanup</c> attempts.</summary>
	public static readonly FrozenSet<string> CleanupKeys =
		All.Where(entry => entry.Owner == RuleOwner.Cleanup)
			.Select(entry => entry.Id)
			.ToFrozenSet(StringComparer.OrdinalIgnoreCase);

	/// <summary>Looks up a rule by id, case-insensitively. Null for an id the SDK does not name.</summary>
	public static RuleEntry? Find(string id) => ById.TryGetValue(id, out var entry) ? entry : null;

	/// <summary>True when <c>kerf cleanup</c> attempts this rule.</summary>
	public static bool IsCleanupRule(string id) => CleanupKeys.Contains(id);
}
