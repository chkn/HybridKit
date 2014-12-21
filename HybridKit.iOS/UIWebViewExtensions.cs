using System;
using System.IO;

using UIKit;
using Foundation;

namespace HybridKit {

	public static class UIWebViewExtensions {

		public static void LoadFromBundle (this UIWebView webView, string bundleRelativePath)
		{
			var url = BundleCache.GetBundleUrl (bundleRelativePath);
			if (url == null)
				throw new FileNotFoundException (bundleRelativePath);
			var req = NSUrlRequest.FromUrl (url);
			webView.LoadRequest (req);
		}

		public static dynamic GetGlobalObject (this UIWebView webView)
		{
			return new ScriptObject (new UIWebViewInterface (webView));
		}
	}
}

