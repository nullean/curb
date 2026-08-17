# Kerf

A C# formatter that reads your `.editorconfig` — all of it.

Kerf reflows C# to a line width, the way Prettier does, while honouring the **complete set of .NET formatting
options** (code style rule [IDE0055](https://learn.microsoft.com/dotnet/fundamentals/code-analysis/style-rules/ide0055)).
Its defaults are Roslyn's defaults, so it agrees with Visual Studio and Rider out of the box instead of fighting them.

> **Status: early development.** Not yet usable. See [the milestones](#milestones).

## Why

Existing tools each solve one half of the problem:

- **`dotnet format`** implements IDE0055 faithfully — all 39 `csharp_*` / `dotnet_*` formatting keys — but never
  wraps a long line.
- **Prettier-style formatters** reflow beautifully but read only a handful of `.editorconfig` keys; every other
  layout decision is fixed by the tool.

So a team that has settled its house style in `.editorconfig` cannot adopt a reflowing formatter without
abandoning that style. Measured on [elastic/docs-builder](https://github.com/elastic/docs-builder) — 1,196 files,
193k LOC, a repo that is already 100% IDE0055-clean — a Prettier-style formatter rewrites **996 of 1,196 files**.
Kerf's goal on that same repo is **zero**, unless you ask for reflow.

| | `dotnet format whitespace` | `dotnet format style` | **Kerf** |
|---|---|---|---|
| Runs on a bare folder, no restore or build | ✅ | ❌ | ✅ |
| All 39 IDE0055 formatting options | ✅ | ✅ | ✅ |
| **Reflow to `max_line_length`** | ❌ | ❌ | ✅ |
| Syntax-only code style (braces, expression bodies, file-scoped namespaces) | ❌ | ✅ | ✅ |
| Semantic code style (unused usings, `var`, naming) | ❌ | ✅ | ⚠️ *`kerf cleanup`, from what a build reported* |

Kerf never *computes* semantics — no compilation, no semantic model — which is what keeps it fast and what lets it run
on a bare folder. `kerf cleanup` closes part of the gap without giving that up: it reads the diagnostics your build
already reported and applies the fixes derivable from a rule id and a span. So it fixes exactly what the build told you
about, which means nothing to silence and nothing changed in a repository that asked for nothing. See
[docs/cleanup.md](docs/cleanup.md). For the rest, `dotnet format style`.

## Install

```sh
dotnet tool install -g Nullean.Kerf
```

Ships as a native-AOT binary per platform (`linux-x64`, `linux-arm64`, `win-x64`, `win-arm64`, `osx-arm64`), with
a portable fallback. ~10 ms startup.

## Use

```sh
kerf format ./src          # format in place
kerf check ./src           # exit non-zero if anything would change
kerf print-config Foo.cs   # show every resolved option and where it came from

dotnet build && kerf cleanup   # fix the code style rules your build reported
kerf rules                     # which rules Kerf fixes, and which it does not
```

Configuration is your `.editorconfig` — there is no second config file to learn.

```ini
[*.cs]
indent_style = tab
max_line_length = 120                      # omit, or set `off`, to disable reflow entirely
csharp_new_line_before_open_brace = all
csharp_space_after_cast = false
csharp_preserve_single_line_blocks = true
```

Unrecognised or not-yet-implemented keys are reported rather than silently ignored.

## Design

- **Full re-print, not a whitespace patcher.** The source is parsed to a syntax tree and printed from scratch
  through a Wadler/Prettier-style document IR, which is what makes width-aware reflow possible.
- **Allocation-aware IR.** Documents live in a pooled, pointer-free arena and text leaves reference spans of the
  original source rather than allocating strings.
- **Safe by construction.** Every format is verified in memory: the tokens emitted must match the tokens parsed.
  A file that fails verification is reported and left untouched, never written.
- **Incomplete but never destructive.** Syntax Kerf does not yet have a printer for is emitted verbatim, so
  coverage grows without risk.

## Building from source

Requires the .NET 10 SDK.

```sh
git clone https://github.com/nullean/kerf.git
cd kerf
./build.sh build
./build.sh test
```

## Milestones

| | |
|---|---|
| M0 | Spike and scaffolding — **in progress** |
| M1 | Vertical slice: arena, printer, CLI, test harness |
| M2 | Syntax printer coverage |
| M3 | The 39 formatting options, onboarded one at a time |
| M4 | Parallelism, caching, conformance and corpus CI |
| M5 | Syntax-only code style rules |

## Credits

Kerf is an independent implementation, but it learns a great deal from
[CSharpier](https://github.com/belav/csharpier) and [Prettier](https://github.com/prettier/prettier). See
[NOTICE](NOTICE).

## License

MIT — see [LICENSE.txt](LICENSE.txt).
