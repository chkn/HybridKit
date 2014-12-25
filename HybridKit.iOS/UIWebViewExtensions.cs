using System;
using System.IO;
using System.Threading.Tasks;

using UIKit;
using Foundation;

namespace HybridKit {

	public static class UIWebViewExtensions {

		/// <summary>
		/// Runs the specified script, possibly asynchronously.
		/// </summary>
		/// <remarks>
		/// This method may dispatch to a different thread to run the passed lambda.
		/// </remarks>
		/// <param name="webView">The Web View in which to run the script.</param>
		/// <param name="script">A lambda that interacts with the passed JavaScript global object.</param>
		public static Task RunScriptAsync (this UIWebView webView, ScriptLambda script)
		{
			var tcs = new TaskCompletionSource<object> ();
			Action closure = () => {
				try {
					script (webView.GetGlobalObject ());
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

		/// <summary>
		/// Gets the JavaScript global window object.
		/// </summary>
		/// <remarks>
		/// On iOS, all calls into JavaScript must be done from the main UI thread.
		/// On Android, calls must NOT be made on the main UI thread. Any other thread is acceptable.
		/// </remarks>
		/// <returns>The global object.</returns>
		/// <param name="webView">Web view.</param>
		public static dynamic GetGlobalObject (this UIWebView webView)
		{
			return new ScriptObject (new UIWebViewInterface (webView));
		}

		// This is an additional, undocumented API that may change in the future:

		public static void LoadFromBundle (this UIWebView webView, string bundleRelativePath)
		{
			var url = BundleCache.GetBundleUrl (bundleRelativePath);
			if (url == null)
				throw new FileNotFoundException (bundleRelativePath);
			var req = NSUrlRequest.FromUrl (url);
			webView.LoadRequest (req);
		}
	}
}

