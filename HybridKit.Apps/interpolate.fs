namespace HybridKit.Apps.Markup

open System
open System.Text

type InterpolatedString =
    | Empty
    | Run of string * InterpolatedString
    | Binding of name:string * typeName:string option * InterpolatedString

    // ToString with text substituted for bindings
    member this.ToString(sb : StringBuilder, fn : string -> string) =
        match this with
        | Empty -> ()
        | Run(str, next) -> next.ToString(sb.Append(str), fn)
        | Binding(name, _, next) -> next.ToString(sb.Append(fn name), fn)
    member this.ToString(fn : string -> string) = let sb = StringBuilder() in this.ToString(sb, fn); sb.ToString()

    // ToString with binding markers
    member this.ToString(sb : StringBuilder) =
        match this with
        | Empty -> ()
        | Run(str, next) -> next.ToString(sb.Append(str))
        | Binding(name, None, next) -> next.ToString(sb.Append("${").Append(name).Append('}'))
        | Binding(name, Some abbr, next) -> next.ToString(sb.Append("${").Append(name).Append(" : ").Append(abbr).Append("}"))
    override this.ToString() = let sb = StringBuilder() in this.ToString(sb); sb.ToString()
