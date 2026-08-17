---
navigation_title: The build integration
description: Nullean.Kerf.MSBuild rewrites your source before the compiler reads it. How it works and every property it exposes.
---

# The build integration

```xml
<PackageReference Include="Nullean.Kerf.MSBuild" Version="*" PrivateAssets="all" />
```

That is the whole setup. From then on `dotnet build` formats the project's source before compiling it.

The package is build-only — `netstandard2.0`, no library, `DevelopmentDependency`, nothing added to your
output. Put it in `Directory.Build.props` to apply it across a solution.

## Why it runs before the compiler

The target is `BeforeTargets="CoreCompile"`, and the ordering is the entire point. From the targets file:

> {{product}} runs before the compiler, not after it.
>
> The point of this ordering is `EnforceCodeStyleInBuild`. With that property set, the IDE analysers run
> inside `CoreCompile` and report IDE0055 and the code style rules as build diagnostics. A target at
> `BeforeTargets="CoreCompile"` has already rewritten the files by then, so the compiler reads formatted
> source and the only style diagnostics left are the ones that genuinely need a compilation to decide.
> Someone — or something — building the project gets the mechanical offences fixed underneath them and
> only has to think about the semantic remainder.
>
> Running after the build, or as a separate command someone has to remember, would not do that.

Running *after* the compiler would still report every mechanical offence as a diagnostic first, which is
precisely the cost this is meant to remove. Running as a separate command means someone has to remember,
and eventually someone does not.

## Rewrite or check

The default depends on configuration, because the two situations want opposite behaviour: a developer
wants their file fixed, and a release or CI build wants to be told, not edited.

| Configuration | Default behaviour |
|---|---|
| `Debug` (and anything not `Release`) | rewrite the file |
| `Release` | check only; fail the build if anything would change |

Override with `Kerf_Check` when you want the other one.

```sh
dotnet build -c Release -p:Kerf_Check=false   # rewrite even in Release
dotnet build -p:Kerf_Check=true               # check without rewriting
```

## Properties

| Property | Default | What it does |
|---|---|---|
| `Kerf_Check` | `true` in `Release`, else `false` | Check instead of rewriting. A check that finds unformatted files raises KERF0001. |
| `Kerf_Bypass` | `false` | Skip {{product}} entirely — no process start, no stamp file, nothing. The single escape hatch for a build that must not be touched. |
| `Kerf_UnformattedAsWarnings` | `false` | Report KERF0001 as a warning instead of an error. Off by default, because a check that does not fail the build is a check nobody notices. |
| `Kerf_LogLevel` | `low` | MSBuild message importance for {{product}}'s own output: `high`, `normal` or `low`. Errors and warnings are raised as diagnostics regardless. |
| `Kerf_Exe` | *unset* | Path to a native {{product}} binary. Roughly a hundred times faster to start than the framework-dependent build the package carries. |
| `Kerf_Dll` | the bundled CLI | The framework-dependent build shipped in the package, run on the SDK doing the build. |
| `Kerf_StampFile` | `$(IntermediateOutputPath)kerf.stamp` | Incrementality stamp. |
| `Kerf_FileList` | `$(IntermediateOutputPath)kerf.files` | The compile set handed to the CLI. |

## Diagnostics

| Code | Severity | Meaning |
|---|---|---|
| `KERF0001` | error, or warning with `Kerf_UnformattedAsWarnings` | Some files are not formatted. Only `check` can produce this. |
| `KERF0002` | error, always | {{product}} itself failed. This is an error whatever the warnings setting says — a formatter that could not run has verified nothing, and saying so quietly would be worse than not running at all. |

## What it costs on an untouched build

Nothing at all. The target declares `Inputs="@(Compile);@(EditorConfigFiles);$(MSBuildProjectFullPath)"`
against an output stamp, so MSBuild skips it entirely when none of those changed — no process start, no
directory walk, no file reads.

The project file is an input because changing it can change which files are compiled. The
`.editorconfig` files are inputs because changing one changes the answer for every file they govern.

There is deliberately no formatting cache. A formatter that re-scans every file on every build needs one
to hide the cost; not needing one is better than having a fast one.

Only the files the project actually compiles are passed, written to a list file and handed over with
`--files`. Passing the folder would reach sources belonging to another project, or to none.

## Making it faster

The package bundles a framework-dependent build of the CLI rather than five native binaries, because
carrying all of them would be roughly 60 MB of which any given machine needs a fifth.

If the per-build process start matters to you — in a large solution it can — install the native tool and
point at it:

```xml
<PropertyGroup>
  <Kerf_Exe>$(HOME)/.dotnet/tools/kerf</Kerf_Exe>
</PropertyGroup>
```

## Turning it off

```sh
dotnet build -p:Kerf_Bypass=true
```

One property, honoured everywhere, so a build that must not be touched has a single thing to set.
{{product}} also skips design-time builds and restore-only invocations automatically, so your IDE is
never fighting you as you type.
