namespace HybridKit.Apps.Markup

open System
open System.IO
open System.Text
open System.Collections.Generic

type Node =
    | Elem of string * attributes * Node list
    | Text of InterpolatedString

type Declaration =
    | Doctype of string
    | XML of string

type Tree = Tree of Declaration option * root:Node

type VisitResponse =
    | Ignore
    | Visit
    | Replace of Node list

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module Tree =

    // FIXME: Fully implement escape/unescape

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

    /// Converts the Tree to markup using a custom interpolator
    let rec toMarkupI (fn : InterpolatedString -> string) (Tree(decl, root)) =
        let buf = StringBuilder()
        match decl with
        | None -> ()
        | Some(Doctype doctype) -> buf.Append("<!DOCTYPE ").Append(doctype).AppendLine(">") |> ignore
        | Some(XML xml) -> buf.Append("<?xml ").Append(xml).AppendLine("?>") |> ignore
        let rec iter = function
        | Text str -> buf.AppendHtmlEscaped(fn str) |> ignore
        | Elem (name, attrs, children) ->
            buf.Append('<').Append(name).AppendAttrs(attrs, fn).Append('>') |> ignore
            children |> List.iter iter
            buf.Append("</").Append(name).Append('>') |> ignore
        iter root
        buf.ToString()

    let toMarkup (fn : string -> string) = toMarkupI (fun istr -> istr.ToString(fn))

    let fromMarkupReader (root : string) (tr : TextReader) =
        let stack = Stack<Node>()

        let rec readAttrs (map : attributes) =
            tr.ConsumeWhitespace()
            match tr.Peek() with
            | -1 | 62(*'>'*) | 63(*'?'*) | 47(*/*) -> map
            | _ ->
                let name = tr.ReadUntil (fun c -> c = '=' || c = '>' || Char.IsWhiteSpace(c))
                tr.ConsumeWhitespace()
                let value =
                    if tr.ConsumeIf '=' then
                        tr.ConsumeWhitespace()
                        tr.ReadHtmlQuotedString()
                    else
                        Empty
                map
                |> Map.add name value
                |> readAttrs

        let readDecl() =
            tr.ConsumeWhitespaceAnd '<'
            match char(tr.Peek()) with
            | '!' when tr.ConsumeIf "!doctype" ->
                tr.ConsumeWhitespace()
                let value = tr.ReadUntil((=) '>')
                tr.ConsumeIf '>' |> ignore
                Some(Doctype value)
            | '?' when tr.ConsumeIf "?xml" ->
                let value = tr.ReadUntil(function '?' | '>' -> true | _ -> false)
                tr.ConsumeIf '?' |> ignore
                tr.ConsumeIf '>' |> ignore
                Some(XML value)
            | _ -> None

        let rec readTree(decl) =
            tr.ConsumeWhitespace()
            match tr.Peek() with
            | -1        -> ()
            | 60(*'<'*) -> readElem(decl); readTree(decl)
            | _         -> readText(); readTree(decl)

        and readElem(decl) =
            tr.ConsumeWhitespaceAnd '<'
            let closing = tr.ConsumeIf '/'
            let comment = tr.ConsumeIf '!'
            // FIXME: ConsumeUntil implementation will not detect "--->"
            if comment then tr.ConsumeUntil("-->") else
            tr.ConsumeWhitespace()
            let name =
                let nm = tr.ReadUntil(fun c -> c = '>' || Char.IsWhiteSpace(c))
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
            let value = tr.ReadAndHtmlUnescapeUntil ((=) '<')
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

        Tree(decl, root)

    let fromMarkupString root str = using (new StringReader(str)) (fromMarkupReader root)
    let fromMarkupFile root filePath = using (FS.openShared filePath) (fromMarkupReader root)
