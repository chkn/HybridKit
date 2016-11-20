namespace HybridKit.Apps

open HybridKit

open System
open System.IO
open System.Reflection
open System.Diagnostics

open FSharp.Quotations
open FSharp.Core.CompilerServices

open ProviderImplementation
open ProviderImplementation.ProvidedTypes

[<TypeProvider>]
type TypeProvider (config : TypeProviderConfig) as this =
    inherit TypeProviderForNamespaces()

    static let [<Literal>] nameSpace = "HybridKit"
    static let objectCtor = typeof<obj>.GetConstructor(Type.EmptyTypes)
    static let scriptObjectCtor =
        typeof<ScriptObject>.GetConstructor(BindingFlags.Instance ||| BindingFlags.NonPublic, null, [| typeof<ScriptObject> |], null)
    static let showMethod = typeof<IHtmlView>.GetMethod("Show")

    do
        let target = Target(config)
        let asm = Assembly.GetExecutingAssembly()
        let makeProvidedAsm() =
            Path.Combine(config.TemporaryFolder, Path.ChangeExtension("hk_" + Path.GetRandomFileName(), "dll"))
            |> ProvidedAssembly

        let implementHtmlViewType typeName path =
            let ty = ProvidedTypeDefinition(asm, nameSpace, typeName, Some typeof<obj>, IsErased = false)
            ty.AddInterfaceImplementation(typeof<IHtmlView>)
            let ctor = ProvidedConstructor(List.empty)
            ctor.BaseConstructorCall <- fun args -> objectCtor, args
            ctor.InvokeCode <- fun _ -> <@@ () @@>
            ty.AddMember(ctor)

            let show = ProvidedMethod("IHtmlView.Show", [ProvidedParameter("webView", typeof<IWebView>)], typeof<Void>)
            show.SetMethodAttrs(MethodAttributes.Private ||| MethodAttributes.HideBySig |||
                MethodAttributes.NewSlot ||| MethodAttributes.Virtual ||| MethodAttributes.Final)
            show.InvokeCode <- fun _ -> <@@ () @@>
            ty.DefineMethodOverride(show, showMethod)
            ty.AddMember(show)

            makeProvidedAsm().AddTypes([ty])
            ty

        let appType =
            let ty = target.MakeAppType(asm, nameSpace, "NewApp")
            
            if not(ty.IsErased) then
                makeProvidedAsm().AddTypes([ty])
            ty

        let htmlViewType = ProvidedTypeDefinition(asm, nameSpace, "HtmlView", Some typeof<obj>, HideObjectMethods = true, IsErased = false)
        let param = ProvidedStaticParameter("fileName", typeof<string>)
        htmlViewType.DefineStaticParameters([param], fun typeName vals ->
            match vals with
            | [| :? string as path |] -> implementHtmlViewType typeName path
            | _ -> failwith "impossible"
        )

        let types = [
            appType
            htmlViewType
        ]
        this.AddNamespace(nameSpace, types)

[<assembly:TypeProviderAssembly>]
do ()
