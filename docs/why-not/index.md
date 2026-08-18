---
navigation_title: Why not X?
description: How Kerf relates to the formatters and tools you probably already use.
---

# Why not X?

The short answer: you probably do not have to choose.

{{product}} is built around the `.editorconfig` you already have and the defaults Roslyn already ships. It is a fixed point of `dotnet format whitespace` — run `dotnet format` over its output and nothing changes. When IDE analysers fire in a build with `EnforceCodeStyleInBuild`, {{product}} has already fixed everything it can see before the compiler read the file. It works *with* the tools on this page, not instead of them.

What is different is what each tool covers:

| | {{product}} | `dotnet format` | CSharpier | Rider/ReSharper cleanup |
|---|---|---|---|---|
| Layout + reflow | ✅ | ❌ (whitespace only) | ✅ | ✅ |
| All 39 IDE0055 formatting options | ✅ | ✅ | read only a few | ✅ |
| No build needed | ✅ | ✅ (whitespace) / ❌ (style) | ✅ | ❌ |
| Runs inside `dotnet build` | ✅ | ❌ | ❌ | ❌ |
| Reads ReSharper `.editorconfig` keys | ✅ | ❌ | ❌ | ✅ |
| On-disk cache | ❌ | ❌ | ✅ | — |
| MSBuild incremental (no-op when nothing changed) | ✅ | ❌ | ❌ | ❌ |

The four pages below go into each comparison in detail.
