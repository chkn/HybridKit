using System;
using System.IO;
using System.Threading.Tasks;
using System.Runtime.InteropServices;

using UIKit;
using Foundation;
using ObjCRuntime;

namespace HybridKit {

	public static class UIWebViewExtensions {

		/// <summary>
		/// Runs the specified script, possibly asynchronously.
		/// </summary>
		/// <remarks>
		/// This method is a convenience for: <code>webView.AsHybridWebView ().RunScriptAsync (script)</code>
		/// This method may dispatch to a different thread to run the passed lambda.
		/// </remarks>
		/// <param name="webView">The Web View in which to run the script.</param>
		/// <param name="script">A lambda that interacts with the passed JavaScript global object.</param>
		public static Task RunScriptAsync (this UIWebView webView, ScriptLambda script)
		{
			return webView.AsHybridWebView ().RunScriptAsync (script);
		}

		/// <summary>
		/// Returns an <c>IWebView</c> interface for the given <c>UIWebView</c>.
		/// </summary>
		/// <param name="webView">Web view for which to return the <c>IWebView</c>.</param>
		public static IWebView AsHybridWebView (this UIWebView webView)
		{
			return webView.GetInterface (create: true);
		}

		/// <summary>
		/// Loads a page from the app bundle into the <c>WebView</c>.
		/// </summary>
		/// <param name="webView">Web view in which to load the page.</param>
		/// <param name="bundleRelativePath">Bundle-relative path of the page to load.</param>
		public static void LoadFromBundle (this UIWebView webView, string bundleRelativePath)
		{
			var url = BundleCache.GetBundleUrl (bundleRelativePath);
			if (url == null)
				throw new FileNotFoundException (bundleRelativePath);
			var req = NSUrlRequest.FromUrl (url);
			webView.LoadRequest (req);
		}

		internal static UIWebViewInterface GetInterface (this UIWebView webView, bool create)
		{
			// First, see if we've already created an interface for this webView
			var existing = webView.GetAssociatedObject (UIWebViewInterface.Key) as UIWebViewInterface;
			return existing ?? (create? new UIWebViewInterface (webView) : null);
		}
	}
}

