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

let private corpusFrom (arguments:ParseResults<Arguments>) (target:string) =
    match arguments.TryGetResult Corpus with
    | Some path ->
        let corpus = DirectoryInfo path
        if not corpus.Exists then failwithf "corpus not found: %s" corpus.FullName
        corpus
    | None -> failwithf "%s needs --corpus <path> pointing at a C# checkout" target

let rec private copyTree (source: string) (target: string) =
    Directory.CreateDirectory target |> ignore
    for file in Directory.GetFiles source do
        File.Copy(file, Path.Combine(target, Path.GetFileName file), true)
    for dir in Directory.GetDirectories source do
        let name = Path.GetFileName dir
        if name <> ".git" && name <> "bin" && name <> "obj" then
            copyTree dir (Path.Combine(target, name))

/// Rewrites every `.editorconfig` under a working tree to the configuration being measured.
///
/// Shared by conformance and churn so the two cannot drift into measuring different configurations,
/// which would make their numbers incomparable — and comparing them is the whole point of having
/// both. Every switch here is additive on top of whatever the corpus already says.
let private configureCorpus (arguments:ParseResults<Arguments>) (work:string) =
    let deterministic = arguments.Contains Deterministic
    let preserve = arguments.Contains Preserve
    let trailingCommas = arguments.Contains TrailingCommas
    // Both mode switches imply keeping the corpus's own widths, because with the width off there is no
    // mode to choose: no width means preservation whatever either key says, so forcing it off would make
    // --deterministic a no-op the binder rejects and --preserve a no-op that changes nothing.
    let keepWidths = arguments.Contains Reflow || deterministic || preserve
    let width = arguments.TryGetResult Width

    for config in Directory.GetFiles(work, ".editorconfig", SearchOption.AllDirectories) do
        let text = File.ReadAllText config
        // A config with no max_line_length already gets the default, which is off.
        let widths =
            match width, keepWidths with
            | Some columns, _ ->
                Text.RegularExpressions.Regex.Replace(
                    text, @"max_line_length\s*=\s*\S+", sprintf "max_line_length = %d" columns)
            | None, true -> text
            | None, false ->
                Text.RegularExpressions.Regex.Replace(text, @"max_line_length\s*=\s*\S+", "max_line_length = off")

        // Appended in its own section so it wins over anything the corpus set, whatever order the
        // corpus's own sections are in.
        let appended = Text.StringBuilder(widths)
        if trailingCommas || deterministic || preserve || (width.IsSome && not (widths.Contains "max_line_length")) then
            appended.Append("\n[*.cs]\n") |> ignore
        if trailingCommas then appended.Append("csharp_trailing_comma_in_multiline_lists = true\n") |> ignore
        if deterministic then appended.Append("csharp_keep_existing_linebreaks = false\n") |> ignore
        if preserve then appended.Append("csharp_keep_existing_linebreaks = true\n") |> ignore
        match width with
        | Some columns when not (widths.Contains "max_line_length") ->
            appended.Append(sprintf "max_line_length = %d\n" columns) |> ignore
        | _ -> ()

        File.WriteAllText(config, appended.ToString())

/// A one-line description of the configuration measured, for the report line.
let private modeLabel (arguments:ParseResults<Arguments>) =
    [ if arguments.Contains Deterministic then "deterministic"
      if arguments.Contains Preserve then "preserving"
      if arguments.Contains TrailingCommas then "trailing commas"
      if arguments.Contains Reflow && not (arguments.Contains Deterministic) then "reflow"
      match arguments.TryGetResult Width with
      | Some columns -> sprintf "width %d" columns
      | None -> () ]
    |> function
       | [] -> ""
       | parts -> sprintf " (%s)" (String.Join(", ", parts))

/// Measures how far Kerf's output is from dotnet format's, which is the product claim made
/// checkable. Reflow is forced off so that every difference is an option disagreement rather than a
/// deliberate wrap: with max_line_length off, Kerf should be a fixed point of dotnet format.
let private conformance (arguments:ParseResults<Arguments>) =
    let corpus = corpusFrom arguments "conformance"

    let root = Path.Combine(Paths.Output.FullName, "conformance")
    if Directory.Exists root then Directory.Delete(root, true)
    let work = Path.Combine(root, "kerf")
    let reference = Path.Combine(root, "reference")

    printfn "copying corpus from %s" corpus.FullName
    copyTree corpus.FullName work

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
    // It is also why deterministic layout is measurable at all. --deterministic changes which breaks
    // Kerf picks, not whether dotnet format tolerates them, so this number has to hold in both modes
    // — and if it does not, deterministic mode is inadmissible however good its churn looks.
    //
    // Reflow is forced off by default so a difference is an option disagreement rather than a wrap
    // Kerf chose; --reflow keeps the corpus's own widths, which is the configuration people run.
    configureCorpus arguments work

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
    printfn "conformance with dotnet format%s: %d/%d files (%.2f%%)" (modeLabel arguments) agreeing total percentage
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

/// Measures what adopting Kerf costs a repository: how many of its files the first run rewrites.
///
/// The other side of conformance, and a different question. Conformance asks whether Kerf's output
/// survives dotnet format — a property about the output alone. Churn asks how far that output is from
/// what the repository already has, which is the number that decides whether anybody installs it. A
/// formatter can be a perfect fixed point of dotnet format and still rewrite every file it touches.
///
/// It exists because that number was folklore. It lived in a docs paragraph as "742 of 1,196" with no
/// way to re-derive it, at the same time as being the stated reason for the largest design decision in
/// the printer. A number that governs a decision that size has to be reproducible on demand.
///
/// Unlike conformance this compares against the *pristine* corpus, which is read and never written.
/// Copying a reference into the tree about to be formatted is the trap the layout notes record: both
/// sides get normalised and the diff comes out empty by construction.
let private churn (arguments:ParseResults<Arguments>) =
    let corpus = corpusFrom arguments "churn"

    let root = Path.Combine(Paths.Output.FullName, "churn")
    if Directory.Exists root then Directory.Delete(root, true)
    let work = Path.Combine(root, "kerf")

    printfn "copying corpus from %s" corpus.FullName
    copyTree corpus.FullName work
    configureCorpus arguments work

    let sourceFiles = Directory.GetFiles(work, "*.cs", SearchOption.AllDirectories)
    let before =
        sourceFiles
        |> Array.map (fun file -> file, File.ReadAllText file)
        |> Map.ofArray

    exec "dotnet" ["run"; "--project"; "src/Nullean.Kerf.Cli"; "-c"; "Release"; "--"; "format"; work] |> ignore

    let lineCount (text:string) = text.Split('\n').Length
    let changed =
        sourceFiles
        |> Array.choose (fun file ->
            let original = before.[file]
            let formatted = File.ReadAllText file
            if original = formatted then None
            else Some (file, lineCount formatted - lineCount original))

    let total = sourceFiles.Length
    let percentage = 100.0 * float changed.Length / float total
    let lineDelta = changed |> Array.sumBy snd

    printfn ""
    printfn "churn%s: %d/%d files rewritten (%.2f%%), %+d lines"
        (modeLabel arguments) changed.Length total percentage lineDelta
    if changed.Length > 0 then
        printfn ""
        printfn "largest line deltas:"
        changed
        |> Array.sortByDescending (fun (_, delta) -> abs delta)
        |> Array.truncate 10
        |> Array.iter (fun (file, delta) -> printfn "  %+5d  %s" delta (Path.GetRelativePath(work, file)))
        printfn ""
        printfn "compare with: diff -ru %s %s" corpus.FullName work

    // A ceiling rather than a floor, which is the one difference from conformance: churn going *up*
    // is the regression. Reported unless a ceiling is asked for, because there is no agreed number to
    // hold deterministic mode to yet — publishing it is the point of this target's first few runs.
    match arguments.TryGetResult Maximum with
    | Some ceiling when percentage > ceiling ->
        failwithf "churn %.2f%% is above the permitted %.2f%%" percentage ceiling
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

/// Cleans a corpus that really builds, then requires that it still builds and that dotnet format style has
/// nothing left to say about the rules Kerf owns.
///
/// The end-to-end claim, and the only gate that can catch a fix which compiles but is wrong. `cleanupsafety`
/// proves a wrong verdict cannot damage a file; it cannot prove a *right* verdict was applied correctly,
/// because it never compiles anything. This does: the corpus is built, cleaned, and built again.
///
/// Needs a corpus that builds with the SDK in global.json, which is why it is elastic/docs-builder rather
/// than dotnet/roslyn — Roslyn pins an SDK only its own Arcade bootstrap fetches. Clones it if `--corpus` is
/// not given.
let private cleanupConformance (arguments:ParseResults<Arguments>) =
    let corpus =
        match arguments.TryGetResult Corpus with
        | Some c ->
            let d = DirectoryInfo(c)
            if not d.Exists then failwithf "corpus not found: %s" d.FullName
            d
        | None ->
            let cached = DirectoryInfo(Path.Combine(Paths.Output.FullName, "corpus", "docs-builder"))
            if not cached.Exists then
                printfn "cloning elastic/docs-builder into %s" cached.FullName
                Directory.CreateDirectory(cached.Parent.FullName) |> ignore
                exec "git" ["clone"; "--depth"; "1"; "https://github.com/elastic/docs-builder.git"; cached.FullName] |> ignore
            cached

    let work = DirectoryInfo(Path.Combine(Paths.Output.FullName, "cleanup-conformance"))
    if work.Exists then work.Delete(true)

    let rec copyTree (source: string) (target: string) =
        Directory.CreateDirectory target |> ignore
        for file in Directory.GetFiles source do
            File.Copy(file, Path.Combine(target, Path.GetFileName file), true)
        for dir in Directory.GetDirectories source do
            let name = Path.GetFileName dir
            if name <> ".git" && name <> "bin" && name <> "obj" && name <> "node_modules" then
                copyTree dir (Path.Combine(target, name))

    printfn "copying corpus from %s" corpus.FullName
    copyTree corpus.FullName work.FullName

    // The rules Kerf owns, asked for rather than restated. A second copy of the list here would drift from
    // the catalog, and the drift would look like a passing gate.
    let ownedIds =
        let result = Proc.Start("dotnet", [| "run"; "--project"; "src/Nullean.Kerf.Cli"; "-c"; "Release"; "--"; "rules"; "--cleanup-ids" |])
        result.ConsoleOut
        |> Seq.map (fun l -> l.Line.Trim())
        |> Seq.filter (fun l -> l.StartsWith("IDE", StringComparison.Ordinal))
        |> Seq.tryHead
        |> Option.defaultWith (fun () -> failwith "could not read the cleanup rule ids from `kerf rules --cleanup-ids`")
        |> fun line -> line.Split(' ') |> Array.filter (fun s -> s.Length > 0)

    printfn "rules: %s" (String.concat " " ownedIds)

    // Frontend builds need npm, which a CI runner for a C# repository has no reason to have. Both npm
    // targets in docs-builder are Inputs/Outputs-gated, so writing their outputs makes MSBuild skip them.
    // If a future corpus needs something else the build fails loudly, which is the right way to find out.
    for project in Directory.GetFiles(work.FullName, "package.json", SearchOption.AllDirectories) do
        let dir = Path.GetDirectoryName(project)
        if Directory.GetFiles(dir, "*.csproj") |> Array.isEmpty |> not then
            for relative in [ "node_modules/.install-stamp"; "_static/.build-stamp"; "_static/main.js"; "_static/styles.css" ] do
                let file = Path.Combine(dir, relative.Replace('/', Path.DirectorySeparatorChar))
                Directory.CreateDirectory(Path.GetDirectoryName(file)) |> ignore
                File.WriteAllText(file, "")

    // Escalate the owned rules, and set the preference keys they read so the analyser reports the direction
    // Kerf fixes.
    //
    // A .globalconfig rather than an append to the corpus's .editorconfig. Appending was tried and reported
    // nothing: a corpus carries its own sections, its own `root=true`, and — because the work tree sits inside
    // this repository — Kerf's own .editorconfig turns up as an ancestor too. A global config has no globs, no
    // sections and no walk, so there is nothing left to reason about. It is also what the SDK itself uses to
    // set these severities, and `global_level` settles ties out loud rather than by file position.
    let globalConfig = Path.Combine(work.FullName, "kerf-conformance.globalconfig")
    let severities =
        ownedIds |> Array.map (sprintf "dotnet_diagnostic.%s.severity = warning") |> String.concat "\n"

    let preferences =
        [ "csharp_style_var_for_built_in_types = true"
          "csharp_style_var_when_type_is_apparent = true"
          "csharp_style_var_elsewhere = true"
          "dotnet_style_readonly_field = true"
          "dotnet_style_require_accessibility_modifiers = for_non_interface_members"
          "csharp_style_implicit_object_creation_when_type_is_apparent = true"
          "csharp_style_prefer_readonly_struct = true"
          "csharp_style_prefer_readonly_struct_member = true" ]
        |> String.concat "\n"

    File.WriteAllText(globalConfig,
        sprintf "is_global = true\nglobal_level = 100\n\n%s\n%s\n" severities preferences)

    // A .editorconfig entry for a specific rule outranks a global config for that rule, whatever
    // `global_level` says — the level only breaks ties between global configs. docs-builder sets
    // `dotnet_diagnostic.IDE0005.severity = none`, so nine rules reported and that one did not. Any such line
    // in the work tree is commented out so the global config governs.
    for config in Directory.GetFiles(work.FullName, ".editorconfig", SearchOption.AllDirectories) do
        let lines = File.ReadAllLines config
        let neutralised =
            lines
            |> Array.map (fun line ->
                let trimmed = line.TrimStart()
                if trimmed.StartsWith("dotnet_diagnostic.", StringComparison.OrdinalIgnoreCase)
                   && ownedIds |> Array.exists (fun id -> trimmed.StartsWith("dotnet_diagnostic." + id + ".", StringComparison.OrdinalIgnoreCase))
                then "# neutralised by ./build.sh cleanupconformance: " + line
                else line)

        if neutralised <> lines then
            printfn "neutralised rule severities in %s" (Paths.RootRelative config)
            File.WriteAllLines(config, neutralised)

    // Only $(ErrorLog) goes in a file, because it needs $(TargetFramework) — a single path shared by two
    // inner builds produces two concatenated JSON documents, which the reader refuses. Directory.Build.targets
    // rather than .props, because the corpus has nested props files (src/, tests/, src/tooling/) that would
    // override a root one.
    //
    // Everything else is a global property on the command line, and that is not a style choice. Measured:
    // EnforceCodeStyleInBuild set in Directory.Build.targets loaded *zero* IDE analysers — the SDK decides
    // which analysers to add before that file is imported, so the whole run reported nothing and the gate
    // would have passed while measuring nothing. A global property is set before evaluation and also
    // outranks the nested props files, which is what un-escalating their warnings needs.
    let targets = Path.Combine(work.FullName, "Directory.Build.targets")
    let targetsXml =
        [ "<Project>"
          "  <PropertyGroup Condition=\"'$(TargetFramework)' != ''\">"
          "    <ErrorLog>$(IntermediateOutputPath)kerf.sarif,version=2.1</ErrorLog>"
          "  </PropertyGroup>"
          "  <ItemGroup>"
          "    <EditorConfigFiles Include=\"$(MSBuildThisFileDirectory)kerf-conformance.globalconfig\" />"
          "  </ItemGroup>"
          "</Project>" ]
        |> String.concat "\n"

    File.WriteAllText(targets, targetsXml)

    let build stage =
        let result =
            Proc.Start("dotnet",
                [| "build"; work.FullName; "-tl:off"; "--nologo"
                   "-p:EnforceCodeStyleInBuild=true"
                   // Without this the analysers are loaded and never run. docs-builder sets
                   // RunAnalyzersDuringBuild=false unless CI=true, which is sensible for a local build and
                   // fatal for this gate: every rule reported nothing and the run looked clean. The "all ten
                   // must be reported" assertion below is what caught it.
                   "-p:RunAnalyzersDuringBuild=true"
                   "-p:GenerateDocumentationFile=true"
                   // GenerateDocumentationFile turns every undocumented public member into CS1591, and the
                   // corpus's own warning policy is not what is being measured.
                   "-p:TreatWarningsAsErrors=false"
                   // %3B, not `;` and not `,`: MSBuild reads both as separators between properties on the
                   // command line, so either spelling fails with MSB1006 before anything is compiled.
                   "-p:NoWarn=CS1591%3BCS1573%3BCS1574%3BCS1712%3BNU1902%3BNU1903" |])
        let output = result.ConsoleOut |> Seq.map (fun l -> l.Line) |> String.concat "\n"
        if result.ExitCode <> 0 then
            printfn "%s" output
            failwithf "the corpus must build %s, but it did not" stage
        output

    printfn "building the corpus"
    let pristine = build "before anything"

    // What a repository that is already style-clean reports: nothing. docs-builder sets
    // `dotnet_analyzer_diagnostic.category-Style.severity = warning` and builds warning-free, so every one of
    // these rules is already satisfied across its 1,200 files. That is the zero-churn promise as a
    // measurement rather than a claim — and it is why the fixing half has to be exercised by a seeded file,
    // because there is nothing here to fix.
    let pristineReports = ownedIds |> Array.filter (fun id -> pristine.Contains(id, StringComparison.Ordinal))
    printfn "the pristine corpus reports: %s" (if pristineReports.Length = 0 then "(none, as it should)" else String.concat " " pristineReports)

    // One site per rule, in a file of its own, so the fixing half runs against a solution that really
    // compiles. The 1,200 files around it are what the rebuild and the dotnet-format check are for: cleanup
    // must not break any of them.
    let seedProject = Path.Combine(work.FullName, "src", "Elastic.Documentation")
    if not (Directory.Exists seedProject) then
        failwithf "the corpus does not have the project this gate seeds into: %s" seedProject

    let seed =
        [ "#nullable enable"                                  // IDE0240, the project already enables it
          "using System.Text;"                                // IDE0005, and the next line joins its run
          "using System.Globalization;"
          ""
          "namespace Kerf.ConformanceSeed;"
          ""
          "internal struct SeedPoint"                         // IDE0250
          "{"
          "	public int X { get; init; }"
          ""
          "	public int Doubled() => X * 2;"                  // IDE0251
          "}"
          ""
          "internal sealed class Seed"
          "{"
          "	private string _name;"                           // IDE0044
          "	int _count;"                                     // IDE0040
          ""
          "	public Seed() => _name = \"seed\";"
          ""
          "	public string Describe()"
          "	{"
          "		string text = \"seed \";"                      // IDE0007
          "		bool flag = default(bool);"                    // IDE0034
          "		return $\"{text}{_count.ToString()}{_name}{flag}\";"   // IDE0071
          "	}"
          ""
          "	public void Bump() => _count++;"
          ""
          "	private static Seed Create() => new Seed();"     // IDE0090
          ""
          "	public static Seed Make() => Create();"
          "}"
          "" ]
        |> String.concat "\n"

    let seedFile = Path.Combine(seedProject, "KerfConformanceSeed.cs")
    File.WriteAllText(seedFile, seed)

    printfn "building with one violation seeded per rule"
    let before = build "with the seed in place"

    let missing = ownedIds |> Array.filter (fun id -> not (before.Contains(id, StringComparison.Ordinal)))
    if missing.Length > 0 then
        failwithf "the build did not report %s, so those rules are not being measured — has the analyser's shape changed?" (String.concat " " missing)

    // Distinct sites, not lines: MSBuild prints each diagnostic twice, once in the stream and once in the
    // summary, and once per target framework on top of that.
    // Only the rules Kerf owns. Matching every IDE id counted the corpus's own escalations — IDE0058 alone
    // contributes 233 sites — and made the assertion below meaningless.
    let ownedPattern =
        sprintf @"([^\s(]+\.cs)\((\d+),(\d+)\): warning (%s)" (String.concat "|" ownedIds)

    let sites (output: string) =
        Text.RegularExpressions.Regex.Matches(output, ownedPattern)
        |> Seq.map (fun m -> m.Groups[1].Value + m.Groups[2].Value + m.Groups[3].Value + m.Groups[4].Value)
        |> Set.ofSeq

    let tally (output: string) =
        Text.RegularExpressions.Regex.Matches(output, ownedPattern)
        |> Seq.map (fun m -> m.Groups[4].Value, m.Groups[1].Value + m.Groups[2].Value + m.Groups[3].Value)
        |> Seq.distinct
        |> Seq.countBy fst
        |> Seq.sortBy fst
        |> Seq.map (fun (id, n) -> sprintf "%s=%d" id n)
        |> String.concat " "

    let sitesBefore = sites before
    printfn "reported: all %d rules, %d distinct site(s)" ownedIds.Length sitesBefore.Count
    printfn "  before: %s" (tally before)

    printfn "cleaning"
    let cleanup = Proc.Start("dotnet", [| "run"; "--project"; "src/Nullean.Kerf.Cli"; "-c"; "Release"; "--"; "cleanup"; work.FullName |])
    let cleanupOutput = cleanup.ConsoleOut |> Seq.map (fun l -> l.Line) |> String.concat "\n"
    printfn "%s" cleanupOutput
    if cleanup.ExitCode <> 0 then failwith "kerf cleanup failed on the corpus"

    // Churn, published rather than asserted, the way layout-decisions requires of anything that rewrites
    // files. Taken from cleanup's own summary rather than from git, so the copy does not need to carry the
    // corpus's history — which is most of its size.
    let summary = Text.RegularExpressions.Regex.Match(cleanupOutput, @"(\d+) fix\(es\) in (\d+) file\(s\), (\d+) refused")
    if not summary.Success || summary.Groups[1].Value = "0" then
        failwith "cleanup changed nothing, so the gate proves nothing"

    let refused = int summary.Groups[3].Value
    printfn "churn: %s fix(es) across %s file(s), %d refused" summary.Groups[1].Value summary.Groups[2].Value refused

    // The gate that only a real build can be: a fix that compiles but is wrong shows up here and nowhere
    // else. cleanupsafety never compiles anything, so it cannot reach this.
    printfn "rebuilding the cleaned corpus"
    let after = build "after cleanup"
    let sitesAfter = sites after

    // Not "nothing is left": Kerf declines some sites on purpose — a file with a `#if` keeps its using
    // directives, because the compiler decided for one symbol set. So the claim is the exact one it can
    // make: everything that was not explicitly declined was fixed.
    printfn "after cleanup: %d site(s) left, %d declined" sitesAfter.Count refused
    printfn "  after:  %s" (tally after)
    if sitesAfter.Count > refused then
        let leftOver = ownedIds |> Array.filter (fun id -> after.Contains(id, StringComparison.Ordinal))
        failwithf "%d site(s) survived but only %d were declined, so something went unfixed without saying so: %s"
            sitesAfter.Count refused (String.concat " " leftOver)

    // What was fixed has to have actually gone, not merely been counted.
    if sitesAfter.Count >= sitesBefore.Count then
        failwithf "cleanup reported %s fixes but the site count did not fall (%d then %d)"
            summary.Groups[1].Value sitesBefore.Count sitesAfter.Count

    // And what the reference implementation makes of the result. Reported rather than gated, because it will
    // also want to fix the sites Kerf declined — it has a compilation and can reason about symbol sets.
    printfn "asking dotnet format style what it would still do"
    let verify =
        Proc.Start("dotnet",
            Array.concat [
                [| "format"; "style"; work.FullName; "--verify-no-changes"; "--no-restore"
                   "--severity"; "info"; "--diagnostics" |]
                ownedIds
            ])

    if verify.ExitCode = 0 then
        printfn "dotnet format style: nothing left at all"
    else
        let remaining =
            verify.ConsoleOut
            |> Seq.map (fun l -> l.Line)
            |> Seq.filter (fun l -> ownedIds |> Array.exists (fun id -> l.Contains(id, StringComparison.Ordinal)))
            |> Seq.length
        printfn "dotnet format style: %d line(s) still to fix, which should be the declined ones" remaining

    printfn ""
    printfn "cleanup conformance verified: %d site(s) fixed across a solution that still builds, %d declined and said so"
        (sitesBefore.Count - sitesAfter.Count) sitesAfter.Count

/// Feeds a corpus verdicts Kerf has no business trusting, and requires that none of them damages a file.
///
/// The counterpart to `conformance`, for the half of cleanup that conformance cannot reach. Every other
/// cleanup test hands over a diagnostic the compiler really reported; this one claims every rule fires
/// everywhere it could, so it deletes needed imports and writes `var` where the type was load-bearing.
/// Those are wrong verdicts on purpose. What is asserted is that a wrong verdict still cannot produce a
/// file the parser rejects, still cannot fail verification, and still leaves the node-kind gate refusing a
/// second pass — which is the property that makes consuming a verdict from an earlier build safe.
///
/// Nothing is written. The corpus is read and cleaned in memory, so this is safe to point at a checkout
/// somebody is working in — which also means it does not need the corpus to build, and a repository whose
/// SDK cannot be resolved here is still a usable corpus.
let private cleanupSafety (arguments:ParseResults<Arguments>) =
    let corpus =
        match arguments.TryGetResult Corpus with
        | Some c -> DirectoryInfo(c)
        | None -> failwith "cleanupsafety needs --corpus <path> pointing at a C# checkout"
    if not corpus.Exists then failwithf "corpus not found: %s" corpus.FullName

    printfn "sweeping %s" corpus.FullName
    let summaryFile = Path.Combine(Path.GetTempPath(), "kerf-cleanup-summary.txt")
    if File.Exists summaryFile then File.Delete summaryFile
    Environment.SetEnvironmentVariable("KERF_CLEANUP_CORPUS", corpus.FullName)
    Environment.SetEnvironmentVariable("KERF_CLEANUP_SUMMARY", summaryFile)

    let result =
        Proc.Start("dotnet",
            [| "run"; "--project"; "tests/Nullean.Kerf.Tests"; "-c"; "Release"; "--"
               "--treenode-filter"; "/*/*/CleanupCorpusTests/*" |])

    let output = result.ConsoleOut |> Seq.map (fun l -> l.Line) |> String.concat "\n"
    if result.ExitCode <> 0 then
        printfn "%s" output
        failwith "the corpus sweep found a wrong verdict that damaged a file"

    if File.Exists summaryFile then printfn "%s" (File.ReadAllText(summaryFile).Trim())

/// Proves `kerf cleanup` fixes what a build reported, and nothing else.
///
/// Four assertions, where the MSBuild formatting smoke test needs two. Two of them are the same idea —
/// it works, and it would have failed without us, because an assertion that passes when the analysers
/// are switched off proves nothing. The other two exist because cleanup can be wrong in ways a
/// formatter cannot: it could silence a rule instead of fixing it, and it could rewrite a repository
/// that never asked for anything.
let private cleanupSmoketest (arguments:ParseResults<Arguments>) =
    exec "dotnet" ["publish"; "src/Nullean.Kerf.Cli"; "-c"; "Release"; "-o"; ".artifacts/cleanup-smoketest/kerf"
                   "-p:PublishAot=false"; "-p:SelfContained=false"] |> ignore

    let kerfDll = Path.GetFullPath(Path.Combine(".artifacts", "cleanup-smoketest", "kerf", "Nullean.Kerf.Cli.dll"))

    let sample = Path.Combine("examples", "kerf-cleanup-smoketest")
    let source = Path.Combine(sample, "Widget.cs")
    let pristine = Path.Combine(sample, "Widget.unclean")
    let editorConfig = Path.Combine(sample, ".editorconfig")
    let pristineConfig = File.ReadAllText(editorConfig)

    let restore () =
        File.Copy(pristine, source, true)
        // File.Copy carries the source's timestamp across. That would leave the file looking older than
        // it is, and cleanup's freshness gate — which refuses a file edited after the build wrote its
        // log — reads exactly that timestamp, so the harness has to set it rather than inherit it.
        File.SetLastWriteTimeUtc(source, DateTime.UtcNow)

    let build () =
        let args = ["build"; sample; "-c"; "Debug"; "--nologo"; "-tl:off"; sprintf "-p:Kerf_Dll=%s" kerfDll]
        let result = Proc.Start("dotnet", List.toArray args)
        let output = result.ConsoleOut |> Seq.map (fun l -> l.Line) |> String.concat "\n"
        result.ExitCode, output

    // The repository sets UseArtifactsOutput, so the sample's $(IntermediateOutputPath) is under
    // .artifacts rather than beside its project file. Found rather than assumed, because which of the two
    // layouts a consumer uses is not this test's business — and `kerf cleanup` run from a repository root
    // finds the log either way.
    let findLogs () =
        Directory.GetFiles(".", "kerf.sarif", SearchOption.AllDirectories)
        |> Array.filter (fun p -> p.Contains "kerf-cleanup-smoketest")

    let clearLogs () = findLogs () |> Array.iter File.Delete

    let cleanup (extra: string list) =
        let logs = findLogs () |> Array.toList |> List.collect (fun l -> ["--diagnostics"; l])
        let args = ["exec"; kerfDll; "cleanup"; sample] @ logs @ extra
        let result = Proc.Start("dotnet", List.toArray args)
        let output = result.ConsoleOut |> Seq.map (fun l -> l.Line) |> String.concat "\n"
        result.ExitCode, output

    try
        // 1. The build fails. IDE0005 is an error here and nothing has fixed it, so if this succeeds the
        //    analysers are not running and the rest of the test is measuring nothing.
        restore ()
        clearLogs ()
        printfn "building the sample dirty"
        let dirtyCode, dirtyOutput = build ()
        if dirtyCode = 0 then
            failwith "the sample built clean while still holding unnecessary usings, so this proves nothing — is EnforceCodeStyleInBuild still on, and GenerateDocumentationFile?"
        // Every rule in the slice, not just the first. A fixture that only exercises one would let the
        // others rot without anything noticing.
        for rule in [ "IDE0005"; "IDE0007"; "IDE0040"; "IDE0044"; "IDE0090" ] do
            if not (dirtyOutput.Contains rule) then
                failwithf "expected %s from the dirty build, got:\n%s" rule dirtyOutput

        // The compiler writes its error log even when it fails. That is the fact a post-build MSBuild
        // target could not have used, because it would never have run.
        if Array.isEmpty (findLogs ()) then
            failwith "the failing build wrote no kerf.sarif, so cleanup has nothing to read"

        // 2. Cleanup reads that log and the source changes.
        printfn "running kerf cleanup"
        let cleanCode, cleanOutput = cleanup []
        if cleanCode <> 0 then failwithf "kerf cleanup failed:\n%s" cleanOutput
        let cleaned = File.ReadAllText(source)
        if cleaned = File.ReadAllText(pristine) then
            failwithf "kerf cleanup changed nothing:\n%s" cleanOutput
        if cleaned.Contains "System.Globalization" || cleaned.Contains "System.Numerics" then
            failwithf "kerf cleanup left part of a run behind — it read the start of the span and not its end:\n%s" cleaned
        if not (cleaned.Contains "System.Text.RegularExpressions") then
            failwith "kerf cleanup removed a directive the file needs; the run's extent was read wrong"

        // The other four rules, asserted on the output rather than only on the exit code. Each is a
        // different delta — a modifier inserted, a type name dropped, a type name swapped for a keyword —
        // and a rule that silently stopped firing would otherwise look like a clean run.
        for expected in [ "private readonly string _name"; "private int _count"; "var text = "; "=> new()" ] do
            if not (cleaned.Contains expected) then
                failwithf "kerf cleanup did not produce %s:\n%s" expected cleaned

        // 3. The next build is clean, and IDE0005 is absent rather than downgraded. Kerf never writes a
        //    severity; a muted rule and a fixed one look the same from the exit code alone, so the
        //    output is checked too.
        printfn "building the sample again"
        let cleanBuildCode, cleanBuildOutput = build ()
        if cleanBuildCode <> 0 then
            failwithf "the sample must build after cleanup, but it failed:\n%s" cleanBuildOutput
        for rule in [ "IDE0005"; "IDE0007"; "IDE0040"; "IDE0044"; "IDE0090" ] do
            if cleanBuildOutput.Contains rule then
                failwithf "%s is still reported after cleanup, so it was not actually fixed:\n%s" rule cleanBuildOutput

        // 4. With nothing escalated, cleanup changes nothing. Measured: with EnforceCodeStyleInBuild on
        //    and no severity set, the compiler reports no IDE rule at all — so the opt-in is structural
        //    and this is what holds it that way.
        printfn "building and cleaning with no severity escalated"
        restore ()
        let withoutSeverities =
            pristineConfig.Split('\n')
            |> Array.filter (fun line -> not (line.StartsWith("dotnet_diagnostic.", StringComparison.Ordinal)))
            |> String.concat "\n"
        File.WriteAllText(editorConfig, withoutSeverities)
        clearLogs ()
        let defaultCode, defaultOutput = build ()
        if defaultCode <> 0 then failwithf "the sample must build when nothing is escalated:\n%s" defaultOutput
        let before = File.ReadAllText(source)
        cleanup [] |> ignore
        if File.ReadAllText(source) <> before then
            failwith "cleanup rewrote a file nobody asked it to; the fixed set must be the reported set"

        printfn ""
        printfn "kerf cleanup verified: fixes what the build reported, whole runs, nothing else, and nothing unasked"
    finally
        // Leave the sample unclean and escalated, which is how it is checked in.
        File.WriteAllText(editorConfig, pristineConfig)
        File.Copy(pristine, source, true)

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
    cmd Churn.Name (Some [Build.Name]) None <| fun _ -> churn parsed
    cmd Perf.Name (Some [Build.Name]) None <| fun _ -> perf parsed
    cmd MsbuildSmoketest.Name (Some [Build.Name]) None <| fun _ -> msbuildSmoketest parsed
    cmd CleanupSmoketest.Name (Some [Build.Name]) None <| fun _ -> cleanupSmoketest parsed
    cmd CleanupSafety.Name (Some [Build.Name]) None <| fun _ -> cleanupSafety parsed
    cmd CleanupConformance.Name (Some [Build.Name]) None <| fun _ -> cleanupConformance parsed

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
