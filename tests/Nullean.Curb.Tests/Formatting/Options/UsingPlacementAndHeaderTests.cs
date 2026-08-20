namespace Nullean.Curb.Tests.Formatting.Options;

/// <summary>
/// <c>csharp_using_directive_placement</c> (IDE0065) and <c>file_header_template</c> (IDE0073).
/// </summary>
/// <remarks>
/// The last two of the code style rules Curb applies without a compilation. Both were checked against
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
	public Task A_mismatched_comment_header_is_corrected() => Formats(
		// The one shape Curb rewrites: a leading `//` block, compared against the template line by
		// line the way Roslyn's own analyzer does. Getting this wrong is exactly the failure mode
		// IDE0073 exists to catch, so leaving a stale header alone forever would be silently
		// incomplete rather than merely cautious.
		"""
		// Copyright someone else, all rights reserved.

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
		editorConfig: Header);

	[Test]
	public Task A_header_that_already_matches_is_left_alone() => Unchanged(
		// Idempotency: the second run must not touch a header the first run already wrote, and
		// neither should the first run if the file happened to start out correct.
		"""
		// Licensed to the Foo Foundation.
		// See LICENSE.

		namespace N;

		public class C
		{
		}
		""",
		editorConfig: Header);

	[Test]
	public Task A_matching_header_tolerates_incidental_whitespace() => Unchanged(
		// Roslyn's own comparison trims each line before comparing, so extra space between the
		// comment marker and the text is not a mismatch either.
		"""
		//    Licensed to the Foo Foundation.
		// See LICENSE.

		namespace N;

		public class C
		{
		}
		""",
		editorConfig: Header);

	[Test]
	public Task A_block_comment_header_is_left_alone_even_if_wrong() => Unchanged(
		// Only a `//` block is ever rewritten. A `/* */` header cannot be told apart from an
		// unrelated comment that happens to lead the file with nothing more than the template to
		// compare against, so it is never touched, right or wrong.
		"""
		/* Copyright someone else, all rights reserved. */

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

	[Test]
	public Task A_blank_line_ahead_of_a_missing_header_does_not_survive_it() => Formats(
		// Roslyn's own fixer drops a blank line already at the top of the file rather than leave one
		// in front of the header it inserts and another (the header's own) right behind it.
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
		editorConfig: Header);

	// ---- {fileName} ----------------------------------------------------------------------------------

	[Test]
	public Task FileName_is_substituted_into_the_header() => Formats(
		"""
		namespace N;

		public class C
		{
		}
		""",
		"""
		// File: Widget.cs

		namespace N;

		public class C
		{
		}
		""",
		editorConfig: "file_header_template = File: {fileName}",
		fileName: "Widget.cs");

	[Test]
	public Task FileName_takes_part_in_the_mismatch_comparison() => Formats(
		// A header naming the wrong file is exactly as wrong as one naming the wrong company.
		"""
		// File: OldName.cs

		namespace N;

		public class C
		{
		}
		""",
		"""
		// File: Widget.cs

		namespace N;

		public class C
		{
		}
		""",
		editorConfig: "file_header_template = File: {fileName}",
		fileName: "Widget.cs");

	[Test]
	public Task An_absent_fileName_substitutes_an_empty_string() => Formats(
		// Roslyn does the same for a document with no path: Path.GetFileName("") is "".
		"""
		namespace N;

		public class C
		{
		}
		""",
		"""
		// File:

		namespace N;

		public class C
		{
		}
		""",
		editorConfig: "file_header_template = File: {fileName}");

	// ---- interaction with using sorting ---------------------------------------------------------------

	[Test]
	public Task A_mismatched_header_is_left_alone_when_usings_will_be_sorted() => Formats(
		// Sorting splits a leading comment-then-blank-line "banner" off the first directive and
		// reprints it verbatim ahead of the reordered block (UsingOrganiser.BannerEnd). Rewriting the
		// same trivia here as well would either duplicate it or fight that logic for the same
		// characters, so the header stays exactly as written even though the usings still sort.
		"""
		// Copyright someone else, all rights reserved.

		using System.Text;
		using System;

		namespace N;

		public class C
		{
		}
		""",
		"""
		// Copyright someone else, all rights reserved.

		using System;
		using System.Text;

		namespace N;

		public class C
		{
		}
		""",
		editorConfig: Header + "\ndotnet_sort_system_directives_first = true");

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
