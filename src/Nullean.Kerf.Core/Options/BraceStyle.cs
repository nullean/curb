namespace Nullean.Kerf.Options;

/// <summary>
/// Which constructs put their opening brace on a new line.
/// </summary>
/// <remarks>
/// The value set of <c>csharp_new_line_before_open_brace</c>. It is a flags enum because the option
/// accepts a comma-separated list as well as the shorthands <c>all</c> and <c>none</c> — Allman and
/// K&amp;R respectively. The default is <see cref="All"/>, matching Roslyn.
/// </remarks>
[Flags]
public enum BraceStyle : ushort
{
	/// <summary>K&amp;R: every brace stays on the line that introduces it.</summary>
	None = 0,

	Accessors = 1 << 0,
	AnonymousMethods = 1 << 1,
	AnonymousTypes = 1 << 2,
	ControlBlocks = 1 << 3,
	Events = 1 << 4,
	Indexers = 1 << 5,
	Lambdas = 1 << 6,
	LocalFunctions = 1 << 7,
	Methods = 1 << 8,
	ObjectCollectionArrayInitializers = 1 << 9,
	Properties = 1 << 10,
	Types = 1 << 11,

	/// <summary>Allman: every brace on its own line. The default.</summary>
	All = Accessors
		| AnonymousMethods
		| AnonymousTypes
		| ControlBlocks
		| Events
		| Indexers
		| Lambdas
		| LocalFunctions
		| Methods
		| ObjectCollectionArrayInitializers
		| Properties
		| Types,
}

/// <summary>Parses the value of <c>csharp_new_line_before_open_brace</c>.</summary>
public static class BraceStyleParser
{
	/// <summary>The names the option accepts, as written in a <c>.editorconfig</c>.</summary>
	public static readonly string[] Names =
	[
		"all",
		"none",
		"accessors",
		"anonymous_methods",
		"anonymous_types",
		"control_blocks",
		"events",
		"indexers",
		"lambdas",
		"local_functions",
		"methods",
		"object_collection_array_initializers",
		"properties",
		"types",
	];

	/// <summary>Parses a comma-separated list, or one of the <c>all</c> / <c>none</c> shorthands.</summary>
	public static bool TryParse(string value, out BraceStyle style)
	{
		style = BraceStyle.None;

		foreach (var range in value.AsSpan().Split(','))
		{
			var name = value.AsSpan()[range].Trim();
			if (name.IsEmpty)
				continue;

			if (name.Equals("all", StringComparison.OrdinalIgnoreCase))
			{
				style = BraceStyle.All;
				continue;
			}

			if (name.Equals("none", StringComparison.OrdinalIgnoreCase))
				continue;

			if (!TryParseOne(name, out var single))
				return false;

			style |= single;
		}

		return true;
	}

	private static bool TryParseOne(ReadOnlySpan<char> name, out BraceStyle style)
	{
		style = name switch
		{
			"accessors" => BraceStyle.Accessors,
			"anonymous_methods" => BraceStyle.AnonymousMethods,
			"anonymous_types" => BraceStyle.AnonymousTypes,
			"control_blocks" => BraceStyle.ControlBlocks,
			"events" => BraceStyle.Events,
			"indexers" => BraceStyle.Indexers,
			"lambdas" => BraceStyle.Lambdas,
			"local_functions" => BraceStyle.LocalFunctions,
			"methods" => BraceStyle.Methods,
			"object_collection_array_initializers" => BraceStyle.ObjectCollectionArrayInitializers,
			"properties" => BraceStyle.Properties,
			"types" => BraceStyle.Types,
			_ => BraceStyle.None,
		};

		return style != BraceStyle.None;
	}
}
