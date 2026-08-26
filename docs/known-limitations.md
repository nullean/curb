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

In certain initializer patterns, {{product}}'s output indentation can change between consecutive
passes. An initializer anchors to the indentation of the line it starts on; when {{product}}'s own
output moves that line, the next run anchors somewhere else. The same applies to comment alignment in
some cases. It is the anchor mechanism being unstable under its own output on real code.
