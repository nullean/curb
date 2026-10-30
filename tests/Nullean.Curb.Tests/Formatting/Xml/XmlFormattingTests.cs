namespace Nullean.Curb.Tests.Formatting.Xml;

/// <summary>
/// XML formatting as Curb defines it: the author's layout, re-indented, with nothing that might be a
/// value touched. Expectations are hand-written, since XML has no reference formatter to agree with.
/// </summary>
public class XmlFormattingTests : XmlFormattingTest
{
	/// <summary>MSBuild reads `\n true\n` here, which is not `true`. The property moves; its value does not.</summary>
	[Test]
	public Task Property_text_is_content_even_when_its_parent_is_reindented() =>
		Formats(
			"""
			<Project>
			<PropertyGroup>
			<Flag>
			 true
			</Flag>
			</PropertyGroup>
			</Project>
			""",
			"""
			<Project>
			  <PropertyGroup>
			    <Flag>
			 true
			</Flag>
			  </PropertyGroup>
			</Project>
			""");

	/// <summary>MSBuild reads a property that holds elements as its inner XML, whitespace included.</summary>
	[Test]
	public Task A_property_holding_elements_keeps_its_inner_xml() =>
		Unchanged(
			"""
			<Project>
			  <PropertyGroup>
			    <Xml>
			        <Inner A="1"/>
			      </Xml>
			  </PropertyGroup>
			</Project>
			""");

	[Test]
	public Task Item_metadata_keeps_its_inner_xml() =>
		Unchanged(
			"""
			<Project>
			  <ItemGroup>
			    <None Include="a.txt">
			      <Pack>
			          true</Pack>
			    </None>
			  </ItemGroup>
			</Project>
			""");

	[Test]
	public Task Item_metadata_elements_are_reindented_as_children_of_the_item() =>
		Formats(
			"""
			<Project>
			  <ItemGroup>
			    <None Include="a.txt">
			          <Pack>true</Pack>
			    </None>
			  </ItemGroup>
			</Project>
			""",
			"""
			<Project>
			  <ItemGroup>
			    <None Include="a.txt">
			      <Pack>true</Pack>
			    </None>
			  </ItemGroup>
			</Project>
			""");

	[Test]
	public Task An_empty_value_of_whitespace_is_a_value() =>
		Unchanged(
			"""
			<Project>
			  <PropertyGroup>
			    <NoWarn> </NoWarn>
			  </PropertyGroup>
			</Project>
			""");

	[Test]
	public Task Project_extensions_are_left_as_written() =>
		Unchanged(
			"""
			<Project>
			  <ProjectExtensions>
			        <VisualStudio>
			     <UserProperties />
			        </VisualStudio>
			  </ProjectExtensions>
			</Project>
			""");

	[Test]
	public Task An_inline_task_body_is_left_as_written() =>
		Unchanged(
			"""
			<Project>
			  <UsingTask TaskName="T" TaskFactory="RoslynCodeTaskFactory" AssemblyFile="x.dll">
			    <Task>
			          <Code Type="Fragment" Language="cs"><![CDATA[ Log.LogMessage("hi"); ]]></Code>
			    </Task>
			  </UsingTask>
			</Project>
			""");

	/// <summary>nuget.config is generic XML: only MSBuild reads inner XML as a value.</summary>
	[Test]
	public Task Generic_xml_reindents_what_msbuild_would_keep() =>
		Formats(
			"""
			<configuration>
			<packageSources>
			<add key="a" value="b"/>
			</packageSources>
			</configuration>
			""",
			"""
			<configuration>
			  <packageSources>
			    <add key="a" value="b"/>
			  </packageSources>
			</configuration>
			""",
			fileName: "nuget.config");

	[Test]
	public Task Element_only_content_is_reindented_line_by_line() =>
		Formats(
			"""
			<Project>
			      <PropertyGroup>
			<A>1</A>
			            <B>2</B>
			   </PropertyGroup>
			</Project>
			""",
			"""
			<Project>
			  <PropertyGroup>
			    <A>1</A>
			    <B>2</B>
			  </PropertyGroup>
			</Project>
			""");

	/// <summary>A break is never added where the source had no whitespace.</summary>
	[Test]
	public Task Nodes_the_author_wrote_on_one_line_stay_on_one_line() =>
		Unchanged(
			"""
			<Project>
			  <ItemGroup><A Include="a" /><B Include="b" /></ItemGroup>
			</Project>
			""");

	[Test]
	public Task Whitespace_without_a_line_break_is_kept_as_written() =>
		Unchanged(
			"""
			<Project>
			  <ItemGroup><A Include="a" />   <B Include="b" /></ItemGroup>
			</Project>
			""");

	[Test]
	public Task Blank_lines_are_capped_at_one_by_default() =>
		Formats(
			"""
			<Project>
			  <PropertyGroup />



			  <ItemGroup />
			</Project>
			""",
			"""
			<Project>
			  <PropertyGroup />

			  <ItemGroup />
			</Project>
			""");

	[Test]
	public Task Blank_lines_follow_xml_max_blank_lines_between_tags() =>
		Formats(
			"""
			<Project>
			  <PropertyGroup />



			  <ItemGroup />

			  <Target Name="T" />
			</Project>
			""",
			"""
			<Project>
			  <PropertyGroup />
			  <ItemGroup />
			  <Target Name="T" />
			</Project>
			""",
			editorConfig: "xml_max_blank_lines_between_tags = 0");

	[Test]
	public Task Two_blank_lines_kept_when_asked() =>
		Formats(
			"""
			<Project>
			  <PropertyGroup />



			  <ItemGroup />
			</Project>
			""",
			"""
			<Project>
			  <PropertyGroup />


			  <ItemGroup />
			</Project>
			""",
			editorConfig: "xml_max_blank_lines_between_tags = 2");

	[Test]
	public Task Trailing_whitespace_between_nodes_is_dropped() =>
		Formats(
			"<Project>   \n  <PropertyGroup />  \n  \n</Project>",
			"""
			<Project>
			  <PropertyGroup />

			</Project>
			""");

	[Test]
	public Task Indent_follows_indent_size() =>
		Formats(
			"""
			<Project>
			  <PropertyGroup>
			    <A>1</A>
			  </PropertyGroup>
			</Project>
			""",
			"""
			<Project>
			    <PropertyGroup>
			        <A>1</A>
			    </PropertyGroup>
			</Project>
			""",
			editorConfig: "indent_size = 4");

	[Test]
	public Task Tabs_indent_when_asked() =>
		Formats(
			"""
			<Project>
			  <PropertyGroup>
			    <A>1</A>
			  </PropertyGroup>
			</Project>
			""",
			"<Project>\n\t<PropertyGroup>\n\t\t<A>1</A>\n\t</PropertyGroup>\n</Project>",
			editorConfig: "indent_style = tab");

	[Test]
	public Task Leading_whitespace_before_the_root_is_dropped() =>
		Formats(
			"\n\n<Project>\n</Project>",
			"""
			<Project>
			</Project>
			""");

	/// <summary>Multi-line comments are not re-indented inside in this version.</summary>
	[Test]
	public Task Comments_move_with_their_line_but_keep_their_inner_lines() =>
		Formats(
			"""
			<Project>
			      <!-- one
			           two -->
			  <PropertyGroup />
			</Project>
			""",
			"""
			<Project>
			  <!-- one
			           two -->
			  <PropertyGroup />
			</Project>
			""");

	[Test]
	public Task A_comment_after_a_node_on_its_line_stays_there() =>
		Unchanged(
			"""
			<Project>
			  <PropertyGroup /> <!-- why -->
			</Project>
			""");

	[Test]
	public Task Mixed_content_is_left_as_written() =>
		Unchanged(
			"""
			<doc>
			  <p>Some <b>bold</b>
			      and <i>italic</i> text.</p>
			</doc>
			""",
			editorConfig: "curb_xml_extensions = .xml",
			fileName: "Test.xml");

	[Test]
	public Task Cdata_is_left_as_written() =>
		Unchanged(
			"""
			<Project>
			  <Target Name="T">
			    <Exec Command="x" />
			    <Message Text="m"><![CDATA[
			        a  <b>
			    ]]></Message>
			  </Target>
			</Project>
			""");

	[Test]
	public Task Entity_references_stay_raw() =>
		Unchanged(
			"""
			<Project>
			  <PropertyGroup>
			    <A>a &amp; b &lt; &#x41; &#65; &quot;</A>
			  </PropertyGroup>
			</Project>
			""");

	[Test]
	public Task A_defined_entity_is_accepted_when_there_is_a_doctype() =>
		Unchanged(
			"""
			<!DOCTYPE doc [
			  <!ENTITY e "x > y">
			  <!-- ] > -->
			]>
			<doc>&e;</doc>
			""",
			editorConfig: "curb_xml_extensions = .xml",
			fileName: "Test.xml");

	[Test]
	public Task Xml_space_preserve_keeps_a_subtree_as_written() =>
		Formats(
			"""
			<root>
			      <keep xml:space="preserve">
			            <a/>
			      </keep>
			      <b>
			            <c/>
			      </b>
			</root>
			""",
			"""
			<root>
			  <keep xml:space="preserve">
			            <a/>
			      </keep>
			  <b>
			    <c/>
			  </b>
			</root>
			""",
			editorConfig: "curb_xml_extensions = .xml",
			fileName: "Test.xml");

	[Test]
	public Task Declaration_and_processing_instructions_are_left_as_written() =>
		Unchanged(
			"""
			<?xml version="1.0"   encoding="utf-8"?>
			<?xml-stylesheet  href="a.xsl"?>
			<Project>
			</Project>
			""");

	/// <summary>The printer trims only whitespace it owns; a text node's trailing spaces are content.</summary>
	[Test]
	public Task Trailing_spaces_inside_text_survive_trim_trailing_whitespace() =>
		Formats(
			"<Project>\n<PropertyGroup>\n<A>x   \n  y</A>\n</PropertyGroup>\n</Project>",
			"<Project>\n  <PropertyGroup>\n    <A>x   \n  y</A>\n  </PropertyGroup>\n</Project>",
			editorConfig: "trim_trailing_whitespace = true");

	[Test]
	public Task Crlf_is_kept_when_end_of_line_is_unset() =>
		Formats(
			"<Project>\r\n<PropertyGroup />\r\n</Project>\r\n",
			"<Project>\r\n  <PropertyGroup />\r\n</Project>\r\n");

	/// <summary>An XML parser reads every line ending as \n, so rewriting the ones inside text changes nothing it reports.</summary>
	[Test]
	public Task End_of_line_rewrites_every_line_break() =>
		Formats(
			"<Project>\r\n  <PropertyGroup>\r\n    <A>a\r\nb</A>\r\n  </PropertyGroup>\r\n</Project>\r\n",
			"<Project>\n  <PropertyGroup>\n    <A>a\nb</A>\n  </PropertyGroup>\n</Project>\n",
			editorConfig: "end_of_line = lf");

	[Test]
	public Task A_missing_final_newline_stays_missing_when_unset() =>
		Unchanged(
			"""
			<Project>
			</Project>
			""");

	[Test]
	public Task Insert_final_newline_adds_one() =>
		Formats(
			"""
			<Project>
			</Project>
			""",
			"<Project>\n</Project>\n",
			editorConfig: "insert_final_newline = true");

	[Test]
	public Task Insert_final_newline_false_removes_it() =>
		Formats(
			"<Project>\n</Project>\n\n",
			"""
			<Project>
			</Project>
			""",
			editorConfig: "insert_final_newline = false");

	[Test]
	public Task Spaces_between_attributes_on_one_line_collapse() =>
		Formats(
			"""
			<Project>
			  <Import   Project="a.props"    Condition="true"  />
			  <Target Name="T"  >
			  </Target>
			</Project>
			""",
			"""
			<Project>
			  <Import Project="a.props" Condition="true" />
			  <Target Name="T">
			  </Target>
			</Project>
			""");

	[Test]
	public Task Attribute_values_and_quotes_are_left_as_written() =>
		Unchanged(
			"""
			<Project>
			  <Target Name='T' Condition=" '$(A)' == 'b'  ">
			    <Exec Command="echo &quot;x&quot;   y" />
			  </Target>
			</Project>
			""");

	/// <summary>A value's own continuation lines are content and do not move.</summary>
	[Test]
	public Task Broken_attributes_keep_their_offset_from_a_moved_element() =>
		Formats(
			"""
			<Project>
			      <Target Name="T"
			              Condition="'$(A)' == 'b'
			                         and '$(C)' == 'd'"
			              DependsOnTargets="X">
			      </Target>
			</Project>
			""",
			"""
			<Project>
			  <Target Name="T"
			          Condition="'$(A)' == 'b'
			                         and '$(C)' == 'd'"
			          DependsOnTargets="X">
			  </Target>
			</Project>
			""");

	[Test]
	public Task Aligned_attributes_under_an_element_that_did_not_move_are_unchanged() =>
		Unchanged(
			"""
			<Project>
			  <Target Name="T"
			          DependsOnTargets="X" />
			</Project>
			""");

	[Test]
	public Task Single_indent_puts_broken_attributes_one_level_in() =>
		Formats(
			"""
			<Project>
			  <Target Name="T"
			          DependsOnTargets="X"
			          Inputs="I" />
			</Project>
			""",
			"""
			<Project>
			  <Target Name="T"
			    DependsOnTargets="X"
			    Inputs="I" />
			</Project>
			""",
			editorConfig: "xml_attribute_indent = single_indent");

	[Test]
	public Task Double_indent_puts_broken_attributes_two_levels_in() =>
		Formats(
			"""
			<Project>
			  <Target Name="T"
			          DependsOnTargets="X" />
			</Project>
			""",
			"""
			<Project>
			  <Target Name="T"
			      DependsOnTargets="X" />
			</Project>
			""",
			editorConfig: "xml_attribute_indent = double_indent");

	[Test]
	public Task Align_by_first_attribute_lines_attributes_up() =>
		Formats(
			"""
			<Project>
			  <Target Name="T"
			    DependsOnTargets="X"
			                  Inputs="I" />
			</Project>
			""",
			"""
			<Project>
			  <Target Name="T"
			          DependsOnTargets="X"
			          Inputs="I" />
			</Project>
			""",
			editorConfig: "xml_attribute_indent = align_by_first_attribute");

	[Test]
	public Task A_close_on_its_own_line_stays_there() =>
		Formats(
			"""
			<Project>
			    <Target
			        Name="T"
			    >
			    </Target>
			</Project>
			""",
			"""
			<Project>
			  <Target
			      Name="T"
			  >
			  </Target>
			</Project>
			""");

	/// <summary>On by default, so a project file keeps the author's choice rather than gaining a space on every reference.</summary>
	[Test]
	public Task Self_closing_is_kept_as_written_in_msbuild_files() =>
		Unchanged(
			"""
			<Project>
			  <Import Project="a"/>
			  <Import Project="b" />
			</Project>
			""");

	[Test]
	public Task Self_closing_space_can_be_added_to_msbuild_files() =>
		Formats(
			"""
			<Project>
			  <Import Project="a"/>
			</Project>
			""",
			"""
			<Project>
			  <Import Project="a" />
			</Project>
			""",
			editorConfig: "xml_space_before_self_closing = true");

	[Test]
	public Task Self_closing_is_kept_as_written_in_generic_xml() =>
		Unchanged(
			"""
			<configuration>
			  <a/>
			  <b />
			</configuration>
			""",
			fileName: "nuget.config");

	[Test]
	public Task Self_closing_space_can_be_removed() =>
		Formats(
			"""
			<Project>
			  <Import Project="a" />
			</Project>
			""",
			"""
			<Project>
			  <Import Project="a"/>
			</Project>
			""",
			editorConfig: "xml_space_before_self_closing = false");

	[Test]
	public Task Self_closing_space_can_be_added_to_generic_xml() =>
		Formats(
			"""
			<configuration>
			  <a/>
			</configuration>
			""",
			"""
			<configuration>
			  <a />
			</configuration>
			""",
			editorConfig: "xml_space_before_self_closing = true",
			fileName: "nuget.config");

	[Test]
	public Task A_start_tag_that_does_not_fit_breaks_one_attribute_per_line() =>
		Formats(
			"""
			<Project>
			  <PackageReference Include="Some.Long.Package.Name" Version="1.2.3" PrivateAssets="all" />
			  <Short A="1" />
			</Project>
			""",
			"""
			<Project>
			  <PackageReference
			    Include="Some.Long.Package.Name"
			    Version="1.2.3"
			    PrivateAssets="all" />
			  <Short A="1" />
			</Project>
			""",
			editorConfig: "max_line_length = 60");

	[Test]
	public Task Width_aligned_by_first_attribute() =>
		Formats(
			"""
			<Project>
			  <PackageReference Include="Some.Long.Package.Name" Version="1.2.3" PrivateAssets="all" />
			</Project>
			""",
			"""
			<Project>
			  <PackageReference Include="Some.Long.Package.Name"
			                    Version="1.2.3"
			                    PrivateAssets="all" />
			</Project>
			""",
			editorConfig: """
			max_line_length = 60
			xml_attribute_indent = align_by_first_attribute
			""");

	[Test]
	public Task Width_never_joins_a_tag_the_author_broke() =>
		Unchanged(
			"""
			<Project>
			  <Import Project="a"
			          Condition="true" />
			</Project>
			""",
			editorConfig: "max_line_length = 120");

	[Test]
	public Task Width_leaves_a_tag_with_a_multi_line_value_alone() =>
		Unchanged(
			"""
			<Project>
			  <Target Name="T" Condition="'$(A)' == 'b' and '$(LongPropertyName)' == 'd'
			          or '$(E)' == 'f'" />
			</Project>
			""",
			editorConfig: "max_line_length = 40");

	[Test]
	public Task Width_does_not_touch_content() =>
		Unchanged(
			"""
			<Project>
			  <PropertyGroup>
			    <Description>A description that is a great deal longer than the width asked for, left alone.</Description>
			  </PropertyGroup>
			</Project>
			""",
			editorConfig: "max_line_length = 40");

	[Test]
	public Task A_solution_in_visual_studios_shape_is_unchanged() =>
		Unchanged(
			"""
			<Solution>
			  <Folder Name="/build/">
			    <File Path="build.sh" />
			  </Folder>
			  <Project Path="src/A/A.csproj" />
			  <Project Path="src/B/B.csproj">
			    <BuildDependency Project="src/A/A.csproj" />
			  </Project>
			</Solution>
			""",
			fileName: "Test.slnx");

	/// <summary>Visual Studio rewrites solution files in this shape, so any other indent would be undone.</summary>
	[Test]
	public Task A_solution_is_indented_with_two_spaces_whatever_the_configuration_says() =>
		Formats(
			"<Solution>\n\t<Folder Name=\"/a/\">\n\t\t<Project Path=\"a.csproj\"/>\n\t</Folder>\n</Solution>",
			"""
			<Solution>
			  <Folder Name="/a/">
			    <Project Path="a.csproj" />
			  </Folder>
			</Solution>
			""",
			editorConfig: """
			indent_style = tab
			indent_size = 4
			""",
			fileName: "Test.slnx");

	[Test]
	public Task A_solution_is_never_reordered() =>
		Unchanged(
			"""
			<Solution>
			  <Project Path="z.csproj" />
			  <Project Path="a.csproj" />
			</Solution>
			""",
			fileName: "Test.slnx");

	[Test]
	public Task A_mismatched_end_tag_is_refused() =>
		Rejects(
			"""
			<Project><A></B></Project>
			""");

	[Test]
	public Task An_unterminated_element_is_refused() =>
		Rejects(
			"""
			<Project><A></Project>
			""");

	[Test]
	public Task An_unterminated_tag_is_refused() =>
		Rejects(
			"""
			<Project><A x="1"
			""");

	[Test]
	public Task A_duplicate_attribute_is_refused() =>
		Rejects(
			"""
			<Project><A x="1" x="2"/></Project>
			""");

	[Test]
	public Task A_document_without_a_root_is_refused() =>
		Rejects(
			"""
			<!-- nothing -->
			""");

	[Test]
	public Task Two_roots_are_refused() =>
		Rejects(
			"""
			<A/><B/>
			""");

	[Test]
	public Task Text_outside_the_root_is_refused() =>
		Rejects(
			"""
			<A/>text
			""");

	[Test]
	public Task A_comment_between_attributes_is_refused() =>
		Rejects(
			"""
			<Project><A x="1" <!-- c --> y="2"/></Project>
			""");

	[Test]
	public Task Double_hyphen_in_a_comment_is_refused() =>
		Rejects(
			"""
			<Project><!-- a -- b --></Project>
			""");

	[Test]
	public Task A_less_than_in_an_attribute_value_is_refused() =>
		Rejects(
			"""
			<Project><A x="a<b"/></Project>
			""");

	[Test]
	public Task An_unquoted_attribute_is_refused() =>
		Rejects(
			"""
			<Project><A x=1/></Project>
			""");

	[Test]
	public Task Attributes_without_whitespace_between_them_are_refused() =>
		Rejects(
			"""
			<Project><A x="1"y="2"/></Project>
			""");

	[Test]
	public Task An_undefined_entity_is_refused() =>
		Rejects(
			"""
			<Project><A>&nbsp;</A></Project>
			""");

	[Test]
	public Task A_bare_ampersand_is_refused() =>
		Rejects(
			"""
			<Project><A>a & b</A></Project>
			""");

	[Test]
	public Task An_undeclared_prefix_is_refused() =>
		Rejects(
			"""
			<Project><x:A/></Project>
			""");

	[Test]
	public Task A_declaration_after_the_start_is_refused() =>
		Rejects(
			"\n<?xml version=\"1.0\"?><Project/>");

	[Test]
	public Task A_forbidden_character_is_refused() =>
		Rejects(
			"<Project>\u0001</Project>");

	[Test]
	public Task An_end_tag_with_nothing_open_is_refused() =>
		Rejects(
			"""
			<Project/></Project>
			""");

	[Test]
	public Task A_cdata_end_in_text_is_refused() =>
		Rejects(
			"""
			<Project>a ]]> b</Project>
			""");
}
