namespace HybridKit.Apps

open HybridKit

open System
open System.IO
open System.Net
open System.Text
open System.Net.WebSockets
open System.Diagnostics
open System.Threading
open System.Threading.Tasks
open System.Collections.Generic

type DebugServerSession(sck : WebSocket, cancelToken : CancellationToken) =
    let loaded = Event<_,_>()
    let navigating = Event<_,_>()

    // We can only have one active eval at a time
    let evalSema = new SemaphoreSlim(1, 1)

    static let makeCall (fnName : string) (arg : obj) =
        let buf = StringBuilder(fnName).Append('(')
        JSON.Stringify(arg, buf)
        buf.Append(')') |> ignore
        Encoding.UTF8.GetBytes(buf.ToString())

    let buffer = Array.zeroCreate<byte> 8192
    let receive() = async {
        let mutable index = 0
        let mutable fin = false
        while not fin do
            let! result = Async.AwaitTask(sck.ReceiveAsync(ArraySegment<_>(buffer, index, buffer.Length - index), cancelToken))
            index <- index + result.Count
            fin <- result.EndOfMessage
        return Encoding.UTF8.GetString(buffer)
    }
    let eval (script : string) = async {
        do! Async.AwaitTask(evalSema.WaitAsync())
        try
            do! Async.AwaitTask(sck.SendAsync(ArraySegment<_>(makeCall "evalScript" script), WebSocketMessageType.Text, true, cancelToken))
            return! receive()
        finally
            ignore(evalSema.Release())
    }

    // Start by loading the HybridKit helper script
    do
        use reader = new StreamReader(typeof<ScriptObject>.Assembly.GetManifestResourceStream("HybridKit.HybridKit.js"))
        Async.RunSynchronously(async { let! _ = eval(reader.ReadToEnd()) in () })

    member this.LoadString(html : string) =
        sck.SendAsync(ArraySegment<_>(makeCall "loadHtml" html), WebSocketMessageType.Text, true, cancelToken).Wait()
        loaded.Trigger(this, EventArgs.Empty)

    interface IWebView with
        member __.Cache = failwith "nope"
        [<CLIEvent>]
        member __.Loaded = loaded.Publish
        [<CLIEvent>]
        member __.Navigating = navigating.Publish
        member __.LoadFile(brp) = failwith "ahh"
        member this.LoadString(html, baseUrl) = this.LoadString(html)
        member this.EvalAsync(script) = Async.StartAsTask(eval script, TaskCreationOptions.None, cancelToken)

/// Provides a simple debug server via HttpListener
[<AbstractClass>]
type DebugServerApp(basePath : string, isFsi : bool) as this =

    // Check it: if we are in REPL mode and the whole file gets re-evaluated,
    //  we will end up creating another instance of DebugServerApp. But we will be
    //  tricksy foxes and detect we already have sessions here and pick those up.
    static let sessions = List<DebugServerSession>() // LOCK!

    [<Literal>]
    static let Endpoint = "http://localhost:8050/"
    static let PageStream = typeof<DebugServerApp>.Assembly.GetManifestResourceStream("index.html")

    let listen (listener : HttpListener) (cancelToken : CancellationToken) = async {
        while true do
            let! ctx = Async.AwaitTask(listener.GetContextAsync())
            let req  = ctx.Request
            let resp = ctx.Response
            try
                match req.Url.AbsolutePath with
                | "/" ->
                    resp.ContentType     <- "text/html"
                    resp.ContentLength64 <- PageStream.Length
                    PageStream.Position  <- 0L
                    do! Async.AwaitTask(PageStream.CopyToAsync(resp.OutputStream, 81920, cancelToken))
                    resp.OutputStream.Dispose()
                    resp.Close()

                | "/_session" when req.IsWebSocketRequest ->
                    let! wsCtx = Async.AwaitTask(ctx.AcceptWebSocketAsync(null))
                    let session = DebugServerSession(wsCtx.WebSocket, cancelToken)
                    lock (sessions) (fun _ ->
                        sessions.Add(session)
                        this.OnRun(session)
                    )

                | _ ->
                    resp.StatusCode <- 404
                    resp.Close()
            with e ->
                Console.Error.WriteLine(e)
    }

    do
        // Ensure async exceptions get logged
        TaskScheduler.UnobservedTaskException.Add(fun e -> Console.Error.WriteLine(e.Exception))

        let startNewSession =
            lock (sessions) (fun _ ->
                if sessions.Count > 0 then
                    for session in sessions do this.OnRun(session)
                    false
                else true
            )
        if startNewSession then
            // Don't `use` these because we may be running in interactive mode
            //  and need to keep them around after this scope exits
            let listener = new HttpListener()
            let cancel = new CancellationTokenSource()

            listener.Prefixes.Add Endpoint
            listener.Start()
            Async.StartImmediate(listen listener cancel.Token, cancel.Token)

            Process.Start Endpoint |> ignore
            Console.WriteLine("Listening on {0}", Endpoint)

            // What we do next depends on whether we're in REPL mode
            //  or running non-interactively. Unfortunately, FSI supports
            //  both of these modes, and there doesn't appear to be a saner
            //  way of figuring out which one we're in other than parsing the
            //  command line :(
            if FSI.isRepl isFsi then
                Console.WriteLine("Running in interactive mode.")
            else
                Console.WriteLine("Press any key to exit...")
                Console.ReadKey() |> ignore
                cancel.Cancel()

    abstract OnRun : IWebView -> unit
