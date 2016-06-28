using System;

using Android.App;

using Xamarin.Forms;
using Xamarin.Forms.Platform.Android;

using AndroidHybridWebView = HybridKit.Android.HybridWebView;
using AndroidWebChromeClient = Android.Webkit.WebChromeClient;
using AndroidWebViewClient = Android.Webkit.WebViewClient;

[assembly:ExportRenderer (typeof (HybridKit.Forms.HybridWebView), typeof (HybridKit.Forms.HybridWebViewRenderer))]

namespace HybridKit.Forms {

	public class HybridWebViewRenderer : WebViewRenderer {

		//FIXME:  We need to kludge to get this private class to have all the functionality
		//  of the builtin Android WebView renderer.
		static Type webClientType = Type.GetType ("Xamarin.Forms.Platform.Android.WebViewRenderer+WebClient, Xamarin.Forms.Platform.Android");

		public static void Init ()
		{
			// Actual work done in static ctor (for XAML previewer compatibility),
			//  but still have people call this to prevent link out.

			// Call the ctor here to work around a very weird linker(?) issue
			new HybridWebViewRenderer ();
		}

		protected override void OnElementChanged (ElementChangedEventArgs<WebView> e)
		{
			var activity = Context as Activity;
			if (activity == null)
				throw new InvalidOperationException ("HybridKit requires the Context to be an Activity");

			// Replace the default Android WebView with ours
			var hybridWebView = new AndroidHybridWebView (activity);
			var webClient = (AndroidWebViewClient)Activator.CreateInstance (webClientType, this);
			hybridWebView.SetWebViewClient (webClient);
			hybridWebView.SetWebChromeClient (new AndroidWebChromeClient ());
			hybridWebView.Settings.JavaScriptEnabled = true;
			SetNativeControl (hybridWebView);

			base.OnElementChanged (e);
			((HybridWebView)e.NewElement).Native = hybridWebView;
		}
	}
}

