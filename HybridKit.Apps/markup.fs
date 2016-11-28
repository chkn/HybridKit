namespace HybridKit.Apps

open System
open System.IO
open System.Text
open System.Collections.Generic

type attributes = Map<string,InterpolatedString>

type Node =
    | Elem of string * attributes * Node seq
    | Text of InterpolatedString

type Tree = Tree of doctype:string option * root:Node

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module Tree =

    // FIXME: Fully implement escape/unescape
    
    /// Converts the Tree to markup using a custom interpolator
    let rec toMarkupI (fn : InterpolatedString -> string) (Tree(doctype, root)) =
        let buf = StringBuilder()
        match doctype with
        | Some doctype -> buf.Append("<!DOCTYPE ").Append(doctype).AppendLine(">") |> ignore
        | _ -> ()
        let rec iter = function
        | Text str -> buf.AppendHtmlEscaped(fn str) |> ignore
        | Elem (name, attrs, children) ->
            buf.Append('<').Append(name) |> ignore
            attrs |> Map.iter (fun k v ->
                buf.Append(' ').Append(k) |> ignore
                match v with
                | Empty -> ()
                | value -> buf.Append("=\"").AppendHtmlEscaped(fn value).Append('"') |> ignore //"
            )
            buf.Append('>') |> ignore
            children |> Seq.iter iter
            buf.Append("</").Append(name).Append('>') |> ignore
        iter root
        buf.ToString()

    let toMarkup (fn : string -> string) = toMarkupI (fun istr -> istr.ToString(fn))

    let fromMarkupReader (root : string) (tr : TextReader) =
        let stack = Stack<Node>()

        let rec readTree() =
            tr.ConsumeWhitespace()
            match tr.Peek() with
            | -1        -> ()
            | 60(*'<'*) -> readElem(); readTree()
            | _         -> readText(); readTree()

        and readDoctype() =
            tr.ConsumeWhitespaceAnd '<'
            if tr.ConsumeIf "!doctype" then
                tr.ConsumeWhitespace()
                let value = tr.ReadUntil((=) '>')
                tr.ConsumeIf '>' |> ignore
                Some value
            else None
        and readElem() =
            tr.ConsumeWhitespaceAnd '<'
            let closing = tr.ConsumeIf '/'
            tr.ConsumeWhitespace()
            let name = (tr.ReadUntil (fun c -> c = '>' || Char.IsWhiteSpace(c))).ToLowerInvariant()
            if not(String.IsNullOrEmpty(name)) then
                let rec readAttrs (map : attributes) =
                    tr.ConsumeWhitespace()
                    match tr.Peek() with
                    | -1 | 62(*'>'*) -> map
                    | _ ->
                        let name = tr.ReadUntil (fun c -> c = '=' || c = '>' || Char.IsWhiteSpace(c))
                        tr.ConsumeWhitespace()
                        let value =
                            if tr.ConsumeIf '=' then
                                tr.ConsumeWhitespace()
                                tr.ReadHtmlQuotedString()
                            else
                                Empty
                        map.Add(name, value) |> readAttrs
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
                    Elem(name,attrs,Seq.empty) |> stack.Push

        and readText() =
            //FIXME: Unescape things, etc.
            let value = tr.ReadAndHtmlUnescapeUntil ((=) '<')
            Text(value) |> stack.Push
            
        tr.ConsumeWhitespace()
        let doctype = readDoctype()
        readTree()

        // If the stack isn't empty at this point, it means there was no
        //  root element, so create our implicit one
        let root =
            if stack.Count > 1 then
                let children = stack.ToArray()
                Array.Reverse children
                Elem(root,Map.empty,children)
            else
                stack.Pop()
        Tree(doctype, root)

    let fromMarkupString root str = using (new StringReader(str)) (fromMarkupReader root)
