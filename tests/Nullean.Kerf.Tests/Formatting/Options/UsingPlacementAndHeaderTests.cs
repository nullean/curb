namespace Nullean.Kerf.Tests.Formatting.Options;

/// <summary>
/// <c>csharp_using_directive_placement</c> (IDE0065) and <c>file_header_template</c> (IDE0073).
/// </summary>
/// <remarks>
/// The last two of the code style rules Kerf applies without a compilation. Both were checked against
/// <c>dotnet format style</c> followed by <c>dotnet format whitespace</c> and match it byte for byte.
/// </remarks>
public class UsingPlacementAndHeaderTests : FormattingTest
{
	private const string Inside = "csharp_using_directive_placement = inside_namespace";
	private const string Header = "file_header_template = Licensed to the Foo Foundation.\\nSee LICENSE.";

	// ---- using placement ---------------------------------------------------------------------------

	[Test]
	public Task Directives_stay_where_they_are_without_the_key() => Unchanged(
		"""
		using System;

		namespace N
		{
		    public class C
		    {
		    }
		}
		""");

	[Test]
	public Task Directives_move_inside_a_block_namespace() => WithAndWithout(
		"""
		using System;
		using System.Text;

		namespace N
		{
		    public class C
		    {
		    }
		}
		""",
		"""
		using System;
		using System.Text;

		namespace N
		{
		    public class C
		    {
		    }
		}
		""",
		"""
		namespace N
		{
		    using System;
		    using System.Text;

		    public class C
		    {
		    }
		}
		""",
		Inside);

	[Test]
	public Task Directives_move_inside_a_file_scoped_namespace_too() => Formats(
		// "Inside" a file-scoped namespace means after the declaration, since there is no brace.
		"""
		using System;

		namespace N;

		public class C
		{
		}
		""",
		"""
		namespace N;

		using System;

		public class C
		{
		}
		""",
		editorConfig: Inside);

	[Test]
	public Task Two_namespaces_stop_the_move() => Unchanged(
		// Which one would a directive belong to? Putting it in the wrong one changes what the names
		// inside that namespace resolve to, so this is a refusal rather than a guess.
		"""
		using System;

		namespace A
		{
		}

		namespace B
		{
		}
		""",
		editorConfig: Inside);

	[Test]
	public Task A_type_beside_the_namespace_stops_it() => Unchanged(
		"""
		using System;

		namespace A
		{
		}

		public class Outside
		{
		}
		""",
		editorConfig: Inside);

	[Test]
	public Task A_file_with_no_namespace_has_nowhere_to_move_them() => Unchanged(
		"""
		using System;

		public class C
		{
		}
		""",
		editorConfig: Inside);

	// ---- file header -------------------------------------------------------------------------------

	[Test]
	public Task No_header_is_added_without_the_key() => Unchanged(
		"""
		namespace N;

		public class C
		{
		}
		""");

	[Test]
	public Task A_missing_header_is_added() => WithAndWithout(
		"""
		namespace N;

		public class C
		{
		}
		""",
		"""
		namespace N;

		public class C
		{
		}
		""",
		"""
		// Licensed to the Foo Foundation.
		// See LICENSE.

		namespace N;

		public class C
		{
		}
		""",
		Header);

	[Test]
	public Task It_goes_above_the_using_directives() => Formats(
		"""
		using System;

		namespace N;

		public class C
		{
		}
		""",
		"""
		// Licensed to the Foo Foundation.
		// See LICENSE.

		using System;

		namespace N;

		public class C
		{
		}
		""",
		editorConfig: Header);

	[Test]
	public Task A_file_that_already_opens_with_a_comment_is_left_alone() => Unchanged(
		// Added, never replaced. Roslyn's fixer rewrites a header that differs from the template, but
		// telling "the wrong header" from "a comment that happens to lead the file" needs more than
		// the template to compare against, and deleting somebody's copyright notice because it was
		// worded differently is not a mistake worth risking.
		"""
		// Copyright someone else, all rights reserved.

		namespace N;

		public class C
		{
		}
		""",
		editorConfig: Header);

	[Test]
	public Task A_doc_comment_counts_as_opening_with_one() => Unchanged(
		"""
		/// <summary>A file that starts with documentation.</summary>
		public class C
		{
		}
		""",
		editorConfig: Header);

	[Test]
	public Task A_directive_at_the_top_is_not_a_header() => Formats(
		// `#if` is not a comment, so a file opening with one can still be given a header.
		"""
		#if NET10_0
		namespace N;

		public class C
		{
		}
		#endif
		""",
		"""
		// Licensed to the Foo Foundation.
		// See LICENSE.

		#if NET10_0
		namespace N;

		public class C
		{
		}
		#endif
		""",
		editorConfig: Header);

	[Test]
	public Task An_empty_template_asks_for_no_header() => Unchanged(
		// Which is how Roslyn documents turning the rule off.
		"""
		namespace N;

		public class C
		{
		}
		""",
		editorConfig: "file_header_template =");

	// ---- together ------------------------------------------------------------------------------------

	[Test]
	public Task The_header_lands_above_directives_that_moved_inside() => Formats(
		"""
		using System;

		namespace N
		{
		    public class C
		    {
		    }
		}
		""",
		"""
		// Licensed to the Foo Foundation.
		// See LICENSE.

		namespace N
		{
		    using System;

		    public class C
		    {
		    }
		}
		""",
		editorConfig: Header + "\n" + Inside);
}
