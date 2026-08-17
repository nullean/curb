---
navigation_title: Style enforcement that costs no context
description: Stop writing formatting rules into your agent instructions. Put them in .editorconfig and let the build apply them.
---

# Style enforcement that costs no context

Open almost any `AGENTS.md` or `CLAUDE.md` and you will find a section like this:

```markdown
## Style
- Use tabs, not spaces
- Allman braces
- File-scoped namespaces
- Always use braces, even for single statements
- Keep lines under 160 columns
- `var` where the type is apparent
```

It is well intentioned and it does not work well. Here is what it actually costs.

## The problem with style as prose

**It is instructions, not enforcement.** Natural-language rules get followed inconsistently, especially
in a long session where the style section is thousands of tokens back in the context. The failure is
silent — nothing tells the agent it drifted.

**Every correction is a round trip.** The agent writes a file, the build reports IDE0055 diagnostics,
the agent reads them, edits the file, and the build runs again. Each lap costs tokens, latency and a
tool call, and every single one of those edits is mechanical — there was exactly one right answer, and
it was already written down in your `.editorconfig`.

**It crowds out the instructions that matter.** Context spent on brace placement is context not spent on
your architecture, your invariants, or the bug being fixed.

**It pollutes the diff.** Formatting churn in an agent's pull request is noise a human reviewer has to
read past to find the change that matters.

The deeper issue is that the rules already exist in machine-readable form. Your `.editorconfig` is the
source of truth your IDE and `dotnet format` already use. Restating it as English prose for an agent
creates a second, weaker copy — one that can drift from the first, and that has to be re-read on every
turn.

## The inversion

Do not tell the agent about your style. Make the build apply it.

```xml
<PackageReference Include="Nullean.Kerf.MSBuild" Version="*" PrivateAssets="all" />
```

{{product}} runs before `CoreCompile`. By the time the compiler reads your source, the file has already
been rewritten to match your `.editorconfig`, so the mechanical offences are simply gone — not reported,
not queued for a follow-up edit, gone. The agent never sees them, never spends a token on them, and
never has to be told about them.

What reaches the agent is the part that needed judgement: the diagnostics that require a compilation to
decide, and therefore could not have been fixed mechanically. That is
[the semantic remainder](../concepts/index.md), and it is the only thing worth a coding agent's
attention.

This works because of [the pass split](../concepts/index.md). The syntax pass needs no build, so it can
run *inside* the build. A tool that needed a compilation could not run before the compiler.

## What to write instead

Delete the style section. Replace it with a line that tells the agent the problem is handled:

```markdown
## Style
Formatting and syntax-level code style are applied automatically by the build — see `.editorconfig`.
Do not hand-format code, and do not "fix" formatting you see in a diff; `dotnet build` will do it.
```

That last clause earns its place. Without it, a diligent agent that notices misformatted code will
helpfully reformat it by hand, which is the behaviour you were trying to eliminate.

Then put the actual rules where they belong:

```ini
[*.cs]
indent_style = tab
max_line_length = 160
csharp_new_line_before_open_brace = all
csharp_prefer_braces = true
csharp_style_namespace_declarations = file_scoped
csharp_preferred_modifier_order = public,private,protected,internal,static,readonly,async
```

One source of truth, read by your IDE, by `dotnet format`, and by {{product}}.

## What is actually guaranteed

Worth being precise, because "the build fixes everything" is not true and the useful claim is narrower.

**Fully handled — every layout rule.** Indentation, spacing, brace placement, blank lines and reflow are
all decided from the parse tree, so the build fixes 100% of them with no input from the agent. Nothing
in this class ever reaches your context window.

**Handled where you opt in — syntax style.** Braces, expression bodies, file-scoped namespaces, modifier
order, using placement and file headers are also fixed by the build, but only for the keys you set.
{{product}} defaults to leaving your code as written, so it fixes exactly what you configured and
nothing else.

**Handled after the build — most of the semantic remainder.** `var`, unused usings, `readonly` and six
more need a compilation to decide, so the build cannot fix them before the compiler runs. It does not have
to: the compiler already decided, and `kerf cleanup` reads its answer and applies the rewrite. One command
after the build, and those diagnostics are gone without {{product}} ever loading a compilation of its own.
See [Cleanup](cleanup.md).

**Not handled at all — and deliberately.** Naming, unused members and unread assignments are left. Their
fixes delete declarations or rename symbols, which can compile and still change which overload binds or
break a reflection string no compiler check sees. {{product}} reports them and says why rather than
guessing. That remainder is genuinely a judgement call, and it is the work you want the agent doing.

## The rest of the loop

**In CI, check rather than rewrite.** The build integration checks in `Release` and rewrites otherwise,
which is usually the behaviour you want without configuring anything: a developer or an agent gets the
file fixed underneath them, and CI is told rather than edited. See
[the build integration](msbuild.md).

**The diff stays clean.** Because formatting is applied deterministically by the build, an agent's pull
request contains its actual change and nothing else. Reviewers stop reading past whitespace.

**Nothing is silently mangled.** {{product}} verifies every file before writing it and leaves anything
that fails verification untouched. Automatic rewriting is only a good idea if it cannot damage code —
see [Safety](../concepts/safety.md).
