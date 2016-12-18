module HybridKit.Apps.Cli

open System

open FSharp.Reflection

type ArgAttribute(desc : string, name : string option) =
    inherit Attribute()
    new(desc) = ArgAttribute(desc, None)
    new(desc, name : string) = ArgAttribute(desc, Some name)
    member __.Name = name
    member __.Desc = desc

let getArgs<'t>() =
    FSharpType.GetUnionCases(typeof<'t>)
    |> Seq.choose (fun case ->
        match case.GetCustomAttributes(typeof<ArgAttribute>) with
        | [| :? ArgAttribute as attr |] ->
            Some(defaultArg attr.Name (case.Name.ToLowerInvariant()), attr.Desc, FSharpValue.MakeUnion(case, [||]) :?> 't)
        | _ -> None
    )

let getArg<'t> (arg : string) =
    getArgs<'t>()
    |> Seq.tryPick (fun (name, _, value) -> if name = arg.ToLowerInvariant() then Some value else None) 

/// Prints command-line usage and exits. Must only be called if running in FSI.
let printUsageAndExit<'targets>() =
    let scriptName = FSI.commandLineArgs.Force().[0]
    printfn "HybridKit App Command Line Interface"
    printfn "Usage: %s %s [target]" FSI.commandName scriptName
    printfn "%sWhere target is:" Environment.NewLine
    for name, desc, _ in getArgs<'targets>() do
        printfn "\t%s - %s" name desc

    printfn "%sBecause it is the default target, you can start the server and drop into a REPL with:" Environment.NewLine
    printfn "%s --use:%s" FSI.commandName scriptName

    Environment.Exit(1)

/// Parses command-line arguments. Must only be called if running in FSI.
let parseArgs<'targets>() =
    let args = FSI.commandLineArgs.Force()
    let target =
        if args.Length > 1 then
            match getArg<'targets> args.[1] with
            | None -> printUsageAndExit<'targets>(); None
            | some -> some
        else None
    target
