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
  a class-per-node graph. It buys an O(n) zero-allocation verifier, which in turn lets Kerf make the
  round-trip re-parse conditional: it only runs when the printer detected a moved token boundary, not
  on every file.
- **The `preserve_single_line_*` options are supported**, which makes output depend on input layout.
  Kerf is idempotent but deliberately not canonicalising — that is correct IDE0055 behaviour.
- **`UnhandledNode` prints unknown syntax verbatim.** Kerf is safe but incomplete from day one;
  printer coverage grows without ever risking code.

## Current state

M0–M5 are done; see the milestone table in [README.md](README.md) for the caveats. The formatter works
end to end: arena, printer, verifier, CLI, MSBuild integration, and ~90 `.editorconfig` keys — all 39
IDE0055 formatting options, the 8 core keys, and 43 further syntax-style, wrapping and blank-line
options. Native AOT on five RIDs, ~11 MB with ~10 ms startup.

CI gates conformance on every push: byte-identical to `dotnet format whitespace` with reflow off (100%),
99.9% with reflow on, zero failed or unparsable files across a 1,196-file corpus.

Still open: redundant parentheses (IDE0047/0048), the `wrap_if_long` fill layout, the option-catalog
generator that will produce `docs/options.md`, and replacing the hand-rolled CLI parser with
`Nullean.Argh`.

## Public documentation

`docs/` is a [docs-builder](https://github.com/elastic/docs-builder) site published to GitHub Pages by
`.github/workflows/docs.yml`.

```sh
./build.sh docs                  # build, apply the landing override, serve, open a browser
./build.sh docs --port 9000
./build.sh docs --noserve        # build only
```

`build/scripts/Documentation.fs` downloads docs-builder into `.artifacts/tools` on first use (it is a
native binary, not a dotnet tool, so `dotnet tool restore` cannot fetch it), then runs the same two
steps the workflow runs, in the same order. Use it rather than `docs-builder serve`, which renders
pages on demand and so never applies the landing page override.

Two things about the site are easy to get wrong:

- **`docs/kerf-landing.html` is not generated.** It is a standalone page CI copies over the generated
  `index.html`, so the build's `prefix` never reaches it. Its links are relative and resolve against a
  `<base href="/formatter/">` that must stay in step with the workflow's `prefix: formatter` and with
  `Documentation.PathPrefix`. `./build.sh docs` fails if the three drift apart — nothing else would
  catch it, because docs-builder never reads the landing page.
- **Prose uses `{{product}}`**, substituted from `_docset.yml`, so the codename can be changed in one
  place. Headings use the literal name on purpose — slugs are generated before substitution, so
  `## What {{product}} does` would anchor as `#what-product-does`.
