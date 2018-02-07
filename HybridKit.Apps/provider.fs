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

    let addToProvidedAsm (ty : ProvidedTypeDefinition) =
        if not ty.IsErased then
            let providedAsm =
                Path.Combine(config.TemporaryFolder, Path.ChangeExtension("hk_" + Path.GetRandomFileName(), "dll"))
                |> ProvidedAssembly
            providedAsm.AddTypes([ty])

    do
        // First evaluate our launch environment
        let isRepl = FSI.isRepl config.IsHostedExecution
        let warnOnNewBindings = isRepl && not config.IsInvalidationSupported
        let target =
            if config.IsHostedExecution && not isRepl then
                match Cli.parseArgs<TargetKind>() with
                | Some(target) when target <> DebugServer ->
                    Build.createProjectAndExit target
                | _ ->
                    Target(config, DebugServer)
            else
                Target(config)

        if config.IsInvalidationSupported then
            invalidateTrigger.Triggered.Add(this.Invalidate)
        this.Disposing.Add(fun _ -> invalidateTrigger.Dispose())

        // Now construct the types
        let asm = Assembly.GetExecutingAssembly()

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
                | _ -> failwith "impossible"
                |> FS.resolvePath config.ResolutionFolder
                |> Path.GetFullPath
            lock (views) (fun () ->
                let viewProvider =
                    match views.TryGetValue(path) with
                    | true, vp -> vp
                    | _ ->
                        let vp = ViewTypeProvider(target, invalidateTrigger, path, warnOnNewBindings)
                        views.Add(path, vp)
                        vp
                let viewType = viewProvider.CreateViewType(asm, nameSpace, typeName)
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
