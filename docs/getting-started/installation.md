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

## Verifying the install

```sh
curb --version
```

```sh
curb check ./src
```

On a repository already formatted by the IDE, the check should report no changes.
