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
    let invalidateTrigger = new RateLimitedTrigger()
    let views = Dictionary<string,ViewTypeProvider>() // LOCK!
    do
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
                    if Path.IsPathRooted(path) then path else Path.Combine(config.ResolutionFolder, path)
                    |> Path.GetFullPath
                lock (views) (fun () ->
                    match views.TryGetValue(path) with
                    | true, vp -> vp.ProvidedType
                    | _ ->
                        let vp = ViewTypeProvider(target, invalidateTrigger, asm, nameSpace, typeName, path)
                        views.Add(path, vp)
                        addToProvidedAsm vp.ProvidedType
                        vp.ProvidedType
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
