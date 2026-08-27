---
navigation_title: Installation
description: Install Curb as a global dotnet tool, an MSBuild package, or a native binary.
---

# Installation

## Global tool

```sh
dotnet tool install -g Nullean.Curb
```

Installs the `curb` command globally. Requires the .NET 10 SDK. After this, `curb format ./src` and `curb check ./src` work from any directory.

The tool ships as a native-AOT binary per platform — about 11 MB, about 10 ms to start — with a portable managed fallback for platforms not in the list.

**Supported platforms:**

| Platform | RID |
|---|---|
| Linux x64 | `linux-x64` |
| Linux arm64 | `linux-arm64` |
| Windows x64 | `win-x64` |
| Windows arm64 | `win-arm64` |
| macOS arm64 | `osx-arm64` |

To update:

```sh
dotnet tool update -g Nullean.Curb
```

## MSBuild package

```xml
<PackageReference Include="curb" Version="*" PrivateAssets="all" />
```

Add this to your `Directory.Build.props` (or directly to a project file) and `dotnet build` will format your source before compiling it. The package is build-only — nothing lands in your output assemblies.

The package bundles a framework-dependent build of the CLI. To use the native binary instead, which starts roughly 100× faster:

```xml
<PropertyGroup>
  <Curb_Exe>$(HOME)/.dotnet/tools/curb</Curb_Exe>
</PropertyGroup>
```

See [The build integration](../workflow/msbuild.md) for all properties, diagnostics and incremental-build behaviour.

## Native binary directly

Download the binary for your platform from the GitHub releases page and put it on `PATH`. No SDK required to run it — only to install it via `dotnet tool`.

## GitHub Action

```yaml
- uses: nullean/curb@main
  with:
    path: .            # default; a file or directory
    command: check      # default; or "format" to rewrite in place
```

Runs curb from a pre-built, distroless container (`ghcr.io/nullean/curb`) — no .NET SDK install needed in
the workflow. `command: check` fails the job if anything would be reformatted; switch to `format` to rewrite
files instead. Extra flags pass through verbatim via `args`:

```yaml
- uses: nullean/curb@main
  with:
    command: check
    args: --cache /tmp/curb.cache
```

Linux runners only (`ubuntu-latest` and similar) — container actions can't run on Windows or macOS runners.

## Container image

`ghcr.io/nullean/curb` also works as a general-purpose container, outside GitHub Actions — GitLab CI, a
local machine without the .NET SDK, anywhere `docker run` works:

```sh
docker run --rm -v "$(pwd)":/workspace ghcr.io/nullean/curb:edge check /workspace
```

Distroless: native-AOT, chiseled `runtime-deps` base, no shell, ~53 MB, runs as a non-root user. Tags follow
curb's own releases — `edge` tracks the latest commit on `main`, `latest` and a semver tag (e.g. `0.6.0`)
follow tagged releases.

## Verifying the install

```sh
curb --version
```

```sh
curb check ./src
```

On a repository already formatted by the IDE, the check should report no changes.
