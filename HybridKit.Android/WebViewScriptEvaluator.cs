using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Runtime.CompilerServices;
using System.Collections.Concurrent;

using Java.Interop;

using Android.Webkit;
using Android.App;
using Android.OS;

namespace HybridKit.Android {

	sealed class WebViewScriptEvaluator : Java.Lang.Object, IScriptEvaluator {

		const string CallbackGlobalObject = "HybridKit_CallbackHandler";
		const string CallbackMethod = nameof(PostEvalResult);
		const string Callback = CallbackGlobalObject + "." + CallbackMethod;

		readonly HybridWebView webView;
		readonly Activity dispatchActivity;
		readonly ConcurrentQueue<TaskCompletionSource<string>> results;

		public WebViewScriptEvaluator (HybridWebView webView)
		{
			this.webView = webView;
			this.dispatchActivity = (Activity)webView.Context;
			this.results = new ConcurrentQueue<TaskCompletionSource<string>> ();
			webView.AddJavascriptInterface (this, CallbackGlobalObject);
		}

		internal void LoadHelperScript ()
		{
			using (var reader = new StreamReader (typeof (HybridKit).Assembly.GetManifestResourceStream ("HybridKit.HybridKit.js")))
				EvalNoResult (reader.ReadToEnd ());
		}

		void EvalNoResult (string script)
		{
			if (HybridWebView.IsJellybeanOrOlder) {
				var uri = "javascript:" + script;
				webView.LoadUrl (uri);
			} else {
				webView.EvaluateJavascript (script, null);
			}
		}

		async void StartEval (TaskCompletionSource<string> tcs, string script)
		{
			try {
				TaskCompletionSource<string> currentTcs = null;
				while (results.TryPeek (out currentTcs) && (currentTcs != tcs))
					await currentTcs.Task;

				EvalNoResult (script);
			} catch (Exception ex) {
				tcs.TrySetException (ex);
			}
		}

		public async Task<string> EvalAsync (string script)
		{
			var myTcs = new TaskCompletionSource<string> ();
			results.Enqueue (myTcs);

			if (webView.IsInWebClientFrame) {
				//FIXME: Is this really necessary?
				await Task.Yield ();
			}

			script = Callback + "(" + script + ")";
			dispatchActivity.RunOnUiThread (() => StartEval (myTcs, script));
			return await myTcs.Task;
		}

		[Export, JavascriptInterface]
		public void PostEvalResult (string result) {
			TaskCompletionSource<string> tcs;
			if (results.TryDequeue (out tcs))
				tcs.TrySetResult (result);
		}
	}
}

