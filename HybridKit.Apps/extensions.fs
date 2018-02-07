namespace HybridKit.Apps.Markup

open System
open System.IO
open System.Text

type attributes = Map<string,Interpolated>

module internal Predicates =
    let endElementToken c = c = '/' || c = '>' || Char.IsWhiteSpace(c)

[<AutoOpen>]
module internal Extensions =

    let inline (|Attr|_|) name attrs = Map.tryFind name attrs

    let htmlEscape = function
    | '"' -> "&quot;"
    | '&' -> "&amp;"
    | '<' -> "&lt;"
    | '>' -> "&gt;"
    // FIXME: more?
    | chr -> string chr

    type TextWriter with

        member wr.WriteHtmlEscaped(str : string) =
            String.iter (htmlEscape >> wr.Write) str

        member wr.WriteAttrs(attrs : attributes, fn) =
            attrs |> Map.iter (fun k v ->
                wr.Write(' ')
                match v with
                | Empty ->  wr.Write(k)
                | Binding(TwoWay({ OmitAttribute = Some _ }), _, _, _) as value ->
                    fn wr value
                | value ->
                    wr.Write(k)
                    wr.Write("=\"")
                    fn wr value
                    wr.Write('"')
            )

    type TextReader with

        member tr.ConsumeUntil(pred) =
            let rec loop() =
                let next = tr.Peek()
                let chr = char next
                if next <> -1 && not(pred(chr)) then
                    tr.Read() |> ignore
                    loop()
            loop()
        member tr.ConsumeUntil(str : string) =
            let rec loop() =
                let mutable i = 0
                while i < str.Length && Char.ToLowerInvariant(char(tr.Peek())) = Char.ToLowerInvariant(str.[i]) do
                    ignore(tr.Read())
                    i <- i + 1
                if i < str.Length then
                    ignore(tr.Read())
                    loop()
            loop()

        member tr.ReadUntil(pred) =
            let buf = StringBuilder()
            let rec loop() =
                let next = tr.Peek()
                let chr = char next
                if next <> -1 && not(pred(chr)) then
                    buf.Append(chr) |> ignore
                    tr.Read() |> ignore
                    loop()
            loop()
            buf.ToString()
    
        member tr.ConsumeWhitespace() = tr.ConsumeUntil(Char.IsWhiteSpace >> not)
        member tr.ConsumeWhitespaceAnd(other) = tr.ConsumeUntil(fun c -> c <> other && not(Char.IsWhiteSpace(c)))
        member inline tr.ConsumeIf(chr) =
            if Char.ToLowerInvariant(char(tr.Peek())) = Char.ToLowerInvariant(chr) then
                ignore(tr.Read()); true
            else false
        member tr.ConsumeIf(str : string) =
            let mutable i = 0
            while i < str.Length && Char.ToLowerInvariant(char(tr.Peek())) = Char.ToLowerInvariant(str.[i]) do
                ignore(tr.Read())
                i <- i + 1
            i >= str.Length

        member tr.ReadAndHtmlUnescapeUntil(pred, bindingType : BindingType option) =
            let buf = StringBuilder()
            let rec loop() =
                let next = tr.Peek()
                let chr = char next
                if next = -1 || pred(chr) then
                    if buf.Length > 0 then Run(buf.ToString(), Empty) else Empty
                else
                    tr.Read() |> ignore
                    match chr with
                    | '&' ->
                        match char(tr.Peek()) with
                        | 'q' -> if tr.ConsumeIf "quot;" then buf.Append('"') |> ignore //"
                        | 'a' -> if tr.ConsumeIf "amp;"  then buf.Append('&') |> ignore
                        | 'l' -> if tr.ConsumeIf "lt;"   then buf.Append('<') |> ignore
                        | 'g' -> if tr.ConsumeIf "gt;"   then buf.Append('>') |> ignore
                        // FIXME: the rest of these
                        | c when Char.IsWhiteSpace(c) -> buf.Append('&') |> ignore
                        | _ -> ()
                        loop()
                    | '$' when char(tr.Peek()) = '{' -> readBinding Scalar
                    | '@' when char(tr.Peek()) = '{' -> readBinding Vector
                    | '{' when bindingType.IsSome    -> readBinding bindingType.Value
                    | _ ->
                        buf.Append(chr) |> ignore
                        loop()
            and readBinding bindingType =
                tr.ConsumeWhitespaceAnd('{')
                let endChr = function '}' | ':' | '<' -> true | _ -> false
                let endChrOrWs = fun c -> Char.IsWhiteSpace(c) || endChr c
                let name = tr.ReadUntil(endChrOrWs)
                tr.ConsumeWhitespace()
                let ty =
                    if char(tr.Peek()) = ':' then
                        tr.Read() |> ignore
                        tr.ConsumeWhitespace()
                        Some(tr.ReadUntil(endChrOrWs))
                    else
                        None
                tr.ConsumeUntil(endChr) // consume any whitespace
                tr.ConsumeIf '}' |> ignore
                if buf.Length > 0 then
                    let text = buf.ToString()
                    buf.Clear() |> ignore
                    Run(text, Binding(bindingType, name, ty, loop()))
                else
                    Binding(bindingType, name, ty, loop()) 
            loop()

        member tr.ReadHtmlQuotedString(bindingType) =
            if tr.ConsumeIf '"' then // "
                let result = tr.ReadAndHtmlUnescapeUntil((=) '"', None) // don't allow TwoWay or Function bindings if string is quoted
                tr.ConsumeIf '"' |> ignore //"
                result
            else // sloppy HTML can have unquoted attributes, or it could be a TwoWay binding
                tr.ReadAndHtmlUnescapeUntil(Predicates.endElementToken, bindingType)
