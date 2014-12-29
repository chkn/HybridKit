using System;

using Android.OS;
using Android.App;
using Android.Webkit;

using HybridKit.Android;

namespace KitchenSink {

	[Activity (Label = "KitchenSink", MainLauncher = true, Icon = "@drawable/icon")]
	public class MainActivity : Activity {

		HybridWebView webView;

		protected override void OnCreate (Bundle bundle)
		{
			base.OnCreate (bundle);
			webView = new HybridWebView (this);

			SetContentView (webView);
			webView.Settings.JavaScriptEnabled = true;
			webView.Settings.JavaScriptCanOpenWindowsAutomatically = true;
			webView.SetWebChromeClient (new WebChromeClient ());
			webView.SetWebViewClient (new Client ());

			webView.LoadUrl ("file:///android_asset/index.html");
		}

		class Client : WebViewClient {

			public override async void OnPageFinished (WebView view, string url)
			{
				base.OnPageFinished (view, url);
				await ((HybridWebView)view).RunScriptAsync (KitchenSink.CallJavaScript);
				Console.WriteLine ("Script finished!");
			}
		}
	}
}


