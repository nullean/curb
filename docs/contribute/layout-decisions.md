---
navigation_title: Layout decisions
description: What a layout rule is allowed to read, and the measured failures behind that constraint.
---

# Layout decisions — what a rule is allowed to read

A development note about the one class of bug that has killed more Kerf features than every other
cause combined, and about the architectural fork underneath it.

Read this before adding any option that decides *where a line break goes*. Spacing options are not
affected; this is only about rules that add, remove or move breaks.

## The rule

> A layout rule may read the **tokens**, and it may read **layout the author owns**.
> It may never read **layout Kerf itself decides**.

Everything below is that sentence, with evidence.

## Why it is a gate and not a preference

```sh
kerf format file.cs     # Kerf rewrites the file
kerf check   file.cs    # -> "this file needs formatting"
```

That is the failure mode. Kerf's own output is rejected by Kerf. CI runs `check`, so a repository
formatted by Kerf would fail its own build, and the MSBuild task would rewrite files on every
compile. `format(format(x)) == format(x)` is asserted on every fixture and on the whole corpus for
exactly this reason — see **Correctness** in
[AGENTS.md](https://github.com/Mpdreamz/formatter/blob/main/AGENTS.md).

Note that a rule can fail this while still *converging*. The attribute family below settles by the
third run. One pass is the contract, so converging late is still broken.

## The discriminator

Kerf reads source layout all over the printer and is idempotent. Reading layout is not the problem.
The problem is reading a property whose value **Kerf's own output changes**.

| The rule asks | Who decides it | Safe |
|---|---|---|
| Is this a method? Does it have a base list? | the grammar | yes |
| Where does this construct start? | the author; Kerf never moves it | yes |
| Did the author break at this dot / before this element? | the author; Kerf **reproduces** it | yes |
| Is this member on one line? | **Kerf's reflow** | **no** |
| Did this parameter list wrap? | **Kerf, on this same run** | **no** |
| Does this construct span lines? | **Kerf's reflow** | **no** |

The distinction is not "does it touch layout". It is **does Kerf preserve that property, or produce
it**. `OnSameLine(node.SpanStart, node.Span.End)` is safe when the span cannot be reflowed and a
loaded gun when it can.

## The failure, traced

`place_method_attribute_on_same_line = if_owner_is_single_line`, `max_line_length = 160`.

```csharp
// input — the body is wrapped, so the member spans two lines
[JsonIgnore]
public string? GitHubRepository =>
    Remote is "elastic/docs-builder-unknown" ? null : ExtractGitHubOrgRepo(Remote);
```

Run 1 asks *is the member one line?* → **no** → the attribute stays above. Reflow then joins the
body, because it fits in 160:

```csharp
// run 1 output — the member is now one line
[JsonIgnore]
public string? GitHubRepository => Remote is "..." ? null : ExtractGitHubOrgRepo(Remote);
```

Run 2 asks the same question of *that* text → **yes** → joins the attribute. Run 1 changed the
answer to its own question.

## Case files

Every one of these was implemented, measured on the 1,196-file corpus, and reverted. The numbers are
kept so nobody re-derives them.

| Rule | What it read | Result |
|---|---|---|
| `blank_lines_around_single_line_*` | is the member one line | 2 files never settle |
| `place_*_attribute_on_same_line` | is the member one line | 68 never settle, 16 lose the fixed point |
| initializer joining (`keep_existing_initializer_arrangement`) | does the initializer fit | 5–7 files never settle, 1182–1191/1196 conformance, across four attempts |
| `chop_always` for arguments / chains / initializers | the enclosing construct's layout | reverted, same class |

The attribute family is the one worth studying, because the cheap measurements cleared it:

| Measurement | Verdict |
|---|---|
| hand-written probe | free ground — `dotnet format` left a joined attribute alone |
| corpus, join unconditionally | **347** files moved — the real rule is *only when the member is one line* |
| corpus, `if_owner_is_single_line`, reflow **off** | conformance back to baseline, 0 non-idempotent |
| corpus, `if_owner_is_single_line`, reflow **on** | **68** never settle, **16** lose the fixed point |

Two of that family's six keys are inadmissible for an unrelated reason, and it is a good example of
the classification rule in **Rider keys** below: `dotnet format` *moves* a type's attribute and an
accessor's onto their own line, so `always` could never be a fixed point there whatever Kerf did.

## The two safe shapes

Everything that shipped is one of these.

**1. Unconditional.** The rule reads options and grammar, nothing else.

```csharp
// csharp_wrap_before_extends_colon — the break is always there or never
if (context.Options.WrapBeforeExtendsColon)
    using (arena.Indent()) { arena.HardLine(); Node.Print(baseList, context); }
else
    { Spacing.BeforeInheritanceColon(context); Node.Print(baseList, context); }
```

**2. Aimed at a group.** The rule keys on a break decision Kerf is making *on this run*, recomputed
identically from tokens and width every time.

```csharp
arena.IfBreak(groupId)          // this branch iff that group broke
arena.IndentIfBroken(groupId)   // indent iff that group broke
arena.LineIfBroken(groupId)     // break iff that group broke
```

`csharp_wrap_before_arrow_with_expressions` is the exemplar. It looks like the dead rules and is
completely safe, because it never asks *whether* to break — the group had already decided that — it
only says which side of that break the `=>` sits on:

```csharp
int M() => Something(aaaa, bbbb);   // fits: byte-identical with the option on or off

int M()                             // doesn't fit: the arrow moves across the break
    => Something(aaaaaaaaaaa, bbbbbbbbbbb, ccccccccccc);
```

**The primitives already existed when the dead rules were written. They asked the source instead.**
That is the practical lesson: if a rule needs to know about a break, aim it at the group, never at
`SpansLines`.

## Alignment is a different failure — do not confuse them

`align_multiline_*` does not oscillate. It fails because two systems disagree about what indentation
*is*. `dotnet format` anchors a brace-bringing construct to *the indentation of the line it starts
on*, and under alignment that indentation **is** the anchor column:

```csharp
// what dotnet format demands once arguments are aligned
new ScopedFileSystem(inner, new ScopedFileSystemOptions([root])
                                                        {
                                                            AllowedHidden = ...
                                                        })

// what Kerf writes — its indent stack holds levels, not columns
new ScopedFileSystem(inner, new ScopedFileSystemOptions([root])
{
    AllowedHidden = ...
})
```

To Kerf, "aligned at column 52" and "aligned at column 8" are the same value. `Anchor` /
`AlignedLine` / a depth-indexed register file all work, and alignment itself came out correct
including nested calls — but every nested construct that anchors meets the mismatch:

| Step | Conformance failures | Never settle |
|---|---|---|
| first cut | 76 | 22 |
| stop compensating for an indent scope no longer opened | 39 | 16 |
| lists holding a brace-bringing argument stand down | 24 | 15 |

The tail does not close, because the problem is not arguments. The cost is making indentation
column-valued throughout — the one thing the hot path cannot absorb quietly.

**This is not a loss.** Kerf's rendering above is the better one and *is* a fixed point in the
default configuration; the 24 failures only appear with the option on. Roslyn's anchor rule makes
argument alignment actively hostile to nested constructs, so the feature is worth less than it looks
even where it is expressible.

## Rider keys: classify before implementing

ReSharper having a key says nothing about whether Kerf may honour it. Before adopting one, measure
which of two cases `dotnet format` falls into:

- **Declines to decide** — free ground. Kerf may choose, and its choice survives Format Document.
- **Decides the other way** — inadmissible. The option would be undone on the next save.

Measured examples: trailing commas and blank lines are free ground; `space_between_attribute_sections`
is not (`dotnet format` *removes* the space, and writes `[A][B]`); a type's or accessor's attribute
placement is not.

Where documentation and the tool disagree, **the tool wins** — it is what the fixed-point property is
measured against.

## The measurement ladder

Each rung caught something the rung above passed. Nothing below rung 4 can green-light a rule that
changes line counts.

| # | Check | Missed |
|---|---|---|
| 1 | hand-written probe | the attribute family, alignment |
| 2 | unit tests | the arrow key's 13 files — every test started from a body that already had an arrow |
| 3 | corpus, reflow **off** | the attribute family, at baseline conformance |
| 4 | corpus, reflow **on** | — killed the attribute family |
| 5 | second format pass over the corpus | — killed the blank-line family |

Two traps in running these, both hit for real:

- Do not copy reference files **into** the tree you are about to format. `dotnet format .` normalises
  both sides and the diff is empty by construction.
- Establish the baseline by **measuring** it, not assuming it. A fix that improved both axes was
  nearly discarded because 41 failures were compared against an assumed baseline of 1; the measured
  baseline was 43.

## The other mode, and why it is not the default

The whole class dissolves if layout is a pure function of tokens and width:

```
layout = f(tokens, width)
```

Tokens are invariant under formatting, so `f(f(x)) == f(x)` holds by construction. There is no layout
question to answer differently, because none is asked. Every rule in the case-file table becomes
implementable. It would likely also be *faster*: `OnSameLine` / `SpansLines` hit
`Text.Lines.GetLineFromPosition` on the hot path, and that mode deletes those calls.

It even has a key already, so Kerf would still invent nothing:
**`csharp_keep_existing_linebreaks = false`** (with `csharp_keep_user_linebreaks` alongside it).

Ordering alone is *not* enough, and this is worth understanding before anyone tries the halfway
house. "Normalise first, then decide" fixes the author-input half and leaves the width feedback: pass
B joins an attribute onto a member's line, which makes the line longer, which changes what pass A
wraps on the next run. The loop is smaller, not gone. The property has to be *not read*, not *read
later*.

**Decision: not the default, and not now.**

Onboarding with zero or near-zero churn is the product promise — for repositories with an existing
`.editorconfig` and for those without. A formatter that rewrites every file the moment it is
installed does not get installed twice. Current default churn is **742 of 1,196** corpus files;
preservation is what bought that (the plan records the earlier figure at 897 files / −4,562 lines
before preservation work). A deterministic mode is the largest diff available and its churn is
unmeasured.

It is worth revisiting as an **opt-in mode** once the default story is settled. When someone does:
measure its churn and publish it before it ships, and expect the dead rules above to come back to
life inside it.

## Checklist for a new layout rule

1. Does it read any source layout? If no, it is safe — go.
2. If yes: can Kerf's own output change that property? If yes, **stop** — aim it at a group instead.
3. Classify against `dotnet format`: declines to decide, or decides the other way?
4. Corpus with reflow **on**, formatted twice. Both numbers, against a **measured** baseline.
5. If it fails, revert and record the numbers here rather than the intent.
