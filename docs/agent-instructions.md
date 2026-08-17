# Instructions for a coding agent

Paste the block below into your repository's `AGENTS.md` or `CLAUDE.md`. It is deliberately short: the
whole point of the design is that there is nothing to learn beyond "build, then clean up."

---

```markdown
## Code style

Formatting is fixed automatically before the compiler runs, so `dotnet build` never reports IDE0055.

If a build reports code style diagnostics (the `IDEnnnn` series), run:

    kerf cleanup

then build again. It fixes the rules it owns — `kerf rules` lists them — using the diagnostics the build
already produced. It never builds anything itself, so it must be run after a build, not before.

What is left after that needs `dotnet format style`, or a human. `kerf cleanup` reports what it declined
and why rather than failing quietly.

To have it hand the remainder on automatically, add `--forward`. That is much slower — it loads an MSBuild
workspace, seconds per solution against Kerf's milliseconds — and the run prints both timings so it is
clear which half the wait belongs to.
```

---

## Why this shape

An agent has one habit worth protecting: it runs `dotnet build` and reads the errors. Anything that
requires it to pass an extra flag, remember a pre-step, or understand a tool's internals gets skipped
under pressure.

So:

- **`dotnet build` stays exactly as it is.** No flag, no wrapper, no environment variable. The
  `Nullean.Kerf.MSBuild` package makes the compiler write its diagnostics down as a side effect; nothing
  about the command changes.
- **`kerf cleanup` takes no arguments in the common case.** It finds the logs itself.
- **The second build is not overhead.** It is the verify step the agent was going to run after any edit,
  and it is what catches a bad fix — as a compile error, immediately.
- **Nothing is silenced.** A diagnostic that is still reported after cleanup is one Kerf genuinely cannot
  fix, so the agent's attention goes where it is actually needed rather than to a mechanical offence.

## What the agent will see

Before:

```
Widget.cs(1,1): error IDE0005: Using directive is unnecessary.
Widget.cs(4,1): error IDE0005: Using directive is unnecessary.
Build FAILED.
```

After `kerf cleanup`:

```
Cleaned 1 file(s) from 1 log(s) in 102ms — 2 fix(es) in 1 file(s), 0 refused, 0 stale, 0 skipped, 0 failed
```

And the next build is clean.

## The two lines worth understanding

**`stale`** means a file changed after the build wrote its log, so nothing was applied to it — a
diagnostic's position refers to the bytes the compiler read, and applying it to different bytes is how a
tool corrupts source. Build again and it will be picked up. This is normal when the agent edited a file
between building and cleaning.

**`refused`** means Kerf understood the diagnostic and declined, with a reason on stderr. The common one
is a file containing `#if`: the compiler decided for one set of symbols, and a using directive needed
only under another would be reported as unnecessary and then lost. A refusal is a correct outcome, not a
failure.
