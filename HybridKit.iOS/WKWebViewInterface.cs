using System;
using System.IO;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Runtime.InteropServices;

using WebKit;
using Foundation;
using ObjCRuntime;

namespace HybridKit {

	sealed class WKWebViewInterface : NSObject, IWKNavigationDelegate, IWebView, IScriptEvaluator {

		internal static readonly IntPtr Key = Selector.GetHandle ("_hybridInterfaceKey");
		static readonly IntPtr webViewDidFinishLoad = Selector.GetHandle ("webViewDidFinishLoad:");
		static readonly Dictionary<IntPtr,IMP> delegateClassToLoadingFinished = new Dictionary<IntPtr,IMP> ();
		static readonly IMP loadingFinishedOverride = LoadingFinishedOverride;
		static IMP webViewSetDelegate;
		static IMP4 webViewPrompt;

		readonly WKWebView webView;
		readonly CachedResources cache;
		IMP delegateLoadingFinished;

		public event EventHandler Loaded;

		public CachedResources Cache {
			get { return cache; }
		}

		public WKWebViewInterface (WKWebView webView)
		{
			if (webView == null)
				throw new ArgumentNullException ("webView");

			this.webView = webView;
			this.cache = new CachedResources ();
			cache.ItemAdded += HandleCacheItemAdded;

			// Cache ourselves as this webView's interface
			webView.SetAssociatedObject (Key, this);

			ConfigureDelegate ();
			LoadHelperScript ();
		}

		[Export ("webView:didFinishNavigation:")]
		public void DidFinishNavigation (WKWebView webView, WKNavigation navigation)
		{
			HandleLoadingFinished ();
		}

		public Task<T> RunScriptAsync<T> (ScriptLambda script)
		{
			var tcs = new TaskCompletionSource<object> ();
			Action closure = () => {
				try {
					script (GetGlobalObject ());
					tcs.TrySetResult (null);
				} catch (Exception e) {
					tcs.TrySetException (e);
				}
			};
			// We must execute synchronously if already on the main thread,
			//  otherwise a Wait() on the returned task would deadlock trying to
			//  dispatch back to the main thread while it's blocking on the Wait().
			if (NSThread.IsMain)
				closure ();
			else
				webView.BeginInvokeOnMainThread (closure);
			return tcs.Task;
		}

		public string Eval (string script)
		{
			return webView.EvaluateJavascript (script);
		}

		public void EvalOnMainThread (string script)
		{
			webView.BeginInvokeOnMainThread (() => Eval (script));
		}

		/// <summary>
		/// Gets the JavaScript global window object.
		/// </summary>
		/// <remarks>
		/// On iOS, all calls into JavaScript must be done from the main UI thread.
		/// On Android, calls must NOT be made on the main UI thread. Any other thread is acceptable.
		/// </remarks>
		/// <returns>The global object.</returns>
		ScriptObject GetGlobalObject ()
		{
			return new ScriptObject (this);
		}

		void LoadHelperScript ()
		{
			if (webView.EvaluateJavascript ("HybridKit.magic") != HybridKit.Magic) {
				using (var reader = new StreamReader (typeof (HybridKit).Assembly.GetManifestResourceStream ("HybridKit.HybridKit.js")))
					webView.EvaluateJavascript (reader.ReadToEnd ());
			}
		}

		void HandleLoadingFinished ()
		{
			LoadHelperScript ();
			var loaded = Loaded;
			if (loaded != null)
				loaded (this, EventArgs.Empty);
		}

		void HandleCacheItemAdded (object sender, CachedEventArgs e)
		{
			var url = NSUrl.FromString (e.Url);
			if (url == null)
				return;

			var req = NSUrlRequest.FromUrl (url);
			var data = NSData.FromStream (e.Item.DataSource ());
			var resp = new NSUrlResponse (url, e.Item.MimeType, (nint)data.Length, "UTF-8");
			var cachedResponse = new NSCachedUrlResponse (resp, data);

			NSUrlCache.SharedCache.StoreCachedResponse (cachedResponse, req);
		}

		#region Delegate handling

		void ConfigureDelegate ()
		{
			// We need to swizzle the delegate property of the UIWebView
			//  so that we can hook the Loaded event even if the user changes the delegate.
			if (webViewSetDelegate == null) {
				var uiWebView = new Class (typeof (UIWebView));
				webViewSetDelegate = class_replaceMethod (uiWebView.Handle, Selector.GetHandle ("setDelegate:"), SetDelegateOverride, IMP_Types);
				webViewPrompt = class_replaceMethod (uiWebView.Handle, Selector.GetHandle ("webView:runJavaScriptTextInputPanelWithPrompt:defaultText:initiatedByFrame:"), PromptOverride, IMP4_Types);
			}

			var currentDel = webView.WeakDelegate;
			if (currentDel == null)
				webView.Delegate = this;
			else
				SwizzleDelegate (currentDel);
		}

		[MonoPInvokeCallback (typeof (IMP))]
		static void SetDelegateOverride (IntPtr webView, IntPtr sel, IntPtr del)
		{
			var @this = GetInstance (webView);
			if (@this != null)
				@this.SwizzleDelegate (Runtime.GetNSObject (del));
			webViewSetDelegate (webView, sel, del);
		}

		[MonoPInvokeCallback (typeof (IMP4))]
		static IntPtr PromptOverride (IntPtr webView, IntPtr sel, IntPtr wv1, IntPtr prompt, IntPtr defaultText, IntPtr frame)
		{
			var promptObj = Runtime.GetNSObject (prompt);
			var defaultTextObj = Runtime.GetNSObject (defaultText);

			string result;
			if (ScriptFunction.HandlePrompt (
				promptObj != null ? promptObj.ToString () : null,
				defaultTextObj != null ? defaultTextObj.ToString () : null,
				out result))
				return NSString.CreateNative (result);

			return webViewPrompt (webView, sel, wv1, prompt, defaultText, frame);
		}

		void SwizzleDelegate (NSObject del)
		{
			if (del == null || del is UIWebViewInterface)
				return;

			delegateLoadingFinished = FindLoadingFinishedImpl (del.Handle);
		}

		[MonoPInvokeCallback (typeof (IMP))]
		static void LoadingFinishedOverride (IntPtr del, IntPtr sel, IntPtr webView)
		{
			var @this = GetInstance (webView);
			if (@this != null) {
				@this.HandleLoadingFinished ();
				if (@this.delegateLoadingFinished != null)
					@this.delegateLoadingFinished (del, sel, webView);
			} else if (del != IntPtr.Zero) {
				var method = FindLoadingFinishedImpl (del);
				if (method != null)
					method (del, sel, webView);
			}
		}

		static IMP FindLoadingFinishedImpl (IntPtr id)
		{
			IMP result;
			var cls = object_getClass (id);
			if (delegateClassToLoadingFinished.TryGetValue (cls, out result))
				return result;

			result = null;
			if (objc_msgSend_bool (id, Selector.GetHandle ("respondsToSelector:"), webViewDidFinishLoad)) {
				result = class_getMethodImplementation (cls, webViewDidFinishLoad);

				// ensure we're not already swizzled on a base class
				var superClass = cls;
				while (result == loadingFinishedOverride) {
					superClass = class_getSuperclass (superClass);
					if (delegateClassToLoadingFinished.TryGetValue (superClass, out result)) {
						delegateClassToLoadingFinished.Add (cls, result);
						return result;
					}
				}
			}
			delegateClassToLoadingFinished.Add (cls, result);
			class_replaceMethod (cls, webViewDidFinishLoad, loadingFinishedOverride, IMP_Types);
			return result;
		}

		static UIWebViewInterface GetInstance (IntPtr webViewPtr)
		{
			var webView = Runtime.GetNSObject (webViewPtr) as UIWebView;
			return webView == null ? null : webView.GetInterface (create: false);
		}

		[DllImport (Constants.ObjectiveCLibrary)]
		static extern IMP class_replaceMethod (IntPtr cls, IntPtr sel, IMP imp, string types);
		[DllImport (Constants.ObjectiveCLibrary)]
		static extern IMP4 class_replaceMethod (IntPtr cls, IntPtr sel, IMP4 imp, string types);

		[DllImport (Constants.ObjectiveCLibrary)]
		static extern IMP class_getMethodImplementation (IntPtr cls, IntPtr sel);

		[DllImport (Constants.ObjectiveCLibrary)]
		static extern IntPtr class_getSuperclass (IntPtr cls);

		[DllImport (Constants.ObjectiveCLibrary)]
		static extern IntPtr object_getClass (IntPtr id);

		[DllImport (Constants.ObjectiveCLibrary, EntryPoint = "objc_msgSend")]
		static extern bool objc_msgSend_bool (IntPtr id, IntPtr sel, IntPtr arg);

		[MonoNativeFunctionWrapper]
		delegate void IMP (IntPtr id, IntPtr sel, IntPtr arg1);
		const string IMP_Types = "v@:@";

		[MonoNativeFunctionWrapper]
		delegate IntPtr IMP4 (IntPtr id, IntPtr sel, IntPtr arg1, IntPtr arg2, IntPtr arg3, IntPtr arg4);
		const string IMP4_Types = "@@:@@@@";

		#endregion
	}
}

