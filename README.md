# Curb

A C# formatter that reads your `.editorconfig` — all of it.

Curb reflows C# to a line width, the way Prettier does, while honouring the **complete set of .NET
formatting options** (code style rule
[IDE0055](https://learn.microsoft.com/dotnet/fundamentals/code-analysis/style-rules/ide0055)).
Its defaults are Roslyn's defaults, so it agrees with Visual Studio and Rider out of the box.

📖 **[Documentation](https://nullean.github.io/curb/)** — full option reference, design rationale,
and the build integration guide.

## Why

**Fast enough to run on every build.** Curb is a native-AOT binary with no warm-up cost. Measured
cold on Newtonsoft.Json (945 files):

| | Curb | dotnet format | CSharpier |
|---|---|---|---|
| Newtonsoft.Json (945 files) | **0.26 s** | 3.47 s | 4.85 s |

Across twelve real repositories Curb is 5–25× faster than `dotnet format` — fast enough to wire
into `dotnet build` and forget about it. Full numbers: [Benchmarks](https://nullean.github.io/curb/benchmarks/).

**Plays well with your IDE and `dotnet format`.** Curb supports the full set of `.editorconfig`
formatting properties that Visual Studio, Rider and `dotnet format` read — all 39 `csharp_*` and
`dotnet_*` keys of [IDE0055](https://learn.microsoft.com/dotnet/fundamentals/code-analysis/style-rules/ide0055),
plus the ReSharper wrapping and blank-line keys. Running `dotnet format whitespace` over Curb's
output never disagrees — measured across 41,000 files, gated on every push.

## Install

### MSBuild — runs on every `dotnet build`

```xml
<PackageReference Include="curb" Version="*" PrivateAssets="all" />
```

Curb runs before `CoreCompile`, rewriting source in `Debug` and checking in `Release`. With
`EnforceCodeStyleInBuild` set, the only style diagnostics left are the ones that genuinely require a
compilation — everything mechanical is already fixed before the compiler reads the file.

This is the recommended integration for most projects. It is particularly effective in agentic
workflows: any code an agent writes or edits is formatted automatically on the next build, without
the agent having to think about it. See [the build integration docs](https://nullean.github.io/curb/workflow/msbuild/).

### CLI — pre-commit hooks, CI, scripting

```sh
dotnet tool install -g curb-cli
```

Ships as a native-AOT binary per platform (`linux-x64`, `linux-arm64`, `win-x64`, `win-arm64`,
`osx-arm64`), with a portable fallback. About 10 ms startup.

```sh
curb format ./src          # format in place
curb check ./src           # exit 1 if anything would change
curb print-config Foo.cs   # show every resolved option and its source
```

For code style rules that need a compilation (unused usings, `var`, naming), run a build first and
let Curb read what it reported:

```sh
dotnet build && curb cleanup
```

`curb rules` lists what Curb fixes and what it leaves to `dotnet format style`.

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
