---
name: curb-cli
description: >-
  Use when formatting C# (and F# through Fantomas) from the command line, in CI or a pre-commit hook: format files,
  check a tree is formatted, see which .editorconfig option applies, or fix compilation-dependent style findings.
---

# curb-cli

```sh
curb format ./src          # rewrite unformatted files in place
curb check ./src           # exit 1 if anything would change (CI)
curb format -f A.cs -f B.cs   # only these files, e.g. the ones you just edited
curb print-config Foo.cs   # every resolved option for this file, and where it came from
dotnet build && curb cleanup   # fix what the build reported that needs a compilation (unused usings, var, ...)
curb rules                 # what curb fixes, and what it leaves to `dotnet format style`
```

Install as a local tool (`dotnet tool install curb-cli`) and run it as `dotnet curb ...`, or globally as `curb`.

## Gotchas

- curb reads `.editorconfig`; there are no command-line formatting options. `print-config` explains a surprising
  result faster than reading the file.
- Its defaults match the IDE's, so `curb format` and the IDE agree. Do not also run `dotnet format whitespace`.
- F# files are formatted only when Fantomas is in the tool manifest, through that exact version.
- Razor is opt-in per `.editorconfig` section (`curb_razor_format_code_blocks = true`).
- `cleanup` needs a build first: it reads the diagnostics that build reported.

Docs: https://curb.nullean.net
