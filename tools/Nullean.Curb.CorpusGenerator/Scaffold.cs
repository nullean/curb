using System.Text;

namespace Nullean.Curb.CorpusGenerator;

/// <summary>
/// The files around the C#: solution, project files, props, editorconfig, and the non-built XML, F# and Razor samples.
/// Project files are written in a style drawn per file, so XML formatting sees the same spread of layouts the C# does.
/// </summary>
public static class Scaffold
{
	/// <summary>A docs-builder-like configuration: tabs, Allman, a width, the IDE0055 keys, some ReSharper keys.</summary>
	public const string EditorConfig =
		"""
		root = true

		[*]
		charset = utf-8
		indent_style = tab
		indent_size = 4

		[*.{fs,fsx,cs,vb}]
		file_header_template = Licensed to the Corpus authors under one or more agreements.\nThe Corpus authors license this file to you under the MIT license.
		max_line_length = 140

		[*.{fs,fsx}]
		indent_style = space
		indent_size = 4

		[*.{md,json,yml,yaml,csproj,fsproj,targets,props,slnx,config,proj}]
		indent_style = space
		indent_size = 2

		[*.{cs,vb}]
		trim_trailing_whitespace = true
		insert_final_newline = true

		dotnet_sort_system_directives_first = true
		dotnet_separate_import_directive_groups = false

		dotnet_style_qualification_for_field = false
		dotnet_style_qualification_for_property = false
		dotnet_style_predefined_type_for_locals_parameters_members = true
		dotnet_style_object_initializer = true
		dotnet_style_collection_initializer = true
		dotnet_style_coalesce_expression = true
		dotnet_style_null_propagation = true
		dotnet_style_require_accessibility_modifiers = for_non_interface_members
		dotnet_style_readonly_field = true

		[*.cs]
		csharp_style_var_for_built_in_types = true
		csharp_style_var_when_type_is_apparent = true
		csharp_style_var_elsewhere = true

		# csharp_style_expression_bodied_methods is left out: see KnownDefects, item 6.
		csharp_style_expression_bodied_constructors = true
		csharp_style_expression_bodied_operators = true
		csharp_style_expression_bodied_properties = true
		csharp_style_expression_bodied_indexers = true
		csharp_style_expression_bodied_accessors = true
		csharp_style_expression_bodied_local_functions = when_on_single_line

		csharp_style_pattern_matching_over_is_with_cast_check = true
		csharp_style_pattern_matching_over_as_with_null_check = true
		csharp_style_inlined_variable_declaration = true
		csharp_style_throw_expression = true
		csharp_style_conditional_delegate_call = true
		csharp_prefer_braces = false
		csharp_preferred_modifier_order = public,private,protected,internal,static,extern,new,virtual,abstract,sealed,override,readonly,unsafe,volatile,async

		# Allman
		csharp_new_line_before_open_brace = all
		csharp_new_line_before_else = true
		csharp_new_line_before_catch = true
		csharp_new_line_before_finally = true
		csharp_new_line_before_members_in_object_initializers = false
		csharp_new_line_before_members_in_anonymous_types = true
		csharp_new_line_between_query_expression_clauses = true

		csharp_indent_case_contents = true
		csharp_indent_switch_labels = true
		csharp_indent_labels = one_less_than_current
		csharp_space_after_cast = false
		csharp_space_after_keywords_in_control_flow_statements = true
		csharp_space_between_method_declaration_parameter_list_parentheses = false
		csharp_space_between_method_call_parameter_list_parentheses = false
		csharp_space_around_binary_operators = before_and_after

		csharp_preserve_single_line_statements = false
		csharp_preserve_single_line_blocks = true

		# ReSharper-derived wrapping
		csharp_place_simple_initializer_on_single_line = true
		csharp_max_initializer_elements_on_line = 5
		csharp_wrap_object_and_collection_initializer_style = chop_if_long
		csharp_wrap_chained_method_calls = chop_if_long
		csharp_wrap_chained_binary_expressions = chop_if_long
		csharp_wrap_before_binary_opsign = true
		csharp_empty_block_style = together_same_line
		resharper_csharp_keep_existing_attribute_arrangement = true

		csharp_style_namespace_declarations = file_scoped

		# The cleanup gate turns this back on; leaving it off here is what docs-builder did, and it exercises that.
		dotnet_diagnostic.IDE0005.severity = none
		dotnet_diagnostic.CA1822.severity = suggestion

		[tests/**/*.cs]
		dotnet_diagnostic.IDE0058.severity = none
		""";

	public const string DirectoryBuildProps =
		"""
		<Project>
		  <!-- Shared by every project in the corpus. Nothing here reaches outside the corpus directory. -->
		  <PropertyGroup>
		    <LangVersion>latest</LangVersion>
		    <Nullable>enable</Nullable>
		    <ImplicitUsings>enable</ImplicitUsings>
		    <TreatWarningsAsErrors>false</TreatWarningsAsErrors>
		    <IsPackable>false</IsPackable>
		    <!-- Fast local builds; CI turns analysers back on, as docs-builder does. -->
		    <RunAnalyzersDuringBuild Condition="'$(CI)' != 'true'">false</RunAnalyzersDuringBuild>
		    <NoWarn>$(NoWarn);CS1591;CS0169;CS0414;CS0649</NoWarn>
		  </PropertyGroup>
		  <PropertyGroup Condition="'$(Configuration)' == 'Release'">
		    <Optimize>true</Optimize>
		    <DebugType>portable</DebugType>
		  </PropertyGroup>
		</Project>
		""";

	public const string TestsDirectoryBuildProps =
		"""
		<Project>
		  <Import Project="$([MSBuild]::GetPathOfFileAbove('Directory.Build.props', '$(MSBuildThisFileDirectory)../'))" />
		  <PropertyGroup>
		    <IsTestProject>true</IsTestProject>
		    <OutputType>Exe</OutputType>
		    <WarningsAsErrors />
		  </PropertyGroup>
		</Project>
		""";

	public const string DirectoryPackagesProps =
		"""
		<Project>
		  <PropertyGroup>
		    <ManagePackageVersionsCentrally>true</ManagePackageVersionsCentrally>
		    <CentralPackageTransitivePinningEnabled>false</CentralPackageTransitivePinningEnabled>
		  </PropertyGroup>
		  <!-- Declared, not referenced: the corpus restores nothing, so it builds without a feed beyond the SDK's own packs. -->
		  <ItemGroup Label="Testing">
		    <PackageVersion Include="xunit.v3" Version="3.1.0" />
		    <PackageVersion Include="AwesomeAssertions" Version="9.2.0" />
		  </ItemGroup>
		  <ItemGroup Label="Runtime">
		    <PackageVersion Include="Microsoft.Extensions.Logging.Abstractions" Version="10.0.0" />
		    <PackageVersion Include="System.Text.Json" Version="10.0.0" />
		  </ItemGroup>
		</Project>
		""";

	public const string NuGetConfig =
		"""
		<?xml version="1.0" encoding="utf-8"?>
		<configuration>
		  <packageSources>
		    <clear />
		    <add key="nuget.org" value="https://api.nuget.org/v3/index.json" protocolVersion="3" />
		  </packageSources>
		</configuration>
		""";

	public const string Solution =
		"""
		<Solution>
		  <Folder Name="/build/">
		    <File Path=".editorconfig" />
		    <File Path="Directory.Build.props" />
		    <File Path="Directory.Packages.props" />
		  </Folder>
		  <Folder Name="/src/">
		    <Project Path="src/Corpus.Abstractions/Corpus.Abstractions.csproj" />
		    <Project Path="src/Corpus.Core/Corpus.Core.csproj" />
		    <Project Path="src/Corpus.Services/Corpus.Services.csproj" />
		    <Project Path="src/Corpus.Cli/Corpus.Cli.csproj" />
		  </Folder>
		  <Folder Name="/tests/">
		    <Project Path="tests/Corpus.Tests/Corpus.Tests.csproj" />
		  </Folder>
		</Solution>
		""";

	/// <summary>A project file, described as properties and items, written in a style drawn from <paramref name="r"/>.</summary>
	public static string Project(
		Rng r,
		string sdk,
		IReadOnlyList<(string Name, string Value)> properties,
		IReadOnlyList<(string Item, string Include, string? Metadata)> items
	)
	{
		var unit = r.Pick("  ", "  ", "    ", "\t");
		var b = new StringBuilder();
		if (r.Chance(0.2))
		{
			b.Append("<?xml version=\"1.0\" encoding=\"utf-8\"?>\n");
		}
		b.Append("<Project Sdk=\"").Append(sdk).Append("\">\n");
		if (r.Chance(0.3))
		{
			b.Append('\n');
		}

		b.Append(unit).Append("<PropertyGroup>\n");
		foreach (var (name, value) in properties)
		{
			if (r.Chance(0.1))
			{
				b.Append(unit).Append(unit).Append("<!-- ").Append(name).Append(" -->\n");
			}
			b.Append(unit).Append(unit).Append('<').Append(name).Append('>').Append(value).Append("</").Append(name).Append(">\n");
		}

		b.Append(unit).Append("</PropertyGroup>\n");

		foreach (var group in items.GroupBy(i => i.Item))
		{
			b.Append(r.Chance(0.6) ? "\n" : "").Append(unit).Append("<ItemGroup>\n");
			foreach (var (item, include, metadata) in group)
			{
				var attributesOnTheirOwnLine = metadata is null && r.Chance(0.1);
				b.Append(unit).Append(unit).Append('<').Append(item);
				b.Append(attributesOnTheirOwnLine ? "\n" + unit + unit + unit : " ").Append("Include=\"").Append(include).Append('"');
				if (metadata is null)
				{
					b.Append(r.Chance(0.8) ? " />\n" : "></" + item + ">\n");
				}
				else
				{
					b.Append(">\n").Append(unit).Append(unit).Append(unit).Append(metadata).Append('\n');
					b.Append(unit).Append(unit).Append("</").Append(item).Append(">\n");
				}
			}

			b.Append(unit).Append("</ItemGroup>\n");
		}

		b.Append("</Project>\n");
		var text = b.ToString();
		if (r.Chance(0.15))
		{
			text = text.Replace("\n", "\r\n");
		}
		return r.Chance(0.1) ? "\uFEFF" + text : text;
	}

	/// <summary>Project files nothing builds, for the XML formatter and xmlsafety: inline tasks, CDATA, preserved space, extensions.</summary>
	public static IEnumerable<(string Path, string Text)> XmlSamples()
	{
		yield return ("samples/xml/Legacy.proj", """
			<?xml version="1.0" encoding="utf-8"?>
			<Project DefaultTargets="Build" xmlns="http://schemas.microsoft.com/developer/msbuild/2003">
			    <PropertyGroup>
			        <Configuration Condition=" '$(Configuration)' == '' ">Debug</Configuration>
			        <OutputPath>bin\$(Configuration)\</OutputPath>
			        <Banner>
			  keep   this   spacing
			        </Banner>
			    </PropertyGroup>

			    <ItemGroup>
			        <Inputs Include="**\*.txt" Exclude="bin\**;obj\**" />
			        <Inputs Include="extra.txt"><Kind>manual</Kind></Inputs>
			    </ItemGroup>
			  <UsingTask TaskName="Shout" TaskFactory="RoslynCodeTaskFactory" AssemblyFile="$(MSBuildToolsPath)\Microsoft.Build.Tasks.Core.dll">
			    <ParameterGroup><Text ParameterType="System.String" Required="true" /></ParameterGroup>
			    <Task>
			      <Code Type="Fragment" Language="cs"><![CDATA[
			    Log.LogMessage(MessageImportance.High, Text.ToUpperInvariant());
			  ]]></Code>
			    </Task>
			  </UsingTask>
			    <Target Name="Build" DependsOnTargets="Prepare"   Inputs="@(Inputs)" Outputs="$(OutputPath)stamp">
			        <Shout Text="building $(Configuration)" />
			        <Message Text="  two  spaces  " Importance="high" xml:space="preserve" />
			    </Target>
			    <Target Name="Prepare"><MakeDir Directories="$(OutputPath)" /></Target>
			    <ProjectExtensions>
			        <VisualStudio>   <UserProperties   a="b" /></VisualStudio>
			    </ProjectExtensions>
			</Project>
			""");

		yield return ("samples/xml/Shared.props", """
			<Project>
				<PropertyGroup>
					<Company>Corpus</Company>
				<Product>Corpus samples</Product>
						<Description>Indented   by   someone   else</Description>
				</PropertyGroup>
				<ItemGroup Condition="'$(TargetFramework)' == 'net10.0'"><Using Include="System.Text" /></ItemGroup>
			</Project>
			""");

		yield return ("samples/xml/Packaging.targets", """
			<Project>
			  <Target Name="StampVersion" BeforeTargets="Build" Condition="'$(StampVersion)' == 'true'">
			    <PropertyGroup><_Stamp>$([System.DateTime]::UtcNow.ToString(yyyyMMdd))</_Stamp></PropertyGroup>
			    <WriteLinesToFile File="$(IntermediateOutputPath)version.txt" Lines="$(Version)-$(_Stamp)" Overwrite="true" />


			    <!-- two blank lines above on purpose -->
			  </Target>
			</Project>
			""");

		yield return ("samples/xml/Sample.csproj", """
			<Project Sdk="Microsoft.NET.Sdk">
			<Import Project="Shared.props" />
			<PropertyGroup><TargetFramework>net10.0</TargetFramework><OutputType>Library</OutputType>
			<RootNamespace>Corpus.Samples</RootNamespace></PropertyGroup>
			<ItemGroup>
			<None Include="readme.txt" CopyToOutputDirectory="PreserveNewest"/>
			<Compile Remove="obsolete/**" />
			</ItemGroup>
			<Import Project="Packaging.targets" />
			</Project>
			""");

		yield return ("samples/xml/app.config", """
			<?xml version="1.0" encoding="utf-8" ?>
			<configuration>
			<appSettings>
			<add key="mode" value="sample"/>
			    <add key="retries"   value="3" />
			</appSettings>
			</configuration>
			""");
	}

	public static IEnumerable<(string Path, string Text)> FSharpSamples()
	{
		yield return ("samples/fsharp/Corpus.Scripts.fsproj", """
			<Project Sdk="Microsoft.NET.Sdk">
			  <PropertyGroup>
			    <TargetFramework>net10.0</TargetFramework>
			  </PropertyGroup>
			  <ItemGroup>
			    <Compile Include="Pricing.fs" />
			    <Compile Include="Program.fs" />
			  </ItemGroup>
			</Project>
			""");

		yield return ("samples/fsharp/Pricing.fs", """
			module Pricing

			type Line = { Sku: string; Quantity: int; UnitPrice: decimal }

			let total (lines: Line list) =
			    lines |> List.sumBy (fun l -> decimal l.Quantity * l.UnitPrice)

			let discount total =
			  match total with
			  | t when t > 1000m -> t * 0.9m
			  | t -> t
			""");

		yield return ("samples/fsharp/Program.fs", """
			module Program
			open Pricing

			[<EntryPoint>]
			let main argv =
			    let lines = [ { Sku = "a"; Quantity = 2; UnitPrice = 1.5m } ; { Sku="b";Quantity=1;UnitPrice=9m } ]
			    printfn "%M" (lines |> total |> discount)
			    0
			""");
	}

	public static IEnumerable<(string Path, string Text)> RazorSamples(Rng r)
	{
		yield return ("samples/razor/.editorconfig", """
			[*.{razor,cshtml}]
			curb_razor_format_code_blocks = true
			""");

		for (var i = 0; i < 12; i++)
		{
			var name = $"Component{i:D2}";
			yield return ($"samples/razor/{name}.razor", $$"""
				@page "/{{name.ToLowerInvariant()}}"
				@using Corpus.Abstractions

				<h1>{{name}}</h1>

				@if (customers.Count == 0)
				{
				    <p>Nothing yet.</p>
				}
				else
				{
				    <ul>
				        @foreach (var c in customers)
				        {
				            <li>@c.Name</li>
				        }
				    </ul>
				}

				<button @onclick="Add">Add</button>

				@code {
				  private List<Customer> customers=new();
				    [Parameter] public int Start { get; set; } = {{r.Next(0, 10)}};

				  private void Add( )
				  {
				    customers.Add(new Customer(customers.Count+Start,"new",null,Status.Pending));
				  }
				}
				""");
		}

		yield return ("samples/razor/Index.cshtml", """
			@page
			@model IndexModel
			<h2>@Model.Title</h2>
			@functions {
			    public string Shout(string text)=>text.ToUpperInvariant( );
			}
			""");
	}
}
