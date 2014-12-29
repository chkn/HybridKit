using System;

using Xamarin.Forms;
using HybridKit.Forms;

namespace KitchenSink
{
	public class App : Application
	{
		public App ()
		{
			var webView = new HybridWebView ();
			MainPage = new ContentPage {
				Content = webView
			};
			webView.Navigated += delegate {
				webView.RunScriptAsync (KitchenSink.CallJavaScript);
			};
			webView.Source = new BundleWebViewSource ("www/index.html");
		}
	}
}
