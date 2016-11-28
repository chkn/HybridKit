namespace HybridKit.Apps

open HybridKit

open System
open System.IO
open System.Net
open System.Diagnostics
open System.Threading

/// Provides a simple debug server via HttpListener
[<AbstractClass>]
type DebugApp(basePath : string) as this =
    let loaded = Event<_,_>()
    let navigating = Event<_,_>()
    let mutable eventWriter = None

    [<Literal>]
    static let Endpoint = "http://localhost:8050/"
    static let PageStream = typeof<DebugApp>.Assembly.GetManifestResourceStream("debugapp.html")

    static let printUsage() =
        Console.WriteLine("Usage coming soon!")

    let listen (listener : HttpListener) = async {
        while true do
            let! ctx = Async.AwaitTask(listener.GetContextAsync())
            let resp = ctx.Response
            try
                match ctx.Request.Url.AbsolutePath with
                | "/" ->
                    resp.ContentType     <- "text/html"
                    resp.ContentLength64 <- PageStream.Length
                    PageStream.Position  <- 0L
                    do! Async.AwaitTask(PageStream.CopyToAsync(resp.OutputStream))
                    resp.OutputStream.Dispose()
                    resp.Close()
                | "/events" ->
                    resp.KeepAlive   <- true
                    resp.ContentType <- "text/event-stream"
                    resp.AddHeader("Cache-Control", "no-cache")
                    eventWriter <- Some(new StreamWriter(resp.OutputStream))
                    this.OnRun(this)
                | _ ->
                    resp.StatusCode <- 404
                    resp.Close()
            with e ->
                Console.Error.WriteLine(e)
    }
    let sendEvent name (msg : string) =
        match eventWriter with
        | Some wr -> wr.Write("event: " + name + "\n" + "data: " + msg.Replace("\n", "\ndata: ") + "\n\n")
        | _ -> ()
    do
        use listener = new HttpListener()
        use cancel = new CancellationTokenSource()

        listener.Prefixes.Add Endpoint
        listener.Start()
        Async.StartImmediate(listen listener, cancel.Token)

        Process.Start Endpoint |> ignore
        Console.WriteLine("Listening on {0}", Endpoint)
        Console.WriteLine("Press any key to exit...")
        Console.ReadKey() |> ignore
        cancel.Cancel()

    abstract OnRun : IWebView -> unit
    interface IWebView with
        member __.Cache = failwith "nope"
        member __.Window = failwith "nope"
        [<CLIEvent>]
        member __.Loaded = loaded.Publish
        [<CLIEvent>]
        member __.Navigating = navigating.Publish
        member __.LoadFile(brp) = failwith "ahh"
        member __.LoadString(html, baseUrl) = failwith "baa"
