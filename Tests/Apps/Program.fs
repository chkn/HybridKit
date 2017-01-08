
open Expecto

open HybridKit.Apps.Markup

let types = Types.defaultTypes

let loadBindings html =
    Tree.fromMarkupString "html" html
    |> Binding.collectInTree

let typeBindings = Binding.computeTypes types

let expectBindings expected html =
    let actual =
        loadBindings html
        |> typeBindings
    Expect.sequenceEqual actual expected (sprintf "expected %A, but got %A" expected actual) 

let expectBindingFailure msg html =
    let test() =
        loadBindings html
        |> typeBindings
        |> ignore
    Expect.throwsC test (fun e -> Expect.stringContains e.Message msg "wrong message")

[<Tests>]
let bindingDeclTests =
    testList "binding decl" [
        testCase "type applies to all instances when type is applied first" <| fun () ->
            @"${Foo : int} ${Foo}"
            |> expectBindings [TypedBinding(Scalar, "Foo", ConcreteType(typeof<int>))]

        testCase "type applies to all instances when type is applied second" <| fun () ->
            @"${Foo} ${Foo : int}"
            |> expectBindings [TypedBinding(Scalar, "Foo", ConcreteType(typeof<int>))]

        testCase "throws error for conflicting data types" <| fun () ->
            @"${Foo : string} ${Foo : int}"
            |> expectBindingFailure "Found multiple declarations of binding `Foo' with conflicting data types, `int' and `string'"

        testCase "throws error for conflicting binding types" <| fun () ->
            @"${Foo} @{Foo}"
            |> expectBindingFailure "Found multiple declarations of binding `Foo' with conflicting binding types, `Vector' and `Scalar'"

        testCase "regular TwoWay binding" <| fun () ->
            @"<input type=""text"" value={Foo}/>"
            |> expectBindings [TypedBinding(TwoWay { Property = "value"; OmitAttribute = None }, "Foo", ConcreteType(typeof<string>))]

        testCase "omit attribute boolean TwoWay binding" <| fun () ->
            @"<input type=""checkbox"" checked?{Foo}/>"
            |> expectBindings [TypedBinding(TwoWay { Property = "checked"; OmitAttribute = Some "checked" }, "Foo", ConcreteType(typeof<bool>))]

        testCase "omit attribute option TwoWay binding" <| fun () ->
            @"<input type=""text"" placeholder={Foo : string option}/>"
            |> expectBindings [TypedBinding(TwoWay { Property = "placeholder"; OmitAttribute = Some "placeholder" }, "Foo", ConcreteType(typeof<string option>))]

        testCase "TwoWay binding can be used as scalar" <| fun () ->
            @"<p>Is checked? ${Foo}</p><input type=""checkbox"" checked?{Foo}/>"
            |> expectBindings [TypedBinding(TwoWay { Property = "checked"; OmitAttribute = Some "checked" }, "Foo", ConcreteType(typeof<bool>))]

        testCase "WildcardType binding" <| fun () ->
            @"${Foo : _}"
            |> expectBindings [TypedBinding(Scalar, "Foo", WildcardType)]
    ]

[<EntryPoint>]
let main argv = runTestsInAssembly defaultConfig argv
