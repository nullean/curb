---
navigation_title: Benchmarks
description: A measurement run over 41,000 files of real .NET source across twelve repositories.
---

# Kerf vs CSharpier vs dotnet format, on twelve real repositories

A measurement run over 41,000 files of real .NET source, timing three tools on identical copies of
each repository.

## Method

Twelve repositories, cloned shallow, each formatted from a pristine copy by each tool. Kerf is the
published native-AOT binary, not the JIT build. For Kerf and CSharpier, `dotnet format whitespace`
was then run over their output and the differences counted — that is the fixed-point number, the one
that decides whether Format Document in an IDE will fight the formatter. Kerf was also run a second
time over its own output to check it settles.

CSharpier is timed twice: cold (cache cleared before each of three runs; best-of-3 taken) and warm
(one run immediately after, with the cache populated). The warm number is what CSharpier reports if
you've already formatted the same content once on this machine. The cold number is what any developer
sees on a fresh checkout, a CI runner, or after `dotnet clean`.

Two repositories have no `.editorconfig` at all (Newtonsoft.Json, ServiceStack), which is the
onboarding case for a repository that has never configured anything.

## Results

<!-- RESULTS -->

```
repo               files  ec |   kerf  csp(cold) csp(warm)    dnf |    kerf    csp    dnf |   kerf   csp |   2nd
                             |         --- seconds ---            |  -- files changed --  |  not fixpt   |  idem
serilog              216   1 |   0.06      0.73      0.34   1.23 |      90    165     20 |      4     3 |     0
FluentValidation     219   1 |   0.06      0.59      0.21   1.48 |     179    218    217 |    216   216 |     0
RestSharp            255   1 |   0.07      0.63      0.30   1.35 |     238    254    171 |     15   249 |     0
logging-log4net      376   3 |   0.15      1.30      0.55   3.15 |     374    376    376 |     11     6 |     0
AutoMapper           512   1 |   0.12      1.23      0.25   2.33 |     389    431    199 |     15     6 |     0
Humanizer            733   3 |   0.37      3.45      0.51   3.35 |     487    690    171 |     10   733 |     0
quartznet            765   2 |   0.30      2.12      0.39   2.77 |     573    714     33 |      1   199 |     0
Newtonsoft.Json      945   0 |   0.26      4.85      0.82   3.47 |     897    913     72 |     17    41 |     0
ServiceStack        4718   0 |   1.29     12.84      1.87   9.96 |    4257   4525   2631 |    290   170 |     0
MassTransit         5502   1 |   0.62      4.75      0.83   8.30 |    5291   5270    411 |     62   252 |     0
efcore              5761   4 |   2.27     19.78      2.49  17.85 |    5288   5338   5340 |    533   260 |     0
roslyn             17167  38 |   7.43     64.83      6.10  33.61 |    9191  14601      0 |      0     0 |     0
```

<!-- /RESULTS -->

`ec` = number of `.editorconfig` files. `2nd idem` = files that change on a second Kerf run; it must
be zero.

### Speed

`csp(cold)` is CSharpier with its machine-global cache cleared before each run — what any developer
sees on a fresh checkout or CI runner. `csp(warm)` is one immediate follow-up run with the cache
populated; it is CSharpier's best case for a file set it has already seen.

Kerf beats CSharpier cold by 5–20× on every repository. Kerf beats CSharpier warm on all
repositories through efcore (5,761 files), typically by 1.1–6×. On roslyn (17,167 files), CSharpier
warm edges out Kerf's re-check time by about 18% (6.1 s vs 7.4 s) — both numbers are re-checks of
already-formatted files. Kerf's first-ever run on roslyn is 8.5 s; CSharpier's is 65 s.

Where Kerf wins unconditionally on any large project: its MSBuild stamp means an unchanged project
starts no process at all, while CSharpier still walks 17,000 files every time.

`dotnet format` is consistently 5–10× slower than CSharpier and 15–25× slower than Kerf. It loads
the full Roslyn workspace per project and is not designed to be fast.

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
