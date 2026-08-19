---
navigation_title: Churn
description: What churn is, how much to expect on first adoption, and how idempotency means Curb and dotnet format agree forever after.
---

# Churn

Churn is how many files Curb rewrites on its first run over a repository that already passes its
existing formatter. The first Curb commit is a diff. This page says what is in it.

## How much to expect

Most repositories will see files changed on the first run — even those that are already
`dotnet format`-clean. The diff is confined to ground `dotnet format` does not cover: blank-line
normalisation, using-directive sorting, chain breaking, comment alignment. With `max_line_length`
set, add reflow on top.

How many files depends on the repository. The [benchmark table](index.md) shows the spread across
twelve real .NET projects.

The table below is from one corpus (elastic/docs-builder, 1,196 files, already 100% IDE0055-clean),
measured across three configurations:

| Configuration | Files changed |
|---|---|
| no `max_line_length` | 669 of 1,196 |
| `max_line_length = 160` | 892 of 1,196 |
| `max_line_length = 160` + `csharp_keep_existing_linebreaks = true` | 685 of 1,196 |

File count understates what a width does — the line count in the `max_line_length = 160` case is
roughly 3× higher, because only that mode rewraps anything. If you're deciding whether to set a
width, the line count is the honest number.

## After the first commit, Curb and dotnet format agree

Churn is a one-time cost. Once the initial commit is in, idempotency takes over.

Curb's output is a fixed point of `dotnet format whitespace`: run `dotnet format` over Curb-formatted
code and nothing changes. This is not a claim — it is a CI-gated measurement, checked against
1,196 files on every push.

The practical consequence: Format Document in your IDE won't undo what Curb wrote. `dotnet format`
in CI won't produce a diff. The two tools genuinely agree, and they stay that way. That is what
"plays nice with existing tooling" means in practice.

### Not-fixed-point count per repository

This is the number of files where `dotnet format whitespace` disagrees with Curb's output — ideally
zero. A non-zero count means Format Document in the IDE can partially undo Curb's work on those files.

| repo | not-fixpt files |
|---|---|
| serilog | 4 |
| FluentValidation | 216 |
| RestSharp | 15 |
| logging-log4net | 11 |
| AutoMapper | 15 |
| Humanizer | 10 |
| quartznet | 1 |
| Newtonsoft.Json | 17 |
| ServiceStack | 290 |
| MassTransit | 62 |
| efcore | 533 |
| roslyn | 0 |

The roslyn case — 17,167 files, already exactly `dotnet format`-clean — shows zero. Most of the
non-zero numbers come from a known limitation with multi-line trivia line endings. See
[known limitations](../known-limitations.md). The efcore number (533) is the most affected because
efcore uses XML doc comments heavily.

### Idempotency: files changed on second run

This is how many files change when Curb is run a second time over its own output. The number must
always be zero — a non-zero count means the formatter has not reached a fixed point. Across all twelve
repositories the count is zero.

| repo | 2nd-run changes |
|---|---|
| serilog | 0 |
| FluentValidation | 0 |
| RestSharp | 0 |
| logging-log4net | 0 |
| AutoMapper | 0 |
| Humanizer | 0 |
| quartznet | 0 |
| Newtonsoft.Json | 0 |
| ServiceStack | 0 |
| MassTransit | 0 |
| efcore | 0 |
| roslyn | 0 |

## What's in the diff

The first-run churn is ground `dotnet format` doesn't cover:

- Blank-line normalisation
- Using-directive sorting
- Chain breaking
- Comment alignment
- Reflow to `max_line_length` (only if set — see below)
- BOM handling (see [known limitations](../known-limitations.md) for the current caveat)

## Reducing churn on adoption

Nothing forces you to take the full diff in one commit.

Omitting `max_line_length` means no reflow happens. Curb becomes a whitespace formatter: it fixes
indentation, spacing, brace placement, and blank lines, and leaves every line exactly as long as it
was. That makes the first-run diff smaller — confined to whitespace-within-lines and blank-line
normalisation. See [Reflow](../design-principles/reflow.md) for what the key does and what each mode
costs.

You can adopt Curb with no `max_line_length`, and add a width later as its own commit when the churn
is convenient.

`curb print-config Foo.cs` prints every resolved option and says which mode you are in. Worth running
before the reformatting commit rather than after.

To reproduce these numbers for your own repository:

```sh
./build.sh churn --corpus /path/to/repo
```
