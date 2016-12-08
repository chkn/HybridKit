namespace HybridKit.Apps

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
        if not config.IsInvalidationSupported && FSI.isRepl config.IsHostedExecution then
            printfn "NOTE: Type provider invalidation is not supported in this session."
            printfn "You will need to re-evaluate HtmlView declarations and instantiations"
            printfn "if you add new bindings to the associated HTML files."
        else
            invalidateTrigger.Triggered.Add(this.Invalidate)
        this.Disposing.Add(fun _ -> invalidateTrigger.Dispose())

        let target = Target(config)
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
        let param = ProvidedStaticParameter("fileName", typeof<string>)
        htmlViewType.DefineStaticParameters([param], fun typeName vals ->
            match vals with
            | [| :? string as path |] ->
                // Canonicalize path first
                let path =
                    config.ResolutionFolder
                    |> FS.resolvePath path
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
            | _ -> failwith "impossible"
        )

        let types = [
            appType
            htmlViewType
        ]
        this.AddNamespace(nameSpace, types)

[<assembly:TypeProviderAssembly>]
do ()
