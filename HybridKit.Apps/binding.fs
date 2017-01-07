namespace HybridKit.Apps.Markup

open System
open System.Reflection
open System.Collections.Generic

open HybridKit.Apps
open HybridKit.Apps.Markup.Tree
open HybridKit.Apps.Markup.Interpolated

type TypeValue =
    | WildcardType
    | ConcreteType of Type

type TypedBinding = TypedBinding of BindingType * name:string * TypeValue

type RegType = RegType of string * TypeValue

type Types = { Types : RegType list } with
    member this.GetType(nm1 : string) =
        List.tryPick (function RegType(nm2, ty) when nm1 = nm2 -> Some ty | _ -> None) this.Types
    member this.GetName(ty1 : TypeValue) =
        List.tryPick (function RegType(nm, ty2) when ty1 = ty2 -> Some nm | _ -> None) this.Types

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module Types =
    let inline add typ types = { types with Types = typ :: types.Types }
    let inline ofList types = { Types = types }
    let defaultTypes =
        ofList [
            RegType("_",       WildcardType)    
            RegType("int",     ConcreteType typeof<int>)
            RegType("int64",   ConcreteType typeof<int64>)
            RegType("string",  ConcreteType typeof<string>)
            RegType("bool",    ConcreteType typeof<bool>)
            RegType("float",   ConcreteType typeof<float>)
            RegType("float32", ConcreteType typeof<float32>)
            RegType("byte",    ConcreteType typeof<byte>)
            RegType("sbyte",   ConcreteType typeof<sbyte>)
        ]

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module Binding =

    /// The default inferred type for the given binding type,
    ///  assuming no other annotation or context is present.
    let defaultType = ConcreteType(typeof<string>)

    /// Separates a Binding from its previous Runs
    let rec (|ExtractBinding|) = function
    | Empty -> Empty, None
    | Run(str, ExtractBinding(prev, binding)) -> Run(str, prev), binding
    | Binding(_, _, _, _) as binding -> Empty, Some binding

    // Intermediate types used for the collect* functions
    type Parent =
        | NoParent
        | Element of name:string * attributes
        | Attribute of name:string * elementName:string * attributes
    type InTree =
        | BindingInTree of BindingType * string * string option * Parent

    let rec collectInString parent lst = function
    | Empty -> lst
    | Run(_, next) -> collectInString parent lst next
    | Binding(bt, name, ty, next) as binding ->
        collectInString parent (BindingInTree(bt, name, ty, parent) :: lst) next

    let rec collectInNode parent lst = function
    | Text(str) -> collectInString parent lst str
    | Elem(elemName, attrs, children) ->
        let lst = Map.fold (fun lst attrName -> collectInString (Attribute(attrName, elemName, attrs)) lst) lst attrs
        Seq.fold (collectInNode (Element(elemName, attrs))) lst children

    let inline collectInTree (Tree(_,root)) = collectInNode NoParent [] root

    /// Given a list of references to the same binding,
    ///  attempts to infer an overall type for the binding.
    ///  Raises errors if there are conflicting type annotations.
    let inferType (types : Types) dupeBindings =
        let mutable bindingType      = None
        let mutable mostSpecificType = None
        let mutable mostProbableType = None
        for (BindingInTree(bty, name, pty, parent)) in dupeBindings do
            // FIXME: TwoWay bindings should be usable as scalar bindings elsewhere
            match bindingType, bty with
            | None, bty -> bindingType <- Some bty
            | Some bty1, bty2 when bty1 <> bty2 ->
                failwithf "Found multiple declarations of binding `%s' with different binding types, `%A' and `%A'" name bty1 bty2
            | _, _ -> ()
            match Option.bind types.GetType pty with
            | Some pty ->
                match mostSpecificType with
                | Some sty when sty <> pty ->
                    failwithf "Found multiple declarations of binding `%s' with different data types, `%s' and `%s'"
                    <| name
                    <| types.GetName(sty).Value
                    <| types.GetName(pty).Value
                | _ -> mostSpecificType <- Some pty
            | _ ->
                let probableType = 
                    match bty with
                    | TwoWay { OmitAttribute = Some _ } -> Some(ConcreteType(typeof<bool>))
                    | _ -> None
                match mostProbableType, probableType with
                | None, Some ty -> mostProbableType <- Some ty
                | Some ty1, Some ty2 when ty1 <> ty2 ->
                    // if we infer 2 different types, just fall back to default
                    mostProbableType <- Some defaultType
                | _, _ -> ()
        bindingType.Value,
        defaultType
        |> defaultArg mostProbableType
        |> defaultArg mostSpecificType

    let computeTypes (types : Types) bindings =
        bindings
        |> List.groupBy (fun (BindingInTree(_, nm, _, _)) -> nm)
        |> List.map (fun (name, bindings) ->
            let bty, pty = inferType types bindings
            TypedBinding(bty, name, pty)
        )

    /// Specifies whether binding children of the specified tag name should be wrapped in an element
    let shouldWrapInElem = function
    | "title"
    | "style"
    | "script" -> false
    | _ -> true

    let createElements getId node =
        let rec visitText = function
        | ExtractBinding(before, Some(Binding(bt, nm, ty, next))) ->
            let result = [
                Text(before)
                Elem("span", Map.ofList [ "class", Run(getId nm, Empty) ], [ Text(Interpolated.Binding(bt, nm, ty, Empty)) ])
            ]
            match visitText next with
            | Replace nextParts -> List.append result nextParts
            | _ -> List.append result [Text(next)]
            |> Replace
        | _ -> Visit
        match node with
        | Elem(name,_,_) -> if shouldWrapInElem name then Visit else Ignore
        | Text(text) -> visitText text
