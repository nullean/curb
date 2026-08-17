---
navigation_title: Reflow
description: What max_line_length does — it sets a width and it selects how Kerf decides line breaks — and what each mode costs.
---

# Reflow

Reflow is the one layout decision
[IDE0055](https://learn.microsoft.com/dotnet/fundamentals/code-analysis/style-rules/ide0055) has no
opinion about: where to break a line that is too long. `dotnet format` never wraps. Prettier-style
formatters always do, and decide everything else themselves.

`max_line_length` is where {{product}} splits the difference, and it does **two** things:

```ini
[*.cs]
max_line_length = 120
```

It sets the width, and it decides **who chooses the line breaks**. Those are the same question, so
they are one key rather than two.

## The two modes

| | **No `max_line_length`** | **A width** |
|---|---|---|
| Line lengths | never changed | wrapped to the width |
| Where breaks come from | yours, reproduced | the tokens and the width |
| Formatting twice | idempotent, by measurement | idempotent, **by construction** |
| Fixed point of `dotnet format` | 1196/1196 | **1196/1196** |

**No width, and {{product}} is a whitespace formatter.** It fixes indentation, spacing, brace placement
and blank lines, and leaves every line exactly as long as it was. Where you broke a long argument list
is where it stays.

**With a width, {{product}} owns the layout.** Line breaks become a function of your tokens and your
width — not of where the previous author happened to press return. A construct either fits on one line
or breaks; there is no third state carried over from the file's history.

That second mode is why formatting twice cannot change anything. Tokens do not move under formatting, so
if breaks are decided from the tokens alone then the second run asks the same question of the same input
and gets the same answer. In the first mode idempotency is a property {{product}} measures on every
fixture and on the whole corpus; in the second it is a property of the design.

## Keeping your own line breaks

If you want reflow but want your arrangement kept where you made one:

```ini
[*.cs]
max_line_length = 120
csharp_keep_existing_linebreaks = true
```

Now {{product}} only breaks lines that are too long, and reproduces the breaks you already have. This is
the closest thing to "wrap, but do not touch anything else", and it is what the first mode does at any
width.

The trade is that this reintroduces the one class of bug the deterministic mode does not have — an option
whose answer depends on layout that {{product}} itself produced. Several ReSharper keys are therefore
unavailable here and say so when you set them; see [what needs a width](#keys-that-need-a-width).

`csharp_keep_existing_linebreaks = false` without a `max_line_length` is refused (**KERF1007**). With no
width nothing is ever too long, so it would join every construct in the file onto one line.

## What it costs

Measured on [elastic/docs-builder](https://github.com/elastic/docs-builder) — 1,196 files, 193k lines,
already 100% IDE0055-clean — and reproducible with `./build.sh churn`:

| Configuration | Files changed | Changed lines |
|---|---|---|
| no width | 669 | 15,214 |
| `max_line_length = 160` | 892 | 43,451 |
| `= 160` plus `keep_existing_linebreaks = true` | 685 | 16,252 |

Two things worth saying plainly rather than letting you find out.

**The no-width number is not zero.** It is 669 files, and almost all of it is {{product}} collapsing runs
of two or more blank lines to one. `dotnet format` has no opinion on blank lines, so this is a choice
{{product}} makes; `csharp_keep_blank_lines_in_code` and `csharp_keep_blank_lines_in_declarations` turn it
off.

**File count understates what a width does.** It is a 33% step by files but **2.9×** the changed lines,
because only the width column rewraps anything — without one, a diff is confined to whitespace *within*
lines. If you are deciding whether to set a width, the line count is the honest number.

None of it moves the conformance guarantee. A width changes which breaks {{product}} picks; it does not
change whether `dotnet format` accepts them, because breaks are what `dotnet format` declines to decide.

## Adopting a width

Setting a width on an existing repository is one large commit. `kerf check` will report most of your
files on the first run, and [the build integration](../workflow/msbuild.md) will rewrite them on the
first compile.

Nothing forces you to take it in one step. A repository can adopt {{product}} with no
`max_line_length` — where it is a `dotnet format whitespace` equivalent and the diff is small — and add a
width later, as its own commit, when the churn is convenient.

`kerf print-config Foo.cs` prints every resolved option and says which mode you are in and what selected
it. Worth running before the reformatting commit rather than after.

## Keys that need a width

Some ReSharper wrapping keys are only honoured under deterministic layout, and setting one without a
width reports **KERF1005** rather than going quiet:

| Key | What it does |
|---|---|
| `csharp_wrap_arguments_style = chop_always` | every argument on its own line, fit or not |
| `csharp_wrap_object_and_collection_initializer_style = chop_always` | the same for initializers |
| `csharp_place_method_attribute_on_same_line = if_owner_is_single_line` | join an attribute to a member that fits on one line — also `_field_`, `_property_`, `_event_` |

The reason is not arbitrary. Forcing a construct to wrap moves whatever is nested inside it, and in the
preserving mode other rules read that nesting's indentation from your source — so the file formats
differently on the second pass. 140 corpus files stopped settling. Deterministic layout has no rule that
reads indentation from the source, so the keys become admissible there and nowhere else.

Two IDE0055 keys go the other way. `csharp_preserve_single_line_blocks` and
`csharp_preserve_single_line_statements` ask {{product}} to keep what *you* put on one line, which a width
takes out of their hands; both report **KERF1004** with what they still do. An empty `{ }` stays collapsed
either way.

The full reasoning for which rules are admissible in which mode, with the measurements behind each, is in
[Layout decisions](../contribute/layout-decisions.md).
