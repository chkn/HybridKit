namespace HybridKit.Apps.Markup

open System
open System.IO
open System.Text

type attributes = Map<string,InterpolatedString>

[<AutoOpen>]
module internal Extensions =

    let htmlEscape = function
    | '"' -> "&quot;"
    | '&' -> "&amp;"
    | '<' -> "&lt;"
    | '>' -> "&gt;"
    // FIXME: more?
    | chr -> string chr

    type StringBuilder with

        member buf.AppendHtmlEscaped(str : string) =
            String.iter (htmlEscape >> buf.Append >> ignore) str
            buf

        member buf.AppendAttrs(attrs : attributes, fn) =
            attrs |> Map.iter (fun k v ->
                    buf.Append(' ').Append(k) |> ignore
                    match v with
                    | Empty -> ()
                    | value -> buf.Append("=\"").AppendHtmlEscaped(fn value).Append('"') |> ignore //"
            )
            buf

        member buf.ToStringAndClear() =
            let result = buf.ToString()
            buf.Clear() |> ignore
            result

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

        member tr.ReadAndHtmlUnescapeUntil(pred) =
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
                        | _   -> () // FIXME: Silly people might just have an ampresand
                                     // that's meant to be passed through.
                        loop()
                    | '$' when char(tr.Peek()) = '{' ->
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
                            Run(buf.ToStringAndClear(), Binding(name, ty, loop()))
                        else
                            Binding(name, ty, loop())                            
                    | _ ->
                        buf.Append(chr) |> ignore
                        loop()
            loop()

        member tr.ReadHtmlQuotedString() =
            if tr.ConsumeIf '"' then // "
                let result = tr.ReadAndHtmlUnescapeUntil((=) '"') // "
                tr.ConsumeIf '"' |> ignore //"
                result
            else
                Empty
