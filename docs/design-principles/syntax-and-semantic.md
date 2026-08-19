---
navigation_title: Syntax and semantic passes
description: The two passes Curb uses — what each one needs, what each one fixes, and why the split matters.
---

# Syntax and semantic passes

{{product}} splits formatting work into two passes with different requirements. The split is not
cosmetic — it determines when each pass can run and what it can safely do.

## The syntax pass — `curb format`

The syntax pass reads only the parse tree. It needs no compilation, no project file, no restored
packages.

It handles:

- All layout rules: indentation, spacing, brace placement, blank lines, reflow to `max_line_length`
- Syntax-level code style: brace insertion, expression bodies, file-scoped namespaces, modifier order,
  using directive placement, file headers

Because it needs no build, it can run *before* the compiler — inside `dotnet build`, before
`CoreCompile`. A file is formatted before the compiler reads it.

`curb format <path>` and `curb check <path>` both run the syntax pass.

## The semantic pass — `curb cleanup`

The semantic pass fixes code style that requires knowing what names mean: unused usings, `var` where
the type is apparent, `readonly` fields, and others.

{{product}} never loads a compilation of its own. Instead, it reads the diagnostics your build already
reported and applies the fixes derivable from a rule ID and a span. No additional build step; the
build you just ran already did the hard work.

Because it depends on a build having run, it cannot run before the compiler. The typical sequence:

```sh
dotnet build && curb cleanup
```

`curb rules` shows which rules the semantic pass fixes, and which it leaves for `dotnet format style`.

## What stays out of both passes

Naming conventions, unused members, and unread assignments are not touched by either pass. Their fixes
delete declarations or rename symbols — changes that can compile and still alter which overload binds,
or break a reflection string no compiler check catches. {{product}} reports these diagnostics and says
why rather than guessing.

{{product}} reports these diagnostics and says why rather than guessing.

## The boundary in the build

With the MSBuild package, the syntax pass runs automatically on every `dotnet build`. The semantic pass
is a separate command you run after a build that produced diagnostics. This keeps the two passes
independent: the syntax pass never waits for a compilation, and the semantic pass never re-does work
the build already did.

See [the build integration](../workflow/msbuild.md) for how to configure both.
