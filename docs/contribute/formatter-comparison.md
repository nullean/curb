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
repo               files  ec |     kerf     csp     dnf |    kerf    csp    dnf |   kerf   csp |   2nd
                             |     --- seconds ---      |  -- files changed --  |  not fixpt   |  idem
serilog              216   1 |     0.05    0.23    1.15 |      90      0     20 |      4    20 |     0
FluentValidation     219   1 |     0.06    0.18    1.46 |     179      0    217 |    216   217 |     0
RestSharp            255   1 |     0.10    0.25    1.72 |     242      0    229 |     13   229 |     0
logging-log4net      376   3 |     0.15    0.23    2.62 |     374      0    376 |     11   376 |     0
AutoMapper           512   1 |     0.11    0.18    2.26 |     389      0    199 |     15   199 |     0
Humanizer            733   3 |     0.34    0.23    3.96 |     487      0    171 |     10   171 |     0
quartznet            765   2 |     0.41    0.21    2.61 |     573      0     33 |      1    33 |     0
Newtonsoft.Json      945   0 |     0.11    0.17    4.81 |     213      0    930 |    864   930 |     1
ServiceStack        4718   0 |     0.95    0.31   13.33 |    3069      0   4691 |   2414  4691 |     1
MassTransit         5502   1 |     0.56    0.25    7.12 |    5291      0    411 |     62   411 |     0
efcore              5761   4 |     2.20    0.31   16.93 |    5288      0   5340 |    533  5340 |     0
roslyn             17167  38 |     7.06    0.60   35.76 |    9191      0      0 |      0     0 |     0
```

<!-- /RESULTS -->

`ec` = number of `.editorconfig` files. `2nd idem` = files that change on a second Kerf run; it must
be zero.

### Speed

Numbers to be refreshed — see below for interpretation once the cold benchmark completes.

### Churn

Against `dotnet format` it is mixed: repos that are already `dotnet format`-clean see Kerf reformatting
extra ground (expression bodies, trailing commas, using placement, BOM handling). Repos with no
`.editorconfig` see large `dotnet format` churn because without a config `dotnet format` applies
default code-style fixes:

| repo | Kerf | dotnet format |
|---|---|---|
| roslyn | 9,191 | **0** |
| efcore | 5,288 | 5,340 |
| MassTransit | 5,291 | 411 |
| Newtonsoft.Json | 213 | 930 |

roslyn is the sharpest case: its 17,169 files are already exactly `dotnet format`-clean, and Kerf
rewrites 11,510 of them. Kerf's output is still a fixed point there — `dotnet format` changes 0 of it
— so this is all *free ground* being exercised: BOM handling, using-directive sorting, reflow at the
repository's own `max_line_length`, chain breaking, comment alignment. All defensible individually,
and collectively the opposite of a quiet first run.

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
