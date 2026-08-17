module CommandLine

open Argu
open Microsoft.FSharp.Reflection

type Arguments =
    | [<CliPrefix(CliPrefix.None);SubCommand>] Clean
    | [<CliPrefix(CliPrefix.None);SubCommand>] Build
    | [<CliPrefix(CliPrefix.None);SubCommand>] Test
    | [<CliPrefix(CliPrefix.None);SubCommand>] Benchmark
    | [<CliPrefix(CliPrefix.None);SubCommand>] Conformance
    | [<CliPrefix(CliPrefix.None);SubCommand>] Churn
    | [<CliPrefix(CliPrefix.None);SubCommand>] Perf
    | [<CliPrefix(CliPrefix.None);SubCommand>] Docs

    | [<CliPrefix(CliPrefix.None);Hidden;SubCommand>] PristineCheck
    | [<CliPrefix(CliPrefix.None);Hidden;SubCommand>] GeneratePackages
    | [<CliPrefix(CliPrefix.None);Hidden;SubCommand>] ValidatePackages
    | [<CliPrefix(CliPrefix.None);Hidden;SubCommand>] GenerateReleaseNotes
    | [<CliPrefix(CliPrefix.None);Hidden;SubCommand>] GenerateApiChanges
    | [<CliPrefix(CliPrefix.None);SubCommand>] Release

    | [<CliPrefix(CliPrefix.None);Hidden;SubCommand>] CreateReleaseOnGithub
    | [<CliPrefix(CliPrefix.None);SubCommand>] Publish

    | [<Inherit>] Corpus of string
    | [<Inherit>] Minimum of double
    | [<Inherit>] MaxAllocationRatio of double
    | [<CliPrefix(CliPrefix.None);SubCommand>] MsbuildSmoketest
    | [<CliPrefix(CliPrefix.None);SubCommand>] VerifyExpectations

    | [<Inherit>] TrailingCommas
    | [<Inherit>] Reflow
    | [<Inherit>] Port of int
    | [<Inherit>] NoServe
    | [<Inherit>] Deterministic
    | [<Inherit>] Preserve
    | [<Inherit>] Width of int
    | [<Inherit>] Maximum of double
    | [<Inherit;AltCommandLine("-s")>] SingleTarget of bool
    | [<Inherit>] Token of string
    | [<Inherit;AltCommandLine("-c")>] CleanCheckout of bool
with
    interface IArgParserTemplate with
        member this.Usage =
            match this with
            | Clean -> "clean known output locations"
            | Build -> "runs build"
            | Test -> "runs build then tests"
            | Benchmark -> "runs the BenchmarkDotNet suite against the AOT-published binary"
            | Conformance -> "measures agreement with dotnet format over a corpus (--corpus <path>)"
            | Churn -> "measures how many of a corpus's files Kerf rewrites (--corpus <path>)"
            | Perf -> "times the AOT binary over a corpus (--corpus <path>); never measure the JIT build"
            | Docs -> "builds the public docs, applies the landing page override, and serves them"
            | Port _ -> "port to serve the documentation on (default 8080)"
            | NoServe -> "build the documentation without serving it"
            | Corpus _ -> "path to a checkout to measure conformance against"
            | Minimum _ -> "fail if conformance falls below this percentage"
            | Maximum _ -> "fail if churn rises above this percentage of files"
            | MaxAllocationRatio _ -> "fail if perf allocates more than this multiple of the source size"
            | MsbuildSmoketest -> "prove the MSBuild integration runs before the compiler"
            | VerifyExpectations -> "prove every expectation in the test suite survives dotnet format"
            | TrailingCommas -> "measure conformance with ReSharper's trailing-comma keys on"
            | Reflow -> "keep the corpus's own max_line_length instead of forcing it off"
            | Deterministic -> "force csharp_keep_existing_linebreaks = false; implies --reflow. Redundant now that a width selects it"
            | Preserve -> "force csharp_keep_existing_linebreaks = true, the opt-out a width no longer gives you"
            | Width _ -> "override the corpus's max_line_length with this column count"
            | Release -> "runs build, tests, then creates and validates the packages shy of publishing them"
            | Publish -> "runs the full release"

            | SingleTarget _ -> "runs the provided sub command without running its dependencies"
            | Token _ -> "token used to authenticate with github"
            | CleanCheckout _ -> "skip the clean checkout check that guards the release/publish targets"

            | PristineCheck
            | GeneratePackages
            | ValidatePackages
            | GenerateReleaseNotes
            | GenerateApiChanges
            | CreateReleaseOnGithub
                -> "Undocumented, dependent target"

    member this.Name =
        match FSharpValue.GetUnionFields(this, typeof<Arguments>) with
        | case, _ -> case.Name.ToLowerInvariant()
