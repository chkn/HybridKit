# HybridKit Object Bridge

HybridKit bridges the C# and script object systems using a C# feature called `dynamic`. When you call the `RunScriptAsync` method on a web view, your lambda receives a dynamic object as an argument that represents the global `window` object inside the web view:

```
await webView.RunScriptAsync (window => { /* ... */ });
```

Because this object is dynamic, you can access any member or call any function from C# that would be available on the `window` object in JavaScript. If you squint, your lambda might actually look like it's written in JavaScript. However, it *is* still C#, which means that the C# objects and data types need to be marshaled when passed into script, and the script objects that are returned will need to be marshaled back to C#.

## Marshaling by Value

Simple objects are passed by value. This category includes:

+ null
+ `System.String`
+ `System.IFormattable` (includes all numeric types)
+ `System.Boolean`
+ `System.DateTime` <-> JavaScript `Date` object

+ Arrays, dictionaries (`System.Collections.IDictionary`), enumerables (`System.Collections.IEnumerable`), and simple C# objects (POCOs) containing only members/elements of the above types can be passed to script by value. By default, all script arrays are marshaled to C# by reference, but if they contain only values of one of the above types, can be cast to a C# array.

+ `System.Text.RegularExpressions.Regex` is automatically surfaced as a JavaScript `RegExp` object. By default, the JavaScript `RegExp` object is passed to C# by reference, but can be cast to `System.Text.RegularExpressions.Regex`.
+ HybridKit also includes special by-value marshaling for JavaScript `Error` objects to `HybridKit.ScriptException`.

## Marshaling by Reference

Objects that do not fall into the above category can be passed by reference. This happens automatically when marshaling script objects to C#. However, on the C# side, there are a few possibilities when passing objects to script:

A. The C# class is a subclass of `HybridKit.ScriptObject` and is simply a strongly-typed wrapper around an object that was exposed from script.

B. The C# class and certain members are marked with the `HybridKit.ScriptableAttribute`. When an object of this class is passed to script, a proxy script object is created to expose the scriptable members.

C. Individual C# methods can be marshaled as script functions using the `HybridKit.ScriptFunction` class.

These scenarios are discussed in more detail in the following sections.

### Wrapper Classes

To enable code completion and better type safety, you can create a strongly-typed C# wrapper around a JavaScript class. Here's how:

1. Create a subclass of `HybridKit.ScriptObject`.  
	You'll note that the `ScriptObject` constructor takes another instance of `ScriptObject` as an argument. This is because you obtain an instance of your strongly-typed wrapper by casting an untyped `ScriptObject`, which then gets passed in to your wrapper's constructor. Here's an example of a boilerplate binding we might have so far:
	
	```
	public class TypedScriptObject : ScriptObject {
	
		protected TypedScriptObject (ScriptObject untyped): base (untyped)
		{
		}
	}
	```
	Note that the constructor is `protected`. Subclasses of `ScriptObject` should not generally expose public constructors.
	



### Scriptable Classes

The default behavior when passing a C# object into JavaScript is to use `JSON.Stringify` and pass the object by value. This works for the types listed in category (A) of the previous section.

However, you can also expose an arbitrary C# object to JavaScript by reference. To do this, attribute the type with the `HybridKit.ScriptableAttribute`. In addition, you must apply this attribute to each member of the type that you want to expose. 

The following C# features are not supported in scriptable members:

- `ref` or `out` parameters on methods

### ScriptFunction

You can expose individual C# methods as first-class JavaScript function objects. The easiest way to do this is to simply pass or assign a C# delegate into a script object. 