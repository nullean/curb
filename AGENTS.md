# Kerf — contributor and agent conventions

Kerf formats C# from a `.editorconfig`, honouring the full IDE0055 option surface while adding
width-aware reflow. It ships as a native-AOT `dotnet tool`.

## Build and test

```sh
./build.sh build          # dotnet build -c Release
./build.sh test           # runs tests/Nullean.Kerf.Tests via `dotnet run`
./build.sh benchmark      # BenchmarkDotNet suite
./build.sh generatepackages
./build.sh conformance --corpus <path>   # byte-identity with dotnet format whitespace
./build.sh perf --corpus <path>          # times the AOT binary; gates on allocations
./build.sh msbuildsmoketest              # formatter runs before CoreCompile
./build.sh cleanupsmoketest              # cleanup fixes what a build reported
./build.sh cleanupsafety --corpus <path> # feed a corpus wrong verdicts; none may damage a file
./build.sh verifyexpectations            # expectations survive dotnet format
```

Requires the .NET 10 SDK (pinned in `global.json`). Test projects are `OutputType=Exe` and run on
Microsoft.Testing.Platform, so they are launched with `dotnet run`, not `dotnet test`.

## Layout

| Path | What |
|---|---|
| `src/Nullean.Kerf.Core` | The engine: document IR, layout printer, syntax printers, verifier. **No IO, no config source.** |
| `src/Nullean.Kerf.Cleanup` | Applies code style fixes from diagnostics a build reported. **No IO, no compilation.** |
| `src/Nullean.Kerf.EditorConfig` | Binds `.editorconfig` to formatting options. Owns the `IFileSystem` dependency. |
| `src/Nullean.Kerf.Cli` | The `kerf` tool. Native-AOT, packed per RID. |
| `tests/Nullean.Kerf.Tests` | Golden files, option matrix, document-IR units, `MockFileSystem` tests. |
| `tests/Nullean.Kerf.Benchmarks` | BenchmarkDotNet, `[MemoryDiagnoser]`. |
| `src/Nullean.Kerf.MSBuild` | Props and targets only, nothing compiled. Runs the formatter before `CoreCompile`, and asks the compiler for an error log. |
| `examples/kerf-aot-smoketest` | Publishes AOT and runs; the CI gate on every RID. Never packed. |
| `examples/kerf-msbuild-smoketest` | Proves the formatter runs before `CoreCompile`. Not in the solution. |
| `examples/kerf-cleanup-smoketest` | Proves cleanup fixes what a build reported, and only that. Not in the solution. |
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

## Adding a cleanup rule

A rule is admissible only if its fix is derivable from `(ruleId, span)` plus syntax. If it needs anything
the diagnostic does not carry, it is not a cleanup rule — record why and stop.

1. Confirm the shape against a real build before writing anything. Escalate the rule in a scratch project,
   build with `-p:ErrorLog=x.sarif,version=2.1`, and read the actual span. **Where the position lands is
   not obvious**: IDE0044 and IDE0040 report the field *identifier*, IDE0007 the *type*, IDE0090 the `new`
   keyword, IDE0005 the first `using` of a whole run. The fixer walks up from there.
2. Add the fixer under `src/Nullean.Kerf.Cleanup/Rules/`, implementing `ICleanupRule`. It must declare
   `NeedsSpan`, and it must gate on node kind — the reported position has to resolve to the syntax the
   rule owns, or it refuses. **That gate is what makes a stale log harmless**, so it is not optional.
3. Add it to `CSharpCleaner.Rules`, and flip its `RuleCatalog` row to `RuleOwner.Cleanup` with its
   `TokenDelta` **in the same change**. `RuleCatalogTests` fails if the two disagree.
4. If the delta is new, teach `ContentVerifier` and `TokenStreamComparer` about it — and add negative
   tests to `CleanupDeltaTests` from **both** sides. A widening that is too generous is invisible: every
   test still passes and the net quietly stops catching what it exists for.
5. Add cases to `tests/Nullean.Kerf.Tests/Cleanup/`, with the diagnostic supplied by hand. One per thing
   it fixes, and **one per thing it refuses** — a rule that fixes the right thing and also the wrong thing
   passes half a suite.
6. Run `./build.sh cleanupsafety --corpus <path>` before claiming it is done, and record the numbers in
   `docs/cleanup.md` rather than the intent. It claims every rule fires everywhere it could, so it feeds
   deliberately wrong verdicts and requires that none of them damages a file. **It found four defects on
   its first run that no unit test reached**, listed in that document. Nothing below this rung green-lights
   a rule that changes tokens — the same discipline as
   [docs/layout-decisions.md](docs/layout-decisions.md), for the same reason.

A refusal is a first-class outcome, not an error. Report it with its reason so "Kerf declined" is
distinguishable from "Kerf is broken."

## Scope boundary

**Kerf never computes semantics.** It never loads a compilation, never resolves a reference and never
uses the semantic model. That is what lets `kerf format` run on a bare folder with no restore and no
build, and it is why Core's only package reference is the exact-pinned parser.

- **`kerf format`, in scope:** IDE0055 formatting, plus syntax-only code style (braces, expression
  bodies, file-scoped namespaces, modifier order, redundant parentheses).
- **`kerf cleanup`, in scope:** semantic code style rules — but only ones a build has already reported,
  and only where the fix is derivable from the diagnostic's position plus syntax. It *consumes a verdict*
  rather than deriving one. `--forward` hands the remainder to `dotnet format` and prints both timings.
  See [docs/cleanup.md](docs/cleanup.md).
- **Out of scope, permanently:** any fix that deletes a declaration (IDE0051/0052) or renames a symbol
  (IDE1006/IDE0130), and any fix the diagnostic does not carry enough information to make (IDE0008 needs
  a type name). Report these and point the user at `dotnet format style`.

**Never add `Microsoft.CodeAnalysis.Workspaces`, to any project, and never construct a
`CSharpCompilation`.** Not to Core, not to Cleanup, not temporarily. The bare-folder and AOT stories rest
on it, and `docs/cleanup.md` records why hosting the SDK's own analysers cannot be made version-safe.

`RuleCatalog` names all 116 IDE rules the SDK can report and who fixes each. A row claims `Cleanup` only
once a fixer exists — never because one is planned — and `RuleCatalogTests` holds the two together, so a
rule cannot claim to be fixed by a fixer nobody wrote.

## Correctness

- Golden fixtures with **no `.expected.test` companion must format to themselves.** This makes
  idempotency assertions free.
- Every fixture also asserts: the token-coverage verifier passes, the re-parse token comparer passes,
  `format(format(x)) == format(x)`, and the `#if` symbol-set loop passes.
- The conformance gate: with `max_line_length = off`, Kerf output must be **byte-identical to
  `dotnet format whitespace`**. That number is the product claim; keep it at 100%.

**Before adding any option that moves a line break, read
[docs/layout-decisions.md](docs/layout-decisions.md).** A layout rule may read the tokens and it may
read layout the author owns; it may never read layout Kerf itself decides. That one mistake has
killed more features than every other cause combined, and the note carries the measurements so they
are not re-derived.

## Style

Tabs, Allman braces, file-scoped namespaces, `var`, 160 columns. All of it is in `.editorconfig`,
which is enforced at build time (`EnforceCodeStyleInBuild`, warnings as errors in `src/`). If the
build complains about formatting, run `dotnet format whitespace . --folder` — and once Kerf can
format itself, `kerf format .`.
