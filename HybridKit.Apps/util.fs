namespace global

open System
open System.IO

module internal FS =

    let resolvePath path basePath =
        if Path.IsPathRooted(path) then path else Path.Combine(basePath, path)

    let createWatcher filePath =
        new FileSystemWatcher(Path.GetDirectoryName(filePath), Path.GetFileName(filePath), NotifyFilter = NotifyFilters.LastWrite)

    let openShared filePath =
        let stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite)
        new StreamReader(stream)

module internal FSI =

    /// Returns `true` iff we are within F# interactive (as denoted by the argument),
    ///  AND it appears to be running in REPL mode (as opposed to run and quit --exec mode)
    let isRepl =
        // If any arg to fsi is not prefixed by '-' or '/', assume it is a script file name
        //  which would put us in --exec mode (whether or not --exec is actually passed)
        let hasExecArg = lazy (
            Environment.GetCommandLineArgs()
            |> Seq.skip 1
            |> Seq.takeWhile ((<>) "--")
            |> Seq.exists (fun arg ->
                match arg.[0] with
                | '-' | '/' -> false
                | _ -> true
            )
        )
        fun isFsi -> isFsi && not(hasExecArg.Force())
