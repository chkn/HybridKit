namespace HybridKit.Apps.Markup

open System
open System.IO
open System.Text
open System.Collections.Generic

type attributes = Map<string,InterpolatedString>

type Node =
    | Elem of string * attributes * Node list
    | Text of InterpolatedString

type Declaration =
    | Doctype of string
    | XML of attributes

type Tree = Tree of Declaration option * root:Node

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module Tree =

    // FIXME: Fully implement escape/unescape

    type StringBuilder with
        member buf.AppendAttrs(attrs : attributes, fn) =
            attrs |> Map.iter (fun k v ->
                    buf.Append(' ').Append(k) |> ignore
                    match v with
                    | Empty -> ()
                    | value -> buf.Append("=\"").AppendHtmlEscaped(fn value).Append('"') |> ignore //"
            )
            buf

    let visit (fn : Node -> Node list option) (Tree(decl, root)) =
        let rec visitNode node =
            match fn node with
            | Some replacement -> replacement
            | _ -> match node with
                   | Elem(name, attrs, children) -> [Elem(name, attrs, List.collect visitNode children)]
                   | other -> [other]
        Tree(decl, List.exactlyOne(visitNode root))

    /// Converts the Tree to markup using a custom interpolator
    let rec toMarkupI (fn : InterpolatedString -> string) (Tree(decl, root)) =
        let buf = StringBuilder()
        match decl with
        | None -> ()
        | Some(Doctype doctype) -> buf.Append("<!DOCTYPE ").Append(doctype).AppendLine(">") |> ignore
        | Some(XML attrs) -> buf.Append("<?xml").AppendAttrs(attrs, fn).AppendLine("?>") |> ignore
        let rec iter = function
        | Text str -> buf.AppendHtmlEscaped(fn str) |> ignore
        | Elem (name, attrs, children) ->
            buf.Append('<').Append(name).AppendAttrs(attrs, fn).Append('>') |> ignore
            children |> Seq.iter iter
            buf.Append("</").Append(name).Append('>') |> ignore
        iter root
        buf.ToString()

    let toMarkup (fn : string -> string) = toMarkupI (fun istr -> istr.ToString(fn))

    let fromMarkupReader (root : string) (tr : TextReader) =
        let stack = Stack<Node>()

        let rec readAttrs (map : attributes) =
            tr.ConsumeWhitespace()
            match tr.Peek() with
            | -1 | 62(*'>'*) | 63(*'?'*) -> map
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
                let value = readAttrs Map.empty
                tr.ConsumeIf '?' |> ignore
                tr.ConsumeIf '>' |> ignore
                Some(XML value)
            | _ -> None

        let rec readTree() =
            tr.ConsumeWhitespace()
            match tr.Peek() with
            | -1        -> ()
            | 60(*'<'*) -> readElem(); readTree()
            | _         -> readText(); readTree()

        and readElem() =
            tr.ConsumeWhitespaceAnd '<'
            let closing = tr.ConsumeIf '/'
            tr.ConsumeWhitespace()
            let name = (tr.ReadUntil (fun c -> c = '>' || Char.IsWhiteSpace(c))).ToLowerInvariant()
            if not(String.IsNullOrEmpty(name)) then
                let attrs = readAttrs Map.empty
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
        readTree()

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
