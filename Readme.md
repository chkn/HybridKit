# HybridKit

Simple C# – JavaScript bridge for building hybrid iOS and Android apps.

### Here's what it does:

- Call script functions and get/set values on script objects from C# using `dynamic`
	- Marshals simple values between script and C# by value (JSON)
	- Marshals complex script objects to C# by reference
- Create strongly-typed wrappers for JavaScript APIs
- Catch script exceptions in C#

See [Docs/ObjectBridge.md](Docs/ObjectBridge.md) for in-depth documentation on the above.


### Here's what it doesn't do (yet):

- Call C# methods from a script within a web view.
- Attach C# event handlers to events on script objects.


# Getting Started

To start utilizing HybridKit, follow the steps below:

1. Add HybridKit to your app. [NuGet](https://www.nuget.org/packages/Xam.Plugin.HybridKit) is the recommended way to do this.
2. Add the following references to your project:
	+ `Microsoft.CSharp`
	+ `Xamarin.Forms` (currently required, even if your project does not use it.)
	+ For Android only:
		- `Mono.Android.Export`	

HybridKit currently requires Xamarin.Forms 1.3.1 or later. If you install HybridKit through NuGet, it will install an appropriate version for you automatically. 

## Create the Web View

### Xamarin.iOS

On iOS, the HybridKit API is exposed as extension methods on `UIWebView`. Declare a new `UIWebView`, as shown in the code snippet below:

```
using UIKit;
using HybridKit;
// ...
var webView = new UIWebView ();
```

### Xamarin.Android

On Android, create an instance of `HybridKit.Android.HybridWebView`. This class extends `Android.WebKit.WebView` and will need to be configured in the same way—generally at least enabling JavaScript, as shown below:

```
using HybridKit.Android;
// ...
var webView = new HybridWebView ();
webView.Settings.JavaScriptEnabled = true;
```

### Xamarin.Forms

When using [Xamarin.Forms](http://xamarin.com/forms) on iOS or Android, create a new `HybridKit.Forms.HybridWebView`, which extends from `Xamarin.Forms.WebView`:

```
using HybridKit.Forms;

// ... On the native app:
HybridWebViewRenderer.Init ();

// ... In the shared code:
var webView = new HybridWebView ();
```

## Load some Content

You may want to load an HTML file that is bundled with your app into the web view. HybridKit makes this easier by suppling the `LoadFromBundle` extension method on **iOS and Android**:

```
webView.LoadFromBundle ("index.html");
```

For **Xamarin.Forms**, HybridKit supplies the `BundleWebViewSource` class:

```
webView.Source = new BundleWebViewSource ("index.html");
```
The path passed into both of these APIs is the bundle-relative path on iOS, or the path relative to the Assets folder for an Android project.

## Run some Script

To execute script inside the web view, call `RunScriptAsync` on the web view.

Generally, you'll want to await the `Task` returned by this method, which simulates a synchronous method:

```
await webView.RunScriptAsync (window => {
	var name = window.prompt ("What is your name?") ?? "World";
	var node = window.document.createTextNode (string.Format ("Hello {0} from C#!", name));
	window.document.body.appendChild (node);
});
```
The `window` argument to the lambda is the JavaScript global object. In the example above, the JavaScript `prompt` function is called, which displays a dialog to the user, and returns their entered text (or the string "World" if they click Cancel). It then creates a new text node and appends it to the DOM. You can see this code in action in the KitchenSink sample app.

## Read more

Read the documentation to get a deeper understanding of HybridKit:

- [Object Bridging/Marshalling](Docs/ObjectBridge.md)
- [Threading Model](Docs/Threading.md)

# Development

This project is brand new, so expect bugs. Feedback and contributions are always welcome!

## Cloning

After cloning this repository, you'll also need to update the submodules:

    git submodule update --init --recursive

## Compiling

I've been working in Xamarin Studio on the Mac, in which you can simply open `HybridKit.sln` and hit build.

On Windows, I've heard reports of compilation issues, probably due to some C# 6 features used in the codebase. If you're on Windows, try compiling with Roslyn or the `mcs` compiler from Mono. If you get it working, let me know and I'll update this section.

## Running the Unit Tests

Run the `HybridKit.Tests.Android` or `HybridKit.Tests.iOS` projects to run the tests on Android or iOS, respectively. Please run the tests on at least one platform before submitting a Pull Request.

