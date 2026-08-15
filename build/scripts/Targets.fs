module Targets

open Argu
open System
open System.IO
open Bullseye
open CommandLine
open Fake.Tools.Git
open ProcNet

let exec binary args =
    // Proc 0.14+: Exec passes args directly to the OS (no shell expansion) and throws on failure.
    Proc.Exec (binary, List.toArray args) |> ignore

let private restoreTools = lazy(exec "dotnet" ["tool"; "restore"])

let private currentVersion =
    lazy(
        restoreTools.Value |> ignore
        let r = Proc.Start("dotnet", "minver", "-p", "canary.0", "-m", "0.1")
        let o = r.ConsoleOut |> Seq.find (fun l -> not(l.Line.StartsWith("MinVer:")))
        o.Line
    )

let private currentVersionInformational =
    lazy(
        match Paths.IncludeGitHashInInformational with
        | false -> currentVersion.Value
        | true -> sprintf "%s+%s" currentVersion.Value (Information.getCurrentSHA1("."))
    )

let private clean (arguments:ParseResults<Arguments>) =
    if (Paths.Output.Exists) then Paths.Output.Delete (true)
    exec "dotnet" ["clean"] |> ignore

let private build (arguments:ParseResults<Arguments>) = exec "dotnet" ["build"; "-c"; "Release"] |> ignore

let private pristineCheck (arguments:ParseResults<Arguments>) =
    let doCheck = arguments.TryGetResult CleanCheckout |> Option.defaultValue true
    match doCheck, Information.isCleanWorkingCopy "." with
    | _, true  -> printfn "The checkout folder does not have pending changes, proceeding"
    | false, _ -> printf "Checkout is dirty but -c was specified to ignore this"
    | _ -> failwithf "The checkout folder has pending changes, aborting"

let private test (arguments:ParseResults<Arguments>) =
    // TUnit runs on Microsoft.Testing.Platform; `dotnet run` avoids the deprecated VSTest path.
    exec "dotnet" ["run"; "--project"; "tests/Nullean.Kerf.Tests"; "-c"; "Release"] |> ignore

let private benchmark (arguments:ParseResults<Arguments>) =
    exec "dotnet" ["run"; "--project"; "tests/Nullean.Kerf.Benchmarks"; "-c"; "Release"] |> ignore

let private generatePackages (arguments:ParseResults<Arguments>) =
    let output = Paths.RootRelative Paths.Output.FullName
    if not Paths.Output.Exists then Paths.Output.Create()

    // Managed library packages.
    for project in Paths.mapProjectToNuget.Keys do
        exec "dotnet" ["pack"; sprintf "src/%s/%s.csproj" project project; "-c"; "Release"; "-o"; output] |> ignore

    // The CLI is a RID-specific tool. A plain `dotnet pack` emits the root package (whose
    // DotnetToolSettings.xml v2 maps each RID to its own package) AND a package per RID — but
    // native AOT can only compile for the machine it runs on, so those per-RID outputs are
    // self-contained MANAGED builds, silently missing the AOT compilation. We therefore keep only
    // the root and the portable 'any' fallback here, and take the real per-RID packages from the
    // CI matrix, where each is compiled on a matching runner.
    let staging = DirectoryInfo(Path.Combine(Paths.Output.FullName, "..", "cli-staging")).FullName
    if Directory.Exists staging then Directory.Delete(staging, true)
    exec "dotnet" ["pack"; "src/Nullean.Kerf.Cli/Nullean.Kerf.Cli.csproj"; "-c"; "Release"; "-o"; Paths.RootRelative staging] |> ignore

    let ridSuffixes = Paths.AotRuntimeIdentifiers |> List.map (sprintf "Nullean.Kerf.%s.")
    DirectoryInfo(staging).GetFiles("*.nupkg")
    |> Seq.filter (fun f -> not (ridSuffixes |> List.exists f.Name.StartsWith))
    |> Seq.iter (fun f ->
        let destination = Path.Combine(Paths.Output.FullName, f.Name)
        printfn "keeping %s" f.Name
        f.CopyTo(destination, true) |> ignore)

    Directory.Delete(staging, true)

let private validatePackages (arguments:ParseResults<Arguments>) =
    let output = Paths.RootRelative <| Paths.Output.FullName
    // Only managed library packages carry signed assemblies. The root tool package holds just
    // DotnetToolSettings.xml and the per-RID packages hold native binaries; both fail a signing check.
    let nugetPackages =
        Paths.Output.GetFiles("*.nupkg") |> Seq.sortByDescending(fun f -> f.CreationTimeUtc)
        |> Seq.map (fun p -> Paths.RootRelative p.FullName)
        |> Seq.filter (fun p ->
            let baseName = Path.GetFileNameWithoutExtension(p).Replace("." + currentVersion.Value, "")
            Paths.mapNugetToProject.ContainsKey(baseName))

    let args = ["-v"; currentVersionInformational.Value; "-k"; Paths.SignKey; "-t"; output]
    nugetPackages |> Seq.iter (fun p -> exec "dotnet" (["nupkg-validator"; p] @ args) |> ignore)

let private generateApiChanges (arguments:ParseResults<Arguments>) =
    let output = Paths.RootRelative <| Paths.Output.FullName
    let currentVersion = currentVersion.Value
    // Only diff managed packages — per-RID AOT packages have no managed assembly to diff.
    let nugetPackages =
        Paths.Output.GetFiles("*.nupkg") |> Seq.sortByDescending(fun f -> f.CreationTimeUtc)
        |> Seq.map (fun p -> Path.GetFileNameWithoutExtension(Paths.RootRelative p.FullName).Replace("." + currentVersion, ""))
        |> Seq.filter (fun p -> Paths.mapNugetToProject.ContainsKey(p))
    nugetPackages
    |> Seq.iter(fun p ->
        let outputFile = Path.Combine(output, sprintf "breaking-changes-%s.md" p)
        let folder = Paths.mapNugetToProject.TryFind p |> Option.defaultValue p
        let tfm = Paths.mapNugetToTFM.TryFind p |> Option.defaultValue Paths.MainTFM
        let args =
            [
                "assembly-differ"
                (sprintf "previous-nuget|%s|%s|%s" p currentVersion tfm)
                (sprintf "directory|.artifacts/bin/%s/release" folder)
                "-a"; "true"; "--target"; p; "-f"; "github-comment"; "--output"; outputFile
            ]
        printfn "dotnet %A" args
        exec "dotnet" args |> ignore
    )

let private generateReleaseNotes (arguments:ParseResults<Arguments>) =
    let currentVersion = currentVersion.Value
    let output =
        Paths.RootRelative <| Path.Combine(Paths.Output.FullName, sprintf "release-notes-%s.md" currentVersion)
    let tokenArgs =
        match arguments.TryGetResult Token with
        | None -> []
        | Some token -> ["--token"; token]
    let releaseNotesArgs =
        (Paths.Repository.Split("/") |> Seq.toList)
        @ ["--version"; currentVersion
           "--label"; "enhancement"; "New Features"
           "--label"; "bug"; "Bug Fixes"
           "--label"; "documentation"; "Docs Improvements"
        ] @ tokenArgs
        @ ["--output"; output]

    exec "dotnet" (["release-notes"] @ releaseNotesArgs) |> ignore

let private createReleaseOnGithub (arguments:ParseResults<Arguments>) =
    let currentVersion = currentVersion.Value
    let tokenArgs =
        match arguments.TryGetResult Token with
        | None -> []
        | Some token -> ["--token"; token]
    let releaseNotes = Paths.RootRelative <| Path.Combine(Paths.Output.FullName, sprintf "release-notes-%s.md" currentVersion)
    let breakingChanges =
        Paths.Output.GetFiles("breaking-changes-*.md")
        |> Seq.map(fun f -> ["--body"; Paths.RootRelative f.FullName])
        |> Seq.collect id
        |> Seq.toList
    let releaseArgs =
        (Paths.Repository.Split("/") |> Seq.toList)
        @ ["create-release"
           "--version"; currentVersion
           "--body"; releaseNotes
        ] @ breakingChanges @ tokenArgs

    exec "dotnet" (["release-notes"] @ releaseArgs) |> ignore

let private release (arguments:ParseResults<Arguments>) = printfn "release"

let private publish (arguments:ParseResults<Arguments>) = printfn "publish"

let Setup (parsed:ParseResults<Arguments>) (subCommand:Arguments) =
    let step (name:string) action = Targets.Target(name, new Action(fun _ -> action(parsed)))

    let cmd (name:string) commandsBefore steps action =
        let singleTarget = (parsed.TryGetResult SingleTarget |> Option.defaultValue false)
        let deps =
            match (singleTarget, commandsBefore) with
            | (true, _) -> []
            | (_, Some d) -> d
            | _ -> []
        let steps = steps |> Option.defaultValue []
        Targets.Target(name, deps @ steps, Action(action))

    step Clean.Name clean
    cmd Build.Name None (Some [Clean.Name]) <| fun _ -> build parsed

    cmd Test.Name (Some [Build.Name]) None <| fun _ -> test parsed
    cmd Benchmark.Name (Some [Build.Name]) None <| fun _ -> benchmark parsed

    step PristineCheck.Name pristineCheck
    step GeneratePackages.Name generatePackages
    step ValidatePackages.Name validatePackages
    step GenerateReleaseNotes.Name generateReleaseNotes
    step GenerateApiChanges.Name generateApiChanges
    cmd Release.Name
        (Some [PristineCheck.Name; Test.Name])
        (Some [GeneratePackages.Name; ValidatePackages.Name; GenerateReleaseNotes.Name; GenerateApiChanges.Name])
        <| fun _ -> release parsed

    step CreateReleaseOnGithub.Name createReleaseOnGithub
    cmd Publish.Name
        (Some [Release.Name])
        (Some [CreateReleaseOnGithub.Name])
        <| fun _ -> publish parsed
