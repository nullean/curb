---
name: curb
description: >-
  Use when a .NET project references the curb package, when a build fails with "file is not formatted" from curb, or
  when deciding how C# formatting is enforced. curb formats C# from .editorconfig before the compiler runs, matching
  the IDE's own formatting (IDE0055).
---

# curb (MSBuild)

The package runs curb before `CoreCompile` on every `dotnet build`:

- **Debug**: rewrites unformatted files in place. Just build; the code you wrote comes out formatted.
- **Release**: checks instead, and **fails the build** naming the unformatted files.

So after editing C#, run a Debug `dotnet build` and the formatting is done. Do not hand-format, and do not run
`dotnet format whitespace` as well: curb already applies the same `.editorconfig` options, and the two can disagree on
layout curb reflows.

```xml
<PackageReference Include="curb" PrivateAssets="all" />
```

| Property | Default | Use |
|---|---|---|
| `Curb_Check` | `true` in Release, else `false` | `true` to check without rewriting |
| `Curb_Bypass` | `false` | `true` to skip curb entirely for one build |
| `Curb_UnformattedAsWarnings` | `false` | report unformatted files as warnings, not errors |
| `Curb_Cache` | `true` | reuse the previous verdict for unchanged files |
| `Curb_LogLevel` | `low` | `normal`/`high` to see curb's summary line |

## Gotchas

- A Release build failing on formatting is fixed by building in Debug once (or `curb format`), not by setting
  `Curb_Bypass`.
- Formatting is driven by `.editorconfig`; `max_line_length` turns on reflow. Change options there, not in code.
- Style rules that need a compilation (unused usings, `var`, naming) are not curb's: they stay IDE diagnostics.
  The `curb-cli` tool's `curb cleanup` fixes them from a build's report.
- The package never formats Razor files.

Docs: https://curb.nullean.net/workflow/msbuild/
