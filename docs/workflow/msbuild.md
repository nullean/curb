---
navigation_title: The build integration
description: curb rewrites your source before the compiler reads it. How it works and every property it exposes.
---

# The build integration

```xml
<PackageReference Include="curb" Version="*" PrivateAssets="all" />
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

Override with `Curb_Check` when you want the other one.

```sh
dotnet build -c Release -p:Curb_Check=false   # rewrite even in Release
dotnet build -p:Curb_Check=true               # check without rewriting
```

## Properties

| Property | Default | What it does |
|---|---|---|
| `Curb_Check` | `true` in `Release`, else `false` | Check instead of rewriting. A check that finds unformatted files raises CURB0001. |
| `Curb_Bypass` | `false` | Skip {{product}} entirely — no process start, no stamp file, nothing. The single escape hatch for a build that must not be touched. |
| `Curb_UnformattedAsWarnings` | `false` | Report CURB0001 as a warning instead of an error. Off by default, because a check that does not fail the build is a check nobody notices. |
| `Curb_LogLevel` | `low` | MSBuild message importance for {{product}}'s own output: `high`, `normal` or `low`. Errors and warnings are raised as diagnostics regardless. |
| `Curb_Exe` | *unset* | Path to a native {{product}} binary. Roughly a hundred times faster to start than the framework-dependent build the package carries. |
| `Curb_Dll` | the bundled CLI | The framework-dependent build shipped in the package, run on the SDK doing the build. |
| `Curb_StampFile` | `$(IntermediateOutputPath)curb.stamp` | Incrementality stamp. |
| `Curb_Cache` | `true` | Reuse the previous run's verdict for files that have not changed. Set `false` to build without a cache. |
| `Curb_CacheFile` | `$(IntermediateOutputPath)curb.cache` | Where that cache lives. |
| `Curb_FileList` | `$(IntermediateOutputPath)curb.files` | The compile set handed to the CLI. |
| `Curb_UnformattedFile` | `$(IntermediateOutputPath)curb.unformatted` | The paths `check` reports back as unformatted, one per line — what the target reads to attach CURB0001 to each of them. |

## Diagnostics

| Code | Severity | Meaning |
|---|---|---|
| `CURB0001` | error, or warning with `Curb_UnformattedAsWarnings` | A file is not formatted. Only `check` can produce this, and it is raised once per unformatted file, attached to that file — not to the project — so a GitHub Actions annotation names the file a reviewer needs to look at. |
| `CURB0002` | error, always | {{product}} itself failed. This is an error whatever the warnings setting says — a formatter that could not run has verified nothing, and saying so quietly would be worse than not running at all. |

## How incrementality works

There are two layers, and the first one matters more.

### Defence 1 — MSBuild stamp

The target declares `Inputs="@(Compile);@(EditorConfigFiles);$(MSBuildProjectFullPath)"` against an
output stamp. When none of those changed, MSBuild skips the target entirely — no process start, no
directory walk, no file reads.

The project file is an input because changing it can change which files are compiled. `.editorconfig`
files are inputs because changing one changes the answer for every file they govern.

This is the common case on every build after the first.

### Defence 2 — formatting cache

Once the target does run — because one file changed — the cache decides how much work {{product}} does.
Without it, a project where one file out of eight hundred changed re-parses the other seven hundred and
ninety-nine only to conclude they were already formatted. With it, those files cost a hash comparison
rather than a parse.

The cache lives at `$(IntermediateOutputPath)curb.cache` and is passed to the CLI as `--cache`. It
records, per file, that {{product}} ran the formatter over exactly those bytes under exactly those
resolved options and got them back unchanged. A file whose bytes moved, or whose `.editorconfig` answer
moved, is not in it and gets formatted normally.

It earns the most with `Curb_Check=true`. A failing check never stamps, so the target re-runs on every
build until someone formats the file. With the cache, those re-runs cost one file rather than the whole
project.

The cache is in `FileWrites`, so `dotnet clean` removes it along with the stamp.

Two things it does not do. It never records a file {{product}} just rewrote — only one it was watched to
leave alone — so an idempotency bug still shows up as a file that keeps changing. And it does not skip
reading source: the key is the file's content, so every file is still read, just not parsed.

{{product}} has no ambient cache under a user profile directory. The caller names the path or there is no
cache — one nobody named is one nobody can find, clear, or reason about.

## Making it faster

The package bundles a framework-dependent build of the CLI rather than five native binaries, because
carrying all of them would be roughly 60 MB of which any given machine needs a fifth.

If the per-build process start matters to you — in a large solution it can — install the native tool and
point at it:

```xml
<PropertyGroup>
  <Curb_Exe>$(HOME)/.dotnet/tools/curb</Curb_Exe>
</PropertyGroup>
```

## Turning it off

```sh
dotnet build -p:Curb_Bypass=true
```

One property, honoured everywhere, so a build that must not be touched has a single thing to set.
{{product}} also skips design-time builds and restore-only invocations automatically, so your IDE is
never fighting you as you type.
