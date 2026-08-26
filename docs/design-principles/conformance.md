---
navigation_title: Conformance
description: How Curb's output is measured as a fixed point of dotnet format — and what that means in practice.
---

# Conformance

{{product}} states compatibility with `dotnet format` as a measurement, gated in CI on every push
against a 1,196-file corpus:

- With reflow off, {{product}}'s output is **byte-identical to `dotnet format whitespace`** — 100%, enforced as a build gate.
- With reflow on, also **100%** — deterministic layout has no arrangement inherited from the source for `dotnet format` to disagree with, so it is the cleaner of the two.
- With reflow on *and* `csharp_keep_existing_linebreaks = true`, **99.9%**. One file falls short: a property pattern that reflow breaks, and whose brace `dotnet format` then moves. Measured and held rather than quietly rounded up.
- **Zero** failed or unparsable files across the corpus, also gated.

What is measured is that {{product}}'s output is a *fixed point* of `dotnet format`: run `dotnet format`
over a {{product}}-formatted file and nothing changes. That is what decides whether Format Document in
your IDE will fight your formatter.

The same measurement over twelve real repositories and 41,000 files is in [the benchmarks](../benchmarks/index.md).

## The precise definition

For a reference tool `T` — `dotnet format whitespace`, `dotnet format style`, or `jb cleanupcode` — and an
input `x` formatted under some `.editorconfig`, call {{product}}'s output `Z = curb(x)`. Conformance with
`T` means:

```
T(Z) == Z
```

{{product}}'s output is a fixed point of `T`. This is a weaker, and more useful, claim than raw agreement.
It does **not** require `Z` to equal `X = T(x)` — what `T` would have produced from the same raw input on
its own. {{product}} is free to pick a shape `T` would never have chosen by itself, as long as `T` accepts
that shape once it exists and does not move it again. That is what makes opinionated and deterministic
layout admissible at all: `dotnet format` declines to decide almost everything about line breaks, so
anything it declines to decide {{product}} may decide while remaining a fixed point.

`X != Z` while `T(Z) == Z` still holds is the common, expected case, not something each instance needs to
justify on its own: most of the ReSharper-derived wrapping, blank-line and reflow keys have no `dotnet
format` opinion to agree with at all, so hundreds of hand-written cases legitimately differ this way.
`./build.sh verifyexpectations` reports how many, the same way `./build.sh churn` reports adoption cost —
published rather than asserted, because it is worth knowing, but gating on it case by case would mean
writing an entry for every one of {{product}}'s opinions individually, which drowns the exceptions this
page exists to surface in paperwork nobody would read.

### When `T(Z) == Z` cannot hold at all

The rarer and more serious case is `T(Z) != Z` itself failing — {{product}}'s own chosen output is not even
a fixed point, and no choice of `Z` would make it one. This is what `./build.sh verifyexpectations` actually
gates, on every case, with zero tolerance for an undocumented one. It happens where `T` cannot recognise
what {{product}} is doing at all, so it undoes the opinion however it is expressed:

- A ReSharper-only key set alongside an IDE0055 key `T` does enforce, where the two are in direct conflict —
  `csharp_empty_block_style = together` next to `csharp_preserve_single_line_blocks = false` is the current
  example: `T` always re-expands the single-line block the ReSharper key just collapsed.
- A language-level suppression `T` has no concept of, because it is not diagnostic-driven — a `#pragma
  warning disable` region {{product}} leaves untouched is always reformatted by `dotnet format whitespace`
  regardless of the pragma.
- An unrecognised option value, where {{product}} falls back to the option's documented default and `T`'s
  own fallback for the same invalid value does not match its documented default either.

Every one of these is recorded in [conformance divergences](conformance-divergences.md), by the test case
that demonstrates it. An undocumented one fails the build outright — the two-step "reported, then gated
once a number exists" pattern `churn` uses does not apply here, because the whole point of this check is
that a new one is never accepted silently. Each entry needs to be either a real, permanent incompatibility
(documented and accepted) or a bug to fix — never a silent gap in a floor.

The same fixed-point property, and the same documentation discipline for a `T(Z) != Z` case, is what
{{product}} holds itself to against `dotnet format style` (for the semantic cleanup rules `curb cleanup`
applies) — gated in CI by `./build.sh verifycleanupexpectations`, the same way `verifyExpectations` gates
the whitespace side.

### `jb cleanupcode`: measured, not yet gated

`jb cleanupcode` is where the ReSharper-derived wrapping and blank-line keys would ideally be checked —
they have no `dotnet format` opinion to compare against at all. `./build.sh verifyexpectationsjb` runs the
same cases through it and reports the count, but does **not** gate on it and is **not** wired into CI —
though for a narrower reason than it first looked like.

An early version gave each case its own throwaway project batched into one solution, on the theory that
`jb`'s cost is per invocation rather than per project — wrong on both counts it was measured against. Speed:
going from one project to five in the same invocation cost nothing extra (`jb` pays a large, ~8 second fixed
platform-startup cost regardless), but the full 838-project run took ~5 minutes, so real per-project MSBuild
evaluation overhead was the dominant cost. Determinism: that same shape also measured non-deterministic —
three otherwise-identical runs against the same 838-case dump landed on 330, 377 and 404 disagreements, with
no code change between them.

One project holding every case as its own uniquely-namespaced file, rather than one project per case, fixes
both: the run takes ~30 seconds now, and two runs landed on the exact same 307-case set — name for name, not
just the same count. Many separate project/compilation contexts, not `jb`'s cleanup logic itself, was almost
certainly the source of both problems.

What still blocks gating is scale, not reliability. `dotnet format whitespace` disagrees with Curb on 5 of
842 cases, each root-caused to a real, documented incompatibility. `jb` disagrees on roughly 307 of 842 —
`dotnet format` mostly declines to decide and so rarely fights Curb's choices, where `jb` is a full
opinionated formatter with its own stance on almost everything (a brace Curb keeps around a single embedded
statement is one observed example). Gating "every non-fixed-point named individually" the way
`verifyExpectations` does would mean triaging roughly 307 cases into registry entries in one sitting — the
same reason the whitespace side's `X != Z` shape-divergence check is reported in aggregate rather than
itemised. The likely fix is the same one used there: find the handful of root causes behind the 307 cases
(the empty-block-collapse default was one; there are probably a handful more) and gate on those being
documented, the way the whitespace side gates on 5 categories rather than hundreds of cases. That
categorisation is future work.
