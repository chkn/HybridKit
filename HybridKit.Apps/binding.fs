﻿namespace HybridKit.Apps.Markup

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
    member this.GetType(nm : string) =
        let isOption = nm.EndsWith("option", StringComparison.Ordinal)
        let nm1 = if isOption then nm.Substring(0, nm.Length - "option".Length).Trim() else nm
        let ty  = List.tryPick (function RegType(nm2, ty) when nm1 = nm2 -> Some ty | _ -> None) this.Types
        match isOption, ty with
        | true, Some(ConcreteType ty) -> typedefof<option<_>>.MakeGenericType(ty) |> ConcreteType |> Some
        | true, _ -> None
        | false, ty -> ty
    member this.GetName(ty1 : TypeValue) =
        List.tryPick (function RegType(nm, ty2) when ty1 = ty2 -> Some nm | _ -> None) this.Types

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module Types =
    let inline ofName nm (types : Types) = types.GetType(nm)
    let inline toName ty (types : Types) = types.GetName(ty)
    let inline iter fn { Types = types } = List.iter fn types
    let inline map fn { Types = types }  = List.map fn types
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

    /// The default inferred type for a binding,
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

    let collectInNode node =
        let rec collect parent lst = function
        | Text(str) -> collectInString parent lst str
        | Elem(elemName, attrs, children) ->
            let lst = Map.fold (fun lst attrName -> collectInString (Attribute(attrName, elemName, attrs)) lst) lst attrs
            Seq.fold (collect (Element(elemName, attrs))) lst children
        collect NoParent [] node

    let inline collectInTree (Tree(_,root)) = collectInNode root

    /// Given a list of references to the same binding,
    ///  attempts to infer an overall type for the binding.
    ///  Raises errors if there are conflicting type annotations.
    let inferType (types : Types) dupeBindings =
        let mutable bindingType      = None
        let mutable mostSpecificType = None
        let mutable mostProbableType = None
        for (BindingInTree(bty, name, pty, parent)) in dupeBindings do
            match bindingType, bty with
            // TwoWay bindings can be used as scalar...
            | Some Scalar, TwoWay _
            | None, _ -> bindingType <- Some bty
            // ...but type the binding as TwoWay, not Scalar
            | Some(TwoWay _), Scalar -> ()
            | Some bty1, bty2 when bty1 <> bty2 ->
                failwithf "Found multiple declarations of binding `%s' with conflicting binding types, `%A' and `%A'" name bty1 bty2
            | _, _ -> ()
            match pty with
            | Some pty ->
                let pty = 
                    match Types.ofName pty types with
                    | None -> failwithf "Binding `%s' has invalid type, `%s'" name pty
                    | Some pty -> pty
                match mostSpecificType with
                | Some sty when sty <> pty ->
                    failwithf "Found multiple declarations of binding `%s' with conflicting data types, `%s' and `%s'"
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
        | ExtractBinding(before, Some(Binding(bt, nm, ty, next))) when nm <> "_" ->
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
