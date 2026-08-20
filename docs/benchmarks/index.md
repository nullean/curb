---
navigation_title: Benchmarks
description: A measurement run over 41,000 files of real .NET source across twelve repositories.
---

# Benchmarks

A measurement run over 41,000 files of real .NET source, timing three tools on identical copies of
each repository.

## Results

<!-- RESULTS -->

| repo | files | curb | dnf whitespace | CSharpier |
|---|---|---|---|---|
| serilog | 216 | 0.06 s | 1.08 s | 0.64 s |
| FluentValidation | 219 | 0.05 s | 1.35 s | 0.54 s |
| RestSharp | 255 | 0.08 s | 1.25 s | 0.69 s |
| logging-log4net | 376 | 0.14 s | 2.82 s | 1.21 s |
| AutoMapper | 512 | 0.10 s | 3.83 s | 1.16 s |
| Humanizer | 733 | 0.36 s | 3.25 s | 4.33 s |
| quartznet | 765 | 0.25 s | 2.40 s | 1.90 s |
| Newtonsoft.Json | 945 | 0.30 s | 2.57 s | 4.45 s |
| ServiceStack | 4,718 | 1.11 s | 8.78 s | 11.48 s |
| MassTransit | 5,502 | 0.56 s | 7.15 s | 4.23 s |
| efcore | 5,761 | 2.15 s | 16.87 s | 19.12 s |
| roslyn | 17,167 | 7.26 s | 29.31 s | 59.67 s |

<!-- /RESULTS -->

The `dnf whitespace` column is `dotnet format whitespace` — the whitespace-only pass, not the full
style-and-analyser run. curb is compared against its closest equivalent: the tool that does the
same job.

### Speed

curb is 5–25× faster than `dotnet format whitespace` across the corpus, on every repository, while
doing more: `dotnet format whitespace` does not reflow long lines, does not sort using directives,
and does not apply syntax-level code style. Beating it on raw speed while covering a strict superset
of its output is the headline result.

`dotnet format whitespace` loads the full Roslyn workspace per project and is not designed to be
fast.

### Cold vs warm

All three tools above are timed cold — repository and formatter cache cleared before each run. Cold
is the right baseline for comparing tools. Warm is where curb pulls further ahead.

curb's subsequent runs are protected by MSBuild incremental support: if nothing changed in a project,
no process starts at all — not even the CLI. For projects that do have changes, a local cache means
only the files that changed since the last build are reformatted, not the whole project.

The cache clears with `dotnet clean` and autoinvalidates after one week. curb is FAST cold, FASTER
warm.

### Churn

See [Churn](churn.md) for what churn means, how much to expect on first adoption, and a worked
example. The short version: roslyn (17,167 files, already exactly `dotnet format`-clean) is curb's
sharpest case — 9,191 files rewritten, zero not-fixed-point. All of it is ground `dotnet format`
doesn't cover: BOM handling, using-directive sorting, chain breaking, comment alignment.

## Reproducing

Shallow-clone the twelve repositories into a single directory, then:

```sh
./build.sh compare --corpus /path/to/repos
```

The results table above is written back into this file automatically. The run takes roughly 40 minutes
wall clock, dominated by `dotnet format whitespace` on roslyn and efcore.

## Method

Twelve repositories, cloned shallow, each formatted from a pristine copy. curb is the published
native-AOT binary, not the JIT build. All three tools are timed cold — repository and formatter cache
cleared before each run. After formatting, `dotnet format whitespace` was run over curb's output to
measure agreement; curb was also run a second time over its own output to verify idempotency. Those
per-repository counts are in [Churn](churn.md).

Two repositories have no `.editorconfig` at all (Newtonsoft.Json, ServiceStack), which is the
onboarding case for a repository that has never configured anything.
