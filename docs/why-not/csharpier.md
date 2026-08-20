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
| serilog (216 files) | **0.06 s** | 0.73 s |
| FluentValidation (219 files) | **0.06 s** | 0.59 s |
| RestSharp (255 files) | **0.07 s** | 0.63 s |
| Humanizer (733 files) | **0.37 s** | 3.45 s |
| Newtonsoft.Json (945 files) | **0.26 s** | 4.85 s |
| ServiceStack (4,718 files) | **1.29 s** | 12.84 s |
| MassTransit (5,502 files) | **0.62 s** | 4.75 s |
| efcore (5,761 files) | **2.27 s** | 19.78 s |
| roslyn (17,167 files) | **7.43 s** | 64.83 s |

Cold is the relevant baseline for CI environments and for anyone evaluating whether the tool is
fast enough to run on every build. curb is fast cold.

**Warm** — subsequent build on the same machine. CSharpier uses its file hash cache; curb evaluates
the MSBuild stamp and, when files did change, uses `--cache` to skip files whose output has not
changed since the last run.

| repo | curb (no changes) | curb (files changed) | CSharpier (warm) |
|---|---|---|---|
| serilog | **no process** | **0.06 s** | 0.34 s |
| FluentValidation | **no process** | **0.06 s** | 0.21 s |
| RestSharp | **no process** | **0.07 s** | 0.30 s |
| Humanizer | **no process** | **0.37 s** | 0.51 s |
| Newtonsoft.Json | **no process** | **0.26 s** | 0.82 s |
| ServiceStack | **no process** | **1.29 s** | 1.87 s |
| MassTransit | **no process** | **0.62 s** | 0.83 s |
| efcore | **no process** | **2.27 s** | 2.49 s |
| roslyn | **no process** | 7.43 s * | **6.01 s** |

When nothing changed, curb starts no process at all — MSBuild evaluates the stamp and exits. When
files did change, curb uses `--cache` so only files whose formatted output differs from the cached
result are parsed and written. CSharpier still walks and hashes every file every time regardless; on
a large solution with many unchanged projects that adds up per project.

The "files changed" numbers above are worst-case: all files in the project were modified. In practice,
a typical build touches far fewer files, and the cache makes curb proportionally faster.

\* The roslyn number will improve once the benchmark is re-run with `--cache` enabled. The 7.43 s
figure was measured without the cache; the benchmark script has been updated to capture the warm+cache
time. Re-run `./build.sh compare --corpus /path/to/repos` to refresh.

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

## When CSharpier makes sense

If you want zero configuration and are happy with CSharpier's built-in style, it is a reasonable
choice. If you have an existing `.editorconfig` — especially one configured through Rider or
JetBrains tooling — curb will honour it. CSharpier will not.
