---
navigation_title: Project and solution files
description: curb format also formats MSBuild project files, props and targets, .slnx solutions and nuget.config. What it changes, what it never touches, and how to turn it off.
---

# Project and solution files

`curb format` and `curb check` format the XML next to your code as well as the code: `.csproj`, `.fsproj`,
`.vbproj`, `.proj`, `.props`, `.targets`, `.slnx` and `nuget.config`. It's on by default and reads the same
`.editorconfig`. On a project file Visual Studio or a person wrote, expect no change, or whitespace only.

## What changes

{{product}} keeps your layout. It owns two kinds of whitespace and nothing else:

| Whitespace | What {{product}} does |
|---|---|
| Between the child elements of an element that holds only elements and comments | Re-indents each line that starts a child, keeps your line breaks, and caps blank lines at `xml_max_blank_lines_between_tags` (1). Never adds a line break where you had none. |
| Inside a start tag | One space between attributes on the same line. An attribute you put on its own line keeps its offset from its element. Whitespace before `>` goes; before `/>` follows `xml_space_before_self_closing`. |

With `max_line_length` set, a start tag that does not fit and that you wrote on one line breaks one
attribute per line. Content is never reflowed.

Solution files get two-space indents and `<Project … />`, the shape Visual Studio writes and would put back.

## What never changes

- Text, CDATA, comments (apart from where their first line starts), processing instructions and the DOCTYPE.
- Attribute values and their quotes.
- The order of anything.
- Anything inside an element whose whitespace may be a value: a property, an item's metadata,
  `ProjectExtensions`, an inline task's `Task` body, and any `xml:space="preserve"` subtree. MSBuild reads
  `<Flag>\n true\n</Flag>` as `\n true\n`, not `true`.

Every result is checked before it is written. {{product}} lexes the output again and compares it with the
input token by token; anything but the whitespace above is a verification failure, and the file is left as
it was. A file that is not well-formed XML is reported as unparsable and is never repaired.

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
