---
navigation_title: Reflow
description: What max_line_length does — it sets a width and it selects how Kerf decides line breaks — and what each mode costs.
---

# Reflow

Reflow is the one layout decision
[IDE0055](https://learn.microsoft.com/dotnet/fundamentals/code-analysis/style-rules/ide0055) has no
opinion about: where to break a line that is too long. `dotnet format` never wraps. Prettier-style
formatters always do, and decide everything else themselves.

`max_line_length` is where {{product}} splits the difference:

```ini
[*.cs]
max_line_length = 120
```

## The two modes

| | **No `max_line_length`** | **A width** |
|---|---|---|
| Line lengths | never changed | wrapped to the width |
| Where breaks come from | yours, reproduced | the tokens and the width |
| Formatting twice | idempotent, by measurement | idempotent, by construction |
| Fixed point of `dotnet format` | 1196/1196 | **1196/1196** |

With no width, {{product}} is a whitespace formatter. It fixes indentation, spacing, brace placement
and blank lines, and leaves every line exactly as long as it was.

With a width, {{product}} owns the layout. Line breaks become a function of your tokens and your
width, not of where the previous author happened to press return. A construct either fits on one line
or breaks; there is no third state carried over from the file's history.

## Formal properties

Two properties hold, and they are different properties.

*Idempotency* — `f(f(x)) = f(x)`. Formatting twice gives the same result as formatting once. In the
no-width mode this is measured on every fixture and on the full corpus. In the width mode it follows
from the design: tokens do not move under formatting, so the second run asks the same question of the
same input and gets the same answer. The practical consequence is that `kerf check` cannot report a
file that `kerf format` just wrote.

*Input-independence* — `f(x) = f(y)` when `tokens(x) = tokens(y)`. This is the stronger property,
and it only holds in the width mode. Two files with different whitespace but identical tokens produce
identical formatted output. In formal-methods terms it is closest to *confluence* — all starting
points reach the same normal form — or *canonicality*. Without a width, `csharp_keep_existing_linebreaks`
reproduces the breaks the author chose, so the property does not hold; that is deliberate.

The reasoning behind which keys are admissible in each mode is in the git history under
`docs/contribute/layout-decisions.md`.

## What it costs

See [Churn](../benchmarks/churn.md) for the file and line counts across configurations, and a worked
example of what {{product}}'s first run looks like on a repository that is already `dotnet format`-clean.

## Keeping your own line breaks

```ini
[*.cs]
max_line_length = 120
csharp_keep_existing_linebreaks = true
```

Now {{product}} only breaks lines that are too long, and reproduces the breaks you already have. This is
the closest thing to "wrap, but do not touch anything else".

The trade is that this reintroduces the one class of bug the deterministic mode does not have — an option
whose answer depends on layout that {{product}} itself produced. Several ReSharper keys are therefore
unavailable here and say so when you set them; see [what needs a width](#keys-that-need-a-width).

`csharp_keep_existing_linebreaks = false` without a `max_line_length` is refused (**KERF1007**). With no
width nothing is ever too long, so it would join every construct in the file onto one line.

## Adopting a width

Setting a width on an existing repository is one large commit. `kerf check` will report most of your
files on the first run, and [the build integration](../workflow/msbuild.md) will rewrite them on the
first compile.

Nothing forces you to take it in one step. A repository can adopt {{product}} with no `max_line_length`
— where it is a `dotnet format whitespace` equivalent — and add a width later, as its own commit, when
the churn is convenient.

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
preserving mode other rules read that nesting's indentation from your source, so the file formats
differently on the second pass. Deterministic layout has no rule that reads indentation from the source,
so the keys become admissible there and nowhere else.

Two IDE0055 keys go the other way. `csharp_preserve_single_line_blocks` and
`csharp_preserve_single_line_statements` ask {{product}} to keep what *you* put on one line, which a
width takes out of their hands; both report **KERF1004** with what they still do. An empty `{ }` stays
collapsed either way.
