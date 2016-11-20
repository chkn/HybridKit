using System;
using System.Threading.Tasks;
using System.Collections.Generic;

using Android.OS;
using Android.App;
using Android.Views;
using Android.Webkit;
using Android.Graphics;
using Android.Net.Http;

using Window = HybridKit.DOM.Window;

namespace HybridKit.Android {

	public class HybridWebView : WebView, IWebView {

		internal static readonly bool IsJellybeanOrOlder = (int)Build.VERSION.SdkInt < (int)BuildVersionCodes.Kitkat;

		readonly HybridClient hybridClient;
		readonly WebViewScriptEvaluator evaluator;

		public event EventHandler Loaded {
			add { hybridClient.Loaded += value; }
			remove { hybridClient.Loaded -= value; }
		}

		public event EventHandler<NavigatingEventArgs> Navigating {
			add { hybridClient.Navigating += value; }
			remove { hybridClient.Navigating -= value; }
		}

		public CachedResources Cache => hybridClient.Cache;

		/// <summary>
		/// Gets the JavaScript global window object.
		/// </summary>
		public Window Window => new Window (evaluator);

		internal bool IsInWebClientFrame => hybridClient.IsInWebClientFrame;
		internal bool CanRunScriptOnMainThread => IsJellybeanOrOlder && !IsInWebClientFrame;

		public HybridWebView (Activity context): base (context)
		{
			this.hybridClient = new HybridClient (this);
			this.evaluator = new WebViewScriptEvaluator (this);

			base.SetWebViewClient (hybridClient);
		}

		public void LoadFile (string bundleRelativePath)
		{
			var url = HybridKit.AndroidAssetPrefix + bundleRelativePath;
			LoadUrl (url);
		}

		void IWebView.LoadString (string html, string baseUrl)
		{
			LoadDataWithBaseURL (baseUrl ?? HybridKit.AndroidAssetPrefix, html, "text/html", "UTF-8", null);
		}

		public override void SetWebViewClient (WebViewClient client)
		{
			hybridClient.BaseClient = client;
		}

		sealed class HybridClient : WebViewClient {

			readonly HybridWebView parent;

			public event EventHandler Loaded;
			public event EventHandler<NavigatingEventArgs> Navigating;

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
					Loaded?.Invoke (parent, EventArgs.Empty);
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
					var result = BaseClient?.ShouldInterceptRequest (view, url);
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
					var result = BaseClient?.ShouldOverrideUrlLoading (view, url) ?? false;
					if (result)
						return true;

					var args = new NavigatingEventArgs (url);
					Navigating?.Invoke (parent, args);
					if (args.Cancel)
						return true;

					return base.ShouldOverrideUrlLoading (view, url);
				} finally {
					IsInWebClientFrame = false;
				}
			}
		}
	}
}

