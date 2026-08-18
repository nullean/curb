---
navigation_title: Conformance
description: How Kerf's output is measured as a fixed point of dotnet format — and what that means in practice.
---

# Conformance

"Compatible with `dotnet format`" is the kind of claim every tool makes. {{product}} states it as a measurement, gated in CI on every push against a 1,196-file corpus:

- With reflow off, {{product}}'s output is **byte-identical to `dotnet format whitespace`** — 100%, enforced as a build gate.
- With reflow on, also **100%** — deterministic layout has no arrangement inherited from the source for `dotnet format` to disagree with, so it is the cleaner of the two.
- With reflow on *and* `csharp_keep_existing_linebreaks = true`, **99.9%**. One file falls short: a property pattern that reflow breaks, and whose brace `dotnet format` then moves. Measured and held rather than quietly rounded up.
- **Zero** failed or unparsable files across the corpus, also gated.

The framing matters: what is measured is that {{product}}'s output is a *fixed point* of `dotnet format` — run `dotnet format` over a {{product}}-formatted file and nothing changes. That is the property you actually need if the two tools are going to coexist in one repository, and it is what decides whether Format Document in your IDE will fight your formatter.

The same measurement run over twelve real repositories and 41,000 files, against CSharpier and `dotnet format` as well, is written up in [the formatter comparison](../contribute/formatter-comparison.md).
