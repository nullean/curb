---
navigation_title: Benchmarks
description: A measurement run over 41,000 files of real .NET source across twelve repositories.
---

# Benchmarks

A measurement run over 41,000 files of real .NET source, timing three tools on identical copies of
each repository.

## Method

Twelve repositories, cloned shallow, each formatted from a pristine copy. Kerf is the published
native-AOT binary, not the JIT build. After formatting, `dotnet format whitespace` was run over
Kerf's output and the differences counted — that is the "not-fixpt" number, and it decides whether
Format Document in an IDE will fight the formatter. Kerf was also run a second time over its own
output to verify idempotency.

Two repositories have no `.editorconfig` at all (Newtonsoft.Json, ServiceStack), which is the
onboarding case for a repository that has never configured anything.

## Results

<!-- RESULTS -->

| repo | files | Kerf | dotnet format | Kerf not-fixpt | 2nd idem |
|---|---|---|---|---|---|
| serilog | 216 | 0.06 s | 1.23 s | 4 | 0 |
| FluentValidation | 219 | 0.06 s | 1.48 s | 216 | 0 |
| RestSharp | 255 | 0.07 s | 1.35 s | 15 | 0 |
| logging-log4net | 376 | 0.15 s | 3.15 s | 11 | 0 |
| AutoMapper | 512 | 0.12 s | 2.33 s | 15 | 0 |
| Humanizer | 733 | 0.37 s | 3.35 s | 10 | 0 |
| quartznet | 765 | 0.30 s | 2.77 s | 1 | 0 |
| Newtonsoft.Json | 945 | 0.26 s | 3.47 s | 17 | 0 |
| ServiceStack | 4,718 | 1.29 s | 9.96 s | 290 | 0 |
| MassTransit | 5,502 | 0.62 s | 8.30 s | 62 | 0 |
| efcore | 5,761 | 2.27 s | 17.85 s | 533 | 0 |
| roslyn | 17,167 | 7.43 s | 33.61 s | 0 | 0 |

<!-- /RESULTS -->

"Kerf not-fixpt" = files where `dotnet format` disagrees with Kerf's output — ideally zero.
"2nd idem" = files that change on a second Kerf run — must be zero.

### Speed

Kerf is 5–25× faster than `dotnet format` across the corpus, on every repository. `dotnet format`
loads the full Roslyn workspace per project and is not designed to be fast.

On unchanged projects the MSBuild stamp means no process starts at all.

### Churn

See [Churn](churn.md) for what churn means, how much to expect on first adoption, and a worked
example. The short version: roslyn (17,167 files, already exactly `dotnet format`-clean) is Kerf's
sharpest case — 9,191 files rewritten, zero not-fixed-point. All of it is ground `dotnet format`
doesn't cover: BOM handling, using-directive sorting, chain breaking, comment alignment.

## Reproducing

Shallow-clone the twelve repositories into a single directory, then:

```sh
./build.sh compare --corpus /path/to/repos
```

The results table above is written back into this file automatically. The run takes roughly 40 minutes
wall clock, dominated by `dotnet format` on roslyn and efcore.
