namespace HybridKit.Apps.Markup

open System
open System.IO
open System.Text
open System.Collections.Generic

type Node =
    | Elem of string * attributes * Node list
    | Text of Interpolated

type Declaration =
    | Doctype of string
    | XML of string

type Tree =
    | Tree of Declaration option * root:Node
    /// Converts the Tree to markup using a custom interpolator
    member this.ToMarkup(wr : TextWriter, fn : TextWriter -> Interpolated -> unit) =
        let (Tree(decl, root)) = this
        match decl with
        | None -> ()
        | Some(Doctype doctype) ->
            wr.Write("<!DOCTYPE ")
            wr.Write(doctype)
            wr.WriteLine('>')
        | Some(XML xml) ->
            wr.Write("<?xml ")
            wr.Write(xml)
            wr.WriteLine("?>")
        let rec iter = function
        | Text str -> fn wr str
        | Elem (name, attrs, children) ->
            wr.Write('<')
            wr.Write(name)
            wr.WriteAttrs(attrs, fn)
            wr.Write('>')
            children |> List.iter iter
            wr.Write("</")
            wr.Write(name)
            wr.Write('>')
        iter root
    member this.ToMarkup(fn) =
        use sw = new StringWriter()
        this.ToMarkup(sw, fn)
        sw.ToString()

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module Tree =

    // FIXME: Fully implement escape/unescape

    type VisitResponse =
        | Ignore
        | Visit
        | Replace of Node list
    let visit fn (Tree(decl, root)) =
        let rec visitNode node =
            match fn node with
            | Ignore -> [node]
            | Replace replacement -> replacement
            | Visit ->
                match node with
                | Elem(name, attrs, children) -> [Elem(name, attrs, List.collect visitNode children)]
                | other -> [other]
        Tree(decl, List.exactlyOne(visitNode root))

    let toMarkupW (fn : TextWriter -> string -> unit) wr (tree : Tree) = tree.ToMarkup(wr, fun wr istr -> istr.ToString(wr, fn))
    let toMarkup (fn : string -> string) (tree : Tree) = tree.ToMarkup(fun tw istr -> istr.ToString(tw, fn))

    let fromMarkupReader (root : string) (tr : TextReader) =
        let stack = Stack<Node>()

        let rec readAttrs (map : attributes) =
            tr.ConsumeWhitespace()
            match tr.Peek() with
            | -1 | 62(*'>'*) | 63(*'?'*) | 47(*/*) -> map
            | _ ->
                let name = tr.ReadUntil (fun c -> c = '=' || c = '?' || Predicates.endElementToken c)
                tr.ConsumeWhitespace()
                let value =
                    let isOmission = tr.ConsumeIf '?'
                    if isOmission || tr.ConsumeIf '=' then
                        tr.ConsumeWhitespace()
                        tr.ReadHtmlQuotedString(Some { Property = name; OmitAttribute = if isOmission then Some name else None })
                    else
                        Empty
                map
                |> Map.add name value
                |> readAttrs

        let readDecl() =
            tr.ConsumeWhitespace()
            let ateBracket = tr.ConsumeIf '<'
            tr.ConsumeWhitespace()
            match char(tr.Peek()) with
            | '!' when tr.ConsumeIf "!doctype" ->
                tr.ConsumeWhitespace()
                let value = tr.ReadUntil((=) '>')
                tr.ConsumeIf '>' |> ignore
                Some(Doctype value), ateBracket
            | '?' when tr.ConsumeIf "?xml" ->
                let value = tr.ReadUntil(function '?' | '>' -> true | _ -> false)
                tr.ConsumeIf '?' |> ignore
                tr.ConsumeIf '>' |> ignore
                Some(XML value), ateBracket
            | _ -> None, ateBracket

        let rec readTree(decl, implicitOpenBrace) =
            tr.ConsumeWhitespace()
            match tr.Peek() with
            | -1 -> ()
            | 60(*'<'*) -> readElem(decl); readTree(decl, false)
            | _ ->
                if implicitOpenBrace then readElem(decl) else readText()
                readTree(decl, false)

        and readElem(decl) =
            tr.ConsumeWhitespaceAnd '<'
            let closing = tr.ConsumeIf '/'
            let comment = tr.ConsumeIf '!'
            // FIXME: ConsumeUntil implementation will not detect "--->"
            if comment then tr.ConsumeUntil("-->") else
            tr.ConsumeWhitespace()
            let name =
                let nm = tr.ReadUntil(Predicates.endElementToken)
                match decl with
                | Some(XML _) -> nm
                | _ -> nm.ToLowerInvariant()
            if not(String.IsNullOrEmpty(name)) then
                let attrs = readAttrs Map.empty
                tr.ConsumeIf '/' |> ignore
                tr.ConsumeIf '>' |> ignore
                if closing then
                    // pop back to opening tag and er'ybody under that gets parented there
                    let mutable loop = true
                    let mutable children = []
                    while loop && stack.Count <> 0 do
                        match stack.Pop() with
                        | Elem(n,a,_) as elem ->
                            if n = name then
                                Elem(n,a,children) |> stack.Push
                                loop <- false
                            else
                                children <- elem :: children
                        | node -> children <- node :: children
                    //if loop is still true, it means we have a closing tag w/o an opening one
                    //  handle this by ignoring it
                    if loop then
                        for child in children do
                            stack.Push(child)
                else
                    Elem(name, attrs, List.empty) |> stack.Push

        and readText() =
            //FIXME: Unescape things, etc.
            let value = tr.ReadAndHtmlUnescapeUntil ((=) '<', None)
            Text(value) |> stack.Push
            
        tr.ConsumeWhitespace()
        let decl = readDecl()
        readTree(decl)

        let root =
            match stack.Count with
            | 0 -> Elem(root, Map.empty, List.empty)
            | 1 -> stack.Pop()
            | _ ->
                let children = stack.ToArray()
                Array.Reverse children
                Elem(root, Map.empty, Array.toList children)

        Tree(fst decl, root)

    let fromMarkupString root str = using (new StringReader(str)) (fromMarkupReader root)
    let fromMarkupFile root filePath = using (FS.openShared filePath) (fromMarkupReader root)
