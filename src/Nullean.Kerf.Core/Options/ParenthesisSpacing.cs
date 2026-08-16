namespace Nullean.Kerf.Options;

/// <summary>
/// Which parenthesised constructs get a space just inside their parentheses.
/// </summary>
/// <remarks>
/// The value set of <c>csharp_space_between_parentheses</c>, which takes a comma-separated list and
/// defaults to none of them. It is deliberately narrow: it does not reach a method's parameter or
/// argument list, which have six options of their own, nor a tuple.
/// </remarks>
[Flags]
public enum ParenthesisSpacing : byte
{
	/// <summary>The default: parentheses hug their contents.</summary>
	None = 0,

	/// <summary><c>if ( a )</c>, and every other control-flow header.</summary>
	ControlFlowStatements = 1 << 0,

	/// <summary><c>( a + b )</c>.</summary>
	Expressions = 1 << 1,

	/// <summary><c>( int )value</c>.</summary>
	TypeCasts = 1 << 2,

	All = ControlFlowStatements | Expressions | TypeCasts,
}

/// <summary>Parses the value of <c>csharp_space_between_parentheses</c>.</summary>
public static class ParenthesisSpacingParser
{
	/// <summary>The names the option accepts, as written in a <c>.editorconfig</c>.</summary>
	public static readonly string[] Names =
	[
		"control_flow_statements",
		"expressions",
		"type_casts",
	];

	/// <summary>Parses a comma-separated list. <c>false</c> and <c>none</c> both mean no spacing.</summary>
	public static bool TryParse(string value, out ParenthesisSpacing spacing)
	{
		spacing = ParenthesisSpacing.None;

		foreach (var range in value.AsSpan().Split(','))
		{
			var name = value.AsSpan()[range].Trim();
			if (name.IsEmpty)
				continue;

			// Roslyn documents the off value as `false`; `none` is accepted for symmetry with the
			// other list-valued options rather than because anything writes it.
			if (name.Equals("false", StringComparison.OrdinalIgnoreCase)
				|| name.Equals("none", StringComparison.OrdinalIgnoreCase))
				continue;

			var single = name switch
			{
				"control_flow_statements" => ParenthesisSpacing.ControlFlowStatements,
				"expressions" => ParenthesisSpacing.Expressions,
				"type_casts" => ParenthesisSpacing.TypeCasts,
				_ => ParenthesisSpacing.None,
			};

			if (single == ParenthesisSpacing.None)
				return false;

			spacing |= single;
		}

		return true;
	}
}
