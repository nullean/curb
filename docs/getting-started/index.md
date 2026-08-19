---
navigation_title: Getting started
description: Install Kerf, format a folder, and configure it with the .editorconfig you already have.
---

# Getting started

## Install

As a global tool:

```sh
dotnet tool install -g Nullean.Kerf
```

Ships as a native-AOT binary for `linux-x64`, `linux-arm64`, `win-x64`, `win-arm64` and `osx-arm64`,
with a portable fallback for everything else. About 10 ms to start. Installing requires the .NET 10 SDK.

To have your build do the formatting instead — which is usually what you want — see
[the build integration](../workflow/msbuild.md).

## Format something

```sh
kerf format ./src     # rewrite files in place
kerf check ./src      # exit non-zero if anything would change
```

Out of the box {{product}} uses Roslyn's defaults, which are the same defaults Visual Studio and Rider
use. On a repository that is already IDE0055-clean, `kerf format` should change nothing.

## Turn on reflow

Reflow is opt-in. Add `max_line_length = 120` to your `.editorconfig` and {{product}} wraps long
lines; omit it and line lengths are never changed. The first run on an existing repository is a large
commit. See [Reflow](../design-principles/reflow.md) for what the key does, what each mode costs, and
which ReSharper wrapping keys need a width.

## Configure it

There is no second config file to learn. {{product}} reads your `.editorconfig`:

```ini
[*.cs]
indent_style = tab
max_line_length = 120                      # omit, or set `off`, for no reflow and preserved line breaks
csharp_new_line_before_open_brace = all
csharp_space_after_cast = false
csharp_preserve_single_line_blocks = true
csharp_prefer_braces = true
csharp_style_namespace_declarations = file_scoped
```

All 39 IDE0055 formatting options are supported, plus the 8 core EditorConfig keys, plus a set of
syntax-level code style and wrapping options — around 90 keys in total.

Unrecognised or not-yet-implemented keys are **reported, not silently ignored**, with a "did you mean"
suggestion for likely typos. Semantic code style keys are passed over deliberately, because they belong
to a tool that loads a compilation.

## See what it resolved

When output surprises you, this is the first thing to run:

```sh
kerf print-config Program.cs
```

It prints every resolved option for that specific file, the value in force, and any diagnostics — so you
can see what your `.editorconfig` cascade actually produced rather than what you expected. Options are
resolved per file, not per directory, because a section can discriminate on filename.

## Commands

| Command | What it does |
|---|---|
| `kerf format <path>` | Format files in place. |
| `kerf check <path>` | Exit non-zero if anything would change. Writes nothing. |
| `kerf print-config <file>` | Show every resolved option for a file, and any diagnostics. |
| `kerf doc-tree <file>` | Dump the internal document IR. A debugging aid. |
| `kerf --version` | |

| Flag | Applies to | What it does |
|---|---|---|
| `-f`, `--files <path>` | `format`, `check` | Work on only these files, instead of walking a directory. Repeatable. |
| `--msbuild-list-file <path>` | `format`, `check` | Work on the paths listed in this file, one per line, instead of walking a directory. What the build integration passes, since a compile set can be too long for a command line. |
| `-c`, `--cache <path>` | `format`, `check` | Skip files a previous run watched format to themselves. The caller names the path — there is no ambient cache. The build integration uses `obj/kerf.cache`; a pre-commit hook uses `.git/kerf.cache`. Ignored with `--coverage`. |
| `--coverage` | `check` | Report which syntax kinds are still emitted verbatim, and how often. |
| `--no-verify` | `format`, `check` | Skip re-parsing the output to prove the token stream is unchanged. Not recommended — see [Safety](../design-principles/safety.md). |

`obj/` and `bin/` are skipped automatically.

## Exit codes

| Code | Meaning |
|---|---|
| `0` | Success. |
| `1` | `check` found files that would change. |
| `2` | Unknown command. |
| `3` | A file failed verification, or a named path did not exist. |

Only `1` means "your code needs formatting". Anything else means {{product}} did not do its job, which
is why the build integration treats them differently.

## Where next

- [Why {{product}}](../why.md) — why it's fast, why it doesn't fight `dotnet format`, and why running inside the build matters.
- [The two passes](../design-principles/syntax-and-semantic.md) — what {{product}} will and will not touch.
- [Integrations](../workflow/integrations.md) — the argument for putting this in
  your build rather than in your agent's instructions.
