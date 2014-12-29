# HybridKit

Simple C# - JavaScript bridge for building hybrid iOS and Android apps.

### Here's what it does:

- Call JavaScript functions inside the web view using C# `dynamic`
- Get/set values on JavaScript objects inside the web view from C#
- Marshal simple values between JavaScript and C# by value (JSON)
- Marshal complex JavaScript objects to C# by reference
- Catch JavaScript exceptions in C#

### Here's what it doesn't do (yet):

- Call C# methods from JavaScript
- Attach C# event handlers to JavaScript events
- Create strongly-typed wrappers for JavaScript APIs
- Marshal complex C# objects to JavaScript by reference

# Getting Started

1. Add HybridKit to your app.
2. Add the following references to your project:
	+ `Microsoft.CSharp`
	+ `Mono.Android.Export` (Android only)
3. Create the web view as appropriate for your platform:

### Xamarin.iOS

On iOS, the HybridKit API is exposed as extension methods on `UIWebView`:

```
using HybridKit;
// ...
var webView = new UIWebView ();
```

### Xamarin.Android

On Android, create an instance of `HybridKit.Android.HybridWebView`. It extends `Android.WebKit.WebView` and will need to be configured in the same way (generally at least enabling JavaScript, as shown below):

```
using HybridKit.Android;
// ...
var webView = new HybridWebView ();
webView.Settings.JavaScriptEnabled = true;
```

### Xamarin.Forms

On iOS or Android when using [Xamarin.Forms](http://xamarin.com/forms), create a new `HybridKit.Forms.HybridWebView`, which extends from `Xamarin.Forms.WebView`:

```
using HybridKit.Forms;
// ...
var webView = new HybridWebView ();
```

## RunScriptAsync

Call this API to execute script inside the web view.

Although the name of this method ends with "Async," it may actually run synchronously depending on the threading constraints of the current platform (see important note on threading below). Generally, you will await the `Task` returned by this method, which simulates a synchronous method call in either case:

```
await webView.RunScriptAsync (window => {
	var name = window.prompt ("What is your name?") ?? "World";
	var node = window.document.createTextNode (string.Format ("Hello {0} from C#!", name));
	window.document.body.appendChild (node);
});
```
The `window` argument to the lambda is the JavaScript global object. In the above example, we are calling the JavaScript `prompt` function, which displays a dialog to the user and returns their entered text. Then we are creating a new text node and appending it to the DOM. You can see this code in action in the KitchenSink sample app.

**Important Note about Threading**

Within the script lambda passed to `RunScriptAsync`, HybridKit evaluates JavaScript synchronously. Unfortunately, different platforms have different requirements for this:

- On iOS, all calls into JavaScript must be done from the main UI thread.
- On newer versions of Android, we will deadlock if we try to call JavaScript synchronously from the main UI thread (as of KitKat and later).

To accommodate these different requirements, the `RunScriptAsync` method may dispatch to a different thread to run the passed lambda. Because of this, you must be careful to properly synchronize external objects used in the lambda, and take proper care of any script objects persisted outside the scope of the lambda. Script objects may be used safely in multiple `RunScriptAsync` calls. For example:

```
// DO NOT do this! It causes an implicit ToString call on the current thread, which might not work:

dynamic foo;
await webView.RunScriptAsync (window => {
	foo = window.foo;
});
// ...
Console.WriteLine (foo);
```

```
// This is OK - the RunScriptAsync method ensures the implicit ToString call happens on the correct thread:

dynamic foo;
await webView.RunScriptAsync (window => {
	foo = window.foo;
});
// ...
await webView.RunScriptAsync (_ => Console.WriteLine (foo));
```

## GetGlobalObject

This API returns the JavaScript global `window` object. Due to the threading considerations mentioned above, this API is only available on iOS and Android.

```
var window = webView.GetGlobalObject ();
var name = window.prompt ("What is your name?") ?? "World";
var node = window.document.createTextNode (string.Format ("Hello {0} from C#!", name));
window.document.body.appendChild (node);
```

As mentioned in the previous section, here are the requirements for calling into JavaScript:

**iOS** - All calls must be made on the main UI thread.  
**Android** - Calls must NOT be made on the main UI thread. Any other thread is acceptable.

The `RunScriptAsync` method automatically ensures your code is run on the right thread.

# Current Status

This project is brand new, so expect bugs. Feedback and contributions always welcome!
