# HybridKit Threading Model

Although the name of the method, `RunScriptAsync`, ends with "Async," it may actually run synchronously depending on the threading constraints of the current platform.

Within the script lambda passed to `RunScriptAsync`, HybridKit evaluates JavaScript synchronously. Unfortunately, different platforms have different requirements for this:

- On iOS, all calls into JavaScript must be done from the main UI thread.
- On Android KitKat and Lollypop, it will deadlock if a call to JavaScript is made synchronously from the main UI thread. **Do not** call `Wait` on the `Task` returned by `RunScriptAsync` on the main thread on these platforms or your code will deadlock! This issue is apparently fixed on Android 5.1 -- these are the bugs for reference:
	+ https://code.google.com/p/android/issues/detail?id=79924
	+ https://code.google.com/p/chromium/issues/detail?id=438255

To accommodate these different requirements, the `RunScriptAsync` method *may* dispatch to a different thread to run the passed lambda. Because of this, you must be careful to properly synchronize external objects used in the lambda, and take proper care of any script objects persisted outside the scope of the lambda. Script objects may be used safely in multiple `RunScriptAsync` calls:

**DO NOT do this!** – It causes an implicit `ToString` call on the current thread, which might not work:

```
dynamic foo;
await webView.RunScriptAsync (window => {
	foo = window.foo;
});
// ...
Console.WriteLine (foo);
```

**This is OK** – the `RunScriptAsync` method ensures the implicit `ToString` call happens on the correct thread:

```
dynamic foo;
await webView.RunScriptAsync (window => {
	foo = window.foo;
});
// ...
await webView.RunScriptAsync (_ => Console.WriteLine (foo));
```
In debug builds, your code will receive an exception if it attempts to use script objects from the wrong thread.