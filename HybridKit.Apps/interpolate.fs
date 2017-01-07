namespace HybridKit.Apps.Markup

open System
open System.IO

type TwoWayBindingInfo = {
    /// The DOM property of the element that will be updated
    Property:string

    /// If `Some`, gives the name of the attribute and indicates
    ///  that this binding will be responsible for writing it iff
    ///  its value isn't `false` or `None`
    OmitAttribute:string option
    }

type BindingType =
    | Scalar
    | Vector
    | TwoWay of TwoWayBindingInfo
    member this.Prefix =
        match this with
        | Scalar -> "$"
        | Vector -> "@"
        | TwoWay _ -> ""

/// Interpolated string.
type Interpolated =
    | Empty
    | Run of string * Interpolated
    | Binding of BindingType * name:string * typeName:string option * Interpolated

    // ToString with text substituted for bindings
    member this.ToString(wr : TextWriter, fn : TextWriter -> string -> unit) =
        match this with
        | Empty -> ()
        | Run(str, next) ->
            wr.Write(str)
            next.ToString(wr, fn)
        | Binding(_, name, _, next) ->
            fn wr name
            next.ToString(wr, fn)
    member this.ToString(wr : TextWriter, fn : string -> string) = this.ToString(wr, fun wr nm -> wr.Write(fn nm))
    member this.ToString(fn : string -> string) = use sw = new StringWriter() in this.ToString(sw, fn); sw.ToString()

    // ToString with binding markers
    member this.ToString(wr : TextWriter) =
        match this with
        | Empty -> ()
        | Run(str, next) ->
            wr.Write(str)
            next.ToString(wr)
        | Binding(bt, name, None, next) ->
            wr.Write(bt.Prefix)
            wr.Write('{')
            wr.Write(name)
            wr.Write('}')
            next.ToString(wr)
        | Binding(bt, name, Some abbr, next) ->
            wr.Write(bt.Prefix)
            wr.Write('{')
            wr.Write(name)
            wr.Write(" : ")
            wr.Write(abbr)
            wr.Write('}')
            next.ToString(wr)
    override this.ToString() = use sw = new StringWriter() in this.ToString(sw); sw.ToString()

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module Interpolated =

    let inline istr str = Run(str, Empty)
