/// Builds the public documentation locally, exactly as CI does, and serves it.
///
/// The reason this needs a target at all: `docs-builder serve` renders pages on demand and knows
/// nothing about the branded landing page, which is a standalone HTML file that replaces the
/// generated `index.html` after the build. So the only way to preview the real site is to build,
/// apply the override, and serve the output — which is what this does, in the order CI does it.
module Documentation

open System
open System.IO
open System.IO.Compression
open System.Net
open System.Net.Http
open System.Text
open System.Threading
open System.Threading.Tasks
open ProcNet

let private exec binary args = Proc.Exec(binary, List.toArray args) |> ignore

/// The sub-path GitHub Pages serves this repo from. Three files have to agree on it: this one,
/// `.github/workflows/docs.yml` (the `prefix:` input) and the `<base href>` in the landing page.
/// `checkPrefixesAgree` below turns drift between them into a local failure rather than a 404 in
/// production, because nothing else would catch it — docs-builder never reads the landing page.
let PathPrefix = "formatter"

let private docsSource = "docs"
let private landingPage = Path.Combine(docsSource, "kerf-landing.html")
let private robotsTxt = Path.Combine(docsSource, "robots.txt")
let private workflow = Path.Combine(".github", "workflows", "docs.yml")

/// docs-builder always writes here; the GitHub action does not make it configurable, so neither
/// does this.
let private htmlOutput = Path.Combine(".artifacts", "docs", "html")

// ─────────────────────────────  acquiring docs-builder  ─────────────────────────────

/// Cached under .artifacts rather than build/output, because `clean` deletes the latter and
/// re-downloading 24 MB after every clean build is a poor trade for one cached file.
let private toolPath =
    let exe = if OperatingSystem.IsWindows() then "docs-builder.exe" else "docs-builder"
    Path.Combine(".artifacts", "tools", exe)

let private archiveName () =
    let arch =
        match Runtime.InteropServices.RuntimeInformation.OSArchitecture with
        | Runtime.InteropServices.Architecture.Arm64 -> "arm64"
        | Runtime.InteropServices.Architecture.X64 -> "x64"
        | other -> failwithf "docs-builder ships no binary for %O" other
    if OperatingSystem.IsMacOS() then sprintf "docs-builder-mac-%s.zip" arch
    elif OperatingSystem.IsLinux() then sprintf "docs-builder-linux-%s.zip" arch
    elif OperatingSystem.IsWindows() then sprintf "docs-builder-win-%s.zip" arch
    else failwith "unsupported operating system for docs-builder"

/// docs-builder ships as a native binary, not a dotnet tool, so `dotnet tool restore` cannot bring
/// it in. The published install script wants to write to /usr/local/bin and asks for sudo; a build
/// target has no business doing that, so this pulls the same archive into .artifacts instead.
///
/// Delete .artifacts/tools/docs-builder to pick up a newer release, or set DOCS_BUILDER_VERSION to
/// pin one if a release ever breaks the build.
let ensureTool () =
    if File.Exists toolPath then toolPath
    else

    let archive = archiveName ()
    let version =
        match Environment.GetEnvironmentVariable "DOCS_BUILDER_VERSION" with
        | null | "" -> "latest"
        | v -> v
    let url =
        match version with
        | "latest" -> sprintf "https://github.com/elastic/docs-builder/releases/latest/download/%s" archive
        | v -> sprintf "https://github.com/elastic/docs-builder/releases/download/%s/%s" v archive

    printfn "docs-builder not cached, downloading %s" url
    Directory.CreateDirectory(Path.GetDirectoryName toolPath) |> ignore

    let zip = Path.Combine(Path.GetTempPath(), archive)
    use client = new HttpClient()
    client.Timeout <- TimeSpan.FromMinutes 5.0
    do
        use response = client.GetAsync(url).GetAwaiter().GetResult()
        response.EnsureSuccessStatusCode() |> ignore
        use file = File.Create zip
        response.Content.CopyToAsync(file).GetAwaiter().GetResult()

    let name = Path.GetFileName toolPath
    do
        use zipFile = ZipFile.OpenRead zip
        let entry =
            zipFile.Entries
            |> Seq.tryFind (fun e -> String.Equals(e.Name, name, StringComparison.OrdinalIgnoreCase))
            |> Option.defaultWith (fun () -> failwithf "%s did not contain %s" archive name)
        entry.ExtractToFile(toolPath, true)
    File.Delete zip

    if not (OperatingSystem.IsWindows()) then
        File.SetUnixFileMode(
            toolPath,
            UnixFileMode.UserRead ||| UnixFileMode.UserWrite ||| UnixFileMode.UserExecute
            ||| UnixFileMode.GroupRead ||| UnixFileMode.GroupExecute
            ||| UnixFileMode.OtherRead ||| UnixFileMode.OtherExecute)

    printfn "docs-builder cached at %s" toolPath
    toolPath

// ─────────────────────────────  the prefix must not drift  ─────────────────────────────

/// The landing page is not generated, so `--path-prefix` never reaches it: its links resolve against
/// a hand-written <base href>. If that stops matching the prefix CI builds with, every link on the
/// home page 404s in production and nothing else in the build would notice.
let checkPrefixesAgree () =
    let expectedBase = sprintf "/%s/" PathPrefix

    let landing = File.ReadAllText landingPage
    let m = Text.RegularExpressions.Regex.Match(landing, "<base\\s+href=\"([^\"]*)\"")
    if not m.Success then
        failwithf "%s has no <base href>; its relative links cannot resolve under a sub-path" landingPage
    if m.Groups[1].Value <> expectedBase then
        failwithf
            "%s has <base href=\"%s\"> but the site builds with prefix '%s' (expected \"%s\"). See Documentation.PathPrefix."
            landingPage m.Groups[1].Value PathPrefix expectedBase

    if File.Exists workflow then
        let yaml = File.ReadAllText workflow
        let w = Text.RegularExpressions.Regex.Match(yaml, "prefix:\\s*(\\S+)")
        if w.Success && w.Groups[1].Value <> PathPrefix then
            failwithf
                "%s builds with prefix '%s' but Documentation.PathPrefix is '%s'. These must agree."
                workflow w.Groups[1].Value PathPrefix

// ─────────────────────────────  build  ─────────────────────────────

/// Build, then apply the same two overrides the workflow applies, in the same order. Anything done
/// here and not in `.github/workflows/docs.yml` is a lie about what gets published.
let build () =
    checkPrefixesAgree ()
    let tool = ensureTool ()

    exec tool ["build"; "--path"; docsSource; "--path-prefix"; PathPrefix]

    if not (Directory.Exists htmlOutput) then
        failwithf "docs-builder reported success but %s does not exist" htmlOutput

    File.Copy(landingPage, Path.Combine(htmlOutput, "index.html"), true)
    File.Copy(robotsTxt, Path.Combine(htmlOutput, "robots.txt"), true)
    printfn "applied the landing page override -> %s" (Path.Combine(htmlOutput, "index.html"))

// ─────────────────────────────  serve  ─────────────────────────────

let private contentType (path: string) =
    match Path.GetExtension(path).ToLowerInvariant() with
    | ".html" | ".htm" -> "text/html; charset=utf-8"
    | ".css" -> "text/css; charset=utf-8"
    | ".js" | ".mjs" -> "text/javascript; charset=utf-8"
    | ".json" -> "application/json; charset=utf-8"
    | ".svg" -> "image/svg+xml"
    | ".woff2" -> "font/woff2"
    | ".woff" -> "font/woff"
    | ".ttf" -> "font/ttf"
    | ".png" -> "image/png"
    | ".jpg" | ".jpeg" -> "image/jpeg"
    | ".gif" -> "image/gif"
    | ".webp" -> "image/webp"
    | ".avif" -> "image/avif"
    | ".ico" -> "image/x-icon"
    | ".txt" -> "text/plain; charset=utf-8"
    | ".xml" -> "application/xml; charset=utf-8"
    | ".wasm" -> "application/wasm"
    // Pagefind ships its search index as extensionless binary fragments.
    | _ -> "application/octet-stream"

let private write (response: HttpListenerResponse) (path: string) =
    response.ContentType <- contentType path
    let bytes = File.ReadAllBytes path
    response.OutputStream.Write(bytes, 0, bytes.Length)

let private notFound (response: HttpListenerResponse) (raw: string) =
    response.StatusCode <- 404
    response.ContentType <- "text/plain; charset=utf-8"
    let body = Encoding.UTF8.GetBytes(sprintf "404 %s" raw)
    response.OutputStream.Write(body, 0, body.Length)

/// Rather than staging the output under a directory literally named after the prefix — which would
/// need either a copy or a symlink, and symlinks need privileges on Windows — the server strips the
/// prefix from the request path. The bytes served are exactly the bytes CI publishes.
let private handle (root: string) (context: HttpListenerContext) =
    let response = context.Response
    try
        try
            let raw = Uri.UnescapeDataString context.Request.Url.AbsolutePath
            let mount = sprintf "/%s" PathPrefix

            // Everything lives under the prefix, so bounce the bare root at it. Without this,
            // hitting localhost:8080 gives a 404 and reads like the build failed.
            if raw = "/" || raw = "" then response.Redirect(mount + "/")
            elif raw = mount then response.Redirect(mount + "/")
            elif not (raw.StartsWith(mount + "/", StringComparison.Ordinal)) then notFound response raw
            else

            let relative = raw.Substring(mount.Length).TrimStart('/')
            let candidate = Path.GetFullPath(Path.Combine(root, relative))

            // Never serve anything outside the output directory, whatever the request asks for.
            if not (candidate.StartsWith(root, StringComparison.Ordinal)) then notFound response raw
            elif File.Exists candidate then write response candidate
            elif Directory.Exists candidate then
                // A directory URL has to end in a slash, or every relative link inside the page it
                // serves resolves one level too high.
                if not (raw.EndsWith "/") then response.Redirect(raw + "/")
                else
                    let index = Path.Combine(candidate, "index.html")
                    if File.Exists index then write response index else notFound response raw
            else notFound response raw
        with e ->
            response.StatusCode <- 500
            let body = Encoding.UTF8.GetBytes e.Message
            response.OutputStream.Write(body, 0, body.Length)
    finally
        response.OutputStream.Close()

let serve (port: int) =
    let root = Path.GetFullPath htmlOutput
    let url = sprintf "http://localhost:%d/%s/" port PathPrefix

    let listener = new HttpListener()
    listener.Prefixes.Add(sprintf "http://localhost:%d/" port)
    try listener.Start()
    with :? HttpListenerException ->
        failwithf "could not listen on port %d — it is probably already in use. Pass --port <n>." port

    printfn ""
    printfn "  documentation serving at %s" url
    printfn "  ctrl-c to stop; re-run './build.sh docs' to pick up edits"
    printfn ""

    // Opening a browser is the point of the command, and failing to is never worth stopping for.
    // Not on a build agent, though: there is no one to look at it, and no browser to look with.
    let headless =
        [ "CI"; "TF_BUILD"; "GITHUB_ACTIONS" ]
        |> List.exists (fun v -> not (String.IsNullOrEmpty(Environment.GetEnvironmentVariable v)))
    if not headless then
        try
            let opener, args =
                if OperatingSystem.IsMacOS() then "open", url
                elif OperatingSystem.IsWindows() then "cmd", sprintf "/c start %s" url
                else "xdg-open", url
            Diagnostics.ProcessStartInfo(opener, Arguments = args, UseShellExecute = false)
            |> Diagnostics.Process.Start
            |> ignore
        with _ -> ()

    // Stopping the listener is what breaks GetContext out of its wait; a second cancel key press
    // would otherwise be needed to kill the process.
    let mutable running = true
    Console.CancelKeyPress.Add(fun e ->
        e.Cancel <- true
        running <- false
        listener.Stop())

    while running do
        try
            let context = listener.GetContext()
            Task.Run(fun () -> handle root context) |> ignore
        with
        // Both are thrown by the in-flight GetContext when Stop() runs from the cancel handler.
        | :? HttpListenerException -> ()
        | :? ObjectDisposedException -> ()

    (listener :> IDisposable).Dispose()
    printfn "stopped"
