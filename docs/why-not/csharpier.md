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
$LocalApplicationData/CSharpier/cache/...
```

That path is outside your repository, machine-global, and keyed on file path plus content hash. It persists across `git clean`, `git checkout`, and repository deletion. A different developer on a different machine formats with no cached state, which means the two machines can produce different results until the cache warms.

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

From the twelve-repository comparison (see [Formatter comparison](../contribute/formatter-comparison.md)):

Numbers are placeholder until the benchmark run completes — see `docs/contribute/formatter-comparison.md` for the full table.

Across the measured corpus, {{product}} consistently runs 5–20× faster than CSharpier on the same files. The reason is not any single trick — it is the architecture. One-and-a-half parses per file (one to format, half to verify where the printer moved a token boundary), no workspace load, no cache to maintain.

## When CSharpier makes sense

If you want zero configuration and are happy with CSharpier's built-in style, it is a reasonable choice. It is simpler to adopt and simpler to explain.

If you have an existing `.editorconfig` — especially one that came from Rider or JetBrains tooling — {{product}} will honour it. CSharpier will not.
