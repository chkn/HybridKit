using System;

using UIKit;

namespace HybridKit {

	public static class UIWebViewExtensions {

		public static dynamic GetGlobalObject (this UIWebView webView)
		{
			return new ScriptObject (new UIWebViewInterface (webView));
		}
	}
}

