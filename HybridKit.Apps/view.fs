namespace HybridKit.Apps

open HybridKit.Apps.Markup

open System
open System.IO
open System.Reflection
open System.Diagnostics
open System.Collections.Generic

open FSharp.Quotations

open ProviderImplementation
open ProviderImplementation.ProvidedTypes

module internal View =
    let getBindingCtor (TypedBinding(bty, name, pty)) =
        let ctorWith (args : obj array) (ty : Type) =
            Some(ty.GetConstructor(Array.map (fun _ -> typeof<string>) args), args)
        match bty, pty with
        | Scalar, ConcreteType ty -> typedefof<ScalarBinding<_>>.MakeGenericType(ty) |> ctorWith [| name |]
        | Vector, ConcreteType ty -> typedefof<VectorBinding<_>>.MakeGenericType(ty) |> ctorWith [| name |]
        | TwoWay { OmitAttribute = attr }, ConcreteType ty ->
            typedefof<TwoWayBinding<_>>.MakeGenericType(ty) |> ctorWith [| name; (if attr.IsSome then attr.Value else null) |]
        | _, WildcardType -> None

type DebugHtmlView(filePath : string, warnOnNewBindings : bool) as this =
    inherit HtmlView()
    let reload = new RateLimitedTrigger()

    let types = Types.defaultTypes // FIXME
    let mutable bindings = []
    let loadBindings firstLoad =
        lock reload (fun _ ->
            let bindings =
                Tree.fromMarkupFile "html" filePath
                |> Binding.collectInTree
                |> Binding.computeTypes types
                |> Seq.map (fun (TypedBinding(_, name, _) as binding) -> name, View.getBindingCtor binding)
                |> Map.ofSeq
            // Remove old bindings that no longer exist
            this.Bindings
            |> Seq.filter (fun binding -> not(Map.containsKey binding.Name bindings))
            |> Seq.toList // copy to list so we can modify dictionary
            |> List.iter (this.Bindings.Remove)
            // Figure out the bindings that were added
            let newBindings =
                this.Bindings
                |> Seq.fold (fun map binding -> Map.remove binding.Name map) bindings
            if warnOnNewBindings && not firstLoad && not(Map.isEmpty newBindings) then
                printfn "WARN: The following bindings have been added to view `%s' :" (this.GetType().Name)
                printfn "\t%A" (Map.keys newBindings)
                printfn "Type provider invalidation is not supported in this session."
                printfn "The new properties will not be available until the view class"
                printfn "declaration and instantiation is re-evaluated."
            // Add new bindings
            newBindings
            |> Map.iter (fun name ty ->
                match ty with
                | None -> ()
                | Some(ctor, args) ->
                    let binding = ctor.Invoke(args)
                    this.Bindings.Add(binding :?> IBinding)
            )
        )

    // Runtime watcher to refresh debug server when file is updated..
    let watcher = FS.createWatcher filePath
    do
        reload.Triggered.Add(this.OnFileChanged)
        watcher.Changed.Add(fun _ -> reload.Trigger())
        watcher.EnableRaisingEvents <- true
        loadBindings true

    member private this.OnFileChanged() =
        loadBindings false
        this.Update()

    override __.ToString() = filePath
    override this.WriteHtml(writer) =
        Tree.fromMarkupFile "html" filePath
        |> Tree.visit (Binding.createElements this.Bindings.GetId)
        |> Tree.toMarkupW (fun wr nm -> this.Bindings.[nm].WriteHtml(wr)) writer

type ViewTypeProvider(target : Target, invalidate : ITrigger, filePath : string, warnOnNewBindings : bool) =
    // reflect ctors
    static let htmlViewCtor =
        typeof<HtmlView>.GetConstructor(BindingFlags.Public ||| BindingFlags.NonPublic ||| BindingFlags.Instance, null, Type.EmptyTypes, null)
    static let debugHtmlViewCtor =
        typeof<DebugHtmlView>.GetConstructor([| typeof<string>; typeof<bool> |])

    // reflect methods
    static let writeMethod =
        typeof<HtmlView>.GetMethod("WriteHtml", BindingFlags.Public ||| BindingFlags.Instance)
    static let onRealizedMethod =
        typeof<HtmlView>.GetMethod("OnRealized", BindingFlags.Public ||| BindingFlags.Instance)

    let mutable bindings = []
    let mutable lastCreatedType = Unchecked.defaultof<_>

    let loadBindings() =
        Tree.fromMarkupFile "html" filePath
        |> Binding.collectInTree

    let localInvalidate = new RateLimitedTrigger()
    do
        localInvalidate.Triggered.Add(fun _ -> bindings <- loadBindings(); invalidate.Trigger())

    // Design-time watcher to invalidate the TP when the file is updated..
    let watcher = FS.createWatcher filePath
    do
        watcher.Changed.Add(fun _ -> localInvalidate.Trigger())
        watcher.EnableRaisingEvents <- true
        localInvalidate.Trigger()

    let createType asm nameSpace name =
        let baseType, baseCtorCall =
            match target.Kind with
            | DebugServer -> typeof<DebugHtmlView>, (fun args -> debugHtmlViewCtor, args@[ Expr.Value(filePath); Expr.Value(warnOnNewBindings) ])
            | Android -> typeof<HtmlView>, (fun args -> htmlViewCtor, args)

        let ty = ProvidedTypeDefinition(asm, nameSpace, name, Some baseType, IsErased = false)

        // FIXME: Find subtypes
        let types = Types.defaultTypes

        // The OnRealized override will be responsible for adding updaters
        (*
        let onRealized = ProvidedMethod("OnRealized", [ProvidedParameter("webView", typeof<IWebView>)], typeof<Void>)
        ty.DefineMethodOverride(onRealized, onRealizedMethod)
        ty.AddMember(onRealized)

        // Add an updater for each binding
        onRealized.InvokeCode <- fun [this; webView] ->
            let this = Expr.Coerce(this, typeof<HtmlView>)
            bindings
            |> List.fold (fun expr (BindingInTree(bty, name, pty, parent)) ->
                let updaterExpr =
                    match parent with
                    | Element(_, _) ->
                        <@@ ElementsUpdater.ForClassName(%%webView, (%%this : HtmlView).Bindings.GetId(name), ElementsUpdater.InnerHTML) @@>
                    | Attribute

            )
        *)
        let typedBindings = Binding.computeTypes types bindings

        let ctor = ProvidedConstructor(List.empty)
        ctor.BaseConstructorCall <- baseCtorCall
        ctor.InvokeCode <- fun args ->
            let this =
                match args with
                | [this] -> this
                | _ -> failwithf "Unexpected args: %A" args
            match target.Kind with
            | DebugServer -> <@@ () @@>
            | Android ->
                let htmlView = Expr.Coerce(this, typeof<HtmlView>)
                let (|NewBindingExpr|_|) (TypedBinding(_, name, _) as binding) =
                    match View.getBindingCtor binding with
                    | None -> None
                    | Some(ctor, args) ->
                        let args =
                            args
                            |> Seq.map Expr.Value
                            |> Seq.toList
                        Some(Expr.Coerce(Expr.NewObject(ctor, args), typeof<IBinding>))
                let rec addBinding = function
                | (NewBindingExpr(expr) :: lst) ->
                    <@@
                        (%%htmlView : HtmlView).Bindings.Add(%%expr)
                        %%(addBinding lst)
                    @@>
                | _ -> <@@ () @@>
                addBinding typedBindings
        ty.AddMember(ctor)

        // Add binding members
        typedBindings
        |> List.iter (fun (TypedBinding(_, name, pty)) ->
            match pty with
            | WildcardType -> ()
            | ConcreteType pty ->
                let prop = ProvidedProperty(name, pty)
                let nameExpr = Expr.Value(name)
                let getBinding this =
                    let htmlView = Expr.Coerce(this, typeof<HtmlView>)
                    let bty = typedefof<Binding<_>>.MakeGenericType(pty)
                    Expr.Coerce(<@@ (%%htmlView : HtmlView).Bindings.[%%nameExpr] @@>, bty), bty.GetProperty("Value")
                prop.GetterCode <- fun args ->
                    let this =
                        match args with
                        | [this] -> this
                        | _ -> failwithf "Unexpected args: %A" args
                    let binding, value = getBinding this
                    Expr.PropertyGet(binding, value)
                prop.SetterCode <- fun args ->
                    let this, v =
                        match args with
                        | [this; v] -> this, v
                        | _ -> failwithf "Unexpected args: %A" args
                    let binding, value = getBinding this
                    Expr.PropertySet(binding, value, v)
                ty.AddMember(prop)
        )

        // WriteHtml
        match target.Kind with
        | DebugServer -> ()
        | Android ->
            let write = ProvidedMethod("WriteHtml", [ProvidedParameter("writer", typeof<TextWriter>)], typeof<Void>)
            write.SetMethodAttrs(MethodAttributes.Public ||| MethodAttributes.HideBySig ||| MethodAttributes.Virtual)
            write.InvokeCode <- fun args ->
                let this, w =
                    match args with
                    | [this; w] -> this, w
                    | _ -> failwithf "Unexpected args: %A" args
                let htmlView = Expr.Coerce(this, typeof<HtmlView>)
                let rec walkIStr wrapBindings = function
                | Empty -> <@@ () @@>
                | Run(str, next) ->
                    let str = Expr.Value(String.collect htmlEscape str)
                    Expr.Sequential(<@@ (%%w : TextWriter).Write(%%str : string) @@>, walkIStr wrapBindings next)
                | Binding(_, ExprValue(name), _, next) ->
                    let bindingVal = <@@ (%%htmlView : HtmlView).Bindings.[%%name].WriteHtml(%%w) @@>
                    if wrapBindings then
                        <@@
                            (%%w : TextWriter).Write("<span class=\"")
                            (%%w : TextWriter).Write((%%htmlView : HtmlView).Bindings.GetId(%%name))
                            (%%w : TextWriter).Write("\">")
                            %%bindingVal
                            (%%w : TextWriter).Write("</span>")
                        @@>
                    else
                        bindingVal                    
                let walkAttrs =
                    <@@ () @@>
                    |> Map.fold (fun e (k : string) v ->
                        Expr.Sequential(e,
                            Expr.Sequential(
                                <@@
                                    (%%w : TextWriter).Write(' ')
                                    (%%w : TextWriter).Write(k)
                                @@>,
                                match v with
                                | Empty -> <@@ () @@>
                                | value -> Expr.Sequential(Expr.Sequential(<@@ (%%w : TextWriter).Write("=\"") @@>, walkIStr false value), <@@ (%%w : TextWriter).Write('"') @@>)
                            )
                        )
                    )
                let rec walkNode wrapBindings = function
                | Text(istr) -> walkIStr wrapBindings istr
                | Elem(name, attrs, children) ->
                    let opening = Expr.Value("<" + name)
                    let closing = Expr.Value("</" + name + ">")
                    Expr.Sequential(
                        Expr.Sequential(
                            Expr.Sequential(<@@ (%%w : TextWriter).Write(%%opening : string) @@>, walkAttrs attrs),
                            Expr.Sequential(<@@ (%%w : TextWriter).Write('>') @@>, List.fold (fun e c -> Expr.Sequential(e, walkNode (Binding.shouldWrapInElem name) c)) <@@ () @@> children)
                        ),
                        <@@ (%%w : TextWriter).Write(%%closing : string) @@>
                    )
                let (Tree(_, root)) = Tree.fromMarkupFile "html" filePath
                walkNode true root
            ty.DefineMethodOverride(write, writeMethod)
            ty.AddMember(write)
        ty

    member __.CreateViewType(asm, nameSpace, name) =
        lastCreatedType <- createType asm nameSpace name
        lastCreatedType

    member __.LastCreatedType = lastCreatedType