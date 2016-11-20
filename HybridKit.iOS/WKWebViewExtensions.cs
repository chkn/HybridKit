using System;
using System.IO;
using System.Threading.Tasks;
using System.Runtime.InteropServices;

using WebKit;
using Foundation;
using ObjCRuntime;

namespace HybridKit {

	public static class WKWebViewExtensions {

		/// <summary>
		/// Runs the specified script, possibly asynchronously.
		/// </summary>
		/// <remarks>
		/// This method is a convenience for: <code>webView.AsHybridWebView ().RunScriptAsync (script)</code>
		/// </remarks>
		/// <param name="webView">Web view in which to run the script.</param>
		/// <param name="script">A <see cref="string.Format(string, object[])"/>-style string containing the script to execute.</param>
		/// <param name="args">Args by which to replace the placeholders in <c>script</c>.</param>
		public static Task<T> RunScriptAsync<T> (this WKWebView webView, string script, params object [] args)
			where T : ScriptObject
		{
			return webView.AsHybridWebView ().RunScriptAsync<T> (script, args);
		}
		public static Task<ScriptObject> RunScriptAsync (this WKWebView webView, string script, params object [] args)
		{
			return webView.RunScriptAsync<ScriptObject> (script, args);
		}

		/// <summary>
		/// Returns an <c>IWebView</c> interface for the given <c>WKWebView</c>.
		/// </summary>
		/// <param name="webView">Web view for which to return the <c>IWebView</c>.</param>
		public static IWebView AsHybridWebView (this WKWebView webView)
		{
			return webView.GetInterface (create: true);
		}

		/// <summary>
		/// Loads a page from the app bundle into the <c>WebView</c>.
		/// </summary>
		/// <param name="webView">Web view in which to load the page.</param>
		/// <param name="bundleRelativePath">Bundle-relative path of the page to load.</param>
		public static void LoadFromBundle (this WKWebView webView, string bundleRelativePath)
		{
			var url = BundleCache.GetBundleUrl (bundleRelativePath);
			if (url == null)
				throw new FileNotFoundException (bundleRelativePath);
			var req = NSUrlRequest.FromUrl (url);
			webView.LoadRequest (req);
		}

		internal static UIWebViewInterface GetInterface (this WKWebView webView, bool create)
		{
			// First, see if we've already created an interface for this webView
			var existing = webView.GetAssociatedObject (UIWebViewInterface.Key) as UIWebViewInterface;
			return existing ?? (create? new UIWebViewInterface (webView) : null);
		}
	}
}

