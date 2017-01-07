namespace global

open System
open System.IO
open System.Reflection
open System.Threading.Tasks

open FSharp.Quotations

module internal FS =

    let resolvePath basePath path =
        if Path.IsPathRooted(path) then path else Path.Combine(basePath, path)

    let createWatcher filePath =
        new FileSystemWatcher(Path.GetDirectoryName(filePath), Path.GetFileName(filePath), NotifyFilter = NotifyFilters.LastWrite)

    let openShared filePath =
        let stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite)
        new StreamReader(stream)

    let inline writeTextFile filePath content =
        File.WriteAllText(filePath, content)

    let writeBinaryFile filePath (stream : Stream) =
        using (new FileStream(filePath, FileMode.Create, FileAccess.Write)) stream.CopyTo

    let createFileFromResource resName fileName =
        using (Assembly.GetExecutingAssembly().GetManifestResourceStream(resName)) (writeBinaryFile fileName)

module internal FSI =

    // FIXME: This logic feels pretty kludgy
    let commandName = if Environment.OSVersion.Platform = PlatformID.Win32NT then "fsi" else "fsharpi"

    /// Returns `true` iff we are within F# interactive (as denoted by the argument),
    ///  AND it appears to be running in REPL mode (as opposed to run and quit --exec mode)
    let isRepl =
        // If any arg to fsi is not prefixed by '-' or '/', assume it is a script file name
        //  which would put us in --exec mode (whether or not --exec is actually passed)
        let hasExecScript = lazy (
            Environment.GetCommandLineArgs()
            |> Seq.skip 1
            |> Seq.takeWhile ((<>) "--")
            |> Seq.exists (fun arg ->
                match arg.[0] with
                | '-' | '/' -> false
                | _ -> true
            )
        )
        fun isFsi -> isFsi && not(hasExecScript.Force())

    /// Returns the value of `fsi.CommandLineArgs`
    let commandLineArgs = lazy(
        let fsiType = Type.GetType("Microsoft.FSharp.Compiler.Interactive.Settings, FSharp.Compiler.Interactive.Settings")
        let fsiProp = fsiType.GetProperty("fsi", BindingFlags.Public ||| BindingFlags.Static)
        let fsiObj  = fsiProp.GetValue(null)
        let argProp = fsiObj.GetType().GetProperty("CommandLineArgs")
        argProp.GetValue(fsiObj) :?> string[]
    )

[<AutoOpen>]
module internal Patterns =

    let inline (|ExprValue|) value = Expr.Value(value)

[<AutoOpen>]
module internal Async =

    let ignoreAsync item = async { let! _ = item in () }

    type Task<'t> with
        member task.AsAsync = Async.AwaitTask(task)

    type Task with
        member task.AsAsync = Async.AwaitTask(task)

module internal Map =
    let keys map =
        map
        |> Map.toSeq
        |> Seq.map fst
        |> Seq.toList

    let contains key value map =
        map
        |> Map.tryFind key
        |> Option.exists ((=) value)
