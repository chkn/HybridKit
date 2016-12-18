namespace HybridKit.Apps

open HybridKit.Apps.Markup

open System
open System.IO
open System.Reflection
open System.Diagnostics

open FSharp.Quotations

open ProviderImplementation
open ProviderImplementation.ProvidedTypes

type internal TypeName = TypeName of string * Type

type internal Types = {
    Types : TypeName list
    } with
    member this.GetType(name : string) = List.tryPick (function TypeName(nm, ty) when name = nm -> Some ty | _ -> None) this.Types
    member this.GetName(typ : Type) = List.tryPick (function TypeName(nm, ty) when typ = ty -> Some nm | _ -> None) this.Types

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module internal Types =
    let inline (|NamedType|_|) (types : Types) nm = types.GetType(nm)
    let inline add typ types = { types with Types = typ :: types.Types }
    let inline ofList types = { Types = types }
    let defaultTypes =
        ofList [
                TypeName("int", typeof<int>)
                TypeName("int64", typeof<int64>)
                TypeName("string", typeof<string>)
                TypeName("bool", typeof<bool>)
                TypeName("float", typeof<float>)
                TypeName("float32", typeof<float32>)
                TypeName("byte", typeof<byte>)
                TypeName("sbyte", typeof<sbyte>)
        ]

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

    // We need to wrap these because they're protected
    //  (can't be used from lambda otherwise)
    member private this.OnFileChanged() = this.Reload()

    override __.ToString() = filePath
    override this.RenderHtml(writer) =
        let html = Tree.fromMarkupFile "html" filePath
                   |> Tree.visit (Binding.createBindingElements)
                   |> toMarkup
        writer.Write(html)

type ViewTypeProvider(target : Target, invalidate : ITrigger, filePath) =
    static let htmlViewCtor =
        typeof<HtmlView>.GetConstructor(BindingFlags.Public ||| BindingFlags.NonPublic ||| BindingFlags.Instance, null, Type.EmptyTypes, null)
    static let debugHtmlViewCtor =
        typeof<DebugHtmlView>.GetConstructor([| typeof<string> |])
    static let renderMethod =
        typeof<HtmlView>.GetMethod("RenderHtml", BindingFlags.NonPublic ||| BindingFlags.Instance)

    let mutable bindings = []
    let mutable lastCreatedType = Unchecked.defaultof<_>

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
        let (Tree(_, root)) = Tree.fromMarkupFile "html" filePath
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

    let createType asm nameSpace name =
        let baseType, baseCtorCall =
            match target.Kind with
            | DebugServer -> typeof<DebugHtmlView>, (fun args -> debugHtmlViewCtor, args @ [Expr.Value(filePath)])
            | Android -> typeof<HtmlView>, (fun args -> htmlViewCtor, args)

        let ty = ProvidedTypeDefinition(asm, nameSpace, name, Some baseType, IsErased = false)
        let ctor = ProvidedConstructor(List.empty)
        ctor.BaseConstructorCall <- baseCtorCall
        ctor.InvokeCode <- fun _ -> <@@ () @@>
        ty.AddMember(ctor)

        // FIXME: Find subtypes
        let types = Types.defaultTypes

        // Binding members
        bindings
        |> List.iter (function
        | Binding(name, bty, _) ->
            let bty = defaultArg (Option.bind(types.GetType) bty) typeof<string> // FIXME: Base on context?
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
        | DebugServer -> ()
        | Android ->
            let render = ProvidedMethod("RenderHtml", [ProvidedParameter("writer", typeof<TextWriter>)], typeof<Void>)
            render.SetMethodAttrs(MethodAttributes.Family ||| MethodAttributes.HideBySig ||| MethodAttributes.Virtual ||| MethodAttributes.Final)
            render.InvokeCode <- fun [this; w] ->
                let htmlView = Expr.Coerce(this, typeof<HtmlView>)
                let rec walkIStr = function
                | Empty -> <@@ () @@>
                | Run(str, next) ->
                    let str = Expr.Value(String.collect htmlEscape str)
                    Expr.Sequential(<@@ (%%w : TextWriter).Write(%%str : string) @@>, walkIStr next)
                | Binding(ExprValue(name), _, next) ->
                    <@@ (%%w : TextWriter).Write((%%htmlView : HtmlView).GetBinding(%%name)) @@>
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
                                | value -> Expr.Sequential(Expr.Sequential(<@@ (%%w : TextWriter).Write("=\"") @@>, walkIStr value), <@@ (%%w : TextWriter).Write('"') @@>)
                            )
                        )
                    )
                let rec walkNode = function
                | Text(istr) -> walkIStr istr
                | Elem(name, attrs, children) ->
                    let opening = Expr.Value("<" + name)
                    let closing = Expr.Value("</" + name + ">")
                    Expr.Sequential(
                        Expr.Sequential(
                            Expr.Sequential(<@@ (%%w : TextWriter).Write(%%opening : string) @@>, walkAttrs attrs),
                            Expr.Sequential(<@@ (%%w : TextWriter).Write('>') @@>, List.fold (fun e c -> Expr.Sequential(e, walkNode c)) <@@ () @@> children)
                        ),
                        <@@ (%%w : TextWriter).Write(%%closing : string) @@>
                    )
                let (Tree(_, root)) =
                    Tree.fromMarkupFile "html" filePath
                    |> Tree.visit (Binding.createBindingElements)
                walkNode root
            ty.DefineMethodOverride(render, renderMethod)
            ty.AddMember(render)
        ty

    member __.CreateViewType(asm, nameSpace, name) =
        lastCreatedType <- createType asm nameSpace name
        lastCreatedType

    member __.LastCreatedType = lastCreatedType