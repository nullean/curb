---
navigation_title: Known limitations
description: Current limitations in Curb's formatting output and what to expect on first adoption.
---

# Known limitations

## Multi-line trivia keeps source line endings

{{product}} emits multi-line trivia — doc comments, verbatim string literals — as a raw source span,
so their internal newlines survive unchanged while the breaks {{product}} emits itself use the configured
line ending. The result is a file with mixed line endings.

This means `dotnet format` disagrees with {{product}}'s output on any file containing doc comments
whose source line endings differ from the configured `end_of_line`. It is the most common source of
fixed-point failures in the [twelve-repository comparison](benchmarks/index.md).

## Anchor columns feed back into the next run

In certain initializer patterns, {{product}}'s output indentation can change between consecutive
passes. An initializer anchors to the indentation of the line it starts on; when {{product}}'s own
output moves that line, the next run anchors somewhere else. The same applies to comment alignment in
some cases. It is the anchor mechanism being unstable under its own output on real code.
