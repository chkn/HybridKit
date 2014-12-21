using System;
using System.IO;

using UIKit;

namespace HybridKit {

	public class UIWebViewInterface : IWebViewInterface {

		readonly UIWebView webView;

		public UIWebViewInterface (UIWebView webView)
		{
			if (webView == null)
				throw new ArgumentNullException ("webView");
			this.webView = webView;

			// Ensure we've loaded HybridKit.js
			if (webView.EvaluateJavascript ("HybridKit.magic") != HybridKit.Magic) {
				using (var reader = new StreamReader (typeof(UIWebViewInterface).Assembly.GetManifestResourceStream ("HybridKit.HybridKit.js")))
					webView.EvaluateJavascript (reader.ReadToEnd ());
			}
		}

		public string Eval (string script)
		{
			return webView.EvaluateJavascript (script);
		}
	}
}

