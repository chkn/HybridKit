using System;

using UIKit;

namespace HybridKit {

	public class UIWebViewInterface : IWebViewInterface {

		readonly UIWebView webView;

		public UIWebViewInterface (UIWebView webView)
		{
			if (webView == null)
				throw new ArgumentNullException ("webView");
			this.webView = webView;
		}

		public string EvalJavaScript (string script)
		{
			return webView.EvaluateJavascript (script);
		}
	}
}

