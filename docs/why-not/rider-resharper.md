---
navigation_title: Rider / ReSharper
description: Kerf reads the same .editorconfig keys Rider does, so a team using both needs no extra configuration.
---

# Why not just Rider / ReSharper cleanup?

You do not have to choose. {{product}} reads the same `.editorconfig` keys Rider does, so if your repository already has a Rider configuration, that configuration works for {{product}} without a single change.

## The keys

Rider uses several `.editorconfig` key families that are not part of the official IDE0055 surface but are documented by JetBrains and read by ReSharper:

```ini
csharp_wrap_parameters_style = chop_if_long
csharp_wrap_arguments_style = chop_if_long
csharp_wrap_object_and_collection_initializer_style = chop_if_long
csharp_wrap_before_first_method_call = true
csharp_wrap_before_arrow_with_expressions = false
csharp_place_simple_accessorholder_on_single_line = true
csharp_place_simple_enum_on_single_line = true
csharp_blank_lines_around_invocable = 1
csharp_blank_lines_around_type = 1
csharp_trailing_comma_in_multiline_lists = true
```

{{product}} honours all of them. The defaults leave everything off — so a repository that never set them is unchanged — and a repository that set them for Rider gets the same layout from {{product}}.

`dotnet format` does not read any of these keys. If you want wrapping and blank-line rules from your `.editorconfig` enforced consistently across the command line, the build, and CI, {{product}} is currently the only way to do that.

## Where Rider does more

Rider's cleanup can apply semantic fixes: unused imports, `var` where the type is apparent, naming conventions, null-check simplifications. These need to know what a name means, which requires a full project load.

{{product}} does not do any of that. It covers what syntax alone can decide — layout, brace style, modifier order, expression bodies, file-scoped namespaces, using placement. Everything that requires a compilation stays with Rider's cleanup and `dotnet format style`.

That scope boundary is what makes {{product}} fast enough to run inside every build.

## Why both make sense

Rider cleanup runs when a developer explicitly invokes it. {{product}} runs on every `dotnet build`. The two cover different moments: Rider catches things in the editor, {{product}} enforces them in the build so CI never sees a mechanically unformatted commit.

They also cover different keys. A team that uses both gets consistent style whether someone formatted via the IDE or via the command line.
