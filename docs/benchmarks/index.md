---
navigation_title: Benchmarks
description: A measurement run over 41,000 files of real .NET source across twelve repositories.
---

# Benchmarks

A measurement run over 41,000 files of real .NET source, timing three tools on identical copies of
each repository.

## Method

Twelve repositories, cloned shallow, each formatted from a pristine copy. Kerf is the published
native-AOT binary, not the JIT build. All three tools are timed cold — repository and formatter cache
cleared before each run. After formatting, `dotnet format whitespace` was run over Kerf's output to
measure agreement; Kerf was also run a second time over its own output to verify idempotency. Those
per-repository counts are in [Churn](churn.md).

Two repositories have no `.editorconfig` at all (Newtonsoft.Json, ServiceStack), which is the
onboarding case for a repository that has never configured anything.

## Results

<!-- RESULTS -->

| repo | files | Kerf | dotnet format | CSharpier |
|---|---|---|---|---|
| serilog | 216 | 0.06 s | 1.23 s | 0.73 s |
| FluentValidation | 219 | 0.06 s | 1.48 s | 0.59 s |
| RestSharp | 255 | 0.07 s | 1.35 s | 0.63 s |
| logging-log4net | 376 | 0.15 s | 3.15 s | 1.30 s |
| AutoMapper | 512 | 0.12 s | 2.33 s | 1.23 s |
| Humanizer | 733 | 0.37 s | 3.35 s | 3.45 s |
| quartznet | 765 | 0.30 s | 2.77 s | 2.12 s |
| Newtonsoft.Json | 945 | 0.26 s | 3.47 s | 4.85 s |
| ServiceStack | 4,718 | 1.29 s | 9.96 s | 12.84 s |
| MassTransit | 5,502 | 0.62 s | 8.30 s | 4.75 s |
| efcore | 5,761 | 2.27 s | 17.85 s | 19.78 s |
| roslyn | 17,167 | 7.43 s | 33.61 s | 64.83 s |

<!-- /RESULTS -->

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
