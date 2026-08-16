namespace Nullean.Kerf.Tests.Formatting.Options;

/// <summary>
/// <c>csharp_style_namespace_declarations</c>, code style rule IDE0161.
/// </summary>
/// <remarks>
/// <para>
/// The fourth real .NET key Kerf applies without a compilation, and the one with the largest visible
/// effect: the whole file comes back an indent level. There is no re-indent pass behind it, only an
/// indent scope that is never opened.
/// </para>
/// <para>
/// Checked against the real thing. On the plain case and on the awkward one — a
/// <c>#pragma</c> pair straddling the declaration — Kerf's output is byte-identical to
/// <c>dotnet format style</c> followed by <c>dotnet format whitespace</c>. It was also run over the
/// corpus inverted: 1,185 real files were mechanically wrapped back into block form and converted
/// again, and every one returned to its file-scoped original.
/// </para>
/// <para>
/// One direction only. <c>block_scoped</c> is accepted and does nothing, because a file-scoped namespace is
/// already the form the compiler and the templates prefer and putting the braces back would indent a
/// whole file to no end.
/// </para>
/// </remarks>
public class NamespaceStyleTests : FormattingTest
{
	private const string FileScoped = "csharp_style_namespace_declarations = file_scoped";

	// ---- the default ------------------------------------------------------------------------------

	[Test]
	public Task A_block_namespace_stays_one_without_the_key() => Unchanged(
		"""
		namespace Alpha
		{
		    public class C
		    {
		    }
		}
		""");

	[Test]
	public Task Block_scoped_is_accepted_and_changes_nothing() => Unchanged(
		// Kerf never puts the braces back, so this value is inert rather than unsupported. The
		// spelling is `block_scoped`; Roslyn throws on a bare `block` rather than ignoring it.
		"""
		namespace Alpha;

		public class C
		{
		}
		""",
		editorConfig: "csharp_style_namespace_declarations = block_scoped");

	// ---- converting -------------------------------------------------------------------------------

	[Test]
	public Task A_block_namespace_becomes_file_scoped() => WithAndWithout(
		"""
		namespace Alpha.Beta
		{
		    public class C
		    {
		    }
		}
		""",
		"""
		namespace Alpha.Beta
		{
		    public class C
		    {
		    }
		}
		""",
		"""
		namespace Alpha.Beta;

		public class C
		{
		}
		""",
		FileScoped);

	[Test]
	public Task Every_member_comes_back_a_level() => Formats(
		"""
		using System;

		namespace Alpha
		{
		    public class C
		    {
		        public void M()
		        {
		            Call();
		        }
		    }

		    public class D
		    {
		    }
		}
		""",
		"""
		using System;

		namespace Alpha;

		public class C
		{
		    public void M()
		    {
		        Call();
		    }
		}

		public class D
		{
		}
		""",
		editorConfig: FileScoped);

	[Test]
	public Task Usings_inside_the_namespace_come_with_it() => Formats(
		"""
		namespace Alpha
		{
		    using System;

		    public class C
		    {
		    }
		}
		""",
		"""
		namespace Alpha;

		using System;

		public class C
		{
		}
		""",
		editorConfig: FileScoped);

	[Test]
	public Task A_declaration_with_nothing_under_it_gains_the_blank_line() => Formats(
		// dotnet format inserts this, so Kerf does too — with or without the key, since it is
		// agreement rather than an opinion. Missing it was a real gap: the first project to consume
		// the MSBuild package still failed IDE0055 on exactly this line after Kerf had run.
		"""
		namespace Alpha;
		public class C
		{
		}
		""",
		"""
		namespace Alpha;

		public class C
		{
		}
		""");

	// ---- what it refuses --------------------------------------------------------------------------

	[Test]
	public Task Two_namespaces_in_a_file_are_left_alone() => Unchanged(
		// A file-scoped namespace runs to the end of the file, so converting the first would swallow
		// the second. This is a refusal rather than a best guess: getting it wrong moves types into a
		// namespace they were never in.
		"""
		namespace Alpha
		{
		}

		namespace Beta
		{
		}
		""",
		editorConfig: FileScoped);

	[Test]
	public Task A_type_beside_the_namespace_stops_it() => Unchanged(
		"""
		namespace Alpha
		{
		    public class C
		    {
		    }
		}

		public class Outside
		{
		}
		""",
		editorConfig: FileScoped);

	[Test]
	public Task A_nested_namespace_stops_it() => Unchanged(
		"""
		namespace Alpha
		{
		    namespace Beta
		    {
		        public class C
		        {
		        }
		    }
		}
		""",
		editorConfig: FileScoped);

	// ---- trivia -----------------------------------------------------------------------------------

	[Test]
	public Task A_pragma_pair_straddling_the_declaration_survives() => Formats(
		// The shape that made the corpus interesting, and the blank line here is Roslyn's own: its
		// fixer produces exactly this.
		"""
		#pragma warning disable IDE0130
		namespace Westwind.Live
		{
		#pragma warning restore IDE0130

		    public static class C
		    {
		    }
		}
		""",
		"""
		#pragma warning disable IDE0130
		namespace Westwind.Live;

		#pragma warning restore IDE0130

		public static class C
		{
		}
		""",
		editorConfig: FileScoped);

	[Test]
	public Task A_comment_above_the_closing_brace_is_not_lost_with_it() => Formats(
		// The brace goes; anything written above it does not.
		"""
		namespace Alpha
		{
		    public class C
		    {
		    }

		    // trailing note
		}
		""",
		"""
		namespace Alpha;

		public class C
		{
		}

		// trailing note
		""",
		editorConfig: FileScoped);
}
