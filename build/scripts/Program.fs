module Program

open Nullean.Make.Fs
open Proc.Fs
open CommandLine

[<EntryPoint>]
let main argv =
    // `-s` skips what a target depends on, as it did under Bullseye: `conformance -s` measures without
    // building first. `build` composes `clean` rather than depending on it, so `build -s` still cleans.
    app.Bind <| function
        // Cleans Release only, which is the configuration everything downstream builds.
        //
        // Not a preference: this script is itself a project in the solution, and it runs from its Debug
        // output. A bare `dotnet clean` defaults to Debug and so deletes the assemblies of the process
        // executing it — anything the runner had not yet loaded then fails to resolve. It surfaced as
        // `perf` dying on a missing System.Reactive, which reads like a restore problem and is not one.
        | Clean -> target [] {
              if Paths.Output.Exists then Paths.Output.Delete(true)
              // Clean does not restore, and a RID-specific publish or a tool pack restores the CLI for net11.0
              // alone (see its project), which would leave clean without the net10.0 it now asks for.
              sh $"dotnet restore"
              sh $"dotnet clean -c Release" }
        | Build -> command {
              composes [Clean] }
                   |> executes { sh $"dotnet build -c Release" }
        // TUnit runs on Microsoft.Testing.Platform; `dotnet run` avoids the deprecated VSTest path.
        | Test -> target [Build] { sh $"dotnet run --project tests/Nullean.Curb.Tests -c Release" }
        | Benchmark -> target [Build] { sh $"dotnet run --project tests/Nullean.Curb.Benchmarks -c Release" }
        | Conformance -> target [Build] { Targets.conformance () }
        | Churn -> target [Build] { Targets.churn () }
        | Perf -> target [Build] { Targets.perf () }
        | Compare -> target [Build] { Targets.compare () }
        | Options -> target [Build] { sh $"dotnet run --project tools/Nullean.Curb.OptionDocs -c Release" }
        | Msbuildsmoketest -> target [Build] { Targets.msbuildSmoketest () }
        | Fsharpsmoketest -> target [Build] { Targets.fsharpSmoketest () }
        | Cleanupsmoketest -> target [Build] { Targets.cleanupSmoketest () }
        | Cleanupsafety -> target [Build] { Targets.cleanupSafety () }
        | Cleanupconformance -> target [Build] { Targets.cleanupConformance () }
        | Xmlsafety -> target [Build] { Targets.xmlSafety () }
        // Builds docs/ the way the workflow does — including the landing page override, which is the whole
        // reason `docs-builder serve` is not enough on its own — and serves the result.
        //
        // No dependency on build: the documentation is markdown and one static HTML file, and waiting on
        // a Release compile to preview a paragraph would make nobody run it.
        | Docs -> target [] {
              Documentation.build ()
              if noServe.Value then printfn "built; --no-serve given, not serving"
              else Documentation.serve port.Value }
        // No dependency on build: generating runs the test suite and the generator with `dotnet run`, which builds
        // what they need, and the docs-builder source is a git clone.
        | Clonecorpus -> target [] { Targets.cloneCorpusTarget () }
        | Verifyexpectations -> target [Build] { Targets.verifyExpectations () }
        | Verifycleanupexpectations -> target [Build] { Targets.verifyCleanupExpectations () }
        | Verifyexpectationsjb -> target [Build] { Targets.verifyExpectationsJb () }
        | Verifyexpectationside0055 -> target [Build] { Targets.verifyExpectationsIde0055 () }
        | Verifyrazorexpectations -> target [Build] { Targets.verifyRazorExpectations () }
        | Generatepackages -> target [] { Targets.generatePackages () }
        | Validatepackages -> target [] { Targets.validatePackages () }
        | Generateapichanges -> target [] { Targets.generateApiChanges () }
        | Publishcontainers -> target [] { Targets.publishContainers () }

    app.RunAsync(argv).GetAwaiter().GetResult()
