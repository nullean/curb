---
navigation_title: Why Kerf
description: Existing tools each solve one half of the problem. The measurement that shows it, and what Kerf does instead.
---

# Why Kerf

Two kinds of C# formatter exist, and a team that cares about both speed and its own house style cannot
use either.

**`dotnet format` implements IDE0055 faithfully.** All 39 `csharp_*` and `dotnet_*` formatting keys,
exactly as Visual Studio and Rider interpret them. It will never wrap a long line, because IDE0055 has
no opinion about line width — so the one thing you most want a formatter to do, it does not do.

**Prettier-style formatters reflow beautifully.** They wrap to a width and produce genuinely nice
output. But they read a handful of `.editorconfig` keys at most; every other layout decision is the
tool's, not yours.

So the choice on offer is: keep your style and format by hand, or reflow and abandon your style.

## The measurement

This is not a theoretical complaint. On [elastic/docs-builder](https://github.com/elastic/docs-builder)
— 1,196 files, 193k lines, a repository that is already 100% IDE0055-clean — a Prettier-style formatter
rewrites **996 of the 1,196 files**.

Not because those files were wrong. Because the tool disagrees with the `.editorconfig` the team had
already settled on.

{{product}}'s number on that same repository is **zero**, unless you ask for reflow.

## What Kerf does

Reflow to a line width, the way Prettier does — while honouring the complete .NET formatting option
surface. Defaults are Roslyn's defaults, so {{product}} agrees with your IDE out of the box instead of
fighting it. `max_line_length` is the single opt-in on top.

| | `dotnet format whitespace` | `dotnet format style` | **{{product}}** |
|---|---|---|---|
| Runs on a bare folder, no restore or build | ✅ | ❌ | ✅ |
| All 39 IDE0055 formatting options | ✅ | ✅ | ✅ |
| **Reflow to `max_line_length`** | ❌ | ❌ | ✅ |
| Syntax-only code style (braces, expression bodies, file-scoped namespaces) | ❌ | ✅ | ✅ |
| Semantic code style (`var`, unused usings, naming) | ❌ | ✅ | ❌ *by design* |

The last row is the [scope boundary](concepts/index.md), not a missing feature. {{product}} never loads
a compilation, and that is what keeps it fast.

## Conformance, as a number

"Compatible with `dotnet format`" is the kind of claim every tool makes. {{product}} states it as a
measurement, gated in CI on every push against that 1,196-file corpus:

- With reflow off, {{product}}'s output is **byte-identical to `dotnet format whitespace`** — 100%,
  enforced as a build gate.
- With reflow on, **99.9%**. One file falls short: a property pattern that reflow breaks, and whose
  brace `dotnet format` then moves. It is measured and held rather than quietly rounded up.
- **Zero** failed or unparsable files across the corpus, also gated.

The framing matters: what is measured is that {{product}}'s output is a *fixed point* of `dotnet format`
— run `dotnet format` over a {{product}}-formatted file and nothing changes. That is the property you
actually need if the two tools are going to coexist in one repository, and it is what decides whether
Format Document in your IDE will fight your formatter.

The same measurement run over twelve real repositories and 41,000 files, against CSharpier and
`dotnet format` as well, is written up in
[the formatter comparison](contribute/formatter-comparison.md) — including the defects it found.

## Speed, and where it comes from

Measured on the same 6.5 MB corpus, as CPU time:

| | |
|---|---|
| Roslyn parse + full red tree — the floor for any tool | **~300 ms** |
| **{{product}}** | milliseconds on top of that floor |
| `dotnet format whitespace` | **~12,000 ms** |
| CSharpier | **~14,000 ms** |

Parsing is about 2.5% of the budget. Roughly 97% of what a formatter costs is its own work, which is why
{{product}} treats the printer as the thing to optimise and holds an allocation-ratio gate in CI.

It ships as a native-AOT binary per platform, so there is no JIT warm-up to pay on a tool you invoke
constantly: about 10 ms to start.

## Why this shape matters for AI coding agents

Because the syntax pass needs no build, it can run *inside* your build, before the compiler sees your
source. Every mechanical offence gets fixed underneath whoever — or whatever — is editing the code, and
only the semantic remainder surfaces as a diagnostic.

For a coding agent, that is the difference between spending its context window on brace placement and
spending it on your problem. See [Style enforcement that costs no context](workflow/ai-native.md).
