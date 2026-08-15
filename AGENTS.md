# Kerf — contributor and agent conventions

Kerf formats C# from a `.editorconfig`, honouring the full IDE0055 option surface while adding
width-aware reflow. It ships as a native-AOT `dotnet tool`.

## Build and test

```sh
./build.sh build          # dotnet build -c Release
./build.sh test           # runs tests/Nullean.Kerf.Tests via `dotnet run`
./build.sh benchmark      # BenchmarkDotNet suite
./build.sh generatepackages
```

Requires the .NET 10 SDK (pinned in `global.json`). Test projects are `OutputType=Exe` and run on
Microsoft.Testing.Platform, so they are launched with `dotnet run`, not `dotnet test`.

## Layout

| Path | What |
|---|---|
| `src/Nullean.Kerf.Core` | The engine: document IR, layout printer, syntax printers, verifier. **No IO, no config source.** |
| `src/Nullean.Kerf.EditorConfig` | Binds `.editorconfig` to formatting options. Owns the `IFileSystem` dependency. |
| `src/Nullean.Kerf.Cli` | The `kerf` tool. Native-AOT, packed per RID. |
| `tests/Nullean.Kerf.Tests` | Golden files, option matrix, document-IR units, `MockFileSystem` tests. |
| `tests/Nullean.Kerf.Benchmarks` | BenchmarkDotNet, `[MemoryDiagnoser]`. |
| `examples/kerf-aot-smoketest` | Publishes AOT and runs; the CI gate on every RID. Never packed. |
| `build/scripts` | F# Bullseye build runner. |

## Non-negotiables

1. **Fast, low allocation.** Measured: Roslyn's parse floor on a 6.5 MB corpus is ~300 ms CPU and
   ~16.5× source in allocations. CSharpier spends ~14,000 ms of CPU on that same corpus and
   `dotnet format` ~12,000 ms, so **parsing is ~2.5% of the budget and ~97% is our code**. There is
   no excuse for being slow.
2. **All filesystem access through `System.IO.Abstractions.IFileSystem`.** There must be no direct
   `System.IO.File` / `Directory` call anywhere outside a test. `MockFileSystem` has to work.
3. **Native-AOT compatible.** No reflection, no dynamic codegen. The smoke test enforces it.
4. **Never destroy code.** Every format is verified before anything is written. A file that fails
   verification is reported and left untouched.

## Hot-path rules (Roslyn)

These are the difference between fast and slow. Enforce them in review:

- **Never call `token.Text`** — it allocates a string per token. Use `token.Span` and slice the source.
- **Never call `node.ToString()` / `ToFullString()` / `syntaxTree.ToString()`** — each copies.
- **Prefer typed properties** (`node.OpenBraceToken`, `node.Statements`) over `ChildNodes()`, which
  allocates an enumerable. Avoid `DescendantNodes()` in printers.
- **No LINQ in the printer hot path.** It is the most common source of silent per-node allocation.
- `SyntaxToken` and `SyntaxTrivia` are structs; `SyntaxNode` is a class and materialises lazily.
  Budget roughly one red node per syntax node touched — that is Roslyn's floor.
- **Benchmark the AOT binary, not the JIT build.** AOT carries ~35% on red-tree walks (no tiered
  re-JIT or dynamic PGO), so JIT numbers flatter hot paths.

## Adding a formatting option

One option should be a small, isolated change. The generated completeness test fails if you skip
the fixtures.

1. `src/Nullean.Kerf.Core/Options/OptionCatalog.cs` — one declaration (key, allowed values, default,
   bit slot, doc summary, `Implemented = true`).
2. The generator regenerates the accessor, parser, validator, diagnostics, `print-config` row and
   `docs/options.md`. **Do not hand-edit generated output.**
3. Add a helper in `Printing/CSharp/Spacing.cs` (or `BraceLines.cs` / `Indentation.cs`) that turns
   the option into a `Doc`.
4. Swap the hard-coded `" "` / `Doc.Line` / `Doc.Indent(…)` in 1–3 printers for the helper.
5. Add `tests/Nullean.Kerf.Tests/Fixtures/options/<key>/<value>.test`, one per allowed value.
   Generate expectations from `dotnet format` where possible — it is the reference implementation.

Printers must never read options directly; they call a helper. That is what keeps an option's
footprint to one place.

## Scope boundary

Kerf never loads a compilation and never uses the semantic model. That is what lets it run on a bare
folder with no restore and no build.

- **In scope:** IDE0055 formatting, plus syntax-only code style (braces, expression bodies,
  file-scoped namespaces, modifier order, redundant parentheses).
- **Out of scope, permanently:** anything needing binding — `var` (IDE0007/8), unused usings
  (IDE0005), unused members (IDE0051), readonly fields (IDE0044), naming (IDE1006). Report these and
  point the user at `dotnet format style`.

## Correctness

- Golden fixtures with **no `.expected.test` companion must format to themselves.** This makes
  idempotency assertions free.
- Every fixture also asserts: the token-coverage verifier passes, the re-parse token comparer passes,
  `format(format(x)) == format(x)`, and the `#if` symbol-set loop passes.
- The conformance gate: with `max_line_length = off`, Kerf output must be **byte-identical to
  `dotnet format whitespace`**. That number is the product claim; keep it at 100%.

## Style

Tabs, Allman braces, file-scoped namespaces, `var`, 160 columns. All of it is in `.editorconfig`,
which is enforced at build time (`EnforceCodeStyleInBuild`, warnings as errors in `src/`). If the
build complains about formatting, run `dotnet format whitespace . --folder` — and once Kerf can
format itself, `kerf format .`.
