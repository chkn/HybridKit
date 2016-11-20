using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using Android.App;
using Android.Content;
using Android.OS;
using Android.Runtime;
using Android.Views;
using Android.Widget;
using Android.Webkit;

namespace HybridKit.Apps {

	public abstract class App : Activity {

		protected override void OnCreate (Bundle savedInstanceState)
		{
			base.OnCreate (savedInstanceState);
			var webView = new WebView (this);
			webView.Settings.JavaScriptEnabled = true;

			webView.LoadData ("Hello world!", "text/plain", "UTF-8");
			SetContentView (webView);
		}

		protected abstract void OnRun (IWebView webView);
	}
}
