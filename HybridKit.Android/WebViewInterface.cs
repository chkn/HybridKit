using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Runtime.CompilerServices;

using Java.Interop;

using Android.Webkit;
using Android.App;
using Android.OS;

namespace HybridKit {

	class WebViewInterface : Java.Lang.Object, IWebViewInterface {

		static readonly bool IsKitKatOrNewer = (int)Build.VERSION.SdkInt >= (int)BuildVersionCodes.Kitkat;

		readonly object evalLock;
		readonly HybridWebView webView;
		readonly Activity dispatchActivity;
		TaskCompletionSource<string> resultTcs;


		public string CallbackRefScript {
			get { return "HybridKit_CallbackHandler.PostEvalResult"; }
		}

		public WebViewInterface (HybridWebView webView)
		{
			this.evalLock = new object ();
			this.webView = webView;
			this.dispatchActivity = (Activity)webView.Context;
			webView.AddJavascriptInterface (this, "HybridKit_CallbackHandler");
		}

		internal void LoadHelperScript ()
		{
			using (var reader = new StreamReader (typeof(WebViewInterface).Assembly.GetManifestResourceStream ("HybridKit.HybridKit.js")))
				EvalNoResult (reader.ReadToEnd ());
		}

		void EvalNoResult (string script)
		{
			if (IsKitKatOrNewer) {
				webView.EvaluateJavascript (script, null);
			} else {
				var uri = "javascript:" + script;
				webView.LoadUrl (uri);
			}
		}

		public string Eval (string script)
		{
			lock (evalLock) {
				// FIXME: We'll probably need this to be reentrant when we expose C# to JS.
				if (resultTcs != null)
					throw new InvalidOperationException ("Eval already in progress for this WebView.");
				if (webView.IsInWebClientFrame)
					throw new InvalidOperationException ("Cannot call Eval within WebViewClient callback.");
				if (Looper.MyLooper () == Looper.MainLooper)
					throw new InvalidOperationException ("Cannot call Eval on the main thread.");
				resultTcs = new TaskCompletionSource<string> ();
				try {
					dispatchActivity.RunOnUiThread (() => EvalNoResult (script));
					return resultTcs.Task.Result;
				} finally {
					resultTcs = null;
				}
			}
		}

		public void EvalOnMainThread (string script)
		{
			// For Android, we actually don't want to use the main thread :(
			Eval (script);
		}

		[Export, JavascriptInterface]
		public void PostEvalResult (string result)
		{
			resultTcs.TrySetResult (result);
		}
	}
}

