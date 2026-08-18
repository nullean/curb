---
navigation_title: CSharpier
description: How Kerf differs from CSharpier, and why the two are not direct replacements for each other.
---

# Why not CSharpier?

CSharpier is a good formatter. It does Prettier-style reflow well and is fast for what it is. The comparison is useful precisely because the two overlap on the thing most people care about: long lines.

Where they differ is in what each one respects.

## Your .editorconfig, or the tool's opinion

CSharpier ships with its own layout opinions. It reads a small number of `.editorconfig` keys and ignores the rest. If your team had already configured Rider or your IDE with a particular style — indentation, brace placement, spacing, blank lines — CSharpier will rewrite files to its own choices rather than yours.

{{product}} reads the complete IDE0055 formatting surface, the 8 core EditorConfig keys, and the ReSharper wrapping and blank-line keys. The defaults are Roslyn's defaults, which are what Visual Studio and Rider already use. On a repository that has configured the IDE, the first run changes whitespace-within-lines and nothing about layout style.

What you set is what you get. {{product}} invents no formatting keys.

## No hidden on-disk cache

CSharpier writes a formatting cache to your machine's application data directory:

```
$LocalApplicationData/CSharpier/.formattingCache
```

That file is machine-global and outside your repository. It persists across `git clean`, `git checkout`, and repository deletion. A CI runner or a colleague on a fresh checkout has no cache, so every run pays the full formatting cost.

{{product}} has no such cache. Its only state is an MSBuild stamp file written to `$(IntermediateOutputPath)` — inside your project's `obj/` folder, removed by `dotnet clean`, shared with nothing. There is no cache to warm, invalidate, or debug.

## MSBuild native, not a post-build step

CSharpier does not integrate with MSBuild as a first-class build step. You run it as a separate command, typically in a pre-commit hook or a CI step after the build.

{{product}}'s MSBuild package declares:

```xml
Inputs="@(Compile);@(EditorConfigFiles);$(MSBuildProjectFullPath)"
Outputs="$(Kerf_StampFile)"
```

MSBuild evaluates the `Inputs` against the stamp before {{product}} starts. On an untouched project — no changed source files, no changed `.editorconfig`, no changed project file — the target is skipped entirely. No process starts. No directory walks. Nothing.

On a project where only one file changed, {{product}} formats that one file. Not the directory.

CSharpier has no equivalent: it walks the directory every time.

## Incremental builds, for real

The consequence of the above: in a large solution with dozens of projects, `dotnet build` with {{product}} costs nothing extra on the projects that did not change. Each project's stamp is evaluated independently.

This is what "MSBuild-native incrementality" means in practice. Not a cache that sometimes hits. Not a file hash database that has to be loaded and queried. MSBuild's own dependency tracking, which has been doing this correctly for decades.

## Speed

From the [twelve-repository comparison](../contribute/formatter-comparison.md), measured cold
(cache cleared before each run) and warm (cache populated from a prior run):

| repo | Kerf | CSharpier cold | CSharpier warm |
|---|---|---|---|
| serilog (216 files) | **0.06 s** | 0.73 s | 0.34 s |
| FluentValidation (219 files) | **0.06 s** | 0.59 s | 0.21 s |
| RestSharp (255 files) | **0.07 s** | 0.63 s | 0.30 s |
| Humanizer (733 files) | **0.37 s** | 3.45 s | 0.51 s |
| Newtonsoft.Json (945 files) | **0.26 s** | 4.85 s | 0.82 s |
| ServiceStack (4,718 files) | **1.29 s** | 12.84 s | 1.87 s |
| MassTransit (5,502 files) | **0.62 s** | 4.75 s | 0.83 s |
| efcore (5,761 files) | **2.27 s** | 19.78 s | 2.49 s |
| roslyn (17,167 files) | 6.66 s | 64.71 s | **6.01 s** |

Full numbers for all twelve repositories are in the [comparison table](../contribute/formatter-comparison.md).

Kerf beats CSharpier warm on all repositories through efcore (5,761 files). On roslyn, CSharpier
warm edges out Kerf's re-check time by about 10%. Both numbers above are re-checks of
already-formatted files: Kerf's first-ever run on roslyn is 8.5 s; CSharpier's is 65 s. Where Kerf
wins unconditionally: its MSBuild stamp means an unchanged project starts no process at all, while
CSharpier still walks 17,000 files every time.

The cache is cold on every CI runner and fresh checkout, so the cold column is what your team
actually pays unless developers run CSharpier repeatedly on the same unchanged code on the same
machine.

The reason is architecture. {{product}} builds a document IR in a pooled arena — structs, not
objects, reusing memory across files. It loads no workspace, resolves no symbols, and conditionally
re-parses only when the printer moved a token boundary (rarely). CSharpier uses a Prettier-style IR
with per-file allocation, computes a content hash, and writes it to disk. Both format; the overhead
of the cache path is non-trivial on a cold machine.

The MSBuild stamp widens the gap further: on an unchanged project Kerf starts no process at all.
CSharpier walks the directory regardless.

## When CSharpier makes sense

If you want zero configuration and are happy with CSharpier's built-in style, it is a reasonable choice. It is simpler to adopt and simpler to explain.

If you have an existing `.editorconfig` — especially one that came from Rider or JetBrains tooling — {{product}} will honour it. CSharpier will not.
