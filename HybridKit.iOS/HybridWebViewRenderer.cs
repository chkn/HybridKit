using System;
using System.Threading.Tasks;

using UIKit;

using Xamarin.Forms;
using Xamarin.Forms.Platform.iOS;

[assembly:ExportRenderer (typeof (HybridKit.Forms.HybridWebView), typeof (HybridKit.Forms.HybridWebViewRenderer))]

namespace HybridKit.Forms {

	public class HybridWebViewRenderer : WebViewRenderer {

		public static new void Init ()
		{
			// Keeps us from being linked out.

			// Call the ctor here to work around a very weird linker(?) issue
			new HybridWebViewRenderer ();
		}

		protected override void OnElementChanged (VisualElementChangedEventArgs e)
		{
			base.OnElementChanged (e);
			((HybridWebView)e.NewElement).Native = ((UIWebView)NativeView).AsHybridWebView ();
		}
	}
}

