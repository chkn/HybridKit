namespace HybridKit.Apps

open System
open System.Reflection

open FSharp.Quotations
open FSharp.Core.CompilerServices

open ProviderImplementation
open ProviderImplementation.ProvidedTypes

type Router = string -> IHtmlView

[<AutoOpen>]
module internal AssemblyNames =
    let [<Literal>] XamariniOS       = "Xamarin.iOS"
    let [<Literal>] HybridKitiOS     = "HybridKit.iOS"
    let [<Literal>] XamarinAndroid   = "Mono.Android"
    let [<Literal>] HybridKitAndroid = "HybridKit.Android"

type TargetKind =
    | Debug
    | Ios
    | Android
    static member FromAssemblyName(an : AssemblyName) =
        match an.Name with
        | XamariniOS
        | HybridKitiOS -> Some Ios
        | XamarinAndroid
        | HybridKitAndroid -> Some Android
        | _ -> None

type Target(config : TypeProviderConfig) =
    static let debugAppCtor = typeof<App>.GetConstructor([| typeof<string> |])

    let assemblyNames = Array.map AssemblyName.GetAssemblyName config.ReferencedAssemblies
    let kind = defaultArg (Array.tryPick TargetKind.FromAssemblyName assemblyNames) Debug

    // Assembly Loading
    let resolveHandler = ResolveEventHandler(fun _ evt -> 
        let refAn = AssemblyName(evt.Name)
        assemblyNames
        |> Array.tryFind (fun an -> AssemblyName.ReferenceMatchesDefinition(refAn, an))
        |> Option.map (fun an -> Assembly.ReflectionOnlyLoadFrom(an.CodeBase))
        |> Option.toObj
    )
    let load name fn =
        let asmPath =
            assemblyNames
            |> Array.tryPick (fun an -> if an.Name = name then Some an.CodeBase else None)
        match asmPath with
        | None -> failwithf "Missing assembly reference: %s" name
        | Some asmPath ->
            AppDomain.CurrentDomain.add_ReflectionOnlyAssemblyResolve(resolveHandler)
            try
                let asm = Assembly.ReflectionOnlyLoadFrom(asmPath)
                fn asm
            finally
                AppDomain.CurrentDomain.remove_ReflectionOnlyAssemblyResolve(resolveHandler)

    let appBaseType =
        match kind with
        | Debug -> typeof<App>
        | Ios -> failwith "Not yet"
        | Android -> load HybridKitAndroid (fun asm -> asm.GetType("HybridKit.Apps.App"))

    member __.Kind = kind
    member __.CreateAppType(asm, nameSpace, name) =
        let ty = ProvidedTypeDefinition(asm, nameSpace, name, Some appBaseType, IsErased = false)
        // ctor
        let ctor =
            match kind with
            | Debug ->
                let args = [ProvidedParameter("basePath", typeof<string>)]
                ProvidedConstructor(args, BaseConstructorCall = (fun args -> debugAppCtor, args))
            | _ -> failwith "not yet"
        ctor.InvokeCode <- fun _ -> <@@ () @@>
        ty.AddMember(ctor)
        // app.Run
        let makeRunMethod args =
            let run = ProvidedMethod("Run", args, typeof<Void>)
            run.SetMethodAttrs(MethodAttributes.Public ||| MethodAttributes.Static)
            run.InvokeCode <- fun _ -> Expr.NewObject(ctor, [ Expr.Value(config.ResolutionFolder) ])
            ty.AddMember(run)
        makeRunMethod [ProvidedParameter("view", typeof<IHtmlView>)]
        makeRunMethod [ProvidedParameter("router", typeof<Router>)]
        ty
        (*
        let router =
            match args with
            | [view] when view.Type = typeof<IHtmlView> -> <@@ fun (_:string) -> %%view : IHtmlView @@>
            | [router] -> router
            | _ -> failwith "Unexpected arg count!"
        *)