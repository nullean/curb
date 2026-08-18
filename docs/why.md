---
navigation_title: Why Kerf
description: Why a .NET developer should use Kerf — fast, non-fighting, and runs inside dotnet build.
---

# Why Kerf

Kerf runs inside `dotnet build`. It formats your source before the compiler reads it, so formatting
offences never become build errors. Neither you nor a coding agent has to spend time on brace
placement.

## It's fast

350 ms CPU on a 1,196-file, 6.5 MB corpus — within measurement noise of the Roslyn parse floor.
`dotnet format whitespace` on the same files: ~12,000 ms. CSharpier: ~14,000 ms.

Speed is what makes running on every build viable. The [build integration](workflow/msbuild.md)
skips unchanged projects entirely; on a project where nothing changed, no process starts.

## It doesn't fight what you already run

{{product}}'s defaults are Roslyn's defaults — the same values Visual Studio and Rider use for Format
Document. Its output is a 100% fixed point of `dotnet format whitespace`, measured and gated in CI on
every push. Hit Format Document in your IDE after {{product}} ran and nothing moves.

It reads the `.editorconfig` you already have — all 39 IDE0055 formatting options, plus the ReSharper
wrapping and blank-line keys Rider already reads. It invents no keys of its own.

## Turn on EnforceCodeStyleInBuild without fear

```xml
<EnforceCodeStyleInBuild>true</EnforceCodeStyleInBuild>
```

Without a formatter in the build, this setting turns every misformatted file into a build error. With
{{product}}, the syntax-level offences are gone before the compiler runs. The diagnostics that survive
are the ones that needed a compilation to decide — worth fixing, not noise.

## Capability table

| | `dotnet format whitespace` | `dotnet format style` | **{{product}}** |
|---|---|---|---|
| Runs on a bare folder, no restore or build | ✅ | ❌ | ✅ |
| All 39 IDE0055 formatting options | ✅ | ✅ | ✅ |
| Reflow to `max_line_length` | ❌ | ❌ | ✅ |
| Syntax-level code style (braces, expression bodies, file-scoped namespaces) | ❌ | ✅ | ✅ |
| Semantic code style (`var`, unused usings, naming) | ❌ | ✅ | ❌ *by design* |

The last row is the scope boundary. {{product}} never loads a compilation, and that is what keeps it
fast enough to run inside every build.

## Read more

- [Design principles](design-principles/index.md) — parser-only, arena IR, full reprint, safety
- [Reflow](design-principles/reflow.md) — what `max_line_length` does and what each mode costs
- [Performance](design-principles/performance.md) — the numbers and why they hold
- [Conformance](design-principles/conformance.md) — how the `dotnet format` fixed-point is measured
- [Benchmarks](benchmarks/index.md) — twelve repositories, three tools, measured
