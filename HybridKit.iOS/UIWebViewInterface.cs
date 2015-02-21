using System;
using System.IO;
using System.Threading.Tasks;

using UIKit;
using Foundation;
using ObjCRuntime;

namespace HybridKit {

	sealed class UIWebViewInterface : UIWebViewDelegate, IWebView, IScriptEvaluator {

		internal static readonly Selector Key = new Selector ("_hybridInterfaceKey");

		readonly UIWebView webView;
		readonly CachedResources cache;

		public event EventHandler Loaded;

		public CachedResources Cache {
			get { return cache; }
		}

		public UIWebViewInterface (UIWebView webView)
		{
			if (webView == null)
				throw new ArgumentNullException ("webView");

			this.webView = webView;
			this.cache = new CachedResources ();
			cache.ItemAdded += HandleCacheItemAdded;

			// Cache ourselves as this webView's interface
			webView.SetAssociatedObject (Key.Handle, this);

			ConfigureDelegate ();
			LoadHelperScript ();
		}

		public override void LoadingFinished (UIWebView webView)
		{
			LoadHelperScript ();
			var loaded = Loaded;
			if (loaded != null)
				loaded (this, EventArgs.Empty);
		}

		public Task RunScriptAsync (ScriptLambda script)
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

		void ConfigureDelegate ()
		{
			// FIXME: We need to swizzle the delegate property of the UIWebView
			//  so that we can hook the Loaded event even if the user changes the delegate.
			webView.Delegate = this;
		}
	}
}

