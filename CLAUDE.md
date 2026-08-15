# Kerf

See [AGENTS.md](AGENTS.md) for project conventions, layout, hot-path rules and the option-onboarding
playbook. It is the source of truth; this file only adds orientation.

## The one-paragraph version

Kerf parses C# with Roslyn (parser only — never `Workspaces`, never a compilation), builds a
Prettier-style document IR in a pooled arena, and prints it back out. Every layout decision is driven
by a `FormatOptions` struct resolved from `.editorconfig`. Defaults match Roslyn's own, so Kerf agrees
with the IDE out of the box; `max_line_length` opts into reflow on top.

## Where the time goes

Measured in M0 on elastic/docs-builder (1,196 files, 6.5 MB):

- Roslyn parse + full red tree: **~300 ms CPU**, ~16.5× source allocated. This is the floor.
- CSharpier on the same corpus: **~14,000 ms CPU**. `dotnet format whitespace`: **~12,000 ms**.

So ~97% of a formatter's cost is its own work, not parsing. Optimise the printer, not the parse.

## Design decisions worth not re-litigating

- **Full re-print, not a trivia rewriter.** A rewriter cannot reflow, and reflow is the product.
- **Arena document IR** (struct in a pooled array, text leaves referencing source spans) rather than
  a class-per-node graph. It buys an O(n) zero-allocation verifier, which in turn lets Kerf drop the
  expensive re-parse safety net CSharpier runs on every file.
- **The `preserve_single_line_*` options are supported**, which makes output depend on input layout.
  Kerf is idempotent but deliberately not canonicalising — that is correct IDE0055 behaviour.
- **`UnhandledNode` prints unknown syntax verbatim.** Kerf is safe but incomplete from day one;
  printer coverage grows without ever risking code.

## Current state

M0 (spike and scaffolding) is done: the repo builds, tests pass, and the engine links and runs under
native AOT on `osx-arm64` at ~11 MB with ~10 ms startup. There is **no formatter yet** — `CSharpSource`
parses, and that is all. M1 builds the document arena, the printer and the first ~12 syntax printers.
