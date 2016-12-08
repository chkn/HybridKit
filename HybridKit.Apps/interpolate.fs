namespace HybridKit.Apps.Markup

open System
open System.Text

module internal Type =
    let abbrList = [|
        "int", typeof<int>
        "int64", typeof<int64>
        "string", typeof<string>
        "bool", typeof<bool>
        "float", typeof<float>
        "float32", typeof<float32>
        "byte", typeof<byte>
        "sbyte", typeof<sbyte>
    |]
    let forAbbr str = Array.tryPick (function (abbr, ty) when abbr = str -> Some ty | _ -> None) abbrList
    let toAbbr ty = Array.tryPick (function  (abbr, ty2) when ty = ty2 -> Some abbr | _ -> None) abbrList
    let internal (|Abbr|_|) ty = toAbbr ty

type InterpolatedString =
    | Empty
    | Run of string * InterpolatedString
    | Binding of name:string * Type option * InterpolatedString

    // ToString with text substituted for bindings
    member this.ToString(sb : StringBuilder, fn : string -> string) =
        match this with
        | Empty -> ()
        | Run(str, next) -> next.ToString(sb.Append(str), fn)
        | Binding(name, _, next) -> next.ToString(sb.Append(fn name), fn)
    member this.ToString(fn) = let sb = StringBuilder() in this.ToString(sb, fn); sb.ToString()

    // ToString with binding markers
    member this.ToString(sb : StringBuilder) =
        match this with
        | Empty -> ()
        | Run(str, next) -> next.ToString(sb.Append(str))
        | Binding(name, None, next) -> next.ToString(sb.Append("${").Append(name).Append('}'))
        | Binding(name, Some(Type.Abbr(abbr)), next) ->
            next.ToString(sb.Append("${").Append(name).Append(" : ").Append(abbr).Append("}"))
        | Binding(name, ty, _) -> failwithf "Invalid type '%A' for binding '%s'" ty name
    override this.ToString() = let sb = StringBuilder() in this.ToString(sb); sb.ToString()
