namespace HybridKit.Apps

open System
open System.Diagnostics
open System.Threading

type ITrigger =
    inherit IDisposable
    abstract Trigger : unit -> unit
    [<CLIEvent>] abstract Triggered : IEvent<unit>

type RateLimitedTrigger() =
    let stopwatch = Stopwatch()
    let triggered = Event<_>()
    let mutable disposed = false

    // Configurable options
    //  On Windows, there is a race after FileSystemWatcher notifies of a file change
    //  for the file to actually be written, so give a default delay..
    member val Delay = TimeSpan.FromSeconds(0.5) with get, set
    member val MinInterval = TimeSpan.FromSeconds(1.) with get, set

    [<CLIEvent>]
    member this.Triggered = triggered.Publish
    member this.Trigger() =
        lock this (fun _ ->
            Thread.Sleep(this.Delay)
            if not disposed && (not(stopwatch.IsRunning) || stopwatch.Elapsed >= this.MinInterval) then
                triggered.Trigger()
            stopwatch.Restart()
        )
    member this.Dispose() =
        lock this (fun _ ->    
            if not disposed then
                disposed <- true
                stopwatch.Stop()
        )
    interface IDisposable with member this.Dispose() = this.Dispose()
    interface ITrigger with
        member this.Trigger() = this.Trigger()
        [<CLIEvent>] member this.Triggered = this.Triggered
