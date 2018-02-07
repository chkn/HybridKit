
open Expecto
open HybridKit
open HybridKit.Apps.Markup

open System.IO
open System.Reflection

let types = Types.defaultTypes

let roundtripTestCase types (RegType(nm, ty)) =
    testCase nm <| fun () ->
        Expect.equal (Types.toName ty types) (Some nm) "wrong name"
        Expect.equal (Types.ofName nm types) (Some ty) "wrong type"
        match ty with
        | WildcardType -> ()
        | ConcreteType typ ->
            let optionType = ConcreteType(typedefof<option<_>>.MakeGenericType(typ))
            let optionName = nm + " option"
            Expect.equal (Types.toName optionType types) (Some optionName) "wrong option name"
            Expect.equal (Types.ofName optionName types) (Some optionType) "wrong option type"

[<Tests>]
let typesTests =
    types
    |> Types.map (roundtripTestCase types)
    |> testList "Types roundtrip tests"

let loadAndTypeBindings html =
    Tree.fromMarkupString "html" html
    |> Binding.collectInTree
    |> Binding.computeTypes types

let expectBindings expected html =
    let actual = loadAndTypeBindings html
    Expect.sequenceEqual actual expected "wrong bindings" 

let expectBindingFailure msg html =
    let test() = loadAndTypeBindings html |> ignore
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

        testCase "throws error for invalid data types" <| fun () ->
            @"${Foo : _ option}"
            |> expectBindingFailure "asdf"

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

        testCase "function binding" <| fun () ->
            @"<button onclick={Handler}>Click me</button>"
            |> expectBindings [TypedBinding(Function, "Handler", ConcreteType(typeof<unit -> unit>))]
    ]

let expectHtml html (view : HybridKit.Apps.IHtmlWriter) =
    let actual = using (new StringWriter()) (fun wr -> view.WriteHtml(wr); wr.ToString())
    Expect.equal actual html "wrong html"

let expectDoesNotHaveProp<'t> name =
    let hasProp =
        typeof<'t>.GetProperties(BindingFlags.Public ||| BindingFlags.Instance)
        |> Seq.map (fun p -> p.Name)
        |> Seq.contains name
    Expect.isFalse hasProp (sprintf "shouldn't have property: %s" name)

type ScalarBindingView = HtmlView<"html/scalarbinding.html">
let [<Tests>] scalarBindingView =
    testCase "scalar binding view" <| fun () ->
        ScalarBindingView(Foo = "hello")
        |> expectHtml @"<p><span class=""__hk1_Foo"">hello</span></p>"

type TemplateBindingView = HtmlView<"html/templatebinding.html">
let [<Tests>] templateBindingView =
    testCase "template binding view" <| fun () ->
        expectDoesNotHaveProp<TemplateBindingView> "_"
        // assert that there is a nested class for each template
        TemplateBindingView()
        |> expectHtml @"<html><template id=""Tpl1""><b>hello</b></template><template id=""Tpl2""><i>goodbye</i></template><p><b>hello</b><i>goodbye</i></p></html>"

[<EntryPoint>]
let main argv = runTestsInAssembly defaultConfig argv
