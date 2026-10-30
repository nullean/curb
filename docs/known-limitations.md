---
navigation_title: Known limitations
description: Current limitations in Curb's formatting output and what to expect on first adoption.
---

# Known limitations

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

A few continuation lines inside an argument list can still move between the first and second pass.
{{product}} keeps a continuation line at the offset the author gave it from the line it continues. When
{{product}}'s first pass changes which line that is, the second pass measures from a different line and
moves the continuation again.

The two docs-builder corpora the conformance gate runs (1,702 and 1,688 files) settle in one pass,
with widths off, with their own widths, and at width 120. roslyn at `dcfa99c3` (18,283 files, its own `.editorconfig`, no width)
is the hardest case found. Five of its files still change on a second pass, in two shapes:

- a switch expression, or a run of `//` comment lines, that starts a later argument line of a call
- a lone `)` or `));` line closing a call whose arguments the author broke

The shapes that used to account for most unsettled files settle in one pass now: a comment under a
`case` label's trailing comment, a chain line in a lambda argument whose body has an operator, a tuple
broken after its `(`, an initializer opened out inside a lambda argument, and an expression body
converted after a trailing comment. The run is `curb format` twice over a copy, then a diff.
