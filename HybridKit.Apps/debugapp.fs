namespace HybridKit.Apps

open HybridKit

open System
open System.IO
open System.Net
open System.Diagnostics
open System.Threading

type Router = string -> IHtmlView

/// Provides a simple debug server via HttpListener
[<AbstractClass>]
type App(basePath : string) =
    let loaded = Event<_,_>()
    let navigating = Event<_,_>()

    [<Literal>]
    static let Endpoint = "http://localhost:8050/"



    member this.Run(router : Router) =
        let listen = async {
            use listener = new HttpListener()
            listener.Prefixes.Add Endpoint
            listener.Start()
            while true do
                let! ctx = Async.AwaitTask(listener.GetContextAsync())
                let route = router ctx.Request.Url.LocalPath
                using (new StreamWriter(ctx.Response.OutputStream)) (fun out ->
                    out.WriteLine(sprintf "<HTML><BODY>Would show: %A</BODY></HTML>" route)
                )
        }
        do
            use cancel = new CancellationTokenSource()
            Async.StartImmediate(listen, cancel.Token)

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
