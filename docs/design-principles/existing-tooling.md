---
navigation_title: Working with existing tooling
description: Kerf reads the .editorconfig you already have. Defaults are Roslyn's, so it agrees with your IDE out of the box.
---

# Working with existing tooling

{{product}} is designed to work with the configuration and tooling you already have, not to replace it.

## Roslyn defaults

{{product}}'s defaults are Roslyn's defaults — the same values Visual Studio and Rider use for Format
Document. On a repository that has not configured anything, {{product}}'s first run changes nothing that
the IDE would not also change.

That means the IDE and {{product}} agree out of the box. You do not have to choose between them.

## The complete IDE0055 surface

{{product}} reads all 39 `csharp_*` and `dotnet_*` formatting keys from [IDE0055](https://learn.microsoft.com/dotnet/fundamentals/code-analysis/style-rules/ide0055). If your team has configured indentation, spacing, brace placement, or blank lines in `.editorconfig`, {{product}} honours every one of those choices.

A Prettier-style formatter reads a handful of keys and decides the rest itself. {{product}} invents nothing.

## ReSharper and Rider keys

{{product}} also reads the ReSharper wrapping and blank-line key set — `csharp_wrap_*`,
`csharp_place_*`, `csharp_blank_lines_*`, `csharp_trailing_comma_*` and others. Rider already reads
these keys, so a repository configured for Rider is already configured for {{product}} without touching
anything.

## No invented keys

{{product}} reads configuration from your `.editorconfig` and adds nothing to it. Every key it reads
comes from either Microsoft's IDE0055 surface or JetBrains' ReSharper set.

Unrecognised keys are reported rather than silently ignored, with a "did you mean" suggestion for likely
typos. The distinction between "not implemented" and "not known" is surfaced explicitly — a formatter
that silently drops a key you just added gives you no way to know it is not being honoured.

`kerf print-config Foo.cs` prints every resolved option and its source. Worth running on a repository
you are about to reformat.
