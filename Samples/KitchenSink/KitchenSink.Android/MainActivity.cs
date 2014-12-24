using System;
using System.Threading;

using Android.App;
using Android.Content;
using Android.Runtime;
using Android.Views;
using Android.Widget;
using Android.Webkit;
using Android.OS;

using HybridKit;

namespace KitchenSink.Android
{
	[Activity (Label = "KitchenSink", MainLauncher = true, Icon = "@drawable/icon")]
	public class MainActivity : Activity
	{
		HybridWebView webView;

		protected override void OnCreate (Bundle bundle)
		{
			base.OnCreate (bundle);
			webView = new HybridWebView (this);
			SetContentView (webView);

			webView.Settings.JavaScriptEnabled = true;
			webView.Settings.JavaScriptCanOpenWindowsAutomatically = true;
			webView.SetWebChromeClient (new WebChromeClient ());
			webView.SetWebViewClient (new Client (this));

			webView.LoadUrl ("file:///android_asset/index.html");
		}

		class Client : WebViewClient {

			MainActivity parent;

			public Client (MainActivity parent)
			{
				this.parent = parent;
			}

			public override void OnPageFinished (WebView view, string url)
			{
				base.OnPageFinished (view, url);

				// We cannot call JavaScript from within one of this class's callbacks.
				//  Use a handler to defer the call until slightly later.
				ThreadPool.QueueUserWorkItem (_ => {
					var window = parent.webView.GetGlobalObject ();
					KitchenSink.CallJavaScript (window);
				});
			}
		}
	}
}


