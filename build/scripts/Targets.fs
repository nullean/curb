module Targets

#nowarn "3391" // Nullable<int> implicit conversions from ProcNet's ExitCode

open Argu
open System
open System.IO
open System.Text.Json
open Bullseye
open CommandLine
open Fake.Tools.Git
open ProcNet

let exec binary args =
    // Proc 0.14+: Exec passes args directly to the OS (no shell expansion) and throws on failure.
    Proc.Exec (binary, List.toArray args) |> ignore

/// Like exec but returns the exit code rather than throwing on failure.
let private execResult binary args =
    let result = Proc.Start(binary, List.toArray args)
    result.ExitCode

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
    exec "dotnet" ["run"; "--project"; "tests/Nullean.Curb.Tests"; "-c"; "Release"] |> ignore

let private benchmark (arguments:ParseResults<Arguments>) =
    exec "dotnet" ["run"; "--project"; "tests/Nullean.Curb.Benchmarks"; "-c"; "Release"] |> ignore

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

/// Measures how far Curb's output is from dotnet format's, which is the product claim made
/// checkable. Reflow is forced off so that every difference is an option disagreement rather than a
/// deliberate wrap: with max_line_length off, Curb should be a fixed point of dotnet format.
let private conformance (arguments:ParseResults<Arguments>) =
    let corpus = corpusFrom arguments "conformance"

    let root = Path.Combine(Paths.Output.FullName, "conformance")
    if Directory.Exists root then Directory.Delete(root, true)
    let work = Path.Combine(root, "curb")
    let reference = Path.Combine(root, "reference")

    printfn "copying corpus from %s" corpus.FullName
    copyTree corpus.FullName work

    // What this measures is dotnet_format(curb(x)) = curb(x) — that Curb's output is a *fixed point*
    // of dotnet format, not that the two agree on the same input. That is the stronger property and
    // the one that matters: a repository formatted by Curb stays put when anyone runs dotnet format,
    // hits Format Document, or builds with EnforceCodeStyleInBuild.
    //
    // It is also what makes opinionated mode admissible. dotnet format declines to decide almost
    // everything about layout, and anything it declines to decide Curb may decide while staying a
    // fixed point. So --opinionated is gated by this same number: it may change many files, it may
    // not change this.
    //
    // It is also why deterministic layout is measurable at all. --deterministic changes which breaks
    // Curb picks, not whether dotnet format tolerates them, so this number has to hold in both modes
    // — and if it does not, deterministic mode is inadmissible however good its churn looks.
    //
    // Reflow is forced off by default so a difference is an option disagreement rather than a wrap
    // Curb chose; --reflow keeps the corpus's own widths, which is the configuration people run.
    configureCorpus arguments work

    exec "dotnet" ["run"; "--project"; "src/Nullean.Curb.Cli"; "-c"; "Release"; "--"; "format"; work] |> ignore
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

    // A regression here means Curb drifted away from the reference implementation, which is the one
    // number the product claim rests on. Fail rather than merely report it.
    match arguments.TryGetResult Minimum with
    | Some floor when percentage < floor ->
        failwithf "conformance %.2f%% is below the required %.2f%%" percentage floor
    | _ -> ()

/// Measures what adopting Curb costs a repository: how many of its files the first run rewrites.
///
/// The other side of conformance, and a different question. Conformance asks whether Curb's output
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
    let work = Path.Combine(root, "curb")

    printfn "copying corpus from %s" corpus.FullName
    copyTree corpus.FullName work
    configureCorpus arguments work

    let sourceFiles = Directory.GetFiles(work, "*.cs", SearchOption.AllDirectories)
    let before =
        sourceFiles
        |> Array.map (fun file -> file, File.ReadAllText file)
        |> Map.ofArray

    exec "dotnet" ["run"; "--project"; "src/Nullean.Curb.Cli"; "-c"; "Release"; "--"; "format"; work] |> ignore

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
    exec "dotnet" ["publish"; "src/Nullean.Curb.Cli"; "-c"; "Release"; "-r"; rid] |> ignore

    let binary =
        let name = if OperatingSystem.IsWindows() then "curb.exe" else "curb"
        Path.Combine(".artifacts", "publish", "Nullean.Curb.Cli", sprintf "release_%s" rid, name)
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
/// The assertion is deliberately two-sided. Building the sample with Curb must succeed even though
/// IDE0055 is escalated to an error and the source is deliberately misformatted; building it with
/// Curb bypassed must fail with those same errors. Only the pair proves anything — the first alone
/// would pass just as well if the analysers were never running.
///
/// A third build covers the cache, which is the other half of the incrementality story and the half
/// nothing else would catch.
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

    exec "dotnet" ["publish"; "src/Nullean.Curb.Cli"; "-c"; "Release"; "-r"; rid] |> ignore

    let binary =
        let name = if OperatingSystem.IsWindows() then "curb.exe" else "curb"
        Path.GetFullPath(Path.Combine(".artifacts", "publish", "Nullean.Curb.Cli", sprintf "release_%s" rid, name))

    let sample = Path.Combine("examples", "curb-msbuild-smoketest")
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

    printfn "building the sample with Curb"
    let withCurbCode, withCurbOutput = build [sprintf "-p:Curb_Exe=%s" binary]
    if withCurbCode <> 0 then
        failwithf "the sample must build with Curb in the way, but it failed:\n%s" withCurbOutput
    if not (File.ReadAllText(source).Contains("public static int Run(int x)")) then
        failwithf "Curb did not reformat the sample before the compiler read it"

    // The cache only shows up when the target actually runs a second time, and simply building again
    // would not do that: the stamp is fresh, so MSBuild skips the target outright — the first layer of
    // incrementality doing its job. Restoring Program.cs and touching it is what forces the run, and it
    // also guarantees that file misses, so a hit can only be Stable.cs. Hence "exactly one", not "some":
    // a cache that served Program.cs too would be serving a file whose bytes had changed.
    printfn "building the sample again, to prove the cache serves the file that did not change"
    let cachedCode, cachedOutput = build [sprintf "-p:Curb_Exe=%s" binary; "-p:Curb_LogLevel=high"]
    if cachedCode <> 0 then
        failwithf "the sample must build a second time, but it failed:\n%s" cachedOutput
    if not (cachedOutput.Contains "(1 from cache)") then
        failwithf "expected Curb to serve exactly one file from the cache on the second build, got:\n%s" cachedOutput

    printfn "building the sample with Curb bypassed"
    let bypassedCode, bypassedOutput = build ["-p:Curb_Bypass=true"]
    if bypassedCode = 0 then
        failwith "the sample built clean without Curb, so the check proves nothing — is EnforceCodeStyleInBuild still on?"
    if not (bypassedOutput.Contains "IDE0055") then
        failwithf "expected IDE0055 errors without Curb, got:\n%s" bypassedOutput

    // Leave the sample misformatted, which is how it is checked in.
    File.Copy(pristine, source, true)
    printfn ""
    printfn "MSBuild integration verified: formatted before CoreCompile, cached on the way back, and IDE0055 fails without it"

/// Cleans a corpus that really builds, then requires that it still builds and that dotnet format style has
/// nothing left to say about the rules Curb owns.
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

    // The rules Curb owns, asked for rather than restated. A second copy of the list here would drift from
    // the catalog, and the drift would look like a passing gate.
    let ownedIds =
        let result = Proc.Start("dotnet", [| "run"; "--project"; "src/Nullean.Curb.Cli"; "-c"; "Release"; "--"; "rules"; "--cleanup-ids" |])
        result.ConsoleOut
        |> Seq.map (fun l -> l.Line.Trim())
        |> Seq.filter (fun l -> l.StartsWith("IDE", StringComparison.Ordinal))
        |> Seq.tryHead
        |> Option.defaultWith (fun () -> failwith "could not read the cleanup rule ids from `curb rules --cleanup-ids`")
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
    // Curb fixes.
    //
    // A .globalconfig rather than an append to the corpus's .editorconfig. Appending was tried and reported
    // nothing: a corpus carries its own sections, its own `root=true`, and — because the work tree sits inside
    // this repository — Curb's own .editorconfig turns up as an ancestor too. A global config has no globs, no
    // sections and no walk, so there is nothing left to reason about. It is also what the SDK itself uses to
    // set these severities, and `global_level` settles ties out loud rather than by file position.
    let globalConfig = Path.Combine(work.FullName, "curb-conformance.globalconfig")
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
          "    <ErrorLog>$(IntermediateOutputPath)curb.sarif,version=2.1</ErrorLog>"
          "  </PropertyGroup>"
          "  <ItemGroup>"
          "    <EditorConfigFiles Include=\"$(MSBuildThisFileDirectory)curb-conformance.globalconfig\" />"
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
          "namespace Curb.ConformanceSeed;"
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

    let seedFile = Path.Combine(seedProject, "CurbConformanceSeed.cs")
    File.WriteAllText(seedFile, seed)

    printfn "building with one violation seeded per rule"
    let before = build "with the seed in place"

    let missing = ownedIds |> Array.filter (fun id -> not (before.Contains(id, StringComparison.Ordinal)))
    if missing.Length > 0 then
        failwithf "the build did not report %s, so those rules are not being measured — has the analyser's shape changed?" (String.concat " " missing)

    // Distinct sites, not lines: MSBuild prints each diagnostic twice, once in the stream and once in the
    // summary, and once per target framework on top of that.
    // Only the rules Curb owns. Matching every IDE id counted the corpus's own escalations — IDE0058 alone
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
    let cleanup = Proc.Start("dotnet", [| "run"; "--project"; "src/Nullean.Curb.Cli"; "-c"; "Release"; "--"; "cleanup"; work.FullName |])
    let cleanupOutput = cleanup.ConsoleOut |> Seq.map (fun l -> l.Line) |> String.concat "\n"
    printfn "%s" cleanupOutput
    if cleanup.ExitCode <> 0 then failwith "curb cleanup failed on the corpus"

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

    // Not "nothing is left": Curb declines some sites on purpose — a file with a `#if` keeps its using
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
    // also want to fix the sites Curb declined — it has a compilation and can reason about symbol sets.
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

/// Feeds a corpus verdicts Curb has no business trusting, and requires that none of them damages a file.
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
    let summaryFile = Path.Combine(Path.GetTempPath(), "curb-cleanup-summary.txt")
    if File.Exists summaryFile then File.Delete summaryFile
    Environment.SetEnvironmentVariable("CURB_CLEANUP_CORPUS", corpus.FullName)
    Environment.SetEnvironmentVariable("CURB_CLEANUP_SUMMARY", summaryFile)

    let result =
        Proc.Start("dotnet",
            [| "run"; "--project"; "tests/Nullean.Curb.Tests"; "-c"; "Release"; "--"
               "--treenode-filter"; "/*/*/CleanupCorpusTests/*" |])

    let output = result.ConsoleOut |> Seq.map (fun l -> l.Line) |> String.concat "\n"
    if result.ExitCode <> 0 then
        printfn "%s" output
        failwith "the corpus sweep found a wrong verdict that damaged a file"

    if File.Exists summaryFile then printfn "%s" (File.ReadAllText(summaryFile).Trim())

/// Proves `curb cleanup` fixes what a build reported, and nothing else.
///
/// Four assertions, where the MSBuild formatting smoke test needs two. Two of them are the same idea —
/// it works, and it would have failed without us, because an assertion that passes when the analysers
/// are switched off proves nothing. The other two exist because cleanup can be wrong in ways a
/// formatter cannot: it could silence a rule instead of fixing it, and it could rewrite a repository
/// that never asked for anything.
let private cleanupSmoketest (arguments:ParseResults<Arguments>) =
    exec "dotnet" ["publish"; "src/Nullean.Curb.Cli"; "-c"; "Release"; "-o"; ".artifacts/cleanup-smoketest/curb"
                   "-p:PublishAot=false"; "-p:SelfContained=false"] |> ignore

    let curbDll = Path.GetFullPath(Path.Combine(".artifacts", "cleanup-smoketest", "curb", "curb.dll"))

    let sample = Path.Combine("examples", "curb-cleanup-smoketest")
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
        let args = ["build"; sample; "-c"; "Debug"; "--nologo"; "-tl:off"; sprintf "-p:Curb_Dll=%s" curbDll]
        let result = Proc.Start("dotnet", List.toArray args)
        let output = result.ConsoleOut |> Seq.map (fun l -> l.Line) |> String.concat "\n"
        result.ExitCode, output

    // The repository sets UseArtifactsOutput, so the sample's $(IntermediateOutputPath) is under
    // .artifacts rather than beside its project file. Found rather than assumed, because which of the two
    // layouts a consumer uses is not this test's business — and `curb cleanup` run from a repository root
    // finds the log either way.
    let findLogs () =
        Directory.GetFiles(".", "curb.sarif", SearchOption.AllDirectories)
        |> Array.filter (fun p -> p.Contains "curb-cleanup-smoketest")

    let clearLogs () = findLogs () |> Array.iter File.Delete

    let cleanup (extra: string list) =
        let logs = findLogs () |> Array.toList |> List.collect (fun l -> ["--sarif-log"; l])
        let args = ["exec"; curbDll; "cleanup"; sample] @ logs @ extra
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
            failwith "the failing build wrote no curb.sarif, so cleanup has nothing to read"

        // 2. Cleanup reads that log and the source changes.
        printfn "running curb cleanup"
        let cleanCode, cleanOutput = cleanup []
        if cleanCode <> 0 then failwithf "curb cleanup failed:\n%s" cleanOutput
        let cleaned = File.ReadAllText(source)
        if cleaned = File.ReadAllText(pristine) then
            failwithf "curb cleanup changed nothing:\n%s" cleanOutput
        if cleaned.Contains "System.Globalization" || cleaned.Contains "System.Numerics" then
            failwithf "curb cleanup left part of a run behind — it read the start of the span and not its end:\n%s" cleaned
        if not (cleaned.Contains "System.Text.RegularExpressions") then
            failwith "curb cleanup removed a directive the file needs; the run's extent was read wrong"

        // The other four rules, asserted on the output rather than only on the exit code. Each is a
        // different delta — a modifier inserted, a type name dropped, a type name swapped for a keyword —
        // and a rule that silently stopped firing would otherwise look like a clean run.
        for expected in [ "private readonly string _name"; "private int _count"; "var text = "; "=> new()" ] do
            if not (cleaned.Contains expected) then
                failwithf "curb cleanup did not produce %s:\n%s" expected cleaned

        // 3. The next build is clean, and IDE0005 is absent rather than downgraded. Curb never writes a
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
        printfn "curb cleanup verified: fixes what the build reported, whole runs, nothing else, and nothing unasked"
    finally
        // Leave the sample unclean and escalated, which is how it is checked in.
        File.WriteAllText(editorConfig, pristineConfig)
        File.Copy(pristine, source, true)

/// One documented, deliberate case where Curb's chosen shape either differs from what a reference
/// tool would have produced on its own, or — the rarer, stronger claim failing — is not even a fixed
/// point of that tool at all. See docs/design-principles/conformance.md for what the distinction means.
type private Divergence =
    { key: string
      tool: string
      staysFixedPoint: bool
      reason: string
      exampleCase: string }

/// Keyed by `ClassName.MethodName` (`FormattingTest.TestCase`'s format), scoped to one reference tool,
/// because a case documented against `dotnet format whitespace` says nothing about `jb cleanupcode`.
///
/// Read with `JsonDocument` rather than `JsonSerializer.Deserialize<T>` — an F# record has no
/// parameterless or single-parameter constructor `System.Text.Json`'s reflection-based converter can
/// use, and adding `FSharp.SystemTextJson` for one small, stable file is not worth the dependency.
let private loadDivergences tool : Map<string, Divergence> =
    let path = Path.Combine(Paths.Root.FullName, "build", "conformance-divergences.json")
    use doc = JsonDocument.Parse(File.ReadAllText path)
    let str (e: JsonElement) (prop: string) = e.GetProperty(prop).GetString()

    doc.RootElement.GetProperty("divergences").EnumerateArray()
    |> Seq.map (fun e ->
        { key = str e "key"
          tool = str e "tool"
          staysFixedPoint = e.GetProperty("staysFixedPoint").GetBoolean()
          reason = str e "reason"
          exampleCase = str e "exampleCase" })
    |> Seq.filter (fun d -> d.tool = tool)
    |> Seq.map (fun d -> d.exampleCase, d)
    |> Map.ofSeq

/// Proves that every expectation the test suite asserts is a fixed point of dotnet format, and that
/// every case where it is not — or where it is but differs in shape from what dotnet format would have
/// produced on its own — is a documented, deliberate choice rather than a silent gap.
///
/// Until this existed only the corpus proved the fixed-point half, and nothing proved the shape half at
/// all: the hand-written expectations proved only that Curb agrees with itself, so one written from a
/// wrong belief about dotnet format would sit there passing forever — which is exactly how the note
/// about arrow clauses survived several readings.
///
/// Fails if any implemented `.editorconfig` key has no dumped case exercising it — the isolated-test-per-
/// option completeness the option-onboarding playbook in AGENTS.md asks for, checked mechanically rather
/// than trusted. Reads the case list from `.editorconfig` text already on disk rather than requiring each
/// `Formats`/`Unchanged` call site to declare which key it is testing: retrofitting an explicit tag onto
/// several hundred existing calls was rejected as churn for its own sake when the same answer is already
/// sitting in every dumped case's `.editorconfig` file.
let private checkOptionCoverage (dump: string) (cases: string[]) =
    let implemented =
        let result = Proc.Start("dotnet", [| "run"; "--project"; "src/Nullean.Curb.Cli"; "-c"; "Release"; "--"; "options"; "--list-keys" |])
        result.ConsoleOut
        |> Seq.map (fun l -> l.Line.Trim())
        |> Seq.filter (fun l -> l.Length > 0)
        |> Seq.collect (fun l -> l.Split(' '))
        |> Seq.filter (fun s -> s.Length > 0)
        |> Set.ofSeq

    if implemented.IsEmpty then
        failwith "could not read the implemented key list from `curb options --list-keys`"

    // Not the kind of thing a Formats()/Unchanged() case exercises: generated_code is a suppression
    // mechanism and dotnet_diagnostic.ide0055.severity a diagnostic toggle, neither a value a printer
    // helper turns into a Doc. charset is tested at the byte level against real files in
    // Cli/CleanupRunTests.cs and Cli/FormattingRunTests.cs instead — a byte-order mark is not
    // meaningfully representable as a Formats() string comparison the way every other option is.
    //
    // trim_trailing_whitespace = false is a real, open gap, not a scoping decision like the other three:
    // it is bound correctly (see OptionsBindingTests) but has no observable effect anywhere tried,
    // including inside the verbatim-preserved csharp_space_around_declaration_statements = ignore
    // region, where trailing whitespace has nowhere else to come from. DocPrinter.cs's DocKind.Trim case
    // calls _output.TrimTrailingWhitespace() unconditionally, unlike the other three call sites in that
    // file. Excluded here so this check stays green while that gets its own fix, rather than a case
    // that bakes the current, wrong behaviour into a golden expectation — see the comment left next to
    // CoreOptionTests' blank-line cases.
    //
    // csharp_style_expression_bodied_indexers is the same shape of real gap: bound and catalogued as
    // implemented, but IndexerDeclaration (Printers.Members.cs) never calls TryPrintExpressionBody the
    // way PropertyDeclaration and OperatorDeclaration do, so no source indexer is ever converted.
    //
    // csharp_blank_lines_after_using_list used to belong here too (bound into FormatOptions with no
    // dispatch case reaching it) — fixed and given real coverage, see BlankLineOptionTests.
    let excluded =
        set [ "generated_code"; "dotnet_diagnostic.ide0055.severity"; "charset"
              "trim_trailing_whitespace"; "csharp_style_expression_bodied_indexers" ]

    let keyPattern = Text.RegularExpressions.Regex(@"^\s*([a-zA-Z0-9_.]+)\s*=", Text.RegularExpressions.RegexOptions.Multiline)
    let covered =
        cases
        |> Array.collect (fun d ->
            let config = File.ReadAllText(Path.Combine(d, ".editorconfig"))
            keyPattern.Matches(config)
            |> Seq.cast<Text.RegularExpressions.Match>
            |> Seq.map (fun m -> m.Groups[1].Value)
            |> Seq.toArray)
        |> Set.ofArray

    let missing = implemented - excluded - covered |> Set.toArray |> Array.sort

    printfn "%d of %d implemented keys have at least one dumped case (%d excluded, not a testable value)"
        (implemented.Count - excluded.Count - missing.Length) (implemented.Count - excluded.Count) excluded.Count

    if missing.Length > 0 then
        printfn ""
        printfn "implemented but never exercised by a Formats()/Unchanged() case:"
        missing |> Array.iter (fun key -> printfn "  %s" key)
        failwithf "%d implemented key(s) have no isolated test case — see AGENTS.md's option-onboarding playbook" missing.Length

/// Shared by verifyExpectationsJb and verifyExpectationsIde0055: both batch every case an
/// ExpectationDump run produced into ONE synthetic project rather than one project per case (see
/// verifyExpectationsJb's own doc comment for the measured cost/determinism reasons that shape holds
/// for any tool, not just jb), which means every case's declarations have to coexist in the same
/// compilation.
let private caseId (d: string) = Path.GetFileName d
let private caseFile (d: string) = caseId d + ".cs"
let private caseNamespace (d: string) = "Case" + caseId d

// Dozens of cases reuse `namespace N; class Widget` — deliberately minimal, readable boilerplate that
// was never meant to be unique across the whole suite. A namespace rename rather than a class rename:
// the namespace name essentially never reappears in a snippet's body the way a type name might
// (constructors, casts, ...), so it is the smaller, safer edit.
let private namespaceDecl = Text.RegularExpressions.Regex(@"namespace\s+([\w.]+)(\s*;|\s*\r?\n[ \t]*\{)")
let private leadingUsings = Text.RegularExpressions.Regex(@"\A(\s*using[^\n]*\n)*")
let private typeDecl = Text.RegularExpressions.Regex(@"\b(class|struct|interface|enum|record)\s+\w")

// A case with no type declaration at all — an EdgeCaseTests/NamespaceAndUsingTests case testing
// using-directive behaviour in isolation, most of them — has nothing that could collide with another
// case either, so it is left alone rather than given a synthetic namespace. Found by measuring: an
// inserted namespace turned out to change where jb wanted a blank line or how it ordered the
// directives relative to it, a disagreement caused entirely by this harness's own insertion and not by
// anything the case is testing.
//
// A case with no type declaration but real statement content is a top-level-statements file (or a
// file-based program's #: directives plus statements) — excluded from the shared project entirely, not
// just left unrewritten: only one file per compilation may have top-level statements, so a second such
// case would be a compile error regardless of namespace handling.
let private hasTopLevelStatements (source: string) =
    if typeDecl.IsMatch source then
        false
    else
        source.Split('\n')
        |> Array.exists (fun l ->
            let t = l.TrimStart()
            t.Length > 0
            && not (t.StartsWith "using ")
            && not (t.StartsWith "global using")
            && not (t.StartsWith "#:"))

// A new file-scoped namespace rather than a wrapping block one, specifically so nothing already in the
// file needs reindenting under it — a block-scoped wrap would manufacture a fake indent-level
// disagreement of the harness's own making, exactly the kind of false positive both consumers of this
// function exist to avoid introducing.
let private rewriteNamespace (d: string) (source: string) =
    let ns = caseNamespace d
    let m = namespaceDecl.Match source
    if m.Success then
        source.Substring(0, m.Index) + "namespace " + ns + m.Groups[2].Value + source.Substring(m.Index + m.Length)
    elif typeDecl.IsMatch source then
        let lead = leadingUsings.Match(source).Length
        source.Substring(0, lead) + sprintf "namespace %s;\n\n" ns + source.Substring(lead)
    else
        source

// The settings lines out of a case's own dumped .editorconfig ("root = true\n[*.cs]\n<settings>"), with
// every "[...]" header line dropped rather than just the first — some cases pass editorConfig text that
// already opens with its own "[*.cs]" (see AttributeTests' JoinAttributes), which would otherwise
// survive as a bogus second header nested inside this case's section below.
let private caseSettings (d: string) =
    File.ReadAllText(Path.Combine(d, ".editorconfig")).Split('\n')
    |> Array.skip 1
    |> Array.filter (fun l ->
        let t = l.Trim()
        t.Length > 0 && not (t.StartsWith("[")) && t <> "root = true")

/// Slow, and it needs the SDK, so it is its own target rather than part of `test`.
let rec private verifyExpectations (arguments:ParseResults<Arguments>) =
    let dump = Path.Combine(Paths.Output.FullName, "expectations")
    if Directory.Exists dump then Directory.Delete(dump, true)
    Directory.CreateDirectory dump |> ignore

    Environment.SetEnvironmentVariable("CURB_EXPECTATION_DUMP", dump)
    exec "dotnet" ["run"; "--project"; "tests/Nullean.Curb.Tests"; "-c"; "Release"] |> ignore
    Environment.SetEnvironmentVariable("CURB_EXPECTATION_DUMP", null)

    let cases = Directory.GetDirectories dump
    printfn "checking %d expectations against dotnet format" cases.Length
    if cases.Length = 0 then failwith "no expectations were written — is the dump still wired into the harness?"

    checkOptionCoverage dump cases

    // Trailing newlines are compared separately by the harness itself — insert_final_newline is a
    // test subject of its own — so the dump's own trailing newline is not evidence of anything.
    let read (name: string) (d: string) = File.ReadAllText(Path.Combine(d, name)).TrimEnd('\n', '\r')
    let testCaseOf (d: string) =
        let path = Path.Combine(d, "TestCase.txt")
        if File.Exists path then (File.ReadAllText path).Trim() else "?"

    // Source.cs sweeps alongside Expected.cs below — both are ordinary .cs files under `dump` — which is
    // exactly what makes it useful: the post-sweep Source.cs *is* X, dotnet format's own opinion of the
    // raw input, computed for free by the same pass that proves Expected.cs (Z) is a fixed point.
    let beforeExpected = cases |> Array.map (fun d -> d, read "Expected.cs" d) |> Map.ofArray

    exec "dotnet" ["format"; "whitespace"; dump; "--folder"] |> ignore

    let divergences = loadDivergences "dotnet-format-whitespace"

    let results =
        cases
        |> Array.map (fun d ->
            let z = beforeExpected.[d]
            let afterZ = read "Expected.cs" d
            let x = read "Source.cs" d
            {| Directory = d
               TestCase = testCaseOf d
               Z = z
               X = x
               StaysFixedPoint = afterZ = z
               MatchesReference = x = z |})

    // Not a fixed point at all is the mandatory claim failing — rare, serious, and gated on every case
    // being individually documented (see docs/design-principles/conformance.md). A fixed point with a
    // different shape than dotnet format's own is the common, expected case: most of the ReSharper-
    // derived wrapping, blank-line and reflow keys have no dotnet format opinion to agree with at all,
    // so hundreds of cases legitimately differ this way. That is reported in aggregate rather than
    // gated per case — requiring an entry per test method here would drown the notable exceptions this
    // registry exists to surface in paperwork nobody would read.
    let notFixedPoint = results |> Array.filter (fun r -> not r.StaysFixedPoint)
    let differentShape = results |> Array.filter (fun r -> r.StaysFixedPoint && not r.MatchesReference)

    printfn ""
    printfn "%d of %d expectations are an exact fixed point of dotnet format with no shape divergence"
        (cases.Length - notFixedPoint.Length - differentShape.Length) cases.Length
    printfn "%d differ in shape from what dotnet format would have produced on its own, but are still a fixed point"
        differentShape.Length

    gateOnNonFixedPoints "dotnet format whitespace" dump divergences
        (notFixedPoint |> Array.map (fun r -> r.Directory, r.TestCase))

/// Shared by `verifyExpectations` and `verifyCleanupExpectations`: prints every case that is not a fixed
/// point of `toolLabel`, gates on each one being named in the divergence registry, and gates on the
/// registry holding no stale entry that no longer reproduces (which would silently exempt whatever case
/// happens to reuse that name next).
and private gateOnNonFixedPoints (toolLabel: string) (dump: string) (divergences: Map<string, Divergence>) (notFixedPoint: (string * string)[]) =
    if notFixedPoint.Length > 0 then
        printfn ""
        printfn "NOT a fixed point of %s:" toolLabel
        for (directory, case) in notFixedPoint do
            let documented = divergences.ContainsKey case
            printfn "  %s (%s) — %s"
                case (Path.GetRelativePath(dump, directory)) (if documented then "documented" else "UNDOCUMENTED")

    // Every case that is not even a fixed point needs a registry entry naming it, or it fails the build
    // outright — the two-step "reported, then gated once a number exists" pattern churn uses does not
    // apply here: the whole point of this check is that a new one is never accepted silently.
    let undocumented = notFixedPoint |> Array.filter (fun (_, case) -> not (divergences.ContainsKey case))
    if undocumented.Length > 0 then
        printfn ""
        printfn "%d undocumented non-fixed-point(s) against %s. Add an entry to" undocumented.Length toolLabel
        printfn "build/conformance-divergences.json (see docs/design-principles/conformance-divergences.md)"
        printfn "if this is a real, permanent incompatibility, or fix the printer if it is not:"
        undocumented |> Array.iter (fun (_, case) -> printfn "  %s" case)
        failwithf "%d undocumented conformance non-fixed-point(s) against %s" undocumented.Length toolLabel

    // A registry entry that no longer reproduces means the divergence was fixed. Leaving it in would
    // silently exempt whatever test case happens to reuse that name next, so it fails rather than being
    // dropped quietly — remove the entry in the same change that fixes the printer.
    let stale =
        divergences
        |> Map.toArray
        |> Array.filter (fun (case, _) -> not (notFixedPoint |> Array.exists (fun (_, c) -> c = case)))
    if stale.Length > 0 then
        printfn ""
        printfn "stale entries in build/conformance-divergences.json (no longer reproduce, remove them):"
        stale |> Array.iter (fun (case, _) -> printfn "  %s" case)
        failwithf "%d stale conformance divergence entry/ies — remove them or they could mask a real regression" stale.Length

/// Proves that every cleanup-rule case the test suite exercises is a fixed point of `dotnet format
/// style` — the same discipline `verifyExpectations` holds the formatting side to, applied to `curb
/// cleanup`'s semantic rules. `cleanupConformance` measures the same property at corpus scale but only
/// reports it (it also wants to fix sites Curb declined, which is not this check's business); this gates
/// it per case instead, the way `verifyExpectations` gates the formatting side.
///
/// Each dumped case gets its own throwaway project rather than being batched into one: two cleanup cases
/// from different test files both naming `class Widget` in `namespace N` is common (the tests were written
/// to be read in isolation), and batching would make that a compile error rather than the two independent
/// snippets the source intended.
and private verifyCleanupExpectations (arguments:ParseResults<Arguments>) =
    let dump = Path.Combine(Paths.Output.FullName, "cleanup-expectations")
    if Directory.Exists dump then Directory.Delete(dump, true)
    Directory.CreateDirectory dump |> ignore

    Environment.SetEnvironmentVariable("CURB_CLEANUP_EXPECTATION_DUMP", dump)
    exec "dotnet" ["run"; "--project"; "tests/Nullean.Curb.Tests"; "-c"; "Release"] |> ignore
    Environment.SetEnvironmentVariable("CURB_CLEANUP_EXPECTATION_DUMP", null)

    let cases = Directory.GetDirectories dump
    printfn "checking %d cleanup cases against dotnet format style" cases.Length
    if cases.Length = 0 then
        failwith "no cleanup expectations were written — is CleanupExpectationDump still wired into the Clean helpers?"

    let divergences = loadDivergences "dotnet-format-style"

    let results =
        cases
        |> Array.map (fun d ->
            let testCase = (File.ReadAllText(Path.Combine(d, "TestCase.txt"))).Trim()
            let ruleIds =
                (File.ReadAllText(Path.Combine(d, "RuleIds.txt"))).Trim().Split(' ')
                |> Array.filter (fun s -> s.Length > 0)

            // A .globalconfig rather than an ordinary .editorconfig: this directory sits under the repo,
            // which has its own root .editorconfig on the walk, and a global config outranks it without
            // needing to reason about section globs — the same reasoning cleanupConformance documents
            // next to its own globalConfig.
            let severities =
                ruleIds |> Array.map (sprintf "dotnet_diagnostic.%s.severity = warning") |> String.concat "\n"

            // IDE0040 only reports on an interface member at all when the project asks for accessibility
            // everywhere, not just dotnet format's default of for_non_interface_members — a case supplying
            // that diagnostic by hand for an interface member is implicitly asserting that preference, or
            // the compiler could never have reported it. Safe for every other case here regardless: for a
            // non-interface declaration, always and for_non_interface_members require the same thing.
            let preferences = "dotnet_style_require_accessibility_modifiers = always"

            File.WriteAllText(
                Path.Combine(d, "case.globalconfig"),
                sprintf "is_global = true\nglobal_level = 100\n\n%s\n%s\n" severities preferences)

            File.WriteAllText(
                Path.Combine(d, "Directory.Build.targets"),
                [ "<Project>"
                  "  <ItemGroup>"
                  "    <EditorConfigFiles Include=\"$(MSBuildThisFileDirectory)case.globalconfig\" />"
                  "  </ItemGroup>"
                  "</Project>" ]
                |> String.concat "\n")

            File.WriteAllText(
                Path.Combine(d, "Case.csproj"),
                [ "<Project Sdk=\"Microsoft.NET.Sdk\">"
                  "  <PropertyGroup>"
                  "    <TargetFramework>net10.0</TargetFramework>"
                  "    <Nullable>disable</Nullable>"
                  "    <ImplicitUsings>disable</ImplicitUsings>"
                  "  </PropertyGroup>"
                  "</Project>" ]
                |> String.concat "\n")

            // --verify-no-changes reports rather than rewrites, so the exit code alone says whether Case.cs
            // (Z) is already a fixed point — no before/after diff needed, unlike the whitespace side.
            let exitCode =
                execResult "dotnet"
                    (List.concat [
                        ["format"; "style"; d; "--verify-no-changes"; "--severity"; "info"; "--diagnostics"]
                        List.ofArray ruleIds ])

            {| Directory = d; TestCase = testCase; StaysFixedPoint = (exitCode = 0) |})

    let notFixedPoint = results |> Array.filter (fun r -> not r.StaysFixedPoint)

    printfn ""
    printfn "%d of %d cleanup cases are a fixed point of dotnet format style" (cases.Length - notFixedPoint.Length) cases.Length

    gateOnNonFixedPoints "dotnet format style" dump divergences
        (notFixedPoint |> Array.map (fun r -> r.Directory, r.TestCase))

/// Measures — does not yet gate, see the note near the bottom — how many formatting expectations the
/// test suite asserts are a fixed point of `jb cleanupcode` too, the same property `verifyExpectations`
/// gates for `dotnet format whitespace`. This is the one place `jb` can be checked at all: the ReSharper-
/// derived wrapping and blank-line keys have no `dotnet format` opinion to compare against.
///
/// Reuses `ExpectationDump`'s dump (the same `Source.cs` / `Expected.cs` / `.editorconfig` triples, run
/// through its own dump rather than sharing `verifyExpectations`'s, so the two targets do not have to run
/// in a fixed order) rather than dumping separately for jb — the cases are the same, only the reference
/// tool differs.
///
/// One project holding every case as its own file, not one project per case: an earlier version gave
/// each case its own project batched into one solution, on the theory that jb's cost is per invocation
/// rather than per project. Measured wrong twice over. Speed: going from 1 to 5 projects in one
/// invocation cost nothing extra (~8s either way), but the full 838-project run took ~5 minutes, so jb
/// also pays real per-project MSBuild evaluation overhead — one project with 842 files instead runs in
/// ~30s, a ~10x improvement. Determinism: that same 838-project shape also measured non-deterministic
/// (330/377/404 disagreements across three identical runs); two runs of this one-project shape landed
/// on the exact same 307-case set, name for name, not just the same count. Many separate project/
/// compilation contexts, not jb's cleanup logic itself, was almost certainly the source of both problems.
and private verifyExpectationsJb (arguments:ParseResults<Arguments>) =
    let dump = Path.Combine(Paths.Output.FullName, "expectations-jb")
    if Directory.Exists dump then Directory.Delete(dump, true)
    Directory.CreateDirectory dump |> ignore

    Environment.SetEnvironmentVariable("CURB_EXPECTATION_DUMP", dump)
    exec "dotnet" ["run"; "--project"; "tests/Nullean.Curb.Tests"; "-c"; "Release"] |> ignore
    Environment.SetEnvironmentVariable("CURB_EXPECTATION_DUMP", null)

    let cases = Directory.GetDirectories dump
    printfn "checking %d expectations against jb cleanupcode" cases.Length
    if cases.Length = 0 then failwith "no expectations were written — is the dump still wired into the harness?"

    let read (name: string) (d: string) = File.ReadAllText(Path.Combine(d, name)).TrimEnd('\n', '\r')

    // caseId/caseFile/caseNamespace, hasTopLevelStatements, rewriteNamespace and caseSettings are
    // shared with verifyExpectationsIde0055 below, which batches the same dump the same way for a
    // different reference tool — see their definitions just below loadDivergences for the shared
    // reasoning.
    let excluded, cases = cases |> Array.partition (fun d -> hasTopLevelStatements (read "Expected.cs" d))
    if excluded.Length > 0 then
        printfn "excluding %d top-level-statement case(s) from the shared project (not checked against jb):" excluded.Length
        for d in excluded do
            let path = Path.Combine(d, "TestCase.txt")
            printfn "  %s" (if File.Exists path then (File.ReadAllText path).Trim() else caseId d)

    let project = Path.Combine(dump, "project")
    Directory.CreateDirectory project |> ignore

    let before =
        cases
        |> Array.map (fun d ->
            let rewritten = (rewriteNamespace d (read "Expected.cs" d)).TrimEnd('\n', '\r')
            File.WriteAllText(Path.Combine(project, caseFile d), rewritten + "\n")
            d, rewritten)
        |> Map.ofArray

    // A block-scoped namespace, similarly. csharp_style_namespace_declarations defaults to "(as
    // written)" in Curb — it converts nothing unless asked — but jb's own default prefers file-scoped
    // and converts a block-scoped namespace on sight, even with nothing else about the case in play
    // (found in NamespaceAndUsingTests' block-namespace cases). Conditional on before.[d] actually
    // having a block-scoped namespace for the same reason as the empty-block key: a case whose
    // namespace was already file-scoped needs no help agreeing, and forcing block_scoped on it would
    // manufacture a new disagreement in the other direction.
    let blockScopedNamespace = Text.RegularExpressions.Regex(@"namespace\s+[\w.]+\s*\r?\n[ \t]*\{")

    // Distinguishes empty-block-style's two collapsed spellings: "together" keeps the pair on its own
    // line (`)\n{ }`), "together_same_line" joins it to whatever precedes it (`Empty() { }`) — a single
    // non-whitespace character with at most one space between it and `{` means the same line; anything
    // else (a newline in between) means its own line.
    let emptyBlockJoinsPrecedingLine = Text.RegularExpressions.Regex(@"[^\s\r\n]\s?\{\s?\}")

    // The three shapes an accessor's body can take, used together to decide whether it is safe to tell
    // jb to leave a genuinely-multi-line accessor block alone (see accessorExpressionBodyDirection
    // below). `get`/`set`/`init` is unambiguous here — none of the three is a common identifier, and a
    // real one appearing as a local name would need to be followed by one of these exact punctuation
    // shapes to match at all.
    let accessorBlockMultiline = Text.RegularExpressions.Regex(@"\b(get|set|init)\s*\r?\n\s*\{")
    let accessorBlockSingleLine = Text.RegularExpressions.Regex(@"\b(get|set|init)\s*\{[^\r\n{}]*\}")
    let accessorArrow = Text.RegularExpressions.Regex(@"\b(get|set|init)\s*=>")

    // Any non-empty single-line brace block anywhere in the case — not just on an accessor. Used as a
    // whole-file guard on accessorExpressionBodyDirection: a case that preserves some OTHER construct on
    // one line (PreserveSingleLineTests' whole point) is exactly the shape that made an earlier, narrower
    // version of this fix regress `PreserveSingleLineTests.A_one_line_body_with_a_statement_stays_there`
    // — telling jb `csharp_preserve_single_line_blocks = false` to stop it collapsing a genuinely
    // multi-line accessor also stops it honouring a block Curb deliberately kept on one line elsewhere,
    // and jb expanded that one instead. Safer to skip the whole case when any inline block exists at all
    // than to try to scope the keys to just the accessor construct — Curb's own preserve options are not
    // scoped that finely either, so there is no key that would let jb agree in one place but not another.
    let anyInlineBlock = Text.RegularExpressions.Regex(@"\{[^\r\n{}]+\}")

    // One level of nested parens, balanced — `is Point(1, 2)` inside an if-condition, or a lambda
    // parameter list inside a call, both appear in these test snippets and neither is rare enough to
    // ignore. A plain `[^)]*` stops at the first `)`, which is the nested one, not the real close —
    // found by a false negative it caused in bracelessControlFlowBody below.
    let balancedParens = @"\((?:[^()]|\([^()]*\))*\)"

    // A parameter list `(...)` directly before `=>`, at the start of a line (optionally after
    // modifiers/a return type — CallerMemberName-style regexes cannot resolve a type name, so this
    // accepts any run of words, plus at most one parenthesized group for a tuple return type like
    // `public (int First, int Second) M() => (1, 2);`), is what a method, constructor, operator or
    // local function's expression body looks like — the only four of Curb's seven
    // csharp_style_expression_bodied_* constructs that have a parameter list at all, which is what
    // makes this regex specific to them: an accessor's arrow follows a bare `get`/`set`/`init` (no
    // parens), a property's follows the property name alone, and an indexer's follows `]` — none can
    // match `)\s*=>`. Anchored to the start of a line (RegexOptions.Multiline) rather than matching
    // `)\s*=>` anywhere, so a lambda argument nested inside a call on a block-bodied method's only
    // statement — `Call((int a, int b) => a + b);` — does not falsely read as the method's own
    // expression body: balancedParens can only match "Call"'s own parenthesis by consuming everything
    // through its final closing paren as one unit (there is no valid shorter stopping point once the
    // nested lambda's own parens forced a nested match), which leaves nothing for the required `=>` to
    // follow — so this line can never satisfy the pattern.
    //
    // The word-token run is separated by a *required* space (`[ \t]+`, not `[ \t]*`), and the tuple-
    // return-type group may occur at most once rather than inside a repeated alternation with the word
    // tokens — both are what keep this linear. An earlier version allowed `\w+` and a parenthesized
    // group to repeat interchangeably with only optional spacing between them, the classic
    // `(\w+)*`-style catastrophic-backtracking shape: on a long non-matching line (any ordinary method
    // body with no arrow at all) the engine explored exponentially many ways to partition the same run
    // of word characters before concluding there was no match — observed hanging the harness at 100%
    // CPU for minutes on the real dump. Measured this version against a 300+ character deliberately
    // non-matching stress input at 0ms before trusting it.
    let parameterizedExpressionBody =
        Text.RegularExpressions.Regex(
            @"^[ \t]*(?:\w+[ \t]+)*(?:" + balancedParens + @"[ \t]+(?:\w+[ \t]+)*)?\w+[ \t]*" + balancedParens + @"[ \t]*=>[ \t]*[^{\r\n]",
            Text.RegularExpressions.RegexOptions.Multiline)

    // A comma directly before a closing brace/bracket/paren on the next line — the shape both trailing-
    // comma keys default to false (Curb only ever adds one; an already-present one, like an empty
    // block's braces, survives because Curb does not remove what it did not add).
    let trailingCommaBeforeClose = Text.RegularExpressions.Regex(@",\s*\r?\n\s*[\}\]\)]")

    // A control-flow keyword whose body is not immediately a `{` — an unbraced single statement, which
    // is exactly the shape PreferBracesTests' own default cases assert Curb leaves alone. Distinguishes
    // "this case has braces jb would strip" (safe to tell it to require them) from "this case is
    // specifically testing that Curb does not add braces" (telling jb to require them would be wrong in
    // the other direction — found the hard way, on PreferBracesTests.Bodies_are_left_as_written_without_the_key
    // itself, after making the injection unconditional turned out to be too strong a claim).
    let bracelessControlFlowBody =
        Text.RegularExpressions.Regex(
            @"\b(if\s*" + balancedParens + @"|else|for\s*" + balancedParens + @"|foreach\s*" + balancedParens + @"|while\s*" + balancedParens + @"|do)\s*\r?\n?\s*[^\s{]")

    let caseSection (d: string) =
        let settings = caseSettings d
        let z = before.[d]

        // Curb collapses an empty block to `{ }` under deterministic/reflow layout unconditionally — it
        // is not governed by csharp_empty_block_style, which stays a no-op until a repository sets it
        // (see OptionCatalog's remarks). jb has no unconditional default of its own: it only collapses
        // an empty block when csharp_empty_block_style names together/together_same_line explicitly,
        // even with csharp_preserve_single_line_blocks = true set (measured directly).
        //
        // Only added when Z actually contains a collapsed `{ }` or `{}` — not for every case that
        // merely fails to mention the key. An earlier version added it unconditionally, on the theory
        // that it is harmless when nothing empty is present; measured wrong, on a case with no width
        // and no deterministic layout at all: Curb correctly leaves an already-multi-line empty block
        // alone there (a preservation-mode Unchanged case), and the blanket key told jb to collapse it
        // anyway — a disagreement caused by this harness asking for something Curb never chose.
        let emptyBlock =
            if (z.Contains "{ }" || z.Contains "{}")
               && not (settings |> Array.exists (fun l -> l.Contains "csharp_empty_block_style"))
            then
                let spelling = if emptyBlockJoinsPrecedingLine.IsMatch z then "together_same_line" else "together"
                [| sprintf "csharp_empty_block_style = %s" spelling |]
            else [||]

        let blockNamespace =
            if blockScopedNamespace.IsMatch z
               && not (settings |> Array.exists (fun l -> l.Contains "csharp_style_namespace_declarations"))
            then [| "csharp_style_namespace_declarations = block_scoped" |] else [||]

        // Only covers block_scoped -> file_scoped by construction: the case above only fires when Z is
        // already block-scoped. The reverse (file_scoped source, block_scoped requested) is a genuine,
        // confirmed Curb gap, not something this harness can inject its way around — Printers.cs's
        // FileScopedNamespace never checks context.Options.NamespaceStyle at all, so a repository asking
        // for block_scoped gets no conversion when the source is already file-scoped. IDE0161's other
        // direction (IDE0160) has no printer implementation. NamespaceStyleTests.Block_scoped_is_accepted_
        // and_changes_nothing documents this deliberately, by name, so it stays visible rather than
        // reading as an oversight; that case is expected to remain in the "not a fixed point" list below
        // until the reverse conversion is implemented — it is not a candidate for an injected key at all.

        // Curb only ever adds braces around an unbraced control-flow body, never removes existing ones
        // — PreferBracesTests documents why: Roslyn would take them off, but a declaration inside the
        // block stops being scoped to it, a change in meaning rather than layout, so Curb refuses even
        // with csharp_prefer_braces = false. jb defaults to stripping braces around a single embedded
        // statement, aggressively — it did so through four levels of nested if-statements in one case.
        //
        // Conditional, not unconditional: an earlier version reasoned this was always safe, since Curb
        // never removes a brace either way — true, but incomplete. Curb's default also does not add a
        // missing one, and PreferBracesTests' own cases assert exactly that default; forcing `true`
        // unconditionally told jb to add braces to those cases' deliberately braceless bodies, a new
        // disagreement in the other direction. Only injected when Z has no braceless control-flow body
        // of its own to protect.
        let preferBraces =
            if bracelessControlFlowBody.IsMatch z
               || settings |> Array.exists (fun l -> l.Contains "csharp_prefer_braces")
            then [||] else [| "csharp_prefer_braces = true" |]

        // jb defaults to collapsing an already-multi-line accessor, property or indexer body — both
        // toward an expression body (`get => _x;`) and, once that is suppressed, toward a single-line
        // block (`get { return _x; }`) instead — the opposite direction from methods/constructors/
        // operators/local_functions above, confirmed by testing each key in isolation:
        // csharp_style_expression_bodied_{accessors,properties,indexers} = false alone only stops the
        // arrow, not the single-line collapse; csharp_preserve_single_line_{blocks,statements} = false
        // is what stops that second step. Both are needed together.
        //
        // Only injected when Z's own accessor shape is unambiguously multi-line — no accessor already
        // collapsed to a single line or an arrow anywhere in the case (accessorBlockSingleLine,
        // accessorArrow), and no OTHER construct in the file relies on single-line preservation either
        // (anyInlineBlock) — seeing preferBraces's mistake, keeping this scoped to when nothing in Z
        // could be broken by turning both preserve options off case-wide.
        let accessorExpressionBodyDirection =
            if accessorBlockMultiline.IsMatch z
               && not (accessorBlockSingleLine.IsMatch z)
               && not (accessorArrow.IsMatch z)
               && not (anyInlineBlock.IsMatch z)
               && not (settings |> Array.exists (fun l ->
                    l.Contains "csharp_style_expression_bodied_accessors"
                    || l.Contains "csharp_style_expression_bodied_properties"
                    || l.Contains "csharp_style_expression_bodied_indexers"
                    || l.Contains "csharp_preserve_single_line_blocks"
                    || l.Contains "csharp_preserve_single_line_statements"))
            then
                [| "csharp_style_expression_bodied_accessors = false"
                   "csharp_style_expression_bodied_properties = false"
                   "csharp_style_expression_bodied_indexers = false"
                   "csharp_preserve_single_line_blocks = false"
                   "csharp_preserve_single_line_statements = false" |]
            else [||]

        // Not a Curb-implemented key at all — Curb has no option that ever puts a space between two
        // attribute sections it glues together (AttributeTests.A_space_between_sections_on_a_parameter_is_removed
        // confirms it always normalises one away), so there is no case where telling jb to match that
        // could conflict with an alternate Curb behaviour. jb defaults to spacing them apart.
        let attributeSectionSpacing =
            if settings |> Array.exists (fun l -> l.Contains "csharp_space_between_attribute_sections")
            then [||] else [| "csharp_space_between_attribute_sections = false" |]

        // Curb always aligns a broken query expression's clauses under `from` (Printers.Query.cs's
        // QueryAnchor) — unconditional, not gated by any option, so telling jb to match is always safe.
        // jb's default is a flat continuation indent instead, ignoring the alignment entirely; found via
        // the user pointing at ReSharper's own editorconfig schema page for this rather than guessing —
        // `resharper_csharp_align_linq_query` is the ReSharper-native key (not a plain `csharp_` one,
        // unlike most of the keys here) and setting it to `true` reproduces Curb's shape exactly.
        let alignLinqQuery =
            if settings |> Array.exists (fun l -> l.Contains "resharper_csharp_align_linq_query")
            then [||] else [| "resharper_csharp_align_linq_query = true" |]

        // dotnet_style_require_accessibility_modifiers is dotnet_style_*, one of the "not formatting —
        // dotnet format style's territory" keys OptionCatalog.IsOtherCodeStyleKey names: curb format
        // never adds or removes an accessibility modifier, that is IDE0040 and curb cleanup's job. jb
        // defaults to adding an implicit `private` to every member missing one — measured across a
        // large share of cases, since almost none of them bother writing it on a test-only method.
        // never (confirmed not to touch a modifier a case already wrote explicitly, only ones it would
        // otherwise add) says the same thing curb format already means by never touching this at all,
        // so it is unconditional like csharp_prefer_braces — nothing to detect per case.
        let requireAccessibility =
            if settings |> Array.exists (fun l -> l.Contains "dotnet_style_require_accessibility_modifiers")
            then [||] else [| "dotnet_style_require_accessibility_modifiers = never" |]

        // NOT safe unconditionally, unlike requireAccessibility above — tried and measured wrong.
        // csharp_style_var_* is dotnet format style's territory (curb format never rewrites a declared
        // type to `var` or back), and jb's default profile does apply "prefer var" regardless of a bare
        // `.editorconfig` (confirmed: `int x = 1;` came back `var x = 1;` in isolation). But most of the
        // corpus's own fixtures already use `var` idiomatically, so forcing `csharp_style_var_* = false`
        // everywhere told jb to convert THOSE back to explicit types instead — a much larger regression
        // (635 -> 589 of 820) than the handful of explicit-type cases it was meant to protect. Reverted.
        // A real fix here needs to be conditional on Z's own declarations the way preferBraces is on Z's
        // own braces, not a blanket default — not done this pass.

        // jb defaults to expanding an already-expression-bodied method, constructor, operator or local
        // function back into a block — the opposite direction from accessors/properties/indexers below,
        // which is exactly why this is its own detection rather than sharing one with them. Curb's own
        // default is "as written" for all seven csharp_style_expression_bodied_* keys (see
        // ExpressionBodyTests.Bodies_are_left_as_written_without_the_key), so telling jb to prefer
        // keeping what parameterizedExpressionBody already found present is what "as written" means for
        // it too.
        let parameterizedExpressionBodyKeys =
            if parameterizedExpressionBody.IsMatch z then
                [ "methods"; "constructors"; "operators"; "local_functions" ]
                |> List.filter (fun kind -> not (settings |> Array.exists (fun l -> l.Contains (sprintf "csharp_style_expression_bodied_%s" kind))))
                |> List.map (sprintf "csharp_style_expression_bodied_%s = true")
                |> List.toArray
            else [||]

        // Both trailing-comma keys default to false — Curb only ever adds a trailing comma when asked,
        // never removes an existing one — so an already-comma'd list only needs jb told to leave it
        // alone when Z actually has one, the same conditional shape as the empty-block and namespace
        // keys above. jb removed it from every enum, switch-expression-arm list and object initializer
        // sampled, regardless of whether the list was single-line or broken across several.
        let trailingComma =
            if trailingCommaBeforeClose.IsMatch z then
                [ "multiline_lists"; "singleline_lists" ]
                |> List.filter (fun kind -> not (settings |> Array.exists (fun l -> l.Contains (sprintf "csharp_trailing_comma_in_%s" kind))))
                |> List.map (sprintf "csharp_trailing_comma_in_%s = true")
                |> List.toArray
            else [||]

        let extra =
            Array.concat
                [ emptyBlock; blockNamespace; preferBraces; requireAccessibility
                  parameterizedExpressionBodyKeys; trailingComma; attributeSectionSpacing; alignLinqQuery
                  accessorExpressionBodyDirection ]
        sprintf "[%s]\n%s" (caseFile d) (Array.append settings extra |> String.concat "\n")

    File.WriteAllText(
        Path.Combine(project, ".editorconfig"),
        "root = true\n\n" + (cases |> Array.map caseSection |> String.concat "\n\n") + "\n")

    File.WriteAllText(
        Path.Combine(project, "Cases.csproj"),
        [ "<Project Sdk=\"Microsoft.NET.Sdk\">"
          "  <PropertyGroup>"
          "    <TargetFramework>net10.0</TargetFramework>"
          "  </PropertyGroup>"
          "</Project>" ]
        |> String.concat "\n")

    // An ordinary project, just linked into a throwaway solution the same way any project would be —
    // nothing jb-specific about the .slnx itself, unlike the per-case .globalconfig trick the dotnet
    // format style side needs for MSBuild diagnostic severities.
    let sln = Path.Combine(dump, "Cases.slnx")
    File.WriteAllText(sln, "<Solution>\n  <Project Path=\"project/Cases.csproj\" />\n</Solution>\n")

    printfn "running jb cleanupcode over %d case(s) in one project" cases.Length
    // The default profile is "Built-in: Full Cleanup", which does far more than reformat — it removes
    // code it considers redundant, including an unused goto label, so an ordinary formatting case can
    // come back with content deleted rather than reordered. Measured: that alone was responsible for
    // most of a 43% disagreement rate on the first run of this target. "Reformat & Apply Syntax Style"
    // is jb's own narrower profile for exactly the formatting/layout concern this target checks.
    //
    // --caches-home pointed at this run's own dump directory rather than jb's default (shared, outside
    // this tree): reusing the default cache across repeated runs against a directory that gets deleted
    // and recreated each time logged "Concurrent modification?" warnings — a fresh, disposable cache
    // avoids reasoning about jb's staleness rules at all.
    let cachesHome = Path.Combine(dump, ".jb-caches")
    exec "dotnet"
        [ "jb"; "cleanupcode"; sln; "--no-build"
          "--profile=Built-in: Reformat & Apply Syntax Style"
          sprintf "--caches-home=%s" cachesHome
          "--verbosity=WARN" ]

    let divergences = loadDivergences "jb-cleanupcode"

    let results =
        cases
        |> Array.map (fun d ->
            let after = File.ReadAllText(Path.Combine(project, caseFile d)).TrimEnd('\n', '\r')
            {| Directory = d; StaysFixedPoint = after = before.[d] |})
    let testCaseOf (d: string) =
        let path = Path.Combine(d, "TestCase.txt")
        if File.Exists path then (File.ReadAllText path).Trim() else "?"

    let notFixedPoint =
        results
        |> Array.filter (fun r -> not r.StaysFixedPoint)
        |> Array.map (fun r -> r.Directory, testCaseOf r.Directory)

    printfn ""
    printfn "%d of %d expectations are a fixed point of jb cleanupcode" (cases.Length - notFixedPoint.Length) cases.Length

    // Still reported rather than gated with gateOnNonFixedPoints, but for a different reason now: the
    // non-determinism this single-project rewrite set out to test is fixed — repeat runs against the
    // same dump land on the same case set, name for name, not just the same count (the old
    // one-project-per-case shape moved between 330, 377 and 404 with no code change). What blocks gating
    // now is scale, and it is shrinking by category rather than by case: started at 307 of 842 (the
    // count the determinism check above was measured against), now ~235 after five categorised root
    // causes were found and fixed by injecting a key into every case whose shape needs it — the same
    // move whitespace's own X != Z check made, categorising causes rather than triaging cases one at a
    // time. Landed so far: csharp_empty_block_style (both spellings — jb only collapses an empty block
    // when told to, and needs together vs together_same_line to match which line it is on),
    // csharp_style_namespace_declarations (jb defaults to file-scoped, converting a block-scoped
    // namespace on sight), csharp_prefer_braces (jb strips braces around a single embedded statement by
    // default; safe to force on unconditionally since Curb never removes an existing brace either — see
    // PreferBracesTests), and dotnet_style_require_accessibility_modifiers (jb adds an implicit
    // `private` to bare members by default; curb format never touches this dimension at all, so `never`
    // is unconditionally safe too — see OptionCatalog.IsOtherCodeStyleKey). What is left splits into
    // several more real, distinct categories rather than one: expression-body direction disagrees per
    // construct (jb expands an already-expression-bodied method back to a block, but collapses an
    // already-block accessor/indexer to an expression body — the opposite direction, needing per-case
    // shape detection across seven csharp_style_expression_bodied_* keys the way the two keys above
    // needed it), trailing-comma removal before a closing brace, redundant-parentheses-around-operators
    // and qualified-name-shortening (both semantic style preferences Curb never applies), query-clause
    // continuation indentation, and chain/binary-operator continuation position. None of these were
    // chased down this pass — each is its own investigation the size of the four above.
    if notFixedPoint.Length > 0 then
        printfn ""
        printfn "NOT a fixed point of jb cleanupcode (reported only — see the comment on this target for why):"
        for (directory, case) in notFixedPoint |> Array.sortBy snd |> Array.truncate 20 do
            let documented = divergences.ContainsKey case
            printfn "  %s (%s) — %s"
                case (Path.GetRelativePath(dump, directory)) (if documented then "documented" else "undocumented")
        if notFixedPoint.Length > 20 then
            printfn "  ... and %d more" (notFixedPoint.Length - 20)

/// Proves that every expectation the test suite asserts — Expected.cs, what Curb actually ships — is
/// free of IDE0055 under a real, analyzer-driven `dotnet build`, not just a fixed point of `dotnet
/// format whitespace`'s plain rewrite pass (verifyExpectations above). The two are not the same claim:
/// IDE0055's CSharpIndentBlockFormattingRule resolves indentation from full block context, which the
/// whitespace rewriter does not exercise the same way. Issue #77 was exactly this gap — a fixed point
/// of `dotnet format whitespace` (verifyExpectations's own StaysFixedPoint = true) that still failed a
/// real `dotnet build -p:EnforceCodeStyleInBuild=true` outright, with no `.editorconfig` escape hatch.
/// That is what a curb-formatted file failing a code-style-enforced CI build elsewhere actually looks
/// like, and verifyExpectations structurally cannot catch it — this closes that gap.
///
/// One project per case, unlike verifyExpectationsJb's shared batch above — deliberately not reusing
/// that shape, and not for the reason verifyCleanupExpectations already gives for its own per-case
/// projects (two cases both naming `class Widget` colliding). A real, analyzer-driven `dotnet build`
/// turned out to have a much sharper failure mode than a plain compile error: a SEVERE diagnostic
/// anywhere in a shared compilation — not just an unresolved symbol, but a wide, apparently open-ended
/// set of them (multiple base classes, an interface member left unimplemented, an unsafe block without
/// AllowUnsafeBlocks, a name colliding with its own enclosing type used as an attribute, and more found
/// by bisection than were worth enumerating) — silently drops ALL diagnostic reporting for every OTHER
/// file in the same build, not just IDE0055 and not just the offending file's own. Confirmed
/// concretely: a known-bad case (this session's own issue #77 shape) reported its 4 real IDE0055 sites
/// correctly alone, and again paired with one unrelated broken case — but batched with the other ~875,
/// reported nothing, with no build-level error to explain why. Stubbing fixed the unresolved-symbol
/// version of this (a uniform `class X : System.Attribute` per fictitious name resolves both a bare
/// type usage and an attribute usage without ambiguity) and a second stub shape fixed the
/// resolves-to-the-wrong-kind version (`CAttribute` alongside a real, non-Attribute `C`) — but a single
/// bisection pass over the full dump surfaced 37 MORE independently-poisoning cases across a dozen
/// further diagnostic codes, with no reason to believe that was the ceiling. A formatter's own test
/// suite exists specifically to exercise unusual, borderline and outright invalid C# shapes — this
/// failure mode is not a short, enumerable tail to patch around, it is close to the suite's whole
/// purpose. Per-case isolation sidesteps the entire category by construction: no case's diagnostics can
/// ever be silenced by another case's content, because no two cases are ever compiled together.
///
/// Slower for it — measured ~0.6s per case even with a warm build server, ~9 minutes for the full dump
/// run sequentially — so this runs with bounded parallelism (Parallel.ForEach, capped at
/// Environment.ProcessorCount; Array.Parallel.map's own default degree was enough concurrent `dotnet
/// build` processes to make ProcNet's stdout/stderr readers time out outright) rather than
/// Array.Parallel.map's uncapped default, landing at 5-8 minutes depending on the machine. No stubbing
/// needed either: alone, a case's own fictitious types/attributes are ordinary CS0103/CS0246 body-level
/// noise the same way an undefined local already is, confirmed directly earlier — they do not stop that
/// SAME case's own IDE0055 sites from reporting when nothing else is in the compilation to poison.
///
/// `-p:UseSharedCompilation=false` earns its own line in every one of these builds — not a speed tweak,
/// a correctness fix found the hard way. With the shared VBCSCompiler server (the default), which cases
/// reported IDE0055 genuinely varied between otherwise-identical runs against the same, already-
/// complete divergence registry — confirmed by narrowing it down to registry churn first (an early false
/// "it's stable" reading, from two runs that happened to agree) and then reproducing the flip by toggling
/// only this flag with nothing else changed. Hundreds of concurrent `dotnet build` processes sharing one
/// compiler server was apparently enough to make its own diagnostic reporting unreliable under this much
/// load; a fresh compiler process per case is not.
and private verifyExpectationsIde0055 (arguments:ParseResults<Arguments>) =
    let dump = Path.Combine(Paths.Output.FullName, "expectations-ide0055")
    if Directory.Exists dump then Directory.Delete(dump, true)
    Directory.CreateDirectory dump |> ignore

    Environment.SetEnvironmentVariable("CURB_EXPECTATION_DUMP", dump)
    exec "dotnet" ["run"; "--project"; "tests/Nullean.Curb.Tests"; "-c"; "Release"] |> ignore
    Environment.SetEnvironmentVariable("CURB_EXPECTATION_DUMP", null)

    let cases = Directory.GetDirectories dump
    printfn "checking %d expectations against a real dotnet build (IDE0055), one project per case" cases.Length
    if cases.Length = 0 then failwith "no expectations were written — is the dump still wired into the harness?"

    let read (name: string) (d: string) = File.ReadAllText(Path.Combine(d, name)).TrimEnd('\n', '\r')
    let testCaseOf (d: string) =
        let path = Path.Combine(d, "TestCase.txt")
        if File.Exists path then (File.ReadAllText path).Trim() else "?"

    let project = Path.Combine(dump, "project")
    Directory.CreateDirectory project |> ignore

    let ide0055Pattern = Text.RegularExpressions.Regex(@"\): (?:warning|error) IDE0055")

    let buildOne (d: string) =
        let caseDir = Path.Combine(project, caseId d)
        Directory.CreateDirectory caseDir |> ignore

        File.WriteAllText(Path.Combine(caseDir, "Case.cs"), read "Expected.cs" d + "\n")

        // Same shape ExpectationDump already writes ("root = true\n[*.cs]\n<settings>"), with the gate
        // itself added ahead of the case's own settings.
        File.WriteAllText(
            Path.Combine(caseDir, ".editorconfig"),
            sprintf "root = true\n[*.cs]\ndotnet_diagnostic.IDE0055.severity = error\n%s\n" (caseSettings d |> String.concat "\n"))

        File.WriteAllText(
            Path.Combine(caseDir, "Case.csproj"),
            [ "<Project Sdk=\"Microsoft.NET.Sdk\">"
              "  <PropertyGroup>"
              "    <TargetFramework>net10.0</TargetFramework>"
              "    <Nullable>disable</Nullable>"
              // Resolves a case's bare BCL references (Console, Exception, Task, ...) for free, so a
              // case using one is not ALSO reported as a fictitious name — though since this target
              // does not stub fictitious names at all any more, it mostly just keeps a case's own real
              // errors closer to what a real consuming project would actually see.
              "    <ImplicitUsings>enable</ImplicitUsings>"
              "    <EnforceCodeStyleInBuild>true</EnforceCodeStyleInBuild>"
              "  </PropertyGroup>"
              "</Project>" ]
            |> String.concat "\n")

        // Not gated on the exit code — most cases do not compile cleanly and are not meant to (see the
        // doc comment above); IDE0055 is read straight off the console output regardless of whether the
        // rest of the case's own content resolves.
        //
        // -maxcpucount:1: each of these is a one-file project, with nothing for MSBuild's own internal
        // multi-node build to parallelise — left at its default, hundreds of these run concurrently
        // (below) each also fanning out its own worker nodes, and ProcNet's stdout/stderr readers
        // started timing out under the combined load. One node per case, many cases at once instead.
        let result =
            Proc.Start("dotnet",
                [| "build"; caseDir; "-tl:off"; "--nologo"; "-maxcpucount:1"
                   "-p:EnforceCodeStyleInBuild=true"
                   "-p:RunAnalyzersDuringBuild=true"
                   "-p:TreatWarningsAsErrors=false"
                   // Required, not just a speed tweak — see the doc comment above. With the shared
                   // VBCSCompiler server (the default), which cases report IDE0055 genuinely varied
                   // between otherwise-identical runs under this much concurrent build load; a fresh
                   // compiler process per case does not.
                   "-p:UseSharedCompilation=false" |])
        let output = result.ConsoleOut |> Seq.map (fun l -> l.Line) |> String.concat "\n"
        d, ide0055Pattern.IsMatch output

    printfn "building %d isolated case project(s)..." cases.Length
    // A bounded degree rather than Array.Parallel.map's default (~2x processor count): that default was
    // enough concurrently-running `dotnet build` processes to starve ProcNet's own stdout/stderr reader
    // threads, which started timing out outright rather than just running slowly. Matches processor
    // count instead — measured directly (10 cases, 8-way: ~2.1s; sequential: ~6.1s) as the point past
    // which more concurrency stopped buying anything on this machine.
    let results = System.Collections.Concurrent.ConcurrentBag<string * bool>()
    System.Threading.Tasks.Parallel.ForEach(
        cases,
        System.Threading.Tasks.ParallelOptions(MaxDegreeOfParallelism = Environment.ProcessorCount),
        fun d -> results.Add(buildOne d))
    |> ignore
    let results = results.ToArray()

    let divergences = loadDivergences "ide0055"
    let notFixedPoint =
        results
        |> Array.filter snd
        |> Array.map (fun (d, _) -> d, testCaseOf d)

    printfn ""
    printfn "%d of %d expectations report no IDE0055 under a real dotnet build" (cases.Length - notFixedPoint.Length) cases.Length

    gateOnNonFixedPoints "dotnet build (IDE0055)" dump divergences notFixedPoint

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
    exec "dotnet" ["pack"; "src/Nullean.Curb.Cli/Nullean.Curb.Cli.csproj"; "-c"; "Release"; "-o"; Paths.RootRelative staging] |> ignore

    let ridSuffixes = Paths.AotRuntimeIdentifiers |> List.map (sprintf "curb-cli.%s.")
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
    // DotnetToolSettings.xml, the per-RID packages hold native binaries, and Nullean.Curb.MSBuild is
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

/// Tags for the container image, mirroring the versioning currentVersion already derives from git:
/// "edge" always (so `ghcr.io/nullean/curb:edge` is always the latest main build), plus "latest" and
/// the plain semver when this is an exact release tag rather than a canary commit — MinVer's canary
/// suffix always contains a hyphen, a clean tag never does.
let private containerImageTags =
    lazy(
        let version = currentVersion.Value
        if version.Contains("-") then "edge" else sprintf "edge;latest;%s" version
    )

/// Publishes the CLI's native-AOT build as a container image via the .NET SDK's own container
/// support (`dotnet publish -t:PublishContainer`) — the same mechanism elastic/docs-builder uses for
/// its own image, not a hand-written Dockerfile. linux-x64 only for now; a second RID becomes a
/// second manifest-list platform later with no change to action.yml.
///
/// Base image is the chiseled/distroless runtime-deps image: no shell, minimal surface, and correct
/// for an AOT binary specifically because there is no managed runtime to host — a plain `runtime`
/// image would carry a CLR this binary never uses.
///
/// --push is an explicit flag, not inferred from a CI/event-name environment variable: the aot-pack
/// job's linux-x64 leg calls this on every trigger (PR, push, tag) purely to prove the container
/// build itself still works, with no ghcr.io credentials configured there, and an env-based "is this
/// a push?" check would have tried (and failed) to push from that job on every non-PR trigger. Only
/// the build job, which does log in, passes --push.
let private publishContainers (arguments:ParseResults<Arguments>) =
    let baseImageTag = "10.0-noble-chiseled"
    let registryArgs =
        if arguments.Contains Push then ["-p"; "ContainerRegistry=ghcr.io"] else []
    let args =
        ["publish"; "src/Nullean.Curb.Cli"; "-c"; "Release"; "-r"; "linux-x64"]
        @ ["/t:PublishContainer"
           "-p"; "DebugType=none"
           "-p"; sprintf "ContainerBaseImage=mcr.microsoft.com/dotnet/runtime-deps:%s" baseImageTag
           "-p"; sprintf "ContainerRepository=%s" Paths.Repository
           "-p"; sprintf "ContainerImageTags=\"%s\"" containerImageTags.Value
           "-p"; "ContainerUser=1001:1001"]
        @ registryArgs
    exec "dotnet" args |> ignore

/// Timings and metrics for one repository in the compare run.
type private RepoResult = {
    Name: string
    Files: int
    Configs: int
    CurbSeconds: float
    CurbWarmSeconds: float
    CspSeconds: float
    CspWarmSeconds: float
    DnfSeconds: float
    CurbChanged: int
    CspChanged: int
    DnfChanged: int
    CurbNotFixpt: int
    CspNotFixpt: int
    CurbSecond: int
}

/// Times Curb, CSharpier and dotnet-format-whitespace over every sub-directory of a corpus dir
/// and writes a refreshed results table to docs/benchmarks/index.md.
///
/// --corpus must point at a directory whose immediate children are C# repository checkouts. Every
/// subdirectory that contains at least one .cs file is measured. Each tool runs on a fresh copy so
/// no tool sees the other's output.
///
/// The result table is written both to stdout and into docs/benchmarks/index.md
/// between the <!-- RESULTS --> ... <!-- /RESULTS --> marker comments, so the doc stays current
/// without a manual edit.
let private compare (arguments:ParseResults<Arguments>) =
    let corpusDir =
        match arguments.TryGetResult Corpus with
        | Some path ->
            let d = DirectoryInfo path
            if not d.Exists then failwithf "compare: corpus not found: %s" d.FullName
            d
        | None -> failwith "compare needs --corpus <dir-of-repos>"

    // Publish the native binary exactly as `perf` does.
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
    exec "dotnet" ["publish"; "src/Nullean.Curb.Cli"; "-c"; "Release"; "-r"; rid] |> ignore

    let binary =
        let name = if OperatingSystem.IsWindows() then "curb.exe" else "curb"
        Path.GetFullPath(Path.Combine(".artifacts", "publish", "Nullean.Curb.Cli", sprintf "release_%s" rid, name))
    if not (File.Exists binary) then failwithf "expected a native binary at %s" binary

    // Scratch space in the system temp dir — NOT under build/output, which is in .gitignore.
    // CSharpier respects .gitignore, so any path under build/output would produce "Formatted 0 files".
    let scratch = Path.Combine(Path.GetTempPath(), "curb-compare")
    if Directory.Exists scratch then Directory.Delete(scratch, true)
    Directory.CreateDirectory scratch |> ignore

    let countChanged (original: string) (formatted: string) =
        Directory.GetFiles(original, "*.cs", SearchOption.AllDirectories)
        |> Array.filter (fun f ->
            let rel = Path.GetRelativePath(original, f)
            let other = Path.Combine(formatted, rel)
            not (File.Exists other) || File.ReadAllText f <> File.ReadAllText other)
        |> Array.length

    let countNotFixpt (formatted: string) =
        let check = Path.Combine(scratch, "fixpt_" + Guid.NewGuid().ToString("N"))
        copyTree formatted check
        execResult "dotnet" ["format"; "whitespace"; check; "--folder"] |> ignore
        let diffs = countChanged formatted check
        if Directory.Exists check then Directory.Delete(check, true)
        diffs

    // Find every immediate child directory that has at least one .cs file.
    let repos =
        corpusDir.GetDirectories()
        |> Array.filter (fun d -> Directory.GetFiles(d.FullName, "*.cs", SearchOption.AllDirectories).Length > 0)
        |> Array.sortBy (fun d -> Directory.GetFiles(d.FullName, "*.cs", SearchOption.AllDirectories).Length)

    printfn "%d repos found in %s" repos.Length corpusDir.FullName

    let results = System.Collections.Generic.List<RepoResult>()

    for repo in repos do
        printfn ""
        printfn "=== %s ===" repo.Name
        let files = Directory.GetFiles(repo.FullName, "*.cs", SearchOption.AllDirectories).Length
        let configs = Directory.GetFiles(repo.FullName, ".editorconfig", SearchOption.AllDirectories).Length

        // Fresh copies — one per tool so no tool sees the other's output.
        let curbDir = Path.Combine(scratch, repo.Name + "_curb")
        let cspDir  = Path.Combine(scratch, repo.Name + "_csp")
        let dnfDir  = Path.Combine(scratch, repo.Name + "_dnf")
        copyTree repo.FullName curbDir
        copyTree repo.FullName cspDir
        copyTree repo.FullName dnfDir
        configureCorpus arguments curbDir
        configureCorpus arguments cspDir
        configureCorpus arguments dnfDir

        // Curb cold — best of 3.
        // Uses execResult (not exec) so a verification failure on one file does not abort the run.
        // Exit code 3 means a file could not be verified and was left untouched — expected on repos
        // with raw string literals or BOM handling; the changed-file count still reflects what happened.
        let curbSec =
            [ for _ in 1..3 ->
                let sw = Diagnostics.Stopwatch.StartNew()
                execResult binary ["format"; curbDir] |> ignore
                sw.Stop()
                sw.Elapsed.TotalSeconds ]
            |> List.min

        // Curb warm — run with --cache enabled. curbDir is already formatted, so the first run
        // populates the cache; the second run is the warm measurement (all files are cache hits).
        let curbCachePath = Path.Combine(scratch, repo.Name + "_curb.cache")
        execResult binary ["format"; curbDir; "--cache"; curbCachePath] |> ignore
        let curbWarmSec =
            let sw = Diagnostics.Stopwatch.StartNew()
            execResult binary ["format"; curbDir; "--cache"; curbCachePath] |> ignore
            sw.Stop()
            sw.Elapsed.TotalSeconds

        // CSharpier cache path — machine-global, content-hash keyed, outside the repo tree.
        // Clearing it before each cold run is the only way to get accurate formatting times.
        let csharpierCache =
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "CSharpier", ".formattingCache")

        // CSharpier cold — best of 3. Cache cleared before each run; repo re-copied so each run
        // formats the original source, not an already-formatted copy.
        let cspSec =
            [ for i in 1..3 ->
                if i > 1 then
                    if Directory.Exists cspDir then Directory.Delete(cspDir, true)
                    copyTree repo.FullName cspDir
                    configureCorpus arguments cspDir
                if File.Exists csharpierCache then File.Delete csharpierCache
                let sw = Diagnostics.Stopwatch.StartNew()
                execResult "csharpier" ["format"; cspDir] |> ignore
                sw.Stop()
                sw.Elapsed.TotalSeconds ]
            |> List.min

        // CSharpier warm — one run with the cache already populated (no re-copy, no cache clear).
        // cspDir is now CSharpier-formatted; running again exercises the cache path only.
        let cspWarmSec =
            let sw = Diagnostics.Stopwatch.StartNew()
            execResult "csharpier" ["format"; cspDir] |> ignore
            sw.Stop()
            sw.Elapsed.TotalSeconds

        // dotnet format whitespace — best of 3
        let dnfSec =
            [ for i in 1..3 ->
                if i > 1 then
                    if Directory.Exists dnfDir then Directory.Delete(dnfDir, true)
                    copyTree repo.FullName dnfDir
                    configureCorpus arguments dnfDir
                let sw = Diagnostics.Stopwatch.StartNew()
                execResult "dotnet" ["format"; "whitespace"; dnfDir; "--folder"] |> ignore
                sw.Stop()
                sw.Elapsed.TotalSeconds ]
            |> List.min

        // Files changed vs pristine
        let curbChanged = countChanged repo.FullName curbDir
        let cspChanged  = countChanged repo.FullName cspDir
        let dnfChanged  = countChanged repo.FullName dnfDir

        // Not-fixed-point: how many of each tool's outputs are changed by dotnet format whitespace
        printfn "checking fixed-point for curb..."
        let curbNotFixpt = countNotFixpt curbDir
        printfn "checking fixed-point for csharpier..."
        let cspNotFixpt = countNotFixpt cspDir

        // Curb idempotency: second pass over own output
        printfn "checking curb idempotency..."
        let curbDir2 = Path.Combine(scratch, repo.Name + "_curb2")
        copyTree curbDir curbDir2
        execResult binary ["format"; curbDir2] |> ignore
        let curbSecond = countChanged curbDir curbDir2
        if Directory.Exists curbDir2 then Directory.Delete(curbDir2, true)

        // Clean up work dirs to keep disk usage bounded.
        for d in [curbDir; cspDir; dnfDir] do
            if Directory.Exists d then Directory.Delete(d, true)

        let r = {
            Name = repo.Name; Files = files; Configs = configs
            CurbSeconds = curbSec; CurbWarmSeconds = curbWarmSec
            CspSeconds = cspSec; CspWarmSeconds = cspWarmSec; DnfSeconds = dnfSec
            CurbChanged = curbChanged; CspChanged = cspChanged; DnfChanged = dnfChanged
            CurbNotFixpt = curbNotFixpt; CspNotFixpt = cspNotFixpt; CurbSecond = curbSecond
        }
        results.Add(r)
        printfn "%s: curb cold %.2f s, curb warm %.2f s, csp cold %.2f s, csp warm %.2f s, dnf %.2f s; changed %d/%d/%d; not-fixpt %d/%d; 2nd %d"
            r.Name r.CurbSeconds r.CurbWarmSeconds r.CspSeconds r.CspWarmSeconds r.DnfSeconds
            r.CurbChanged r.CspChanged r.DnfChanged r.CurbNotFixpt r.CspNotFixpt r.CurbSecond

    // Emit the table (markdown format; not-fixpt and 2nd-idem columns are in churn.md).
    let header = "| repo | files | curb | dnf whitespace | CSharpier |\n|---|---|---|---|---|"
    let rows =
        results
        |> Seq.map (fun r ->
            sprintf "| %s | %s | %.2f s | %.2f s | %.2f s |"
                r.Name (r.Files.ToString("N0"))
                r.CurbSeconds r.DnfSeconds r.CspSeconds)
        |> String.concat "\n"
    let table = sprintf "%s\n%s" header rows

    printfn ""
    printfn "%s" table

    // Splice into the doc between the marker comments.
    let docPath = "docs/benchmarks/index.md"
    if File.Exists docPath then
        let content = File.ReadAllText docPath
        let startMarker = "<!-- RESULTS -->"
        let endMarker = "<!-- /RESULTS -->"
        let startIdx = content.IndexOf(startMarker, StringComparison.Ordinal)
        let endIdx = content.IndexOf(endMarker, StringComparison.Ordinal)
        if startIdx >= 0 && endIdx > startIdx then
            let before = content.[..startIdx + startMarker.Length - 1]
            let after = content.[endIdx..]
            let updated = sprintf "%s\n\n%s\n\n%s" before table after
            File.WriteAllText(docPath, updated)
            printfn ""
            printfn "updated %s" docPath
        else
            printfn "warning: %s has no <!-- RESULTS --> / <!-- /RESULTS --> markers; table not spliced" docPath
    else
        printfn "warning: %s not found; table not written" docPath

let private options (_:ParseResults<Arguments>) =
    exec "dotnet" ["run"; "--project"; "tools/Nullean.Curb.OptionDocs"; "-c"; "Release"] |> ignore

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
    cmd Compare.Name (Some [Build.Name]) None <| fun _ -> compare parsed
    cmd Options.Name (Some [Build.Name]) None <| fun _ -> options parsed
    cmd MsbuildSmoketest.Name (Some [Build.Name]) None <| fun _ -> msbuildSmoketest parsed
    cmd CleanupSmoketest.Name (Some [Build.Name]) None <| fun _ -> cleanupSmoketest parsed
    cmd CleanupSafety.Name (Some [Build.Name]) None <| fun _ -> cleanupSafety parsed
    cmd CleanupConformance.Name (Some [Build.Name]) None <| fun _ -> cleanupConformance parsed

    // No dependency on build: the documentation is markdown and one static HTML file, and waiting on
    // a Release compile to preview a paragraph would make nobody run it.
    step Docs.Name docs
    cmd VerifyExpectations.Name (Some [Build.Name]) None <| fun _ -> verifyExpectations parsed
    cmd VerifyCleanupExpectations.Name (Some [Build.Name]) None <| fun _ -> verifyCleanupExpectations parsed
    cmd VerifyExpectationsJb.Name (Some [Build.Name]) None <| fun _ -> verifyExpectationsJb parsed
    cmd VerifyExpectationsIde0055.Name (Some [Build.Name]) None <| fun _ -> verifyExpectationsIde0055 parsed

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
    step PublishContainers.Name publishContainers
    cmd Publish.Name
        (Some [Release.Name])
        (Some [CreateReleaseOnGithub.Name; PublishContainers.Name])
        <| fun _ -> publish parsed
