using System.Text;
using System.Globalization;
using System.Text.RegularExpressions;
using System.Diagnostics;
using System.Numerics;

namespace Curb.CleanupSmoketest;

/// <summary>
/// One site for every rule in the first cleanup slice.
/// </summary>
/// <remarks>
/// The using directives are the shape that matters most: two runs with a needed directive between them,
/// because Roslyn reports one IDE0005 per contiguous run rather than one per directive. A fixer reading
/// only the start of a run would leave the second directive of each pair behind, and this catches that.
/// </remarks>
public sealed class Widget
{
	private string _name;
	int _count;

	public Widget() => _name = "w";

	/// <summary>Needs System.Text.RegularExpressions, which is why the third directive survives.</summary>
	public Regex Pattern { get; } = new Regex("[a-z]+");

	public string Describe()
	{
		string text = "widget ";
		return text + _name + _count;
	}

	public void Increment() => _count++;

	private static Widget Create() => new Widget();

	/// <summary>Keeps Create reachable, so IDE0051 has nothing to say about it.</summary>
	public static Widget Factory() => Create();
}
