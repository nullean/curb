using System.Collections.Concurrent;
using System.IO.Abstractions.TestingHelpers;
using Nullean.Kerf;
using Nullean.Kerf.EditorConfig;

namespace Nullean.Kerf.Tests.Formatting;

/// <summary>
/// Turns the literal <c>.editorconfig</c> text a test supplies into <see cref="FormatOptions"/>.
/// </summary>
/// <remarks>
/// Deliberately routed through the real <see cref="EditorConfigOptionsBinder"/> over a
/// <c>MockFileSystem</c> rather than constructing options directly, so that every formatting test
/// also covers config resolution and binding. Results are cached per distinct option text, because
/// several hundred tests share a handful of option sets.
/// </remarks>
internal static class TestOptions
{
	/// <summary>
	/// Tests default to LF and no reflow. LF keeps expected output stable across platforms; reflow
	/// off matches the shipped default, so a test only opts into wrapping when that is the subject.
	/// </summary>
	private static readonly FormatOptions Defaults = new()
	{
		EndOfLine = EndOfLine.Lf,
		MaxLineLength = FormatOptions.Off,
	};

	private static readonly ConcurrentDictionary<string, FormatOptions> Cache = new(StringComparer.Ordinal);

	public static FormatOptions Parse(string? editorConfig)
	{
		if (string.IsNullOrWhiteSpace(editorConfig))
			return Defaults;

		return Cache.GetOrAdd(editorConfig, Bind);
	}

	private static FormatOptions Bind(string editorConfig)
	{
		var fileSystem = new MockFileSystem();
		fileSystem.AddFile("/test/.editorconfig", new MockFileData($"root = true{Environment.NewLine}[*.cs]{Environment.NewLine}{editorConfig}"));
		fileSystem.AddFile("/test/Test.cs", new MockFileData(string.Empty));

		var configuration = new KerfEditorConfig(fileSystem).For("/test/Test.cs");
		var bound = EditorConfigOptionsBinder.Bind(configuration);

		// A test that does not mention end_of_line still wants LF, so expected strings stay stable
		// on Windows; likewise reflow stays off unless max_line_length was actually specified.
		return bound with
		{
			EndOfLine = editorConfig.Contains("end_of_line", StringComparison.Ordinal) ? bound.EndOfLine : EndOfLine.Lf,
		};
	}
}
