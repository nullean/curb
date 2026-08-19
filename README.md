# Curb

A C# formatter that reads your `.editorconfig` — all of it.

Curb reflows C# to a line width, the way Prettier does, while honouring the **complete set of .NET formatting
options** (code style rule [IDE0055](https://learn.microsoft.com/dotnet/fundamentals/code-analysis/style-rules/ide0055)).
Its defaults are Roslyn's defaults, so it agrees with Visual Studio and Rider out of the box instead of fighting them.

📖 **[Documentation](https://nullean.github.io/curb/)** — why it exists, the scope boundary, and the build integration.

## Why

Existing tools each solve one half of the problem:

- **`dotnet format`** implements IDE0055 faithfully — all 39 `csharp_*` / `dotnet_*` formatting keys — but never
  wraps a long line.
- **Prettier-style formatters** reflow beautifully but read only a handful of `.editorconfig` keys; every other
  layout decision is fixed by the tool.

So a team that has settled its house style in `.editorconfig` cannot adopt a reflowing formatter without
abandoning that style. Curb's answer is that **`max_line_length` is the whole decision**:

- **No `max_line_length`.** Curb is an IDE0055 whitespace formatter. It never changes a line's length, and its
  output is byte-identical to `dotnet format whitespace` on every file of the corpus below.
- **`max_line_length` set.** You have asked Curb to decide layout, so it decides all of it: line breaks are a
  function of your tokens and your width, not of where the previous author happened to press return. Formatting
  is idempotent by construction, and the ReSharper wrap keys become available to tune the result.

One switch, two coherent behaviours. `csharp_keep_existing_linebreaks = true` alongside a width is the opt-out
if you want reflow but want your own arrangement kept.

### What it costs, measured

On [elastic/docs-builder](https://github.com/elastic/docs-builder) — 1,196 files, 193k LOC, already 100%
IDE0055-clean — against a Prettier-style formatter's **996 of 1,196 files** rewritten:

| Curb on that repo | Files | Changed lines | Fixed point of `dotnet format` |
|---|---|---|---|
| no width | 742 | 17,035 | 1196/1196 |
| `max_line_length = 160` (what the repo sets) | 892 | ~47,000 | 1196/1196 |
| `= 160` plus the preservation opt-out | 742 | 17,580 | 1195/1196 |

Two things worth being straight about. The no-width number is **not zero** — it is 742 files, and almost all of
it is Curb collapsing runs of blank lines to one. And file count flatters the width column: it is +20% by files
but roughly 2.7× by changed lines, because only that column rewraps anything. Both numbers are reproduced by
`./build.sh churn`, and neither is a projection.

| | `dotnet format whitespace` | `dotnet format style` | **Curb** |
|---|---|---|---|
| Runs on a bare folder, no restore or build | ✅ | ❌ | ✅ |
| All 39 IDE0055 formatting options | ✅ | ✅ | ✅ |
| **Reflow to `max_line_length`** | ❌ | ❌ | ✅ |
| Syntax-only code style (braces, expression bodies, file-scoped namespaces) | ❌ | ✅ | ✅ |
| Semantic code style (unused usings, `var`, naming) | ❌ | ✅ | ⚠️ *`curb cleanup`, from what a build reported* |

Curb never *computes* semantics — no compilation, no semantic model — which is what keeps it fast and what lets it run
on a bare folder. `curb cleanup` closes part of the gap without giving that up: it reads the diagnostics your build
already reported and applies the fixes derivable from a rule id and a span. So it fixes exactly what the build told you
about, which means nothing to silence and nothing changed in a repository that asked for nothing. See
[docs/cleanup.md](docs/cleanup.md). For the rest, `dotnet format style`.

## Install

```sh
dotnet tool install -g Nullean.Curb
```

Ships as a native-AOT binary per platform (`linux-x64`, `linux-arm64`, `win-x64`, `win-arm64`, `osx-arm64`), with
a portable fallback. ~10 ms startup.

## Use

```sh
curb format ./src          # format in place
curb check ./src           # exit non-zero if anything would change
curb print-config Foo.cs   # show every resolved option and where it came from

dotnet build && curb cleanup   # fix the code style rules your build reported
curb rules                     # which rules Curb fixes, and which it does not
```

Configuration is your `.editorconfig` — there is no second config file to learn.

```ini
[*.cs]
indent_style = tab
max_line_length = 120                      # omit, or set `off`, for no reflow and preserved line breaks
csharp_keep_existing_linebreaks = true     # reflow, but keep the breaks you wrote
csharp_new_line_before_open_brace = all
csharp_space_after_cast = false
csharp_preserve_single_line_blocks = true
```

`curb print-config Foo.cs` prints every resolved option, and says which layout mode you are in and what
selected it — worth running first on a repository you are about to reformat.

Unrecognised or not-yet-implemented keys are reported rather than silently ignored.

### Or let the build do it

```xml
<PackageReference Include="curb" Version="*" PrivateAssets="all" />
```

Curb then runs before `CoreCompile`, so the compiler reads source that is already formatted — rewriting
in `Debug`, checking in `Release`. With `EnforceCodeStyleInBuild` set, the only style diagnostics left
are the ones that genuinely need a compilation to decide. Whoever is editing the code — a person or a
coding agent — gets the mechanical offences fixed underneath them and only has to think about the
semantic remainder. See
[the build integration](https://nullean.github.io/curb/workflow/msbuild/).

## Design

- **Full re-print, not a whitespace patcher.** The source is parsed to a syntax tree and printed from scratch
  through a Wadler/Prettier-style document IR, which is what makes width-aware reflow possible.
- **Allocation-aware IR.** Documents live in a pooled, pointer-free arena and text leaves reference spans of the
  original source rather than allocating strings.
- **Safe by construction.** Every format is verified in memory: the tokens emitted must match the tokens parsed.
  A file that fails verification is reported and left untouched, never written.
- **Incomplete but never destructive.** Syntax Curb does not yet have a printer for is emitted verbatim, so
  coverage grows without risk.

## Building from source

Requires the .NET 10 SDK.

```sh
git clone https://github.com/nullean/curb.git
cd curb
./build.sh build
./build.sh test
```

## Milestones

| | | |
|---|---|---|
| M0 | Spike and scaffolding | ✅ |
| M1 | Vertical slice: arena, printer, CLI, test harness | ✅ |
| M2 | Syntax printer coverage | ✅ — `UnhandledNode` remains the safety net; `curb check --coverage` reports where |
| M3 | The 39 formatting options, onboarded one at a time | ✅ — all 39, plus 43 further keys |
| M4 | Parallelism, conformance and corpus CI | ✅ |
| M5 | Syntax-only code style rules | ✅ — braces, expression bodies, file-scoped namespaces, modifier order, using placement, file headers |

Not done: redundant parentheses (IDE0047/0048) and the `wrap_if_long` fill layout. Several ReSharper
option families were built, measured and deliberately reverted — see
[docs/contribute/layout-decisions.md](docs/contribute/layout-decisions.md).

### What CI gates on every push

Against [elastic/docs-builder](https://github.com/elastic/docs-builder) — 1,196 files, 6.5 MB:

- Byte-identical to `dotnet format whitespace` with reflow off (**100%**), and **99.9%** with reflow on.
- Zero failed and zero unparsable files; two format passes produce identical output.
- Native-AOT publish on all five RIDs, each smoke-tested before packing.
- An allocation-ratio ceiling, measured on the AOT binary rather than the JIT build.

## Credits

Curb is an independent implementation, but it learns a great deal from
[CSharpier](https://github.com/belav/csharpier) and [Prettier](https://github.com/prettier/prettier). See
[NOTICE](NOTICE).

## License

MIT — see [LICENSE.txt](LICENSE.txt).
