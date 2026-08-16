module CommandLine

open Argu
open Microsoft.FSharp.Reflection

type Arguments =
    | [<CliPrefix(CliPrefix.None);SubCommand>] Clean
    | [<CliPrefix(CliPrefix.None);SubCommand>] Build
    | [<CliPrefix(CliPrefix.None);SubCommand>] Test
    | [<CliPrefix(CliPrefix.None);SubCommand>] Benchmark
    | [<CliPrefix(CliPrefix.None);SubCommand>] Conformance

    | [<CliPrefix(CliPrefix.None);Hidden;SubCommand>] PristineCheck
    | [<CliPrefix(CliPrefix.None);Hidden;SubCommand>] GeneratePackages
    | [<CliPrefix(CliPrefix.None);Hidden;SubCommand>] ValidatePackages
    | [<CliPrefix(CliPrefix.None);Hidden;SubCommand>] GenerateReleaseNotes
    | [<CliPrefix(CliPrefix.None);Hidden;SubCommand>] GenerateApiChanges
    | [<CliPrefix(CliPrefix.None);SubCommand>] Release

    | [<CliPrefix(CliPrefix.None);Hidden;SubCommand>] CreateReleaseOnGithub
    | [<CliPrefix(CliPrefix.None);SubCommand>] Publish

    | [<Inherit>] Corpus of string
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
            | Corpus _ -> "path to a checkout to measure conformance against"
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
