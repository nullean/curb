---
navigation_title: Known limitations
description: Current limitations in Curb's formatting output and what to expect on first adoption.
---

# Known limitations

## Some continuation lines of untidy source differ from `dotnet format`

On source that was never formatted, {{product}} indents a few kinds of continuation line differently from
`dotnet format`:
- multi-dimensional array initializers;
- `select new { }` in a query;
- tuple patterns in switch arms;
- members of a `with` initializer;
- the `:` of a conditional whose `?` branch is a multi-line lambda.

The CI corpus, which is written to be untidy, finds these in 34 of its 1,868 files at its default width,
and in 137 with no width, where its initializers are kept as written. That is the gap between its conformance
floors and 100%. elastic/docs-builder, whose authors had already formatted it, shows none.

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
arrow. elastic/docs-builder (1,702 files) and the generated CI corpus settle in one pass with widths off, with
their own widths, and at width 120. The run is `curb format` twice over a copy,
then a diff.

The argument-line shapes that used to account for unsettled files with no width settle in one pass
now: a comment under a `case` label's trailing comment, a chain line in a lambda argument whose body
has an operator, a tuple broken after its `(`, an initializer opened out inside a lambda argument, an
expression body converted after a trailing comment, a switch expression opened out inside a one-line
argument list, a `//` comment run between an argument's `name:` and its value, and a lone `)` closing
a list whose `(` the author put on a line of its own.

A block body that a `csharp_style_expression_bodied_*` key converts settles in one pass too, in both layout
modes: the conversion writes the layout the next run gives the arrow it wrote. A `return x switch { … };`
used to come out with the switch below the `=>`, and the next run hugged it to the arrow. The generated CI corpus
now sets `csharp_style_expression_bodied_methods` again, which it left out while that was open.
