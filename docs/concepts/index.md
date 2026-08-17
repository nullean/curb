---
navigation_title: The two passes
description: The vocabulary Kerf uses — the syntax pass and the semantic pass, and the three classes of rule inside them.
---

# The two passes

Cleaning up a C# file involves two very different kinds of work, and almost every argument about
formatting tools comes from conflating them. {{product}} names them apart.

| | **The syntax pass** | **The semantic pass** |
|---|---|---|
| Decided from | the parse tree alone | a compilation — symbols, references, flow |
| Needs a build | **no** | **yes** |
| Runs | before `CoreCompile`, on a bare folder | after the compiler, on build output |
| Cost | milliseconds | seconds to minutes |
| Status | shipping today | in development |

Both halves are {{product}}'s job — it is a syntax *and* semantic style enforcer. But they are built and
shipped separately, because the constraint that makes the syntax pass fast is exactly the one the
semantic pass cannot accept. Until the semantic pass lands, `dotnet format style` covers that ground.

The distinction is a hard line rather than a matter of degree. The syntax pass never loads a compilation
and never touches the semantic model. It parses, and everything it decides is decided from what it can
see in the file in front of it.

The consequence is the interesting part. Because the syntax pass needs no build, it can run before one —
including *inside* one, before the compiler reads your source. That is what
[the build integration](../workflow/msbuild.md) does, and it is why the split matters beyond taxonomy.

## Three classes of rule

Within those two passes there are three classes of rule, distinguished by what they are allowed to
change.

| Class | Changes your tokens? | Examples | Pass |
|---|---|---|---|
| **Layout** | No — and it is checked, per file | indentation, spacing, brace placement, blank lines, reflow to `max_line_length` | syntax |
| **Syntax style** | Yes | braces, expression bodies, file-scoped namespaces, modifier order, using placement, file header, trailing commas | syntax |
| **Semantic style** | Yes | `var`, unused usings, unused members, naming | semantic |

### Layout

Layout rules move whitespace. They can change every space, newline and indent in the file, and they can
reflow a long expression across several lines, but the sequence of tokens they emit is the sequence they
were given.

This is not a promise, it is a check. {{product}} compares the token stream it emits against the token
stream it parsed, on every file, and refuses to write a file that fails. See
[Safety](safety.md).

The 39 formatting options of code style rule
[IDE0055](https://learn.microsoft.com/dotnet/fundamentals/code-analysis/style-rules/ide0055) are all
layout rules, and {{product}} implements all of them. So is reflow, which is the one thing IDE0055 has
no opinion about and no .NET tool does — and which also decides who picks the line breaks. See
[Reflow](reflow.md).

### Syntax style

Syntax style rules change the tokens. Adding a brace, turning a block body into an expression body,
converting a block-scoped namespace to a file-scoped one — these produce different code, not just
differently arranged code.

They are still syntax-pass work, because none of them needs to know what any name *means*. Whether a
statement should get braces is answered by looking at the statement.

Every one of these is off unless your `.editorconfig` asks for it. {{product}} defaults to `as_written`
for the whole class, so adopting it never rewrites code you did not ask to have rewritten.

### Semantic style

Deciding whether `var` is legal here, whether a using is unused, whether a field could be `readonly`, or
what a symbol should be called all require binding: you must resolve names to symbols, which requires
references, which requires a restore and a build.

The syntax pass does not do this and never will. That is not a gap to be filled later — it is the
boundary that makes it fast enough to run on every build. Semantic rules are the other pass's job, and
that pass necessarily runs after the compiler rather than before it.

So when the syntax pass sees a semantic key in your `.editorconfig`, it passes over it silently rather
than warning. The key is not wrong; it is simply addressed to the other half.

## Words used precisely

Three phrases recur across this documentation.

**Mechanical offences** — everything the syntax pass fixes without needing to ask. A missing brace, a
mis-indented block, a line that ran long. There is exactly one right answer and it is written in your
`.editorconfig`.

**The semantic remainder** — what is left after the mechanical offences are gone. This is the part that
needs a compilation, or a person, or both.

**Conformance** — {{product}}'s agreement with `dotnet format`, stated as a measurement rather than a
claim. Specifically: with reflow off, {{product}}'s output is byte-identical to
`dotnet format whitespace` across a 1,196-file corpus, and {{product}}'s output is a *fixed point* of
`dotnet format` — running `dotnet format` over it changes nothing. See [Why {{product}}](../why.md).
