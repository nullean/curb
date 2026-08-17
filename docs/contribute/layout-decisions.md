---
navigation_title: Layout decisions
description: What a layout rule is allowed to read, and the measured failures behind that constraint.
---

# Layout decisions — what a rule is allowed to read

A development note about the one class of bug that has killed more Kerf features than every other
cause combined, and about the architectural fork underneath it.

Read this before adding any option that decides *where a line break goes*. Spacing options are not
affected; this is only about rules that add, remove or move breaks.

Kerf has **two layout modes**, and which one you are writing for changes what is admissible. **The mode
is selected by `max_line_length`**: asking for reflow is asking Kerf to decide layout, so it is one
opt-in rather than two.

| | no `max_line_length` | a width (**the default with one**) | a width plus `csharp_keep_existing_linebreaks = true` |
|---|---|---|---|
| Where breaks come from | the author's, reproduced | width alone | the author's; width decides the rest |
| The rule below | **binding** | does not apply — no such layout to read | **binding** |
| Fixed point of `dotnet format` | 1196/1196 | **1196/1196** | 1195/1196 |
| `format(format(x)) == format(x)` | by measurement | **by construction** | by measurement |
| Corpus churn | 742 files, 17,035 changed lines | 892 files, ~47,000 | 742 files, 17,580 |

Note which column is cleanest against `dotnet format`: the deterministic one, because it has no source
arrangement to disagree about. The rule below is therefore about the *other two* columns — and it is
still binding there, because those are what a repository with no width, or an explicit opt-out, gets.

## The rule

> In preservation mode, a layout rule may read the **tokens**, and it may read **layout the author
> owns**. It may never read **layout Kerf itself decides**.

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

| Rule | What it read | Result | In deterministic mode |
|---|---|---|---|
| `blank_lines_around_single_line_*` | is the member one line | 2 files never settle | still blocked — see below |
| `place_*_attribute_on_same_line` | is the member one line | 68 never settle, 16 lose the fixed point | **shipped**, 0 / 100% |
| initializer joining (`keep_existing_initializer_arrangement`) | does the initializer fit | 5–7 files never settle, 1182–1191/1196 conformance, across four attempts | **implicit** — no key needed |
| `chop_always` for arguments / initializers | the enclosing construct's layout | 140 files never settle | **shipped**, 0 / 100% |
| `chop_always` for chains | the enclosing construct's layout | 156 files never settle | not attempted |

The right-hand column is the point of the whole note. Nothing in the middle column was a bad idea; each
was a rule asking a question the mode it was written for could not answer.

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
| 6 | corpus in **both** modes | — caught the attribute-section space, at 10 files, and `= true`, at 347 |

Rung 6 is not optional now that there are two modes. Both of the things it caught were invisible in the
other one: `[A] [B]` looked right until `dotnet format` glued it, and unconditional joining looked right
until it was asked of a member that spans lines.

Two traps in running these, both hit for real:

- Do not copy reference files **into** the tree you are about to format. `dotnet format .` normalises
  both sides and the diff is empty by construction.
- Establish the baseline by **measuring** it, not assuming it. A fix that improved both axes was
  nearly discarded because 41 failures were compared against an assumed baseline of 1; the measured
  baseline was 43.

## Deterministic layout, which a width selects

Layout becomes a pure function of tokens and width:

```
layout = f(tokens, width)
```

Tokens are invariant under formatting, so `f(f(x)) == f(x)` holds by construction. There is no layout
question to answer differently, because none is asked.

**It cannot be reached without a width, and that is the design.** With `max_line_length = off` the
printer treats the width as infinite and every unbroken group prints flat, so deterministic layout with
no width would join every construct that fits — the largest diff Kerf can produce rather than the
smallest. That used to be papered over with an implied 160, a magic number nobody typed. Gating the mode
on the width instead makes the bad configuration unreachable: `csharp_keep_existing_linebreaks = false`
with no width is refused outright (**KERF1007**) rather than given a width to reflow against.

### Why the width, and not everywhere

The measurement that decided it. By **files** deterministic layout looks like a 20% step from
preservation (742 → 892 of 1,196). By **changed lines** it is 2.7× — 17,035 → ~47,000 — because with no
width Kerf never touches a line's *length*, so its diff is confined to blank lines and spacing, while a
width rewraps the shape of every construct.

A repository that never named a width has not asked for that, and a key spelled *keep existing
linebreaks* is not where anyone would look for the reason it happened. File count would have hidden the
whole difference; it is the wrong metric for this decision and it is worth remembering which metric
answered it.

One correction while measuring this, which matters for anyone quoting the README: **the zero-churn claim
was already false.** Kerf rewrites 742 corpus files with reflow off, almost all of it collapsing runs of
blank lines to one — `csharp_keep_blank_lines_in_*`, which are ReSharper keys rather than IDE0055 ones,
so free ground Kerf chose to occupy. The baseline was never zero.

### What it is measured at

All of it gated in CI, `./build.sh conformance` and `./build.sh churn`:

| | no width | width 160 (default) | width 120 | width 160, `--preserve` |
|---|---|---|---|---|
| Fixed point of `dotnet format` | 1196/1196 | **1196/1196** | 1195/1196 | 1195/1196 |
| Files that never settle | 0 | **0** | 0 | 0 |
| Churn | 742 files, 17,035 lines | 892 files, ~47,000 | 920 files | 742 files, 17,580 |

Deterministic layout is the **better** fixed point of `dotnet format`, not a worse one. The two columns
that lose a file lose the *same* file — a property-pattern brace `dotnet format` moves to its own line,
recorded as held rather than chased.

### What it unlocks

Three of the five entries in the case-file table, plus two keys the binder used to refuse:

- `csharp_place_method_attribute_on_same_line` — and field, property, event —
  `= if_owner_is_single_line`. The family that cost 68 files and 16 fixed points. It works now because
  the attribute and the member are inside **one group**, so "does this fit on a line" is asked once with
  the join already counted. Ordering alone would not have done it: the width feedback is the second half
  of the loop, and putting the two in one measurement is what closes it.
- `csharp_wrap_arguments_style` and `csharp_wrap_object_and_collection_initializer_style` =
  `chop_always`. 140 files never settled in preservation mode. Zero here.
- Initializer joining, with no key at all. In this mode there is no existing arrangement to keep.

### What it does not unlock

- **`csharp_place_*_attribute_on_same_line = true`.** Expressible, idempotent, and still refused —
  KERF1006. `dotnet format` *moves* an attribute back off a member that spans lines, so joining
  unconditionally came back changed on **347 of 1,196** files (849/1,196 conformance). Its own rule turns
  out to be `if_owner_is_single_line`. This is the cleanest measured example of the *decides the other
  way* case: no amount of care inside Kerf can fix a value the reference implementation overrules.
- **`align_multiline_*`.** Its failure is unrelated and determinism does not touch it — see
  [Alignment](#alignment-is-a-different-failure-do-not-confuse-them). Do not expect it here.
- **A type's or an accessor's attribute placement.** Same reason as always: `dotnet format` decides
  those the other way, in either mode.
- **`csharp_wrap_chained_method_calls`.** 156 files in preservation mode and unmeasured here. Likely
  fine; say so with a number before shipping it.
- **`blank_lines_around_single_line_*`**, and the reason is worth knowing because it is not the usual
  one. The rule is admissible — "is this member one line" is a fair question here — but it cannot be
  *asked*, because the blank lines are emitted **before** the member, and a group's mode is only known
  once the printing walk reaches it. `LineIfBroken` and friends read `_groupModes`, which is filled
  forward; aiming at a group that comes later reads an unset slot. What it needs is a flat-width
  estimate taken from the tokens before printing — still `f(tokens, width)`, so still admissible, but
  new machinery rather than a new key. That is the whole of what is missing.

### Known gap

`if_owner_is_single_line` does not join an attribute above a member carrying a **doc comment**. The
comment is leading trivia on the attribute's first token, so its hard lines are inside the member group
and mark it broken. The behaviour is conservative and stable — the join is skipped, never wrongly
applied — but it means the option does nothing for most public API. Closing it means excluding leading
trivia from the group, which is a change to the trivia path rather than to this family. Measured, not
chased.

### The two IDE0055 keys this costs

`csharp_preserve_single_line_blocks` and `csharp_preserve_single_line_statements` are the one place the
default flip costs a capability rather than churn, and the attempt to give them deterministic meanings
failed for a reason worth recording — see `Printers.KeepsOneLine`. Both candidate answers are wrong:
always-expand throws the option's intent away, and flatten-if-it-fits would collapse every short `if`
body into a one-liner, which `dotnet format` never produces. Even the narrow empty-`{ }` case fails,
because `dotnet format` keeps a *collapsed* empty pair but never joins an expanded one — so that rule is
preservation-flavoured too, and trying it broke 20-plus expectations asserting exactly that.

What did land is the deterministic slice of it: with a width, an empty brace pair prints `{ }` rather
than opening out. It **collapses the pair and never moves it** — putting it on the header line as well
wrote `), IAppDataFileSystem { }` where `dotnet format` wanted the brace on its own line, and cost three
corpus files. Where a brace goes stays `csharp_new_line_before_open_brace`'s decision.

So both keys are reported (**KERF1004**) with what they actually do rather than a blanket "no effect" —
and note `preserve_single_line_blocks` is only *partly* inert, because the accessor-list printer reads it
as a plain option. The capability properly belongs to ReSharper's `place_simple_*_on_single_line` family,
of which `place_simple_accessorholder_on_single_line` already works in both modes and
`place_simple_enum_on_single_line` is still preservation-gated. Making the enum one deterministic would
collapse every enum that fits, by default, in both modes — a real ReSharper behaviour with its own churn,
so its own measurement rather than a rider on this.

### Onboarding

Adoption with a width set is one large commit: `kerf check` reports most files on the first run and the
MSBuild task rewrites on the first compile. A repository that wants to adopt gradually can start with no
`max_line_length` — Kerf is then a `dotnet format whitespace` equivalent — and add a width when it is
ready for the reflow commit.

The promise deterministic layout *can* keep is **zero churn for a repository already formatted by a
deterministic formatter whose keys Kerf honours**: Rider with `csharp_keep_existing_linebreaks = false`,
or CSharpier at a matching width. That is why the ReSharper key surface is worth implementing.

## Checklist for a new layout rule

1. **Which mode is it for?** If deterministic only, the binder should say so — KERF1005 — rather than
   letting it half-work in the other one.
2. Does it read any source layout? If no, it is safe — go.
3. If yes: can Kerf's own output change that property? If yes, **stop** — aim it at a group instead. If
   the question spans two constructs (an attribute and its member), put both in **one** group rather than
   asking twice; asking twice is what collapsed a `[Theory]` and nine `[InlineData]`s onto one line.
4. Classify against `dotnet format`: declines to decide, or decides the other way? Measure it — the
   attribute family passed a hand-written probe and failed at 347 files.
5. Corpus with reflow **on**, formatted twice, in **both** modes. Every number, against a **measured**
   baseline. `./build.sh conformance` and `./build.sh churn` both take `--deterministic`.
6. If it fails, revert and record the numbers here rather than the intent.
