namespace Nullean.Curb.Tests.Formatting.Declarations;

/// <summary>Namespaces, using directives and assembly-level attributes.</summary>
public class NamespaceAndUsingTests : FormattingTest
{
	[Test]
	public Task File_scoped_namespace() => Unchanged(
		"""
		namespace Sample;

		public class C
		{
		}
		""");

	[Test]
	public Task Block_scoped_namespace_indents_its_members() => Unchanged(
		"""
		namespace Sample
		{
		    public class C
		    {
		    }
		}
		""");

	[Test]
	public Task Block_scoped_namespace_is_re_indented() => Formats(
		"""
		namespace Sample
		{
		public class C
		{
		}
		}
		""",
		"""
		namespace Sample
		{
		    public class C
		    {
		    }
		}
		""");

	[Test]
	public Task Nested_namespaces() => Unchanged(
		"""
		namespace Outer
		{
		    namespace Inner
		    {
		        public class C
		        {
		        }
		    }
		}
		""");

	[Test]
	public Task Dotted_namespace_name() => Unchanged(
		"""
		namespace Sample.Nested.Deeply;

		public class C
		{
		}
		""");

	[Test]
	public Task Using_directive() => Unchanged(
		"""
		using System;
		""");

	[Test]
	public Task Several_using_directives_each_on_their_own_line() => Unchanged(
		"""
		using System;
		using System.Collections.Generic;
		using System.Linq;
		""");

	[Test]
	public Task Using_alias() => Unchanged(
		"""
		using Alias = System.Collections.Generic.List<string>;
		""");

	[Test]
	public Task Using_static() => Unchanged(
		"""
		using static System.Math;
		""");

	[Test]
	public Task Global_using() => Unchanged(
		"""
		global using System;
		""");

	[Test]
	public Task Global_using_static() => Unchanged(
		"""
		global using static System.Math;
		""");

	[Test]
	public Task Using_inside_a_block_namespace() => Unchanged(
		"""
		namespace Sample
		{
		    using System;

		    public class C
		    {
		    }
		}
		""");

	[Test]
	public Task Assembly_attribute_comes_after_usings() => Unchanged(
		"""
		using System;

		[assembly: CLSCompliant(true)]

		public class C
		{
		}
		""");

	[Test]
	public Task Several_assembly_attributes() => Unchanged(
		"""
		[assembly: CLSCompliant(true)]
		[assembly: ComVisible(false)]
		""");

	[Test]
	public Task Module_attribute() => Unchanged(
		"""
		[module: SkipLocalsInit]
		""");

	[Test]
	public Task Attribute_with_named_arguments() => Unchanged(
		"""
		[assembly: AssemblyMetadata(Key = "a", Value = "b")]
		""");

	[Test]
	public Task Licence_header_stays_above_the_usings() => Unchanged(
		"""
		// Licensed to someone under one or more agreements.

		using System;

		public class C
		{
		}
		""");

	[Test]
	public Task Extern_alias() => Unchanged(
		"""
		extern alias Legacy;

		using System;
		""");

	[Test]
	[Skip("dotnet_sort_system_directives_first is not implemented — usings are never reordered")]
	public Task System_usings_sort_to_the_top() => Formats(
		"""
		using Octokit;
		using System.Linq;
		using System;
		""",
		"""
		using System;
		using System.Linq;
		using Octokit;
		""",
		editorConfig: "dotnet_sort_system_directives_first = true");

	[Test]
	public Task Usings_are_left_in_source_order_by_default() => Unchanged(
		"""
		using Octokit;
		using System.Linq;
		using System;
		""");
}
