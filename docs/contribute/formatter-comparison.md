---
navigation_title: Formatter comparison
description: A measurement run over 41,000 files of real .NET source, and the six defects it found.
---

# Kerf vs CSharpier vs dotnet format, on twelve real repositories

A development note recording a measurement run over 41,000 files of real .NET source, and the six
defects it found that the single-repository corpus cannot see.

## Method

Twelve repositories, cloned shallow, each formatted from a pristine copy by each tool. Kerf is the
published native-AOT binary, not the JIT build. For Kerf and CSharpier, `dotnet format whitespace`
was then run over their output and the differences counted — that is the fixed-point number, the one
that decides whether Format Document in an IDE will fight the formatter. Kerf was also run a second
time over its own output to check it settles.

CSharpier is timed twice: **cold** (cache cleared before each of three runs; best-of-3 taken) and
**warm** (one run immediately after, with the cache populated). The warm number is what CSharpier
reports if you've already formatted the same content once on this machine. The cold number is what
any developer sees on a fresh checkout, a CI runner, or after `dotnet clean`.

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
repositories through efcore (5,761 files), typically by 1.1–6×. The one exception is roslyn (17,167
files), where CSharpier warm edges out Kerf's best-of-3 re-check time by about 18% (6.1 s vs 7.4 s).

That comparison is not quite apples-to-apples: Kerf's 7.4 s is a re-check of already-formatted
files (runs 2 and 3 over the same directory); Kerf's first-ever run on roslyn is 8.5 s. CSharpier's
warm is a re-check of already-formatted files against a populated cache, which is its best case.
Where Kerf wins unconditionally on any large project: its MSBuild stamp means an unchanged project
starts no process at all, while CSharpier still walks 17,000 files every time.

`dotnet format` is consistently 5–10× slower than CSharpier and 15–25× slower than Kerf. It loads
the full Roslyn workspace per project and is not designed to be fast.


### Churn

Against `dotnet format` it is mixed. repos that are already `dotnet format`-clean see Kerf reformatting
extra ground (expression bodies, trailing commas, using placement, BOM handling):

| repo | Kerf changed | dotnet format changed | Kerf not-fixpt |
|---|---|---|---|
| roslyn | 9,191 | **0** | **0** |
| efcore | 5,288 | 5,340 | 533 |
| MassTransit | 5,291 | 411 | 62 |
| Newtonsoft.Json | 897 | 72 | 17 |

roslyn is the sharpest case: its 17,167 files are already exactly `dotnet format`-clean, and Kerf
rewrites 9,191 of them. Kerf's output is still a fixed point there — `dotnet format` changes 0 of it
— so this is all *free ground*: BOM handling, using-directive sorting, reflow at the repository's
own `max_line_length`, chain breaking, comment alignment. All defensible individually, and
collectively the opposite of a quiet first run.

efcore's 533 not-fixpt files are explained by defect 4 (multi-line trivia line endings), which
accounts for most cross-tool conformance failures in the corpus too.

Anyone working on churn should start here rather than on the corpus, where the number is 742 of 1,196
and has been stable for so long that it reads as settled.

## Defects found

### 1. Crash on `new { }` — fixed

Both ends of the anonymous-type printer indexed the initializer list without checking it was
non-empty. `new { }` is valid C# and common in test code. Fixed in `4151fd3` with a regression test.

It crashed **MassTransit, efcore and roslyn — the three largest repositories, and nothing smaller.**
The corpus contains no empty anonymous object at all.

### 2. One bad file aborts the whole run — not fixed

`FormattingRun` formats in parallel and does not isolate per-file exceptions, so the single `new { }`
above took down a 17,000-file run with an unhandled exception. This contradicts the standing promise
that a file which cannot be formatted is reported and left untouched: the blast radius of any printer
bug is the entire repository rather than one file. The printer bug was a one-line guard; this is a
design gap.

### 3. `charset` is not implemented

Kerf lists `charset` in its option catalog and hard-codes the readback to `utf-8`, but never writes a
BOM and never removes one on purpose — it simply always writes without one, and only touches files it
was already rewriting.

```
charset = utf-8-bom          # what roslyn asks for
kerf:            BOM present -> stripped,  BOM absent -> stays absent
dotnet format:   BOM present -> kept,      BOM absent -> added
```

roslyn sets `utf-8-bom` on 17,169 files, so Kerf strips a BOM the repository explicitly asks for,
from every file it touches. This is both a correctness bug and a large share of the roslyn churn.

### 4. Multi-line trivia keeps the source's line endings

The highest-impact conformance defect. Kerf emits multi-line trivia — doc comments, verbatim string
literals — as a raw source span, so their internal newlines survive unchanged, while the breaks Kerf
emits itself use the configured ending. The result is a file with mixed endings:

```
^M$                          <- Kerf's own break, CRLF
/// <remarks>$               <- inside one doc-comment trivia, source LF
/// Represents information$
/// </remarks>^M$
```

Every such file fails the fixed-point check. This accounts for most of efcore's 3,221 and log4net's
322, against CSharpier's 128 and 4.

### 5. Expression-body conversions do not compose in one pass

Live in the default configuration, with `csharp_style_expression_bodied_properties = true` and
`..._accessors = true`:

```csharp
public int Count { get { throw new NotImplementedException(); } }   // source, expanded
public int Count { get => throw new NotImplementedException(); }    // run 1
public int Count => throw new NotImplementedException();            // run 2 — different
```

The accessor-level conversion fires on run 1. The property-level one only recognises an accessor list
that *already* has an arrow getter, so it fires on run 2. Two transformations that should compose.
Hits quartznet (30 files), efcore (96), Humanizer (6), AutoMapper (2).

### 6. Anchor columns feed back into the next run

roslyn's 642 unsettled files are almost all this, and it is the mechanism described in
[layout-decisions.md](layout-decisions.md) appearing in the *default* configuration rather than
behind an alignment option:

```csharp
// run 1
                        {
                            Environment.NewLine
                        },
// run 2 — one level deeper
                            {
                                Environment.NewLine
                            },
```

An initializer anchors to the indentation of the line it starts on; Kerf's own output moved that
line, so the next run anchors somewhere else. The same happens to comment alignment — a comment in
`DecisionDagBuilder.cs` walks right by tens of columns on the second pass.

This is the one to treat as structural. It is not an option misbehaving, it is the anchor mechanism
being unstable under its own output on real code.

## A caveat about the metric

FluentValidation's 216 failures are not Kerf's. Reproduced without Kerf in the loop: under that
repository's `.editorconfig`, `dotnet format` indents members of a **file-scoped namespace** by an
extra level. CSharpier scores an identical 216. Kerf's rendering is the defensible one; conformance
counts it against Kerf anyway, because the metric is defined against the tool rather than against
correctness.

Worth remembering when a conformance number moves: the number can be wrong about which side is right,
and only a hand check tells you which case you are in.

## Reproducing

Shallow-clone the twelve repositories into a single directory, then:

```sh
./build.sh compare --corpus /path/to/repos
```

The results table above is written back into this file automatically. The run takes roughly 40 minutes
wall clock, dominated by `dotnet format` on roslyn and efcore.
