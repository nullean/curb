---
navigation_title: AI coding agents
description: How Kerf's parser-only design lets it run inside the build, removing formatting from the work a coding agent has to do.
---

# AI coding agents

Coding agents spend context on whatever the build reports. If your build reports IDE0055 diagnostics,
the agent reads them, edits the file, and rebuilds — one round trip per mechanical offence, all of them
with a single right answer already written in your `.editorconfig`.

{{product}} eliminates that round trip.

## How it works

{{product}} is parser-only: it needs no compilation, so it can run *before* the compiler — inside the
build, before `CoreCompile`. By the time the compiler reads your source, every formatting offence is
already gone. The agent never sees them.

What reaches the agent is the part that needs judgement: diagnostics that require a compilation to
decide. That is [the semantic pass](syntax-and-semantic.md), and it is the only part worth an agent's
attention.

## What to put in AGENTS.md

Delete the style section. Replace it with one line:

```markdown
## Style
Formatting and syntax-level code style are applied automatically by the build — see `.editorconfig`.
Do not hand-format code; `dotnet build` does it.
```

Then put the actual rules in `.editorconfig`:

```ini
[*.cs]
indent_style = tab
max_line_length = 160
csharp_new_line_before_open_brace = all
csharp_prefer_braces = true
csharp_style_namespace_declarations = file_scoped
```

One source of truth, read by your IDE, by `dotnet format`, and by {{product}}.

## What the build handles

**Fully — every layout rule.** Indentation, spacing, brace placement, blank lines, reflow: all decided
from the parse tree. Nothing in this class reaches the agent's context.

**When you opt in — syntax style.** Braces, expression bodies, file-scoped namespaces, modifier order,
using placement: fixed by the build for every key you set in `.editorconfig`.

**After the build — most of the semantic remainder.** `var`, unused usings, `readonly` and more need a
compilation to decide. `kerf cleanup` reads the diagnostics your build already reported and applies
the rewrites. One command after the build; see [Cleanup](../workflow/cleanup.md).

**Not handled — and deliberately.** Naming, unused members, unread assignments. Their fixes can change
which overload binds or break a reflection string. {{product}} reports them; the agent decides.

See [Integrations](../workflow/integrations.md) for how to wire this into MSBuild, CI, and pre-commit
hooks.
