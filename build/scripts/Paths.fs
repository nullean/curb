module Paths

open System
open System.IO

let ToolName = "kerf"
let Repository = sprintf "nullean/%s" ToolName
let MainTFM = "net10.0"
let SignKey = "b04a6ff7fe029dc7"

let IncludeGitHashInInformational = true

let Root =
    let mutable dir = DirectoryInfo(".")
    while dir.GetFiles("*.sln").Length = 0 && dir.GetFiles("*.slnx").Length = 0 do dir <- dir.Parent
    Environment.CurrentDirectory <- dir.FullName
    dir

let RootRelative path = Path.GetRelativePath(Root.FullName, path)

let Output = DirectoryInfo(Path.Combine(Root.FullName, "build", "output"))

/// The RIDs we ship native-AOT tool packages for. AOT compilation requires a matching
/// OS/arch, so CI packs one RID per runner; this list only documents the set.
let AotRuntimeIdentifiers = ["linux-x64"; "linux-arm64"; "win-x64"; "win-arm64"; "osx-arm64"]

/// Managed (signed) packages only. The Nullean.Kerf root tool package contains just
/// DotnetToolSettings.xml and the per-RID packages contain native binaries — neither
/// has a managed assembly, so both are excluded from signing validation and API diffing.
let mapProjectToNuget =
    Map.empty
        .Add("Nullean.Kerf.Core", "Nullean.Kerf.Core")
        .Add("Nullean.Kerf.EditorConfig", "Nullean.Kerf.EditorConfig")
        .Add("Nullean.Kerf.MSBuild", "Nullean.Kerf.MSBuild")

/// Packages that ship no managed assembly, so signing and API-diff checks have nothing to look at.
let buildOnlyPackages = set [ "Nullean.Kerf.MSBuild" ]

let mapNugetToTFM = Map.empty<string, string>

let mapNugetToProject =
    mapProjectToNuget
    |> Map.fold (fun (m: Map<string, string>) key value -> m.Add(value, key)) Map.empty<string, string>
