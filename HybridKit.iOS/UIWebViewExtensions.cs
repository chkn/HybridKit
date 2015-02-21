using System;
using System.IO;
using System.Threading.Tasks;
using System.Runtime.InteropServices;

using UIKit;
using Foundation;
using ObjCRuntime;

namespace HybridKit {

	enum ObjcAssociationPolicy
	{
		Assign = 0,
		RetainNonatomic = 1,
		CopyNonatomic = 3,
		Retain = 01401,
		Copy = 01403
	};

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
			return webView.GetInterface ();
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

		internal static UIWebViewInterface GetInterface (this UIWebView webView)
		{
			// First, see if we've already created an interface for this webView
			var existing = webView.GetAssociatedObject (UIWebViewInterface.Key.Handle) as UIWebViewInterface;
			return existing ?? new UIWebViewInterface (webView);
		}

		internal static void SetAssociatedObject (this NSObject obj, IntPtr key, NSObject value, ObjcAssociationPolicy policy = ObjcAssociationPolicy.RetainNonatomic)
		{
			objc_setAssociatedObject (obj.Handle, key, value.Handle, policy);
			GC.KeepAlive (value);
			GC.KeepAlive (obj);
		}
		internal static NSObject GetAssociatedObject (this NSObject obj, IntPtr key)
		{
			var result = Runtime.GetNSObject (objc_getAssociatedObject (obj.Handle, key));
			GC.KeepAlive (obj);
			return result;
		}

		[DllImport (Constants.ObjectiveCLibrary)]
		static extern void objc_setAssociatedObject (IntPtr obj, IntPtr key, IntPtr value, ObjcAssociationPolicy policy);
		[DllImport (Constants.ObjectiveCLibrary)]
		static extern IntPtr objc_getAssociatedObject (IntPtr obj, IntPtr key); 
	}
}

