using System;
using System.Threading.Tasks;

using Android.App;
using Android.Webkit;

using NUnit.Framework;

using HybridKit.Android;

namespace HybridKit.Tests {

	public abstract partial class TestBase {

		internal static Activity Activity {
			get;
			set;
		}

		protected HybridWebView NativeWebView {
			get;
			set;
		}

		protected void LoadHtml (string html)
		{
			NativeWebView.LoadData (html, "text/html", "UTF-8");
		}

		[SetUp]
		public async Task BaseSetup ()
		{
			WebView = NativeWebView = new HybridWebView (Activity);
			NativeWebView.Settings.JavaScriptEnabled = true;

			var client = new LoadingTaskClient ();
			NativeWebView.SetWebViewClient (client);
			NativeWebView.LoadUrl ("about:blank");

			await client.Loaded;
			await Setup ();
		}

		class LoadingTaskClient : WebViewClient {

			readonly TaskCompletionSource<object> tcs = new TaskCompletionSource<object> ();

			public Task Loaded {
				get { return tcs.Task; }
			}

			public override void OnPageFinished (WebView view, string url)
			{
				base.OnPageFinished (view, url);
				tcs.TrySetResult (null);
			}
		}
	}
}

