---
navigation_title: Integrations
description: How to wire Kerf into AI coding agents, CI pipelines, MSBuild, and pre-commit hooks.
---

# Integrations

## AI coding agents

Natural-language style rules in `AGENTS.md` get followed inconsistently, especially in a long session
where the style section is thousands of tokens back in context. Every formatting correction is a round
trip: the agent writes a file, the build reports IDE0055 diagnostics, the agent edits, the build
reruns. Each lap costs tokens and latency, and every edit has exactly one right answer already written
in your `.editorconfig`.

The fix is to make the build apply style rather than telling the agent about it:

```xml
<PackageReference Include="Nullean.Kerf.MSBuild" Version="*" PrivateAssets="all" />
```

{{product}} runs before `CoreCompile`. By the time the compiler reads your source, the mechanical
offences are gone — not reported, not queued for a follow-up edit, gone.

### What to write instead

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

### What is actually guaranteed

| Scope | What happens |
|---|---|
| All layout | Indentation, spacing, brace placement, blank lines, reflow — 100% of them. Nothing in this class reaches the context window. |
| Syntax style (opt-in) | Braces, expression bodies, file-scoped namespaces, modifier order, using placement — for every key you set. |
| Semantic remainder (after build) | `var`, unused usings, `readonly` and more. `kerf cleanup` reads the build's diagnostics and applies the rewrites. See [Cleanup](cleanup.md). |
| Not handled, deliberately | Naming, unused members, unread assignments. Their fixes can compile and still change which overload binds. {{product}} reports them and says why. |

See [AI coding agents](../design-principles/ai-native.md) for more detail on why the pass split makes
this possible.

## MSBuild

The MSBuild package runs {{product}} as part of every `dotnet build` — formatting before `CoreCompile`
in debug, checking in release. When nothing changed, MSBuild skips the target entirely via stamp file.
When one file did change, a formatting cache skips every file that is still formatted. See
[MSBuild integration](msbuild.md) for full configuration options and how to scope formatting to
specific projects.

## CI/CD

In CI, use `kerf check` rather than `kerf format`. It exits non-zero if any file would change, and
changes nothing.

```sh
kerf check ./src
```

A typical GitHub Actions step:

```yaml
- name: Check formatting
  run: kerf check ./src
```

If you use the MSBuild package, the build integration already checks in `Release` configuration. You
may not need a separate step at all. The MSBuild integration rewrites in `Debug` and checks in
`Release` by default.

Formatting is applied deterministically, so a pull request contains its actual change and nothing else.
Reviewers stop reading past whitespace.

## Pre-commit hooks

`kerf check` can be used as a git pre-commit hook to catch formatting issues before they are committed.
Point `--cache` at `.git/kerf.cache` and most runs cost a hash comparison per file rather than a full
parse — `.git/` is never committed, and entries expire after seven days, so nothing has to clean up
behind it.

### Plain git hook

```sh
#!/bin/sh
kerf check --cache .git/kerf.cache ./src
```

Save to `.git/hooks/pre-commit` and make it executable (`chmod +x .git/hooks/pre-commit`).

### Husky.NET

With [Husky.NET](https://alirezanet.github.io/Husky.Net/):

```json
{
  "command": "kerf",
  "args": ["check", "--cache", ".git/kerf.cache", "./src"],
  "pathMode": "absolute"
}
```

Add this task to your `.husky/task-runner.json`. Husky handles the hook installation for the whole team.

{{product}} verifies every file before writing it and leaves anything that fails verification
untouched. See [Safety](../design-principles/safety.md).
