---
navigation_title: EditorConfig analyzers
description: How Kerf works with EnforceCodeStyleInBuild and the IDE0055 analyzers, rather than around them.
---

# Why not just EnforceCodeStyleInBuild?

`EnforceCodeStyleInBuild = true` in your project tells the .NET SDK to run the Roslyn style analyzers inside `CoreCompile` and report IDE0055 and the code style rules as build diagnostics. It is the right thing to set. {{product}} is designed to work with it, not instead of it.

## What happens without Kerf

With `EnforceCodeStyleInBuild = true` and no formatter in the build, every mechanically unformatted file reports IDE0055. A developer who forgot to run `dotnet format` sees build errors — and to fix them, they have to run the command, check the output, commit again, and wait for CI to rerun. For a CI pipeline that runs tests after the build, the whole suite gets blocked on a missing semicolon.

## What happens with Kerf

{{product}} runs at `BeforeTargets="CoreCompile"`. By the time `CoreCompile` evaluates the analyzers, {{product}} has already rewritten every file it can format. The only IDE0055 diagnostics that survive are things {{product}} has not implemented — which are reported explicitly rather than silently skipped.

The MSBuild smoketest verifies both directions: building the sample with {{product}} in the path must succeed even with `EnforceCodeStyleInBuild = true` and deliberately misformatted source; building without {{product}} must report IDE0055 errors. Only the pair proves anything.

## The semantic remainder

{{product}} handles syntax-level code style — braces, expression bodies, modifier order, namespace declarations, using placement. It does not handle semantic rules: `var` decisions, naming conventions, unused members. Those genuinely need a compilation.

With both `EnforceCodeStyleInBuild` and {{product}} in place, the analyzer output you see in a build is the semantic remainder — things worth discussing, not things a formatter should have caught. That is what the split is for.

## Running the analyzers selectively

If a file should not be formatted by {{product}}, use `generated_code = true` or `dotnet_diagnostic.IDE0055.severity = none` in your `.editorconfig`. {{product}} honours both and skips the file. The analyzers still run unless you configure their severity separately.

## Diagnostics Kerf emits

{{product}} itself can produce build diagnostics through the MSBuild integration:

| Code | Meaning |
|---|---|
| `KERF0001` | Check mode: files would change. |
| `KERF0002` | {{product}} failed on a file — the file was left untouched. |

`KERF0002` is always an error. A formatter that could not verify its own work has verified nothing, and a quiet warning would hide that.

The `.editorconfig` diagnostics (KERF1001–KERF1007) appear in `kerf print-config` output and in the build log, not as MSBuild diagnostics.
