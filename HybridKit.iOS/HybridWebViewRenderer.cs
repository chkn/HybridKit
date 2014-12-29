using System;
using System.Threading.Tasks;

using UIKit;

using Xamarin.Forms;
using Xamarin.Forms.Platform.iOS;

[assembly:ExportRenderer (typeof (HybridKit.Forms.HybridWebView), typeof (HybridKit.Forms.HybridWebViewRenderer))]

namespace HybridKit.Forms {

	public class HybridWebViewRenderer : WebViewRenderer, IHybridWebViewRenderer {

		public static void Init ()
		{
			// Keeps us from being linked out.
		}

		protected override void OnElementChanged (VisualElementChangedEventArgs e)
		{
			base.OnElementChanged (e);
			((HybridWebView)e.NewElement).Renderer = this;
		}

		#region IHybridWebViewRenderer implementation

		public Task RunScriptAsync (ScriptLambda script)
		{
			return ((UIWebView)NativeView).RunScriptAsync (script);
		}

		#endregion
	}
}

