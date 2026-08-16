namespace Nullean.Kerf.Options;

/// <summary>
/// Whether and how Kerf reorders a file's <c>using</c> directives.
/// </summary>
/// <remarks>
/// <para>
/// Sorting is off unless asked for, and the ask is the presence of one of the two
/// <c>.editorconfig</c> keys that configure it. Neither key says <em>whether</em> to sort — they say
/// where <c>System</c> goes and whether groups are separated — because in Visual Studio sorting is
/// a command you invoke, not something the formatter does on its own. <c>dotnet format whitespace</c>
/// never sorts either; only <c>dotnet format style</c>, which needs a full compilation, does.
/// </para>
/// <para>
/// So a default of "always sort" would reorder the usings of every file in a repository the first
/// time Kerf ran, which is exactly the churn the project exists to avoid. Writing either key is
/// taken as opting in.
/// </para>
/// </remarks>
public enum UsingOrder : byte
{
	/// <summary>Leave the directives exactly where the author put them. The default.</summary>
	AsWritten = 0,

	/// <summary>Sort alphabetically, with <c>System</c> and <c>System.*</c> ahead of everything else.</summary>
	SystemFirst = 1,

	/// <summary>Sort alphabetically, treating <c>System</c> like any other namespace.</summary>
	Alphabetical = 2,
}
