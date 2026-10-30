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

## An expression body can move on a second pass at a width

With a width set, an expression body Curb moved below its `=>` on the first pass can be joined back
onto the arrow's line on the second, or the other way round:

```csharp
public ImmutableArray<CustomModifier> RefCustomModifiers =>
    RetargetingTranslator.RetargetModifiers(
        _underlyingProperty.RefCustomModifiers,
        ref _lazyRefCustomModifiers
    );                                                                                // run 1
public ImmutableArray<CustomModifier> RefCustomModifiers =>
    RetargetingTranslator.RetargetModifiers(_underlyingProperty.RefCustomModifiers, ref _lazyRefCustomModifiers); // run 2
```

roslyn at `dcfa99c3` (18,283 files, its own `.editorconfig`) is the hardest case found. With no width it
now settles in one pass. At width 120, eight of its files still change on a second pass, all of them
this shape: a property's or an accessor's expression body, including a switch expression hung off the
arrow. The two docs-builder corpora the conformance gate runs (1,702 and 1,688 files) settle in one pass
with widths off, with their own widths, and at width 120. The run is `curb format` twice over a copy,
then a diff.

The argument-line shapes that used to account for unsettled files with no width settle in one pass
now: a comment under a `case` label's trailing comment, a chain line in a lambda argument whose body
has an operator, a tuple broken after its `(`, an initializer opened out inside a lambda argument, an
expression body converted after a trailing comment, a switch expression opened out inside a one-line
argument list, a `//` comment run between an argument's `name:` and its value, and a lone `)` closing
a list whose `(` the author put on a line of its own.
