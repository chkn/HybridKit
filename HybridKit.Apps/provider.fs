namespace HybridKit.Apps

open HybridKit.Apps.Build

open ProviderImplementation
open ProviderImplementation.ProvidedTypes

open System
open System.IO
open System.Reflection
open System.Collections.Generic

open FSharp.Core.CompilerServices

[<TypeProvider>]
type TypeProvider(config : TypeProviderConfig) as this =
    inherit TypeProviderForNamespaces()

    [<Literal>]
    static let nameSpace = "HybridKit"
    let views = Dictionary<string,ViewTypeProvider>() // LOCK!
    let invalidateTrigger = new RateLimitedTrigger(Delay = TimeSpan.Zero)

    do
        // First evaluate our launch environment
        let isRepl = FSI.isRepl config.IsHostedExecution
        let target =
            if config.IsHostedExecution && not isRepl then
                match Cli.parseArgs<TargetKind>() with
                | Some(target) when target <> DebugServer ->
                    Build.createProjectAndExit target
                | _ ->
                    Target(config, DebugServer)
            else
                Target(config)

        if not config.IsInvalidationSupported && isRepl then
            printfn "NOTE: Type provider invalidation is not supported in this session."
            printfn "You will need to re-evaluate HtmlView declarations and instantiations"
            printfn "if you add new bindings to the associated HTML files."
        else
            invalidateTrigger.Triggered.Add(this.Invalidate)
        this.Disposing.Add(fun _ -> invalidateTrigger.Dispose())

        let asm = Assembly.GetExecutingAssembly()
        let addToProvidedAsm (ty : ProvidedTypeDefinition) =
            if not ty.IsErased then
                let providedAsm =
                    Path.Combine(config.TemporaryFolder, Path.ChangeExtension("hk_" + Path.GetRandomFileName(), "dll"))
                    |> ProvidedAssembly
                providedAsm.AddTypes([ty])

        let appType =
            let ty = target.CreateAppType(asm, nameSpace, "NewApp")
            addToProvidedAsm ty
            ty

        let htmlViewType = ProvidedTypeDefinition(asm, nameSpace, "HtmlView", Some typeof<obj>, HideObjectMethods = true, IsErased = false)
        let pmtrs = [ ProvidedStaticParameter("fileNameOrPath", typeof<string>); ProvidedStaticParameter("fileName", typeof<string>, "") ]
        htmlViewType.DefineStaticParameters(pmtrs, fun typeName vals ->
            let path =
                match vals with
                | [| :? string as path; :? string as fileName |] -> Path.Combine(path, fileName)
                | [| :? string as path |] -> path
                | _ -> failwith "impossible"
                |> FS.resolvePath config.ResolutionFolder
                |> Path.GetFullPath
            lock (views) (fun () ->
                let viewType =
                    match views.TryGetValue(path) with
                    | true, vp -> vp.CreateViewType(asm, nameSpace, typeName)
                    | _ ->
                        let vp = ViewTypeProvider(target, invalidateTrigger, path)
                        views.Add(path, vp)
                        vp.CreateViewType(asm, nameSpace, typeName)
                addToProvidedAsm viewType
                viewType
            )
        )

        let types = [
            appType
            htmlViewType
        ]
        this.AddNamespace(nameSpace, types)

[<assembly:TypeProviderAssembly>]
do ()
