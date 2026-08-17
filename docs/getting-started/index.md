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

Reflow is the one thing {{product}} does that no other .NET formatter does, and it is opt-in.

```ini
[*.cs]
max_line_length = 120
```

Omit the key, or set it to `off`, and {{product}} will never wrap a line for you.

This key does two things, and the second one surprises people: it sets the width, and it hands
{{product}} the line breaks. With a width, where a line breaks is decided from your tokens and that
width — not from where the previous author pressed return. That is what makes formatting idempotent by
construction, and it is what unlocks the ReSharper wrapping keys.

It also means the first run on an existing repository is a large commit: on a 1,196-file corpus, 892
files against 669 with no width — and about three times the changed lines, since only this mode rewraps
anything. If you want reflow without that, keep your own arrangement:

```ini
[*.cs]
max_line_length = 120
csharp_keep_existing_linebreaks = true
```

`kerf print-config Foo.cs` says which mode you are in and what selected it. [Reflow](../concepts/reflow.md)
has the full picture, including which keys need a width.

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
| `--files <list>` | `format`, `check` | Work on the paths listed in a file, one per line, instead of walking a directory. This is what the build integration uses. |
| `--coverage` | `check` | Report which syntax kinds are still emitted verbatim, and how often. |
| `--no-verify` | `format`, `check` | Skip re-parsing the output to prove the token stream is unchanged. Not recommended — see [Safety](../concepts/safety.md). |

`obj/` and `bin/` are skipped automatically.

## Exit codes

| Code | Meaning |
|---|---|
| `0` | Success. |
| `1` | `check` found files that would change. |
| `2` | Unknown command. |
| `3` | A file failed verification, or `--files` was given without a list. |

Only `1` means "your code needs formatting". Anything else means {{product}} did not do its job, which
is why the build integration treats them differently.

## Where next

- [Why {{product}}](../why.md) — the problem it solves, with the measurements.
- [The two passes](../concepts/index.md) — what {{product}} will and will not touch.
- [Style enforcement that costs no context](../workflow/ai-native.md) — the argument for putting this in
  your build rather than in your agent's instructions.
