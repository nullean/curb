using AwesomeAssertions;
using Microsoft.CodeAnalysis.CSharp;

namespace Nullean.Curb.Tests.Formatting.Options;

/// <summary>
/// <see cref="FormatOptions.LanguageVersion"/>: a rewrite never writes syntax the project's C# version
/// cannot compile (issue #298, file-scoped namespaces in a project that compiles as C# 7.3).
/// </summary>
/// <remarks>
/// <para>
/// Not through the <see cref="FormattingTest"/> harness. The version is not an <c>.editorconfig</c> key,
/// and the harness dumps every case for <c>./build.sh verifyexpectations</c> by its <c>.editorconfig</c>
/// alone, which would record a capped expectation as though the configuration produced it.
/// </para>
/// <para>
/// Each expectation below was compiled by <c>csc</c> at its version. Parsing it at that version proves
/// nothing: Roslyn's parser accepts every feature at every version, and the binder reports CS8370.
/// </para>
/// </remarks>
public class LanguageVersionTests
{
	private const string EveryRewrite = """
		csharp_style_namespace_declarations = file_scoped
		csharp_style_expression_bodied_methods = true
		csharp_style_expression_bodied_constructors = true
		csharp_style_expression_bodied_operators = true
		csharp_style_expression_bodied_local_functions = true
		csharp_style_expression_bodied_accessors = true
		csharp_style_expression_bodied_properties = true
		csharp_style_expression_bodied_indexers = true
		""";

	private const string Source = """
		namespace Alpha
		{
		    public class C
		    {
		        private int _x;
		        public C() { _x = 1; }
		        ~C() { _x = 0; }
		        public int Get() { return _x; }
		        public void Fail() { throw new System.InvalidOperationException(); }
		        public int Value { get { return _x; } }
		        public int Broken { get { throw new System.InvalidOperationException(); } }
		        public int Both { get { return _x; } set { _x = value; } }
		        public int this[int i] { get { return i; } }
		        public static C operator +(C a, C b) { return a; }
		    }
		}
		""";

	private static string Format(string source, LanguageVersion version)
	{
		var options = TestOptions.Parse(EveryRewrite) with { LanguageVersion = version };
		using var formatter = new CSharpFormatter();
		var result = formatter.Format(source, options, produceText: true, forceRoundTrip: true, verifyRoundTrip: true);
		result.Status.Should().Be(FormatStatus.Formatted, result.Message);
		return result.Text!.TrimEnd('\n');
	}

	[Test]
	public async Task Without_a_version_every_rewrite_still_applies()
	{
		Format(Source, LanguageVersion.Default).Should().Be(
			"""
			namespace Alpha;

			public class C
			{
			    private int _x;
			    public C() => _x = 1;
			    ~C() { _x = 0; }
			    public int Get() => _x;
			    public void Fail() => throw new System.InvalidOperationException();
			    public int Value => _x;
			    public int Broken => throw new System.InvalidOperationException();
			    public int Both { get => _x; set => _x = value; }
			    public int this[int i] => i;
			    public static C operator +(C a, C b) => a;
			}
			""");
		await Task.CompletedTask;
	}

	[Test]
	[Arguments(LanguageVersion.Latest)]
	[Arguments(LanguageVersion.LatestMajor)]
	[Arguments(LanguageVersion.Preview)]
	public async Task A_latest_version_caps_nothing(LanguageVersion version)
	{
		Format(Source, version).Should().Be(Format(Source, LanguageVersion.Default));
		await Task.CompletedTask;
	}

	[Test]
	public async Task CSharp_9_keeps_the_block_namespace()
	{
		// The issue's case is 7.3; 9 is the last version without file-scoped namespaces.
		Format(Source, LanguageVersion.CSharp9).Should().Be(
			"""
			namespace Alpha
			{
			    public class C
			    {
			        private int _x;
			        public C() => _x = 1;
			        ~C() { _x = 0; }
			        public int Get() => _x;
			        public void Fail() => throw new System.InvalidOperationException();
			        public int Value => _x;
			        public int Broken => throw new System.InvalidOperationException();
			        public int Both { get => _x; set => _x = value; }
			        public int this[int i] => i;
			        public static C operator +(C a, C b) => a;
			    }
			}
			""");
		await Task.CompletedTask;
	}

	[Test]
	public async Task CSharp_6_converts_only_what_csharp_6_has()
	{
		// Methods, operators and get-only properties and indexers; not constructors, accessors, or any
		// body that only throws, which would need C# 7's throw expression.
		Format(Source, LanguageVersion.CSharp6).Should().Be(
			"""
			namespace Alpha
			{
			    public class C
			    {
			        private int _x;
			        public C() { _x = 1; }
			        ~C() { _x = 0; }
			        public int Get() => _x;
			        public void Fail() { throw new System.InvalidOperationException(); }
			        public int Value => _x;
			        public int Broken { get { throw new System.InvalidOperationException(); } }
			        public int Both { get { return _x; } set { _x = value; } }
			        public int this[int i] => i;
			        public static C operator +(C a, C b) => a;
			    }
			}
			""");
		await Task.CompletedTask;
	}

	[Test]
	public async Task CSharp_5_converts_nothing()
	{
		Format(Source, LanguageVersion.CSharp5).Should().Be(Source);
		await Task.CompletedTask;
	}
}
