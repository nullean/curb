---
navigation_title: dotnet format
description: What Kerf adds on top of dotnet format, and where the two deliberately agree.
---

# Why not just dotnet format?

`dotnet format` is the right answer for teams that want IDE0055 formatting enforced as part of the build. {{product}} agrees with it — by design and by measurement — and adds three things on top.

## What dotnet format does

`dotnet format whitespace` implements all 39 `csharp_*` and `dotnet_*` formatting options from IDE0055. It normalises indentation, spacing, braces, and a dozen other whitespace decisions. It does it correctly, the way Roslyn does it, and it is the reference implementation.

What it does not do: wrap long lines. IDE0055 has no opinion about line width, so `dotnet format` has none either.

`dotnet format style` adds semantic code style — `var` decisions, naming conventions, unused member detection. It needs to load a compilation to do this, which costs roughly 40 seconds per project in a large solution.

## What Kerf adds

**Reflow.** Add `max_line_length = 120` to your `.editorconfig` and {{product}} will wrap long lines. `dotnet format` never will. This is the gap {{product}} was built to close.

**Syntax-level code style, without a compilation.** Several IDE analysers — IDE0011 (braces), IDE0022–0027 (expression bodies), IDE0036 (modifier order), IDE0065 (using placement), IDE0161 (file-scoped namespaces) — need a compilation in `dotnet format style` but are decidable from syntax alone. {{product}} applies them in the same pass as layout, without a workspace. On a large solution, that is 40-odd seconds saved per project per run.

**ReSharper formatting keys.** The `csharp_wrap_*`, `csharp_place_*`, `csharp_blank_lines_*` and `csharp_trailing_comma_*` families are read by Rider but not by `dotnet format`. {{product}} honours them — and the defaults leave them all off, so the configuration you already have is the configuration {{product}} uses.

**Speed.** On the docs-builder corpus (1,196 files, 6.5 MB):

| | |
|---|---|
| `dotnet format whitespace` | ~12,000 ms |
| {{product}} (same options, reflow off) | ~300–800 ms |

About 15× faster at the same task, on the same files. That gap is what makes it viable to run inside every build rather than as a separate step.

## Where they agree

{{product}}'s output is a fixed point of `dotnet format whitespace`. That is a CI-gated measurement, not a claim: run `dotnet format` over {{product}}'s output and nothing changes. The two tools will never fight each other in a repository that uses both — Format Document in the IDE will not undo what {{product}} wrote.

With reflow off, the match is 100%. With reflow on and deterministic layout, also 100%. With reflow on and `csharp_keep_existing_linebreaks = true`, 99.9% — one file, a property pattern, measured and held.

## Running together

The build integration ([workflow/msbuild.md](../workflow/msbuild.md)) puts {{product}} at `BeforeTargets="CoreCompile"`. When `EnforceCodeStyleInBuild` is set, the IDE analysers run inside `CoreCompile` and report whatever {{product}} could not fix — which is the semantic remainder. The two tools complement each other rather than duplicating work.
