namespace HybridKit.Apps

open System
open System.Diagnostics

type ITrigger =
    inherit IDisposable
    abstract Trigger : unit -> unit
    [<CLIEvent>] abstract Triggered : IEvent<unit>

type RateLimitedTrigger(?minInterval : TimeSpan) =
    let minInterval = defaultArg minInterval (TimeSpan.FromSeconds(1.))
    let stopwatch = Stopwatch()
    let triggered = Event<_>()
    let mutable disposed = false
    [<CLIEvent>]
    member this.Triggered = triggered.Publish
    member this.Trigger() =
        lock this (fun _ ->
            if not disposed && (not(stopwatch.IsRunning) || stopwatch.Elapsed >= minInterval) then
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
