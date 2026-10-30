---
navigation_title: Project and solution files
description: curb format also formats MSBuild project files, props and targets, .slnx solutions and nuget.config. What it changes, what it never touches, which settings it reads, and how to turn it off.
---

# Project and solution files

`curb format` and `curb check` format the XML next to your code as well as the code: `.csproj`, `.fsproj`,
`.vbproj`, `.proj`, `.props`, `.targets`, `.slnx` and `nuget.config`. It's on by default. Every change is
whitespace, but the first run does change files: on the repositories it was measured on, between a fifth
and two fifths of project files, a few lines each.

| Repository | Project files changed | Lines changed |
|---|---|---|
| elastic/docs-builder | 16 of 67 | 160 |
| dotnet/runtime (`eng/`, `src/libraries`) | 39 of 305 | 123 |
| dotnet/sdk (`targets/`) | 24 of 64 | 149 |
| dotnet/msbuild (`src/Tasks`) | 10 of 26 | 687 |
| dotnet/arcade (`tools/`) | 9 of 49 | 14 |
| dotnet/roslyn (`eng/`) | 8 of 42 | 19 |

## Upgrading

`curb check` fails on a repository whose project files have not been formatted yet. Either run
`curb format .` once and commit the result, or turn XML formatting off (see below) until you are ready.

## What changes

{{product}} keeps your layout. It owns two kinds of whitespace and nothing else:

| Whitespace | What {{product}} does |
|---|---|
| Between the child elements of an element that holds only elements and comments | Re-indents each line that starts a child, keeps your line breaks, and caps blank lines at `xml_max_blank_lines_between_tags` (1). Never adds a line break where you had none. |
| Inside a start tag | One space between attributes on the same line. An attribute you put on its own line keeps its offset from its element. Whitespace before `>` goes; before `/>` follows `xml_space_before_self_closing`. |

A line holding something that spans lines {{product}} cannot move (a property whose value is several
lines, a multi-line comment, an attribute value with a line break in it) keeps the columns it had, start
tag included, so the rest of it is not stranded.

Trailing whitespace on the lines it lays out stays unless `trim_trailing_whitespace = true` comes from a
section for your XML files.

With `max_line_length` set for your XML files, a start tag that does not fit and that you wrote on one
line breaks one attribute per line. Content is never reflowed.

Solution files get two-space indents and `<Project … />`, the shape Visual Studio writes and would put back.

## What never changes

- Text, CDATA, comments, processing instructions and the DOCTYPE.
- Attribute values and their quotes.
- The order of anything.
- Anything inside an element whose whitespace may be a value: a property, an item's metadata,
  `ProjectExtensions`, an inline task's `Task` body, and any `xml:space="preserve"` subtree. MSBuild reads
  `<Flag>\n true\n</Flag>` as `\n true\n`, not `true`.
- A file that is not UTF-8, or whose XML declaration names another encoding. It is reported as unparsable.

Every result is checked before it is written. {{product}} lexes the output again and compares it with the
input token by token; anything but the whitespace above is a verification failure, and the file is left as
it was. A file that is not well-formed XML is reported as unparsable and is never repaired.

## Which settings apply

XML reads the EditorConfig core keys (`indent_style`, `indent_size`, `tab_width`, `max_line_length`,
`end_of_line`, `insert_final_newline`, `trim_trailing_whitespace` and `charset`) only from sections that
name its files: a glob with an extension or a file name in it, such as `[*.csproj]`, `[*.{csproj,props}]`,
`[*.xml]`, `[Directory.Packages.props]` or `[src/**/*.props]`. A `[*]`, `[**]`, `[**/*]` or `[*.*]`
section is written for your code, and XML ignores those keys in it.

Without such a section XML uses its own defaults: two-space indents, no width, and the line endings, final
newline and trailing whitespace each file already has.

```ini
[*.{csproj,fsproj,vbproj,proj,props,targets}]
indent_size = 2
trim_trailing_whitespace = true
```

The `curb_xml_*` and `xml_*` keys are XML's by name, so they apply from any section, `[*]` included.

## Turning it off

Set `curb_xml_format = false` for the files you want left alone:

```ini
[*.{csproj,fsproj,vbproj,proj,props,targets,slnx,config}]
curb_xml_format = false
```

A file it applies to is not read and not counted. Other XML, such as `*.xml` or `*.runsettings`, is formatted
only when `curb_xml_extensions` names its extension.

The `curb` MSBuild package never formats XML. A build formats the C# it compiles, not the files driving the
build. Use `curb format` from the command line, a pre-commit hook or CI.

See the [option reference](../reference/index.md#xml-files) for every key.
