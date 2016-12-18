namespace HybridKit.Apps.Markup

open HybridKit.Apps

module Binding =

    /// Separates a Binding from its previous Runs
    let rec (|ExtractBinding|) = function
    | Empty -> Empty, None
    | Run(str, ExtractBinding(prev, binding)) -> Run(str, prev), binding
    | binding -> Empty, Some binding

    let createBindingElements node =
        let rec visitText = function
        | ExtractBinding(before, Some(Binding(nm, ty, next))) ->
            let result = [
                Text(before)
                Elem("span", Map.ofList [ "class", Run(HtmlView.GetBindingId(nm), Empty) ], [ Text(Binding(nm, ty, Empty)) ])
            ]
            match visitText next with
            | Replace nextParts -> List.append result nextParts
            | _ -> List.append result [Text(next)]
            |> Replace
        | _ -> Visit
        match node with
        | Elem("title",_,_) -> Ignore
        | Text(text) -> visitText text
        | _ -> Visit
