---
navigation_title: Known limitations
description: Current limitations in Kerf's formatting output and what to expect on first adoption.
---

# Known limitations

## BOM handling

{{product}} lists `charset` in its option catalog but always writes files without a byte-order mark
(BOM). It never adds a BOM to files that lack one, and it strips a BOM from files that have one —
which is incorrect for repositories that set `charset = utf-8-bom`.

**Workaround:** If your repository sets `charset = utf-8-bom`, avoid using {{product}} in rewrite mode until this is fixed.

## Multi-line trivia keeps source line endings

{{product}} emits multi-line trivia — doc comments, verbatim string literals — as a raw source span,
so their internal newlines survive unchanged while the breaks {{product}} emits itself use the configured
line ending. The result is a file with mixed line endings.

This means `dotnet format` disagrees with {{product}}'s output on any file containing doc comments
whose source line endings differ from the configured `end_of_line`. It is the most common source of
fixed-point failures in the [twelve-repository comparison](benchmarks/index.md).

## Expression-body conversions do not compose in one pass

With both `csharp_style_expression_bodied_properties = true` and
`csharp_style_expression_bodied_accessors = true`, the two conversions can produce different output
on consecutive passes:

```csharp
public int Count { get { throw new NotImplementedException(); } }   // source
public int Count { get => throw new NotImplementedException(); }    // run 1
public int Count => throw new NotImplementedException();            // run 2 — different
```

The accessor-level conversion fires on run 1. The property-level conversion only recognises an
accessor list that already has an arrow getter, so it fires on run 2. Two rules that should compose
in one pass currently do not.

## Anchor columns feed back into the next run

In certain initializer patterns, {{product}}'s output indentation can change between consecutive
passes. An initializer anchors to the indentation of the line it starts on; when {{product}}'s own
output moves that line, the next run anchors somewhere else. The same applies to comment alignment in
some cases. It is the anchor mechanism being unstable under its own output on real code.

## Per-file exception isolation

A bug in the printer for one file currently aborts the entire run rather than reporting that file and
continuing. The intended behaviour — a file that cannot be formatted is reported and left untouched —
is not yet implemented.
