---
navigation_title: Churn
description: What churn is, how much to expect on first adoption, and a worked example across configurations.
---

# Churn

Churn is how many files and lines Kerf rewrites on its first run over a repository that already passes
its existing formatter. The first Kerf commit is a large diff. This page says how large, and what is
in it.

## How much to expect

Measured on [elastic/docs-builder](https://github.com/elastic/docs-builder) — 1,196 files, 193k
lines, already 100% IDE0055-clean — and reproducible with `./build.sh churn`:

| Configuration | Files changed | Changed lines |
|---|---|---|
| no width | 669 | 15,214 |
| `max_line_length = 160` | 892 | 43,451 |
| `= 160` plus `csharp_keep_existing_linebreaks = true` | 685 | 16,252 |

Two things to read plainly.

The no-width number is not zero. It is 669 files, and almost all of it is Kerf collapsing runs of two
or more blank lines to one. `dotnet format` has no opinion on blank lines, so this is a choice Kerf
makes. `csharp_keep_blank_lines_in_code` and `csharp_keep_blank_lines_in_declarations` turn it off.

File count understates what a width does. It is a 33% step by files but 2.9× the changed lines,
because only the width column rewraps anything. If you're deciding whether to set a width, the line
count is the honest number.

## What's in the diff

Kerf's output is a fixed point of `dotnet format` in all three modes above — run `dotnet format` over
the result and nothing changes. The churn is ground `dotnet format` doesn't cover:

- Blank-line normalisation
- Using-directive sorting
- Chain breaking
- Comment alignment
- Reflow to `max_line_length` (width modes only)
- BOM handling (see [known limitations](../known-limitations.md) for the current caveat)

## The roslyn case

The sharpest example from the [twelve-repository comparison](index.md):

| repo | Kerf changed | dotnet format changed | Kerf not-fixpt |
|---|---|---|---|
| roslyn | 9,191 | **0** | **0** |
| efcore | 5,288 | 5,340 | 533 |
| MassTransit | 5,291 | 411 | 62 |
| Newtonsoft.Json | 897 | 72 | 17 |

roslyn's 17,167 files are already exactly `dotnet format`-clean. Kerf rewrites 9,191 of them. Its
output is still a fixed point — `dotnet format` changes 0 of it — so this is all free ground: the
categories above, at roslyn scale.

efcore's 533 not-fixed-point files are explained by a known limitation (multi-line trivia line
endings), which accounts for most cross-tool conformance failures in the corpus. See
[known limitations](../known-limitations.md).

## Reducing churn on adoption

Nothing forces you to take it all in one commit. A repository can adopt Kerf with no `max_line_length`
— where it is a `dotnet format whitespace` equivalent and the diff is small — and add a width later,
as its own commit, when the churn is convenient.

`kerf print-config Foo.cs` prints every resolved option and says which mode you are in. Worth running
before the reformatting commit rather than after.

To reproduce the numbers above for your own repository:

```sh
./build.sh churn --corpus /path/to/repo
```
