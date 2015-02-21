using System;
using System.Threading.Tasks;
using System.Collections.Generic;

using Android.OS;
using Android.App;
using Android.Views;
using Android.Webkit;
using Android.Graphics;
using Android.Net.Http;

namespace HybridKit.Android {

	public class HybridWebView : WebView, IWebView {

		internal static readonly bool IsJellybeanOrOlder = (int)Build.VERSION.SdkInt < (int)BuildVersionCodes.Kitkat;

		readonly HybridClient hybridClient;
		readonly WebViewScriptEvaluator evaluator;

		public event EventHandler Loaded {
			add { hybridClient.Loaded += value; }
			remove { hybridClient.Loaded -= value; }
		}

		public CachedResources Cache {
			get { return hybridClient.Cache; }
		}

		internal bool IsInWebClientFrame {
			get { return hybridClient.IsInWebClientFrame; }
		}

		internal bool CanRunScriptOnMainThread {
			get { return IsJellybeanOrOlder && !IsInWebClientFrame; }
		}

		public HybridWebView (Activity context): base (context)
		{
			this.hybridClient = new HybridClient (this);
			this.evaluator = new WebViewScriptEvaluator (this);

			base.SetWebViewClient (hybridClient);
		}

		/// <summary>
		/// Runs the specified script, possibly asynchronously.
		/// </summary>
		/// <remarks>
		/// This method may dispatch to a different thread to run the passed lambda.
		/// </remarks>
		/// <param name="script">A lambda that interacts with the passed JavaScript global object.</param>
		public Task RunScriptAsync (ScriptLambda script)
		{
			if (CanRunScriptOnMainThread) {
				script (GetGlobalObject ());
				return Task.FromResult<object> (null);
			} else {
				// This simply ensures we're not running on the UI thread..
				Action closure = () => script (GetGlobalObject ());
				return Task.Run (closure);
			}
		}

		/// <summary>
		/// Gets the JavaScript global window object.
		/// </summary>
		/// <remarks>
		/// On iOS, all calls into JavaScript must be done from the main UI thread.
		/// On Android, calls must NOT be made on the main UI thread. Any other thread is acceptable.
		/// </remarks>
		/// <returns>The global object.</returns>
		ScriptObject GetGlobalObject ()
		{
			return new ScriptObject (evaluator);
		}

		public override void SetWebViewClient (WebViewClient client)
		{
			hybridClient.BaseClient = client;
		}

		sealed class HybridClient : WebViewClient {

			readonly HybridWebView parent;

			public event EventHandler Loaded;

			public WebViewClient BaseClient {
				get;
				set;
			}

			public CachedResources Cache {
				get;
				private set;
			}

			public bool IsInWebClientFrame {
				get;
				private set;
			}

			public HybridClient (HybridWebView parent)
			{
				this.parent = parent;
				this.Cache = new CachedResources ();
			}

			public override void DoUpdateVisitedHistory (WebView view, string url, bool isReload)
			{
				IsInWebClientFrame = true;
				try {
					if (BaseClient != null)
						BaseClient.DoUpdateVisitedHistory (view, url, isReload);
					else
						base.DoUpdateVisitedHistory (view, url, isReload);
				} finally {
					IsInWebClientFrame = false;
				}
			}

			public override void OnFormResubmission (WebView view, Message dontResend, Message resend)
			{
				IsInWebClientFrame = true;
				try {
					if (BaseClient != null)
						BaseClient.OnFormResubmission (view, dontResend, resend);
					else
						base.OnFormResubmission (view, dontResend, resend);
				} finally {
					IsInWebClientFrame = false;
				}
			}

			public override void OnLoadResource (WebView view, string url)
			{
				IsInWebClientFrame = true;
				try {
					if (BaseClient != null)
						BaseClient.OnLoadResource (view, url);
					else
						base.OnLoadResource (view, url);
				} finally {
					IsInWebClientFrame = false;
				}
			}

			public override void OnPageFinished (WebView view, string url)
			{
				parent.evaluator.LoadHelperScript ();
				IsInWebClientFrame = true;
				try {
					var loaded = Loaded;
					if (loaded != null)
						loaded (this, EventArgs.Empty);
					if (BaseClient != null)
						BaseClient.OnPageFinished (view, url);
					else
						base.OnPageFinished (view, url);
				} finally {
					IsInWebClientFrame = false;
				}
			}

			public override void OnPageStarted (WebView view, string url, Bitmap favicon)
			{
				IsInWebClientFrame = true;
				try {
					if (BaseClient != null)
						BaseClient.OnPageStarted (view, url, favicon);
					else
						base.OnPageStarted (view, url, favicon);
				} finally {
					IsInWebClientFrame = false;
				}
			}

			public override void OnReceivedError (WebView view, ClientError errorCode, string description, string failingUrl)
			{
				IsInWebClientFrame = true;
				try {
					if (BaseClient != null)
						BaseClient.OnReceivedError (view, errorCode, description, failingUrl);
					else
						base.OnReceivedError (view, errorCode, description, failingUrl);
				} finally {
					IsInWebClientFrame = false;
				}
			}

			public override void OnReceivedHttpAuthRequest (WebView view, HttpAuthHandler handler, string host, string realm)
			{
				IsInWebClientFrame = true;
				try {
					if (BaseClient != null)
						BaseClient.OnReceivedHttpAuthRequest (view, handler, host, realm);
					else
						base.OnReceivedHttpAuthRequest (view, handler, host, realm);
				} finally {
					IsInWebClientFrame = false;
				}
			}

			public override void OnReceivedLoginRequest (WebView view, string realm, string account, string args)
			{
				IsInWebClientFrame = true;
				try {
					if (BaseClient != null)
						BaseClient.OnReceivedLoginRequest (view, realm, account, args);
					else
						base.OnReceivedLoginRequest (view, realm, account, args);
				} finally {
					IsInWebClientFrame = false;
				}
			}

			public override void OnReceivedSslError (WebView view, SslErrorHandler handler, SslError error)
			{
				IsInWebClientFrame = true;
				try {
					if (BaseClient != null)
						BaseClient.OnReceivedSslError (view, handler, error);
					else
						base.OnReceivedSslError (view, handler, error);
				} finally {
					IsInWebClientFrame = false;
				}
			}

			public override void OnScaleChanged (WebView view, float oldScale, float newScale)
			{
				IsInWebClientFrame = true;
				try {
					if (BaseClient != null)
						BaseClient.OnScaleChanged (view, oldScale, newScale);
					else
						base.OnScaleChanged (view, oldScale, newScale);
				} finally {
					IsInWebClientFrame = false;
				}
			}

			public override void OnTooManyRedirects (WebView view, Message cancelMsg, Message continueMsg)
			{
				IsInWebClientFrame = true;
				try {
					if (BaseClient != null)
						BaseClient.OnTooManyRedirects (view, cancelMsg, continueMsg);
					else
						base.OnTooManyRedirects (view, cancelMsg, continueMsg);
				} finally {
					IsInWebClientFrame = false;
				}
			}

			public override void OnUnhandledKeyEvent (WebView view, KeyEvent e)
			{
				IsInWebClientFrame = true;
				try {
					if (BaseClient != null)
						BaseClient.OnUnhandledKeyEvent (view, e);
					else
						base.OnUnhandledKeyEvent (view, e);
				} finally {
					IsInWebClientFrame = false;
				}
			}

			public override WebResourceResponse ShouldInterceptRequest (WebView view, string url)
			{
				IsInWebClientFrame = true;
				try {
					WebResourceResponse result = null;
					if (BaseClient != null)
						result = BaseClient.ShouldInterceptRequest (view, url);
					if (result == null) {
						// See if any of our cached resources match..
						var cached = Cache.GetCached (url);
						if (cached != null)
							result = new WebResourceResponse (cached.MimeType, "UTF-8", cached.DataSource ());
					}
					return result ?? base.ShouldInterceptRequest (view, url);
				} finally {
					IsInWebClientFrame = false;
				}
			}

			public override bool ShouldOverrideKeyEvent (WebView view, KeyEvent e)
			{
				IsInWebClientFrame = true;
				try {
					return BaseClient != null ? BaseClient.ShouldOverrideKeyEvent (view, e) : base.ShouldOverrideKeyEvent (view, e);
				} finally {
					IsInWebClientFrame = false;
				}
			}

			public override bool ShouldOverrideUrlLoading (WebView view, string url)
			{
				IsInWebClientFrame = true;
				try {
					return BaseClient != null ? BaseClient.ShouldOverrideUrlLoading (view, url) : base.ShouldOverrideUrlLoading (view, url);
				} finally {
					IsInWebClientFrame = false;
				}
			}
		}
	}
}

