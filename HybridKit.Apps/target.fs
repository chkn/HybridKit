namespace HybridKit.Apps

open HybridKit

open System
open System.Reflection

open FSharp.Quotations
open FSharp.Core.CompilerServices

open ProviderImplementation
open ProviderImplementation.ProvidedTypes

type Router = string -> HtmlView

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
    static let debugAppCtor = typeof<DebugApp>.GetConstructor([| typeof<string> |])

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
        | Debug -> typeof<DebugApp>
        | Ios -> failwith "Not yet"
        | Android -> load HybridKitAndroid (fun asm -> asm.GetType("HybridKit.Apps.App"))
    let baseOnRunMethod =
        appBaseType.GetMethod("OnRun", BindingFlags.Public ||| BindingFlags.NonPublic ||| BindingFlags.Instance)

    member __.Kind = kind

    member __.CreateAppType(asm, nameSpace, name) =
        let ty = ProvidedTypeDefinition(asm, nameSpace, name, Some appBaseType, IsErased = false)
        // make a field to hold the router passed to App.Run
        let routerField = ProvidedField("router", typeof<Router>)
        routerField.SetFieldAttributes(FieldAttributes.Private ||| FieldAttributes.Static)
        ty.AddMember(routerField)
        // ctor
        let ctor =
            match kind with
            | Debug ->
                let args = [ProvidedParameter("basePath", typeof<string>)]
                ProvidedConstructor(args, BaseConstructorCall = (fun args -> debugAppCtor, args))
            | _ -> failwith "not yet"
        ctor.InvokeCode <- fun _ -> <@@ () @@>
        ty.AddMember(ctor)
        // App.Run
        let makeRunMethod args =
            let run = ProvidedMethod("Run", args, typeof<Void>)
            run.SetMethodAttrs(MethodAttributes.Public ||| MethodAttributes.Static)
            run.InvokeCode <- fun args ->
                let router =
                    match args with
                    | [view] when view.Type = typeof<HtmlView> -> <@@ fun (_:string) -> %%view : HtmlView @@>
                    | [router] -> router
                    | _ -> failwith "Unexpected arg count!"
                let platInit =
                    match kind with
                    | Debug -> Expr.NewObject(ctor, [ Expr.Value(config.ResolutionFolder) ])
                    | _ -> <@@ () @@>
                Expr.Sequential(Expr.FieldSet(routerField, router), platInit)
            ty.AddMember(run)
        makeRunMethod [ProvidedParameter("view", typeof<HtmlView>)]
        makeRunMethod [ProvidedParameter("router", typeof<Router>)]
        // app.OnRun
        let args = [ProvidedParameter("webView", typeof<IWebView>)]
        let onRun = ProvidedMethod("OnRun", args, typeof<Void>)
        onRun.SetMethodAttrs((baseOnRunMethod.Attributes &&& MethodAttributes.MemberAccessMask) ||| MethodAttributes.Virtual ||| MethodAttributes.HideBySig)
        onRun.InvokeCode <- fun args ->
            let webView =
                match args with
                | [_; arg] when arg.Type = typeof<IWebView> -> arg
                | _ -> failwith "Unexpected args!"
            let router = Expr.FieldGet(routerField)
            <@@
                let view = (%%router : Router) "/"
                view.Show(%%webView)
            @@>
        ty.DefineMethodOverride(onRun, baseOnRunMethod)    
        ty.AddMember(onRun)
        ty
