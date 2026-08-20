---
navigation_title: CSharpier
description: How curb differs from CSharpier, and why the two are not direct replacements for each other.
---

# Why not CSharpier?

CSharpier ships with a fixed, opinionated style that cannot be changed through configuration. It reads
a small number of `.editorconfig` keys and ignores the rest — including nearly all of IDE0055. If your
team already has a style configured in Rider or Visual Studio, CSharpier will rewrite files to its own
choices rather than yours, and it will not stay in sync with Format Document in your IDE without
installing a separate editor plugin.

curb reads the complete IDE0055 formatting surface and the ReSharper wrapping and blank-line keys. Its
defaults are Roslyn's defaults — the same values Visual Studio and Rider already use. Format Document
and curb agree out of the box, with no extra plugins required.

## Your .editorconfig, or the tool's opinion

CSharpier ships with its own layout opinions. It reads a small number of `.editorconfig` keys and
ignores the rest. If your team had already configured Rider or your IDE with a particular style —
indentation, brace placement, spacing, blank lines — CSharpier will rewrite files to its own choices
rather than yours.

curb reads the complete IDE0055 formatting surface, the 8 core EditorConfig keys, and the ReSharper
wrapping and blank-line keys. The defaults are Roslyn's defaults, which are what Visual Studio and
Rider already use. On a repository that has configured the IDE, the first run changes
whitespace-within-lines and nothing about layout style.

What you set is what you get. curb invents no formatting keys.

## IDE compatibility without extra plugins

Because CSharpier does not follow IDE0055, Format Document in your IDE and CSharpier can disagree.
The recommended fix is to install a separate IDE extension (VS Code, Visual Studio, Rider) that makes
the editor format to CSharpier's style instead.

curb needs no extension. Since its rules are the same IDE0055 keys the editor already reads, Format
Document and curb agree without any additional setup.

## MSBuild integration

Both tools have an MSBuild package. The difference is in how the build integration works.

CSharpier's MSBuild package runs CSharpier on every build. Its incremental story relies on a
machine-global file hash cache:

```
$LocalApplicationData/CSharpier/.formattingCache
```

That cache is outside your repository and persists across `git clean`, `git checkout`, and fresh
checkouts. A CI runner or a colleague on a new machine has no cache, so every run on those machines
pays the full formatting cost.

curb's MSBuild target declares:

```xml
Inputs="@(Compile);@(EditorConfigFiles);$(MSBuildProjectFullPath)"
Outputs="$(Curb_StampFile)"
```

MSBuild evaluates the `Inputs` against the stamp before curb starts. On an untouched project — no
changed source files, no changed `.editorconfig`, no changed project file — the target is skipped
entirely and no process starts. Not "fast" — no process.

On a project where only one file changed, curb formats that one file, not the directory. The stamp
file lives in `$(IntermediateOutputPath)` (your `obj/` folder), is removed by `dotnet clean`, and
autoinvalidates after one week. There is no machine-global state to debug or warm up.

## Speed

These numbers are from the [twelve-repository comparison](../benchmarks/index.md).

**Cold** — cache cleared before each run. This is what CI sees on every run, and what a developer
sees on a fresh checkout. Both tools pay their full cost.

| repo | curb (cold) | CSharpier (cold) |
|---|---|---|
| serilog (216 files) | **0.06 s** | 0.64 s |
| FluentValidation (219 files) | **0.05 s** | 0.54 s |
| RestSharp (255 files) | **0.08 s** | 0.69 s |
| logging-log4net (376 files) | **0.14 s** | 1.21 s |
| AutoMapper (512 files) | **0.10 s** | 1.16 s |
| Humanizer (733 files) | **0.36 s** | 4.33 s |
| quartznet (765 files) | **0.25 s** | 1.90 s |
| Newtonsoft.Json (945 files) | **0.30 s** | 4.45 s |
| ServiceStack (4,718 files) | **1.11 s** | 11.48 s |
| MassTransit (5,502 files) | **0.56 s** | 4.23 s |
| efcore (5,761 files) | **2.15 s** | 19.12 s |
| roslyn (17,167 files) | **7.26 s** | 59.67 s |

Cold is the relevant baseline for CI environments and for anyone evaluating whether the tool is
fast enough to run on every build. curb is fast cold.

**Warm, nothing changed** — subsequent build where no source files were modified. curb evaluates the
MSBuild stamp and exits; CSharpier walks and hashes every file to check its cache.

| repo | curb | CSharpier (warm) |
|---|---|---|
| serilog | **no process** | 0.29 s |
| FluentValidation | **no process** | 0.19 s |
| RestSharp | **no process** | 0.34 s |
| logging-log4net | **no process** | 0.42 s |
| AutoMapper | **no process** | 0.42 s |
| Humanizer | **no process** | 0.46 s |
| quartznet | **no process** | 0.32 s |
| Newtonsoft.Json | **no process** | 0.22 s |
| ServiceStack | **no process** | 1.15 s |
| MassTransit | **no process** | 0.70 s |
| efcore | **no process** | 3.12 s |
| roslyn | **no process** | 5.12 s |

On unchanged projects curb starts no process at all. CSharpier must still walk the entire directory
tree on every build to update its cache, even when nothing changed.

**Warm, files changed** — subsequent build where files were modified. curb uses `--cache` to skip
files whose output has not changed; CSharpier uses its file hash cache. Numbers below are worst-case:
all files in the project were treated as changed.

| repo | curb (warm) | CSharpier (warm) |
|---|---|---|
| serilog | **0.03 s** | 0.29 s |
| FluentValidation | **0.03 s** | 0.19 s |
| RestSharp | **0.06 s** | 0.34 s |
| logging-log4net | **0.05 s** | 0.42 s |
| AutoMapper | **0.04 s** | 0.42 s |
| Humanizer | **0.16 s** | 0.46 s |
| quartznet | **0.06 s** | 0.32 s |
| Newtonsoft.Json | **0.12 s** | 0.22 s |
| ServiceStack | **0.22 s** | 1.15 s |
| MassTransit | **0.20 s** | 0.70 s |
| efcore | **0.49 s** | 3.12 s |
| roslyn | **1.15 s** | 5.12 s |

Even in the worst case — every file changed — curb beats CSharpier warm on every repository. In
practice a typical build touches far fewer files, and curb reformats only those.

curb is FAST cold, FASTER warm.

The reason is architecture. curb builds a document IR in a pooled arena — structs, not objects,
reusing memory across files. It loads no workspace and resolves no symbols. CSharpier uses a
Prettier-style IR with per-file allocation, computes a content hash, and writes it to disk.

## Adoption

CSharpier requires committing to its built-in style. If your codebase currently has `.editorconfig`
formatting rules, CSharpier will change them — you either adopt CSharpier's opinions wholesale or do
not use it.

curb with no `.editorconfig` at all formats to Roslyn's defaults: the same output Visual Studio and
`dotnet format whitespace` produce. There is no style to buy into. Adopt it project by project, and
it agrees with what your IDE already does.

## Maturity

CSharpier has been around longer and has real-world adoption across a wide range of .NET projects.
It has proven itself in production, accumulated community knowledge, and the rough edges have been
filed down. That counts for something.

curb is new. The architecture is different, the goals are different, and the test suite is extensive
— curb has broad formatting test coverage and its output is CI-gated to be byte-identical to
`dotnet format whitespace` across 41,000 files — but it has not yet had the years of production use
that CSharpier has.

That said, if any of the above differences matter to you — IDE0055 compatibility, no editor plugin,
MSBuild-native incrementality, respecting your existing `.editorconfig` — we would love for you to
give curb a try and tell us what you find.

## When CSharpier makes sense

If you want zero configuration and are happy with CSharpier's built-in style, it is a reasonable
choice. If you have an existing `.editorconfig` — especially one configured through Rider or
JetBrains tooling — curb will honour it. CSharpier will not.
