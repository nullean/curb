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

/// Cleans Release only, which is the configuration everything downstream builds.
///
/// Not a preference: this script is itself a project in the solution, and it runs from its Debug
/// output. A bare `dotnet clean` defaults to Debug and so deletes the assemblies of the process
/// executing it — anything the runner had not yet loaded then fails to resolve. It surfaced as
/// `perf` dying on a missing System.Reactive, which reads like a restore problem and is not one.
let private clean (arguments:ParseResults<Arguments>) =
    if (Paths.Output.Exists) then Paths.Output.Delete (true)
    exec "dotnet" ["clean"; "-c"; "Release"] |> ignore

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

/// Builds docs/ the way the workflow does — including the landing page override, which is the whole
/// reason `docs-builder serve` is not enough on its own — and serves the result.
let private docs (arguments:ParseResults<Arguments>) =
    Documentation.build ()
    if arguments.Contains NoServe then
        printfn "built; --no-serve given, not serving"
    else
        Documentation.serve (arguments.TryGetResult Port |> Option.defaultValue 8080)

/// Measures how far Kerf's output is from dotnet format's, which is the product claim made
/// checkable. Reflow is forced off so that every difference is an option disagreement rather than a
/// deliberate wrap: with max_line_length off, Kerf should be a fixed point of dotnet format.
let private conformance (arguments:ParseResults<Arguments>) =
    let corpus =
        match arguments.TryGetResult Corpus with
        | Some path -> DirectoryInfo(path)
        | None -> failwith "conformance needs --corpus <path> pointing at a C# checkout"
    if not corpus.Exists then failwithf "corpus not found: %s" corpus.FullName

    let root = Path.Combine(Paths.Output.FullName, "conformance")
    if Directory.Exists root then Directory.Delete(root, true)
    let work = Path.Combine(root, "kerf")
    let reference = Path.Combine(root, "reference")

    let rec copyTree (source: string) (target: string) =
        Directory.CreateDirectory target |> ignore
        for file in Directory.GetFiles source do
            File.Copy(file, Path.Combine(target, Path.GetFileName file), true)
        for dir in Directory.GetDirectories source do
            let name = Path.GetFileName dir
            if name <> ".git" && name <> "bin" && name <> "obj" then
                copyTree dir (Path.Combine(target, name))

    printfn "copying corpus from %s" corpus.FullName
    copyTree corpus.FullName work

    let trailingCommas = arguments.Contains TrailingCommas
    let keepWidths = arguments.Contains Reflow

    // What this measures is dotnet_format(kerf(x)) = kerf(x) — that Kerf's output is a *fixed point*
    // of dotnet format, not that the two agree on the same input. That is the stronger property and
    // the one that matters: a repository formatted by Kerf stays put when anyone runs dotnet format,
    // hits Format Document, or builds with EnforceCodeStyleInBuild.
    //
    // It is also what makes opinionated mode admissible. dotnet format declines to decide almost
    // everything about layout, and anything it declines to decide Kerf may decide while staying a
    // fixed point. So --opinionated is gated by this same number: it may change many files, it may
    // not change this.
    //
    // Reflow is forced off by default so a difference is an option disagreement rather than a wrap
    // Kerf chose; --reflow keeps the corpus's own widths, which is the configuration people run.
    for config in Directory.GetFiles(work, ".editorconfig", SearchOption.AllDirectories) do
        let text = File.ReadAllText config
        // A config with no max_line_length already gets the default, which is off.
        let widths =
            if keepWidths then text
            else Text.RegularExpressions.Regex.Replace(text, @"max_line_length\s*=\s*\S+", "max_line_length = off")
        let rewritten =
            if trailingCommas then
                widths + "\n[*.cs]\ncsharp_trailing_comma_in_multiline_lists = true\n"
            else widths
        File.WriteAllText(config, rewritten)

    exec "dotnet" ["run"; "--project"; "src/Nullean.Kerf.Cli"; "-c"; "Release"; "--"; "format"; work] |> ignore
    copyTree work reference
    exec "dotnet" ["format"; "whitespace"; reference; "--folder"] |> ignore

    let sourceFiles = Directory.GetFiles(work, "*.cs", SearchOption.AllDirectories)
    let differing =
        sourceFiles
        |> Array.filter (fun file ->
            let other = Path.Combine(reference, Path.GetRelativePath(work, file))
            not (File.Exists other) || File.ReadAllText file <> File.ReadAllText other)

    let total = sourceFiles.Length
    let agreeing = total - differing.Length
    let percentage = 100.0 * float agreeing / float total
    printfn ""
    let mode =
        match arguments.Contains TrailingCommas, arguments.Contains Reflow with
        | false, false -> ""
        | true, false -> " (trailing commas)"
        | false, true -> " (reflow)"
        | true, true -> " (trailing commas, reflow)"
    printfn "conformance with dotnet format%s: %d/%d files (%.2f%%)" mode agreeing total percentage
    if differing.Length > 0 then
        printfn ""
        printfn "first differing files:"
        differing |> Array.truncate 15 |> Array.iter (fun f -> printfn "  %s" (Path.GetRelativePath(work, f)))
        printfn ""
        printfn "compare with: diff -ru %s %s" reference work

    // A regression here means Kerf drifted away from the reference implementation, which is the one
    // number the product claim rests on. Fail rather than merely report it.
    match arguments.TryGetResult Minimum with
    | Some floor when percentage < floor ->
        failwithf "conformance %.2f%% is below the required %.2f%%" percentage floor
    | _ -> ()

/// Times the shipped binary over a corpus.
///
/// This target exists because measuring `dotnet run` is misleading and easy to do by accident: a
/// one-shot format run is short enough that JIT compilation dominates it, so the JIT build reports
/// several times the CPU and allocations of the native binary users actually get. Always measure
/// the AOT publish.
///
/// The gate is on allocations, not time. Allocations are deterministic; wall time on a hosted
/// runner is not, and a flaky perf gate gets disabled, which is worse than no gate.
let private perf (arguments:ParseResults<Arguments>) =
    let corpus =
        match arguments.TryGetResult Corpus with
        | Some path -> DirectoryInfo path
        | None -> failwith "perf needs --corpus <path> pointing at a C# checkout"
    if not corpus.Exists then failwithf "corpus %s does not exist" corpus.FullName

    let rid =
        let os =
            if OperatingSystem.IsWindows() then "win"
            elif OperatingSystem.IsMacOS() then "osx"
            else "linux"
        let arch =
            match Runtime.InteropServices.RuntimeInformation.ProcessArchitecture with
            | Runtime.InteropServices.Architecture.Arm64 -> "arm64"
            | Runtime.InteropServices.Architecture.X64 -> "x64"
            | other -> failwithf "no RID mapping for %O" other
        sprintf "%s-%s" os arch

    printfn "publishing native AOT for %s" rid
    exec "dotnet" ["publish"; "src/Nullean.Kerf.Cli"; "-c"; "Release"; "-r"; rid] |> ignore

    let binary =
        let name = if OperatingSystem.IsWindows() then "Nullean.Kerf.Cli.exe" else "Nullean.Kerf.Cli"
        Path.Combine(".artifacts", "publish", "Nullean.Kerf.Cli", sprintf "release_%s" rid, name)
    if not (File.Exists binary) then failwithf "expected a native binary at %s" binary

    // The first run pays for a cold file cache, so take the best of several rather than the mean.
    let runs =
        [ for _ in 1 .. 5 ->
            let started = Diagnostics.Stopwatch.StartNew()
            let result = Proc.Start(binary, "check", corpus.FullName)
            started.Stop()
            let output = result.ConsoleOut |> Seq.map (fun l -> l.Line) |> String.concat "\n"
            let ratio =
                let matched = Text.RegularExpressions.Regex.Match(output, @"\(([0-9.]+)x source\)")
                if matched.Success then Double.Parse(matched.Groups.[1].Value, Globalization.CultureInfo.InvariantCulture)
                else failwithf "could not read the allocation ratio from:\n%s" output
            started.Elapsed.TotalMilliseconds, ratio, output ]

    let elapsed, _, output = runs |> List.minBy (fun (ms, _, _) -> ms)
    printfn ""
    printfn "%s" output
    printfn "best of %d runs: %.0f ms wall" runs.Length elapsed

    // Best of the runs on the gated measurement too. This used to read the ratio out of whichever
    // run happened to be *fastest*, which pairs the two measurements arbitrarily — and allocation is
    // not quite as deterministic as the note above claims, since `check` partitions the corpus across
    // threads and the partitioning varies. On this machine the spread is about 0.4x, which was enough
    // to make a gate at 30x pass or fail on the same code depending on which run won the stopwatch.
    let ratio = runs |> List.map (fun (_, r, _) -> r) |> List.min

    match arguments.TryGetResult MaxAllocationRatio with
    | Some ceiling when ratio > ceiling ->
        failwithf "allocated %.1fx the source size, above the permitted %.1fx" ratio ceiling
    | _ -> printfn "allocated %.1fx the source size" ratio

/// Proves the MSBuild integration does the one thing it exists for: format before the compiler.
///
/// The assertion is deliberately two-sided. Building the sample with Kerf must succeed even though
/// IDE0055 is escalated to an error and the source is deliberately misformatted; building it with
/// Kerf bypassed must fail with those same errors. Only the pair proves anything — the first alone
/// would pass just as well if the analysers were never running.
let private msbuildSmoketest (arguments:ParseResults<Arguments>) =
    let rid =
        let os =
            if OperatingSystem.IsWindows() then "win"
            elif OperatingSystem.IsMacOS() then "osx"
            else "linux"
        let arch =
            match Runtime.InteropServices.RuntimeInformation.ProcessArchitecture with
            | Runtime.InteropServices.Architecture.Arm64 -> "arm64"
            | Runtime.InteropServices.Architecture.X64 -> "x64"
            | other -> failwithf "no RID mapping for %O" other
        sprintf "%s-%s" os arch

    exec "dotnet" ["publish"; "src/Nullean.Kerf.Cli"; "-c"; "Release"; "-r"; rid] |> ignore

    let binary =
        let name = if OperatingSystem.IsWindows() then "Nullean.Kerf.Cli.exe" else "Nullean.Kerf.Cli"
        Path.GetFullPath(Path.Combine(".artifacts", "publish", "Nullean.Kerf.Cli", sprintf "release_%s" rid, name))

    let sample = Path.Combine("examples", "kerf-msbuild-smoketest")
    let source = Path.Combine(sample, "Program.cs")
    let pristine = Path.Combine(sample, "Program.unformatted")

    let build (extra: string list) =
        File.Copy(pristine, source, true)
        // File.Copy carries the source's timestamp across, which can leave the restored file looking
        // older than the incremental stamp from a previous run — so the target would skip and the
        // assertion below would blame the formatter for the harness's mistake.
        File.SetLastWriteTimeUtc(source, DateTime.UtcNow)
        let args = ["build"; sample; "-c"; "Debug"; "--nologo"] @ extra
        let result = Proc.Start("dotnet", List.toArray args)
        let output = result.ConsoleOut |> Seq.map (fun l -> l.Line) |> String.concat "\n"
        result.ExitCode, output

    printfn "building the sample with Kerf"
    let withKerfCode, withKerfOutput = build [sprintf "-p:Kerf_Exe=%s" binary]
    if withKerfCode <> 0 then
        failwithf "the sample must build with Kerf in the way, but it failed:\n%s" withKerfOutput
    if not (File.ReadAllText(source).Contains("public static int Run(int x)")) then
        failwithf "Kerf did not reformat the sample before the compiler read it"

    printfn "building the sample with Kerf bypassed"
    let bypassedCode, bypassedOutput = build ["-p:Kerf_Bypass=true"]
    if bypassedCode = 0 then
        failwith "the sample built clean without Kerf, so the check proves nothing — is EnforceCodeStyleInBuild still on?"
    if not (bypassedOutput.Contains "IDE0055") then
        failwithf "expected IDE0055 errors without Kerf, got:\n%s" bypassedOutput

    // Leave the sample misformatted, which is how it is checked in.
    File.Copy(pristine, source, true)
    printfn ""
    printfn "MSBuild integration verified: formatted before CoreCompile, and IDE0055 fails without it"

/// Proves that every expectation the test suite asserts is a fixed point of dotnet format.
///
/// Until this existed only the corpus proved that. The hand-written expectations proved only that
/// Kerf agrees with itself, so one written from a wrong belief about dotnet format would sit there
/// passing forever — which is exactly how the note about arrow clauses survived several readings.
///
/// Slow, and it needs the SDK, so it is its own target rather than part of `test`.
let private verifyExpectations (arguments:ParseResults<Arguments>) =
    let dump = Path.Combine(Paths.Output.FullName, "expectations")
    if Directory.Exists dump then Directory.Delete(dump, true)
    Directory.CreateDirectory dump |> ignore

    Environment.SetEnvironmentVariable("KERF_EXPECTATION_DUMP", dump)
    exec "dotnet" ["run"; "--project"; "tests/Nullean.Kerf.Tests"; "-c"; "Release"] |> ignore
    Environment.SetEnvironmentVariable("KERF_EXPECTATION_DUMP", null)

    let cases = Directory.GetDirectories dump
    printfn "checking %d expectations against dotnet format" cases.Length
    if cases.Length = 0 then failwith "no expectations were written — is the dump still wired into the harness?"

    // Trailing newlines are compared separately by the harness itself — insert_final_newline is a
    // test subject of its own — so the dump's own trailing newline is not evidence of anything.
    let read (d: string) = File.ReadAllText(Path.Combine(d, "Expected.cs")).TrimEnd('\n', '\r')

    let before = cases |> Array.map (fun d -> d, read d) |> Map.ofArray

    exec "dotnet" ["format"; "whitespace"; dump; "--folder"] |> ignore

    let changed =
        before
        |> Map.toArray
        |> Array.filter (fun (d, text) -> read d <> text)

    printfn ""
    printfn "%d of %d expectations survive dotnet format" (cases.Length - changed.Length) cases.Length

    if changed.Length > 0 then
        printfn ""
        printfn "first disagreements:"
        changed
        |> Array.truncate 10
        |> Array.iter (fun (d, text) ->
            printfn "  %s" (Path.GetRelativePath(dump, d))
            let now = read d
            printfn "    expected: %s" (text.Replace("\n", "\\n"))
            printfn "    became:   %s" (now.Replace("\n", "\\n")))

    // A floor rather than zero, the same shape as the conformance gate. Seven expectations disagree
    // today and each needs deciding on its own merits: some are tests that deliberately feed an
    // invalid option value and assert the fallback, where Kerf's fallback and Roslyn's differ; others
    // are real questions about which brace-style flag governs indexers and events. The gate stops the
    // number growing while they are worked through, which is worth more than blocking on them.
    let percentage = 100.0 * float (cases.Length - changed.Length) / float cases.Length
    match arguments.TryGetResult Minimum with
    | Some floor when percentage < floor ->
        failwithf "%.2f%% of expectations survive dotnet format, below the required %.2f%%" percentage floor
    | _ -> ()

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
    // DotnetToolSettings.xml, the per-RID packages hold native binaries, and Nullean.Kerf.MSBuild is
    // build-only — props, targets and a CLI payload, with no assembly of its own. All three fail a
    // signing check for the same reason: there is nothing signed in them to check.
    let nugetPackages =
        Paths.Output.GetFiles("*.nupkg") |> Seq.sortByDescending(fun f -> f.CreationTimeUtc)
        |> Seq.map (fun p -> Paths.RootRelative p.FullName)
        |> Seq.filter (fun p ->
            let baseName = Path.GetFileNameWithoutExtension(p).Replace("." + currentVersion.Value, "")
            Paths.mapNugetToProject.ContainsKey(baseName)
            && not (Paths.buildOnlyPackages.Contains baseName))

    let args = ["-v"; currentVersionInformational.Value; "-k"; Paths.SignKey; "-t"; output]
    nugetPackages |> Seq.iter (fun p -> exec "dotnet" (["nupkg-validator"; p] @ args) |> ignore)

let private generateApiChanges (arguments:ParseResults<Arguments>) =
    let output = Paths.RootRelative <| Paths.Output.FullName
    let currentVersion = currentVersion.Value
    // Only diff managed packages — per-RID AOT packages and the build-only MSBuild package have no
    // managed assembly to diff.
    let nugetPackages =
        Paths.Output.GetFiles("*.nupkg") |> Seq.sortByDescending(fun f -> f.CreationTimeUtc)
        |> Seq.map (fun p -> Path.GetFileNameWithoutExtension(Paths.RootRelative p.FullName).Replace("." + currentVersion, ""))
        |> Seq.filter (fun p -> Paths.mapNugetToProject.ContainsKey(p) && not (Paths.buildOnlyPackages.Contains p))
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
    cmd Conformance.Name (Some [Build.Name]) None <| fun _ -> conformance parsed
    cmd Perf.Name (Some [Build.Name]) None <| fun _ -> perf parsed
    cmd MsbuildSmoketest.Name (Some [Build.Name]) None <| fun _ -> msbuildSmoketest parsed

    // No dependency on build: the documentation is markdown and one static HTML file, and waiting on
    // a Release compile to preview a paragraph would make nobody run it.
    step Docs.Name docs
    cmd VerifyExpectations.Name (Some [Build.Name]) None <| fun _ -> verifyExpectations parsed

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
