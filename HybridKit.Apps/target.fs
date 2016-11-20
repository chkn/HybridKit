namespace HybridKit.Apps

open System
open System.Reflection

open FSharp.Quotations
open FSharp.Core.CompilerServices

open ProviderImplementation
open ProviderImplementation.ProvidedTypes

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
        // app.Run
        let makeRunMethod args =
            let run = ProvidedMethod("Run", args, typeof<Void>)
            run.SetMethodAttrs(MethodAttributes.Public)
            run.InvokeCode <- target.GetAppRunExpr
            ty.AddMember(run)
        makeRunMethod [ProvidedParameter("view", typeof<IHtmlView>)]
        makeRunMethod [ProvidedParameter("router", typeof<Router>)]
        let router =
            match args with
            | [view] when view.Type = typeof<IHtmlView> -> <@@ fun (_:string) -> %%view : IHtmlView @@>
            | [router] -> router
            | _ -> failwith "Unexpected arg count!"

    member this.GetAppRunExpr(args : Expr list) =
        
        match this.Kind with
        | Debug ->
            let newApp = Expr.NewObject(appCtor, [ Expr.Value(config.ResolutionFolder) ])
            <@@ (%%newApp : App).Run(%%router) @@>
        | Android -> <@@ () @@>
        | _ -> failwith "not yet"
