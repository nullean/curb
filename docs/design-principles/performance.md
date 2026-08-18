---
navigation_title: Performance
description: How Kerf achieves parse-floor performance, and why that matters for running inside every build.
---

# Performance

Measured on a 6.5 MB corpus (1,196 files), as CPU time:

| | |
|---|---|
| Roslyn parse + full red tree — the floor for any tool | **~300 ms** |
| **{{product}}** | **~350 ms** |
| `dotnet format whitespace` | **~12,000 ms** |
| CSharpier | **~14,000 ms** |

Parsing is about 2.5% of the budget. Roughly 97% of what a formatter costs is its own work. {{product}} adds almost nothing on top of the parse: it is within measurement noise of the floor.

The [twelve-repository comparison](../contribute/formatter-comparison.md) has wall-clock numbers across real repositories; on roslyn (17,167 files) {{product}} takes **7 s** against `dotnet format`'s **36 s**.

## Why it is fast

The document IR lives in a pooled struct arena — structs, not objects, reusing memory across files. {{product}} loads no workspace and resolves no symbols. The conditional round-trip reparse (see [Safety](safety.md)) means most files pay for one parse, not two.

It ships as a native-AOT binary per platform, so there is no JIT warm-up: about 10 ms to start.

## Why parse-floor speed matters

The build integration runs {{product}} before `CoreCompile`. If formatting costs more than parse time, the build slows down. Because {{product}} adds almost nothing beyond the parse, a build with {{product}} is a build without it — measured rather than assumed.

On unchanged projects the MSBuild stamp means no process starts at all. The performance number is for the case where work actually happens.
