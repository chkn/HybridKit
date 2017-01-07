namespace HybridKit.Apps

open HybridKit
open HybridKit.Apps.Cli

open System
open System.IO
open System.Reflection

open FSharp.Quotations
open FSharp.Core.CompilerServices

open ProviderImplementation
open ProviderImplementation.ProvidedTypes

[<AutoOpen>]
module internal Names =
    // Assembly names
    let [<Literal>] XamariniOS       = "Xamarin.iOS"
    let [<Literal>] HybridKitiOS     = "HybridKit.iOS"
    let [<Literal>] XamarinAndroid   = "Mono.Android"
    let [<Literal>] HybridKitAndroid = "HybridKit.Android"

    // Class names
    let [<Literal>] AndroidActivity  = "hybridkit.android.App"

type TargetKind =
    | [<Arg("Runs a debug server that listens locally (default, not for production use)", "server")>] DebugServer
    | [<Arg("Creates a Xamarin.Android app project")>] Android
    override this.ToString() =
        match this with
        | DebugServer -> "Server"
        | Android -> "Android"
    static member FromAssemblyName(an : AssemblyName) =
        match an.Name with
        (*
        | XamariniOS
        | HybridKitiOS -> Some Ios
        *)
        | XamarinAndroid
        | HybridKitAndroid -> Some Android
        | _ -> None

type Target(config : TypeProviderConfig, ?kind) =
    static let debugServerAppCtor = typeof<DebugServerApp>.GetConstructor([| typeof<string>; typeof<bool> |])

    // Try to determine what platform we're targeting by looking at the referenced assemblies.
    //  If none of our other targets are specified then become a debug server.
    let assemblyNames = Array.map AssemblyName.GetAssemblyName config.ReferencedAssemblies
    let kind = defaultArg kind (defaultArg (Array.tryPick TargetKind.FromAssemblyName assemblyNames) DebugServer)

    // Assembly Loading
    let mutable lastAsmRequest = None // KLUDGE!!!!
    let resolveHandler = ResolveEventHandler(fun _ evt -> 
        let refAn = AssemblyName(evt.Name)
        let asm =
            assemblyNames
            |> Array.tryFind (fun an -> AssemblyName.ReferenceMatchesDefinition(refAn, an))
            |> Option.map (fun an -> Assembly.LoadFrom(an.CodeBase))
            |> function
            | Some asm -> asm
            | _ -> // As a fallback, try to load from the same path as requesting assembly
                let curAsm = if isNull evt.RequestingAssembly && lastAsmRequest.IsSome then lastAsmRequest.Value else evt.RequestingAssembly
                match curAsm with
                | reqAsm when not(isNull reqAsm) && not(isNull reqAsm.Location) ->
                    let path = Path.Combine(Path.GetDirectoryName(reqAsm.Location), refAn.Name + ".dll")
                    if File.Exists(path) then Assembly.LoadFrom(path) else
                    // Also try ../v1.0 dir as a kludge for Java.Interop
                    let path = Path.Combine(Path.GetDirectoryName(reqAsm.Location), "..", "v1.0", refAn.Name + ".dll")
                    if File.Exists(path) then Assembly.LoadFrom(path) else
                    // Also try ../v1.0/Facades dir as another kludge for Android
                    let path = Path.Combine(Path.GetDirectoryName(reqAsm.Location), "..", "v1.0", "Facades", refAn.Name + ".dll")
                    if File.Exists(path) then Assembly.LoadFrom(path) else
                    null
                | _ -> null
        lastAsmRequest <- Option.ofObj(asm)
        asm
    )
    let load name fn =
        let asmPath =
            assemblyNames
            |> Array.tryPick (fun an -> if an.Name = name then Some an.CodeBase else None)
        match asmPath with
        | None -> failwithf "Missing assembly reference: %s" name
        | Some asmPath ->
            AppDomain.CurrentDomain.add_AssemblyResolve(resolveHandler)
            try
                let asm = Assembly.LoadFrom(asmPath)
                fn asm
            finally
                AppDomain.CurrentDomain.remove_AssemblyResolve(resolveHandler)

    // Load type info needed to create App type
    let appBaseType, registerType =
        match kind with
        | DebugServer -> typeof<DebugServerApp>, None
        | Android ->
            load HybridKitAndroid (fun asm -> asm.GetType("HybridKit.Apps.App")),
            load XamarinAndroid   (fun asm -> Some(asm.GetType("Android.Runtime.RegisterAttribute")))
    let baseOnRunMethod =
        appBaseType.GetMethod("OnRun", BindingFlags.Public ||| BindingFlags.NonPublic ||| BindingFlags.Instance)
    let controllerMethods =
        appBaseType.GetMethods(BindingFlags.Public ||| BindingFlags.Instance ||| BindingFlags.DeclaredOnly)
        |> Seq.filter (fun m -> m.Name = "SetController")

    member __.Kind = kind

    member __.CreateAppType(asm, nameSpace, name) =
        let ty = ProvidedTypeDefinition(asm, nameSpace, name, Some appBaseType, IsErased = false)
        // add register attr if needed
        match registerType with
        | Some registerType ->
            let cdata = {
                new CustomAttributeData() with
                    override __.Constructor = registerType.GetConstructor([| typeof<string> |])
                    override __.ConstructorArguments = [| CustomAttributeTypedArgument(Names.AndroidActivity) |] :> _
                    override __.NamedArguments = [||] :> _
            }
            ty.AddCustomAttribute(cdata)
        | _ -> ()
        // ctor
        let ctor =
            match kind with
            | DebugServer ->
                let args = [ProvidedParameter("basePath", typeof<string>); ProvidedParameter("isFsi", typeof<bool>)]
                ProvidedConstructor(args, BaseConstructorCall = (fun args -> debugServerAppCtor, args))
            | Android ->
                ProvidedConstructor([], BaseConstructorCall = (fun args -> appBaseType.GetConstructor([||]), args))
        ctor.InvokeCode <- fun _ -> <@@ () @@>
        ty.AddMember(ctor)

        // fields for the various controller types
        let controllerFields =
            controllerMethods
            |> Seq.map (fun m ->
                let p = m.GetParameters().[0]
                let pf = ProvidedField(p.Name, p.ParameterType)
                pf.SetFieldAttributes(FieldAttributes.Private ||| FieldAttributes.Static)
                m, pf)
            |> Seq.toList
        for _, fld in controllerFields do ty.AddMember(fld)

        // app.OnRun
        let onRun = ProvidedMethod("OnRun", [], typeof<Void>)
        onRun.SetMethodAttrs((baseOnRunMethod.Attributes &&& MethodAttributes.MemberAccessMask) ||| MethodAttributes.Virtual ||| MethodAttributes.HideBySig)
        onRun.InvokeCode <- fun args ->
            let this =
                match args with
                | [this] -> this
                | _ -> failwithf "Unexpected args: %A" args
            let rec checkFld = function
            | [] -> <@@ failwith<unit> "App.Run was never called" @@>
            | (mi : MethodInfo, fld) :: lst ->
                let fldVal = Expr.FieldGet(fld)
                let miCall = Expr.Call(Expr.Coerce(this, mi.DeclaringType), mi, [fldVal])
                let fldVal = Expr.Coerce(fldVal, typeof<obj>)
                Expr.IfThenElse(<@@ not(isNull %%fldVal) @@>, miCall, checkFld lst)
            checkFld controllerFields
        ty.DefineMethodOverride(onRun, baseOnRunMethod)
        ty.AddMember(onRun)

        // App.Run
        let makeRunMethod(mi : MethodInfo, fld : ProvidedField) =
            let args =
                mi.GetParameters()
                |> Seq.map (fun p -> ProvidedParameter(p.Name, p.ParameterType))
                |> Seq.toList
            let run = ProvidedMethod("Run", args, typeof<Void>)
            run.SetMethodAttrs(MethodAttributes.Public ||| MethodAttributes.Static)
            run.InvokeCode <- fun args ->
                let storeArgs =
                    match args with
                    | [ctrlr] -> Expr.FieldSet(fld, ctrlr)
                    | _ -> failwith "Unexpected args!"
                let platInit =
                    match kind with
                    | DebugServer -> Expr.NewObject(ctor, [ Expr.Value(config.ResolutionFolder); Expr.Value(config.IsHostedExecution) ])
                    | Android -> <@@ () @@>
                Expr.Sequential(storeArgs, platInit)
            ty.AddMember(run)
        List.iter makeRunMethod controllerFields
        ty
