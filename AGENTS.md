# Curb — contributor and agent conventions

Curb formats C# from a `.editorconfig`, honouring the full IDE0055 option surface while adding
width-aware reflow. It ships as a native-AOT `dotnet tool`.

## Build and test

```sh
./build.sh build          # dotnet build -c Release
./build.sh test           # runs tests/Nullean.Curb.Tests via `dotnet run`
./build.sh benchmark      # BenchmarkDotNet suite
./build.sh docs           # builds docs/ and serves it at http://localhost:8080/formatter/
./build.sh generatepackages
./build.sh conformance --corpus <path>   # byte-identity with dotnet format whitespace
./build.sh perf --corpus <path>          # times the AOT binary; gates on allocations
./build.sh msbuildsmoketest              # formatter runs before CoreCompile
./build.sh cleanupsmoketest              # cleanup fixes what a build reported
./build.sh cleanupsafety --corpus <path> # feed a corpus wrong verdicts; none may damage a file
./build.sh cleanupconformance            # clean a solution that builds; it must still build
./build.sh verifyexpectations            # expectations survive dotnet format
```

Requires the .NET 10 SDK (pinned in `global.json`). Test projects are `OutputType=Exe` and run on
Microsoft.Testing.Platform, so they are launched with `dotnet run`, not `dotnet test`.

## Layout

| Path | What |
|---|---|
| `src/Nullean.Curb.Core` | The engine: document IR, layout printer, syntax printers, verifier. **No IO, no config source.** |
| `src/Nullean.Curb.Cleanup` | Applies code style fixes from diagnostics a build reported. **No IO, no compilation.** |
| `src/Nullean.Curb.EditorConfig` | Binds `.editorconfig` to formatting options. Owns the `IFileSystem` dependency. |
| `src/Nullean.Curb.Cli` | The `curb` tool. Native-AOT, packed per RID. |
| `tests/Nullean.Curb.Tests` | Golden files, option matrix, document-IR units, `MockFileSystem` tests. |
| `tests/Nullean.Curb.Benchmarks` | BenchmarkDotNet, `[MemoryDiagnoser]`. |
| `src/curb` | Props and targets only, nothing compiled. Runs the formatter before `CoreCompile`, and asks the compiler for an error log. |
| `examples/curb-aot-smoketest` | Publishes AOT and runs; the CI gate on every RID. Never packed. |
| `examples/curb-msbuild-smoketest` | Proves the formatter runs before `CoreCompile`. Not in the solution. |
| `examples/curb-cleanup-smoketest` | Proves cleanup fixes what a build reported, and only that. Not in the solution. |
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

1. `src/Nullean.Curb.Core/Options/OptionCatalog.cs` — one declaration (key, allowed values, default,
   bit slot, doc summary, `Implemented = true`).
2. The generator regenerates the accessor, parser, validator, diagnostics, `print-config` row and
   `docs/options.md`. **Do not hand-edit generated output.**
3. Add a helper in `Printing/CSharp/Spacing.cs` (or `BraceLines.cs` / `Indentation.cs`) that turns
   the option into a `Doc`.
4. Swap the hard-coded `" "` / `Doc.Line` / `Doc.Indent(…)` in 1–3 printers for the helper.
5. Add `tests/Nullean.Curb.Tests/Fixtures/options/<key>/<value>.test`, one per allowed value.
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
2. Add the fixer under `src/Nullean.Curb.Cleanup/Rules/`, implementing `ICleanupRule`. It must declare
   `NeedsSpan`, and it must gate on node kind — the reported position has to resolve to the syntax the
   rule owns, or it refuses. **That gate is what makes a stale log harmless**, so it is not optional.
3. Add it to `CSharpCleaner.Rules`, and flip its `RuleCatalog` row to `RuleOwner.Cleanup` with its
   `TokenDelta` **in the same change**. `RuleCatalogTests` fails if the two disagree.
4. If the delta is new, teach `ContentVerifier` and `TokenStreamComparer` about it — and add negative
   tests to `CleanupDeltaTests` from **both** sides. A widening that is too generous is invisible: every
   test still passes and the net quietly stops catching what it exists for.
5. Add cases to `tests/Nullean.Curb.Tests/Cleanup/`, with the diagnostic supplied by hand. One per thing
   it fixes, and **one per thing it refuses** — a rule that fixes the right thing and also the wrong thing
   passes half a suite.
6. Run **both** corpus gates before claiming it is done, and record the numbers in
   `docs/cleanup.md` rather than the intent. It claims every rule fires everywhere it could, so it feeds
   deliberately wrong verdicts and requires that none of them damages a file. **It found four defects on
   its first run that no unit test reached**, listed in that document. `cleanupconformance` is the other
   half and the only one that compiles anything, so it is the only one that can catch a fix which compiles
   but is wrong — it caught an IDE0007/IDE0034 interaction producing CS8716. Nothing below these two rungs
   green-lights a rule that changes tokens — the same discipline as
   [docs/contribute/layout-decisions.md](docs/contribute/layout-decisions.md), for the same reason.

A refusal is a first-class outcome, not an error. Report it with its reason so "Curb declined" is
distinguishable from "Curb is broken."

## Scope boundary

**Curb never computes semantics.** It never loads a compilation, never resolves a reference and never
uses the semantic model. That is what lets `curb format` run on a bare folder with no restore and no
build, and it is why Core's only package reference is the exact-pinned parser.

- **`curb format`, in scope:** IDE0055 formatting, plus syntax-only code style (braces, expression
  bodies, file-scoped namespaces, modifier order, redundant parentheses).
- **`curb cleanup`, in scope:** semantic code style rules — but only ones a build has already reported,
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
- The conformance gate: with `max_line_length = off`, Curb output must be **byte-identical to
  `dotnet format whitespace`**. That number is the product claim; keep it at 100%.
- `./build.sh churn --corpus <path>` reports the other half: how many of a repository's files adoption
  rewrites. Not a gate, but it must be published for any change that moves it.

## The two layout modes

**`max_line_length` selects the layout mode**, and the mode changes what an option is allowed to do. This
is not a preference among others.

- **No width — preservation.** A construct the author opened out stays opened out at their breaks; only one
  they left on a single line is handed to width. Curb never changes a line's length; output is byte-identical
  to `dotnet format whitespace`. 742/1,196 corpus files of churn, almost all blank-line collapsing.
- **A width — deterministic.** `layout = f(tokens, width)`, so idempotency holds by construction rather than
  by measurement, and it is the *better* fixed point of `dotnet format` (1196/1196 against preservation's
  1195/1196 with reflow). 892 files of churn but ~2.7× the changed lines, which is the number that decided
  the gating — file count hides it almost entirely.
- **`csharp_keep_existing_linebreaks`** overrides the resolution in either direction. `= false` with no width
  is refused (CURB1007): with an infinite width every group that fits would be joined.

Practical consequences when adding an option:

- Two named predicates carry every break decision that reads the author's layout: `PrintContext.AuthorBroke`
  and `AuthorJoined`. Both answer false in deterministic mode, so each call site falls through to the
  width-driven branch it already has. **A bare `OnSameLine` in a break decision is a review failure** —
  it is for questions that are not break decisions.
- An option may be admissible in one mode only. Say so through a diagnostic — `CURB1004` for a key
  deterministic layout makes inert, `CURB1005` for one only it can honour, `CURB1006` for a value
  `dotnet format` would undo, `CURB1007` for deterministic layout without a width — rather than letting it
  half-work. Say what it degrades *to*, and per construct rather than per key: "no effect" was wrong for
  `preserve_single_line_blocks`, which accessor lists still honour.
- The mode resolves from `MaxLineLength`, so `csharp_keep_existing_linebreaks` must be bound immediately
  after `max_line_length` in `EditorConfigOptionsBinder`. The diagnostic helpers read the resolved mode
  mid-binding; either order but that one makes them see the wrong thing.
- Measure both modes. `conformance` and `churn` take `--reflow`, `--preserve` and `--width`; with the
  corpus's own width, `--reflow` *is* the default path and `--preserve` is the opt-out.

**Before adding any option that moves a line break, read
[docs/contribute/layout-decisions.md](docs/contribute/layout-decisions.md).** In preservation mode a layout rule may read the
tokens and layout the author owns, and never layout Curb itself decides. That one mistake has killed more
features than every other cause combined; the note carries the measurements so they are not re-derived,
and records which of them deterministic mode has since brought back.

## Style

Tabs, Allman braces, file-scoped namespaces, `var`, 160 columns. All of it is in `.editorconfig`,
which is enforced at build time (`EnforceCodeStyleInBuild`, warnings as errors in `src/`). If the
build complains about formatting, run `dotnet format whitespace . --folder` — and once Curb can
format itself, `curb format .`.

## Docs voice

The `docs/` pages follow these rules. Apply them when writing or reviewing doc changes.

- No "**Bold term.** Explanation sentence." format. Where content is a set of parallel items, use a
  real table or a real list. Bold is fine inside a table cell or as a label (`**Workaround:**`).
- First paragraph sells or explains the page. Everything after it: one idea per sentence, active voice,
  short, concrete. No figurative language, no wandering.
- About one em dash per section. Use commas, parentheses, or a full stop instead.
- No scene-setting opener ("In today's world…"). No wrap-up closer ("In summary…"). Start and end on
  substance.
- Contractions where they read naturally.
- No banned words: delve, leverage, robust, comprehensive, seamless, transformative, holistic, realm,
  landscape (figurative), and the rest of the list in the style guide the user supplied.
- No banned structures: "It's not just X — it's Y", "Not only X but Y", "No X. No Y. Just Z.",
  "This is where X comes in", "Worth noting that".
- Technical listings are fine as listings. Don't convert them into prose.
