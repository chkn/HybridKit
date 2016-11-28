namespace HybridKit.Apps

open System
open System.IO
open System.Reflection
open System.Diagnostics

open FSharp.Quotations

open ProviderImplementation
open ProviderImplementation.ProvidedTypes

module internal FS =
    let createWatcher filePath =
        new FileSystemWatcher(Path.GetDirectoryName(filePath), Path.GetFileName(filePath), NotifyFilter = NotifyFilters.LastWrite)

    let openShared filePath =
        let stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite)
        new StreamReader(stream)

    let loadTree filePath =
        use reader = openShared filePath
        Tree.fromMarkupReader "html" reader

type DebugHtmlView(filePath : string) as this =
    inherit HtmlView()
    let reload = new RateLimitedTrigger()
    let toMarkup = Tree.toMarkup (this.GetBinding)

    // Runtime watcher to refresh debug server when file is updated..
    let watcher = FS.createWatcher filePath
    do
        reload.Triggered.Add(this.OnFileChanged)
        watcher.Changed.Add(fun _ -> reload.Trigger())
        watcher.EnableRaisingEvents <- true

    // We need to wrap Reload() because it is protected
    //  (can't be used from lambda otherwise)
    member private this.OnFileChanged() = this.Reload()

    override __.ToString() = filePath
    override this.RenderHtml(writer) =
        let tree = FS.loadTree filePath
        let html = toMarkup tree
        writer.Write(html)

type ViewTypeProvider(target : Target, invalidate : ITrigger, asm, nameSpace, name, filePath) =
    static let htmlViewCtor =
        typeof<HtmlView>.GetConstructor(BindingFlags.Public ||| BindingFlags.NonPublic ||| BindingFlags.Instance, null, Type.EmptyTypes, null)
    static let debugHtmlViewCtor =
        typeof<DebugHtmlView>.GetConstructor([| typeof<string> |])
    static let renderMethod =
        typeof<HtmlView>.GetMethod("RenderHtml", BindingFlags.NonPublic ||| BindingFlags.Instance)

    let mutable bindings = []
    let loadBindings() =
        let rec iter1 lst = function
        | Empty -> lst
        | Run(_, next) -> iter1 lst next
        | Binding(_, _, next) as binding -> iter1 (binding :: lst) next
        let rec iter2 lst = function
        | Text(v) -> iter1 lst v
        | Elem(_, attrs, children) ->
            let lst = Map.fold (fun lst _ v -> iter1 lst v) lst attrs
            Seq.fold iter2 lst children
        let (Tree(_, root)) = FS.loadTree filePath
        iter2 [] root
    let localInvalidate = new RateLimitedTrigger()
    do
        localInvalidate.Triggered.Add(fun _ -> bindings <- loadBindings(); invalidate.Trigger())

    // Design-time watcher to invalidate the TP when the file is updated..
    let watcher = FS.createWatcher filePath
    do
        watcher.Changed.Add(fun _ -> localInvalidate.Trigger())
        watcher.EnableRaisingEvents <- true
        localInvalidate.Trigger()

    let providedType =
        let baseType, baseCtorCall =
            match target.Kind with
            | Debug -> typeof<DebugHtmlView>, (fun args -> debugHtmlViewCtor, args @ [Expr.Value(filePath)])
            | _ -> typeof<HtmlView>, (fun args -> htmlViewCtor, args)
        let ty = ProvidedTypeDefinition(asm, nameSpace, name, Some baseType, IsErased = false)
        let ctor = ProvidedConstructor(List.empty)
        ctor.BaseConstructorCall <- baseCtorCall
        ctor.InvokeCode <- fun _ -> <@@ () @@>
        ty.AddMember(ctor)
        
        // Binding members
        bindings
        |> List.iter (function
        | Binding(name, bty, _) ->
            let bty = defaultArg bty typeof<string> // FIXME: Base on context?
            let prop = ProvidedProperty(name, bty)
            let nameExpr = Expr.Value(name)
            prop.GetterCode <- fun [this] ->
                let htmlView = Expr.Coerce(this, typeof<HtmlView>)
                <@@ (%%htmlView : HtmlView).GetBinding(%%nameExpr) @@>
            prop.SetterCode <- fun [this; v] ->
                let htmlView = Expr.Coerce(this, typeof<HtmlView>)
                let value = Expr.Coerce(v, typeof<obj>)
                <@@ (%%htmlView : HtmlView).SetBinding(%%nameExpr, %%value) @@>
            ty.AddMember(prop)
        | _ -> failwith "impossible"
        )

        // RenderHtml
        match target.Kind with
        | Debug -> ()
        | _ ->
            let render = ProvidedMethod("RenderHtml", [ProvidedParameter("writer", typeof<TextWriter>)], typeof<Void>)
            render.SetMethodAttrs(MethodAttributes.Family ||| MethodAttributes.HideBySig ||| MethodAttributes.Virtual ||| MethodAttributes.Final)
            render.InvokeCode <- fun args ->
                let this, writer =
                    match args with
                    | [arg0; arg1] when arg1.Type = typeof<TextWriter> -> arg0, arg1
                    | _ -> failwith "Unexpected args!"
                <@@ printfn "Would render in %A" (%%writer : TextWriter)  @@>
            ty.DefineMethodOverride(render, renderMethod)
            ty.AddMember(render)
        ty

    member __.ProvidedType = providedType
