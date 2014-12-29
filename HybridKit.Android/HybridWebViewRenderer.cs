using System;
using System.Threading.Tasks;

using Android.App;

using Xamarin.Forms;
using Xamarin.Forms.Platform.Android;

using AndroidHybridWebView = HybridKit.Android.HybridWebView;
using AndroidWebChromeClient = Android.Webkit.WebChromeClient;
using AndroidWebViewClient = Android.Webkit.WebViewClient;

[assembly:ExportRenderer (typeof (HybridKit.Forms.HybridWebView), typeof (HybridKit.Forms.HybridWebViewRenderer))]

namespace HybridKit.Forms {

	public class HybridWebViewRenderer : WebViewRenderer, IHybridWebViewRenderer {

		static Type webClientType;

		public static void Init ()
		{
			//FIXME:  We need to kludge to get this private class to have all the functionality
			//  of the builtin Android WebView renderer.
			webClientType = Type.GetType ("Xamarin.Forms.Platform.Android.WebViewRenderer+WebClient, Xamarin.Forms.Platform.Android");
		}

		protected override void OnElementChanged (ElementChangedEventArgs<WebView> e)
		{
			if (webClientType == null)
				throw new InvalidOperationException ("HybridWebViewRenderer.Init() must be called before creating a HybridWebView");
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
			((HybridWebView)e.NewElement).Renderer = this;
		}

		#region IHybridWebViewRenderer implementation

		public Task RunScriptAsync (ScriptLambda script)
		{
			return ((AndroidHybridWebView)Control).RunScriptAsync (script);
		}

		#endregion
	}
}

