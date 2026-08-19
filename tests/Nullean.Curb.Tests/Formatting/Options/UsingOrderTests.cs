namespace Nullean.Curb.Tests.Formatting.Options;

/// <summary>
/// <c>dotnet_sort_system_directives_first</c> and <c>dotnet_separate_import_directive_groups</c>.
/// </summary>
/// <remarks>
/// <para>
/// The only transform Curb performs: everything else it does is layout, and this moves tokens.
/// Sorting is off unless asked for, and the ask is the presence of either key — neither says
/// <em>whether</em> to sort, because in the IDE sorting is a command you invoke. Writing either one
/// is taken as opting in; a repository with neither is never reordered.
/// </para>
/// <para>
/// Expectations came from <c>dotnet format style</c> on a real project, since
/// <c>dotnet format whitespace</c> cannot sort at all — it has no workspace.
/// </para>
/// </remarks>
public class UsingOrderTests : FormattingTest
{
	private const string Unsorted = """
		using Zebra.Things;
		using System.Linq;
		using Alpha.Core;
		using System;
		using Microsoft.Extensions.Logging;
		using System.Collections.Generic;

		public class C { }
		""";

	// ---- the default is to leave them alone -------------------------------------------------------

	[Test]
	public Task Usings_are_not_reordered_without_being_asked() => Unchanged(Unsorted);

	// ---- dotnet_sort_system_directives_first ------------------------------------------------------

	[Test]
	public Task System_sorts_ahead_of_everything_else() => Formats(
		Unsorted,
		"""
		using System;
		using System.Collections.Generic;
		using System.Linq;
		using Alpha.Core;
		using Microsoft.Extensions.Logging;
		using Zebra.Things;

		public class C { }
		""",
		editorConfig: "dotnet_sort_system_directives_first = true");

	[Test]
	public Task Setting_it_false_still_sorts_but_treats_System_as_ordinary() => Formats(
		Unsorted,
		"""
		using Alpha.Core;
		using Microsoft.Extensions.Logging;
		using System;
		using System.Collections.Generic;
		using System.Linq;
		using Zebra.Things;

		public class C { }
		""",
		editorConfig: "dotnet_sort_system_directives_first = false");

	// ---- dotnet_separate_import_directive_groups --------------------------------------------------

	[Test]
	public Task Groups_can_be_separated_by_a_blank_line() => Formats(
		Unsorted,
		"""
		using System;
		using System.Collections.Generic;
		using System.Linq;

		using Alpha.Core;

		using Microsoft.Extensions.Logging;

		using Zebra.Things;

		public class C { }
		""",
		editorConfig: """
		dotnet_sort_system_directives_first = true
		dotnet_separate_import_directive_groups = true
		""");

	[Test]
	public Task Separating_groups_on_its_own_implies_sorting() => Formats(
		Unsorted,
		// A group is a run sharing a first segment, which only holds once sorted, so asking for
		// separated groups asks for a sort.
		"""
		using Alpha.Core;

		using Microsoft.Extensions.Logging;

		using System;
		using System.Collections.Generic;
		using System.Linq;

		using Zebra.Things;

		public class C { }
		""",
		editorConfig: """
		dotnet_sort_system_directives_first = false
		dotnet_separate_import_directive_groups = true
		""");

	// ---- what it refuses to touch -----------------------------------------------------------------

	[Test]
	public Task A_file_with_any_directive_is_left_entirely_alone() => Unchanged(
		"""
		using Zebra.Things;
		using System;

		#if NET10_0
		using Net10.Only;
		#endif

		public class C { }
		""",
		editorConfig: "dotnet_sort_system_directives_first = true");

	[Test]
	public Task A_region_around_the_usings_also_stops_it() => Unchanged(
		"""
		#region Imports
		using Zebra.Things;
		using System;
		#endregion

		public class C { }
		""",
		editorConfig: "dotnet_sort_system_directives_first = true");

	[Test]
	public Task A_single_using_has_nothing_to_sort() => Unchanged(
		"""
		using Zebra.Things;

		public class C { }
		""",
		editorConfig: "dotnet_sort_system_directives_first = true");

	// ---- comments ---------------------------------------------------------------------------------

	[Test]
	public Task A_file_banner_stays_at_the_top() => Formats(
		"""
		// Licensed to the Foo Foundation under one or more agreements.
		// The Foo Foundation licenses this file to you under the MIT licence.

		using Zebra.Things;
		using Alpha.Core;
		using System;

		public class C { }
		""",
		// The banner belongs to the file, not to whichever directive happened to come first.
		"""
		// Licensed to the Foo Foundation under one or more agreements.
		// The Foo Foundation licenses this file to you under the MIT licence.

		using System;
		using Alpha.Core;
		using Zebra.Things;

		public class C { }
		""",
		editorConfig: "dotnet_sort_system_directives_first = true");

	[Test]
	public Task A_comment_travels_with_the_directive_it_describes() => Formats(
		"""
		// comment on zebra
		using Zebra.Things;
		// comment on alpha
		using Alpha.Core;
		// comment on system
		using System;

		public class C { }
		""",
		// dotnet format pins the first directive's leading trivia whether or not a blank line
		// separates it, so it leaves `// comment on zebra` behind and it silently reads as a
		// comment on System. Curb keeps each comment with its own directive.
		"""
		// comment on system
		using System;
		// comment on alpha
		using Alpha.Core;
		// comment on zebra
		using Zebra.Things;

		public class C { }
		""",
		editorConfig: "dotnet_sort_system_directives_first = true");

	[Test]
	public Task A_banner_and_a_comment_together_are_left_alone() => Unchanged(
		"""
		// Licensed to the Foo Foundation.

		// comment on zebra
		using Zebra.Things;
		using System;

		public class C { }
		""",
		// Splitting one directive's trivia between a pinned half and a travelling half is the kind
		// of cut that loses a comment, so Curb declines the whole file instead.
		editorConfig: "dotnet_sort_system_directives_first = true");

	[Test]
	public Task A_trailing_comment_travels_with_its_directive() => Formats(
		"""
		using Zebra.Things; // last
		using System; // first

		public class C { }
		""",
		"""
		using System; // first
		using Zebra.Things; // last

		public class C { }
		""",
		editorConfig: "dotnet_sort_system_directives_first = true");

	// ---- aliases and static usings ----------------------------------------------------------------

	[Test]
	public Task Static_usings_and_aliases_sort_after_plain_ones() => Formats(
		"""
		using Zebra.Things;
		using static System.Math;
		using Alias = Alpha.Core.Thing;
		using System;

		public class C { }
		""",
		"""
		using System;
		using Zebra.Things;
		using static System.Math;
		using Alias = Alpha.Core.Thing;

		public class C { }
		""",
		editorConfig: "dotnet_sort_system_directives_first = true");

	// ---- block namespaces -------------------------------------------------------------------------

	[Test]
	public Task Usings_inside_a_block_namespace_sort_too() => Formats(
		"""
		namespace N
		{
		    using Zebra.Things;
		    using Alpha.Core;
		    using System;

		    public class C { }
		}
		""",
		"""
		namespace N
		{
		    using System;
		    using Alpha.Core;
		    using Zebra.Things;

		    public class C { }
		}
		""",
		editorConfig: "dotnet_sort_system_directives_first = true");
}
