---
navigation_title: Why Kerf
description: What Kerf does that existing tools do not, in short.
---

# Why Kerf

Two kinds of C# formatter exist, and a team that cares about both reflow and its own house style cannot
use either. `dotnet format` implements IDE0055 faithfully but never wraps a long line. Prettier-style
formatters reflow but read only a handful of `.editorconfig` keys and decide everything else themselves.

{{product}}'s answer: `max_line_length` is the whole decision. Without it, {{product}} is a
`dotnet format whitespace` equivalent. With it, {{product}} owns the layout — while honouring the
complete formatting option surface you already configured.

| | `dotnet format whitespace` | `dotnet format style` | **{{product}}** |
|---|---|---|---|
| Runs on a bare folder, no restore or build | ✅ | ❌ | ✅ |
| All 39 IDE0055 formatting options | ✅ | ✅ | ✅ |
| **Reflow to `max_line_length`** | ❌ | ❌ | ✅ |
| Syntax-only code style (braces, expression bodies, file-scoped namespaces) | ❌ | ✅ | ✅ |
| Semantic code style (`var`, unused usings, naming) | ❌ | ✅ | ❌ *by design* |

The last row is the scope boundary: {{product}} never loads a compilation, and that is what keeps it fast enough to run inside every build.

## What makes it worth adopting

**Reads your complete `.editorconfig`.** Every key your IDE and `dotnet format` already read,
{{product}} reads too. Defaults are Roslyn's, so it agrees with Format Document out of the box.
[Details →](design-principles/existing-tooling.md)

**Reflows to `max_line_length`.** One key opts in. Without it, {{product}} is whitespace-only and
its output is byte-identical to `dotnet format whitespace`. With it, {{product}} owns the layout —
idempotently, by construction.
[Details →](design-principles/reflow.md)

**Fast enough to run in every build.** ~350 ms CPU on a 1,196-file, 6.5 MB corpus — within
measurement noise of the Roslyn parse floor. The build integration adds nothing you would notice.
[Details →](design-principles/performance.md)

**Safe by construction.** Every file is verified in memory before being written. A file that fails
verification is reported and left untouched. Unknown syntax is emitted verbatim rather than guessed at.
[Details →](design-principles/safety.md)

**Works with AI coding agents.** Parser-only means it runs before the compiler, inside the build.
Formatting offences are fixed automatically before the agent sees anything. Nothing goes in `AGENTS.md`.
[Details →](design-principles/ai-native.md)

**Two passes, one tool.** The syntax pass (`kerf format`) needs no build and runs before the compiler.
The semantic pass (`kerf cleanup`) reads the diagnostics your build reported and applies the fixes.
[Details →](design-principles/syntax-and-semantic.md)

**A fixed point of `dotnet format`.** Run `dotnet format` over {{product}}-formatted code and nothing
changes — 100%, gated in CI on every push. Format Document in your IDE will not fight your formatter.
[Details →](design-principles/conformance.md)
