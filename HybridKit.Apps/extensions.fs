namespace HybridKit.Apps.Markup

open System
open System.IO
open System.Text

[<AutoOpen>]
module internal Extensions =

    type StringBuilder with

        member buf.AppendHtmlEscaped(str : string) =
            let mapping = function
            | '"' -> buf.Append("&quot;") |> ignore //"
            | '&' -> buf.Append("&amp;")  |> ignore
            | '<' -> buf.Append("&lt;")   |> ignore
            | '>' -> buf.Append("&gt;")   |> ignore
            | chr -> buf.Append(chr)      |> ignore
            String.iter mapping str
            buf

        member buf.ToStringAndClear() =
            let result = buf.ToString()
            buf.Clear() |> ignore
            result

    type TextReader with
    
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
    
        member tr.ConsumeWhitespace() = tr.ReadUntil(Char.IsWhiteSpace >> not) |> ignore
        member tr.ConsumeWhitespaceAnd(other) = tr.ReadUntil(fun c -> c <> other && not(Char.IsWhiteSpace(c))) |> ignore
        member inline tr.ConsumeIf(chr) =
            if Char.ToLowerInvariant(char(tr.Peek())) = Char.ToLowerInvariant(chr) then
                ignore(tr.Read()); true
            else false
        member tr.ConsumeIf(str : string) =
            let mutable i = 0
            while i < str.Length && Char.ToLowerInvariant(char(tr.Peek())) = Char.ToLowerInvariant(str.[i]) do
                tr.Read() |> ignore
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
                        let pred = fun c -> Char.IsWhiteSpace(c) || (match c with '}' | ':' | '<' -> true | _ -> false)
                        let name = tr.ReadUntil(pred)
                        tr.ConsumeWhitespace()
                        let ty =
                            if char(tr.Peek()) = ':' then
                                tr.Read() |> ignore
                                tr.ConsumeWhitespace()
                                Type.forAbbr(tr.ReadUntil(pred))
                            else
                                None
                        tr.ConsumeWhitespaceAnd '}' |> ignore
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
