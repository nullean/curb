# Curb

A C# formatter that reads your `.editorconfig` — all of it.

Curb reflows C# to a line width, the way Prettier does, while honouring the **complete set of .NET
formatting options** (code style rule
[IDE0055](https://learn.microsoft.com/dotnet/fundamentals/code-analysis/style-rules/ide0055)).
Its defaults are Roslyn's defaults, so it agrees with Visual Studio and Rider out of the box.

📖 **[Documentation](https://nullean.github.io/curb/)** — full option reference, design rationale,
and the build integration guide.

## Why

Existing tools each solve one half of the problem:

- **`dotnet format`** implements IDE0055 faithfully but never wraps a long line.
- **Prettier-style formatters** reflow beautifully but ignore most `.editorconfig` keys; every other
  layout decision is fixed by the tool.

Curb's answer is that **`max_line_length` is the whole decision**:

- **No `max_line_length`** — Curb is an IDE0055 whitespace formatter. Its output is byte-identical
  to `dotnet format whitespace`.
- **`max_line_length` set** — line breaks are a function of your tokens and your width. Formatting
  is idempotent, and the ReSharper wrap keys tune the result.

One switch, two coherent modes.

## Install

As a global dotnet tool:

```sh
dotnet tool install -g curb-cli
```

Ships as a native-AOT binary per platform (`linux-x64`, `linux-arm64`, `win-x64`, `win-arm64`,
`osx-arm64`), with a portable fallback. About 10 ms startup.

## Use

```sh
curb format ./src          # format in place
curb check ./src           # exit 1 if anything would change
curb print-config Foo.cs   # show every resolved option and its source
```

Configuration is your `.editorconfig` — no second config file.

```ini
[*.cs]
indent_style = tab
max_line_length = 120                  # omit for no reflow and preserved line breaks
csharp_new_line_before_open_brace = all
csharp_preserve_single_line_blocks = true
```

For code style rules that need a compilation (unused usings, `var`, naming), run a build first and
let Curb read what it reported:

```sh
dotnet build && curb cleanup
```

`curb rules` lists what Curb fixes and what it leaves to `dotnet format style`.

## MSBuild integration

```xml
<PackageReference Include="curb" Version="*" PrivateAssets="all" />
```

Curb then runs before `CoreCompile` on every `dotnet build`, rewriting in `Debug` and checking in
`Release`. With `EnforceCodeStyleInBuild` set, the only style diagnostics left are the ones that
genuinely require a compilation. See [the build integration docs](https://nullean.github.io/curb/workflow/msbuild/).

## Building from source

Requires the .NET 10 SDK.

```sh
git clone https://github.com/nullean/curb.git
cd curb
./build.sh build
./build.sh test
```

## License

MIT — see [LICENSE.txt](LICENSE.txt).
