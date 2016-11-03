using System;
using System.Threading.Tasks;

using UIKit;

using Xamarin.Forms;
using Xamarin.Forms.Platform.iOS;

[assembly:ExportRenderer (typeof (HybridKit.Forms.HybridWebView), typeof (HybridKit.Forms.HybridWebViewRenderer))]

namespace HybridKit.Forms {

	public class HybridWebViewRenderer : WebViewRenderer {

		IWebView native;

		public static new void Init ()
		{
			// Keeps us from being linked out.

			// Call the ctor here to work around a very weird linker(?) issue
			new HybridWebViewRenderer ();
		}

		public HybridWebViewRenderer ()
		{
			native = this.AsHybridWebView ();
			native.Loaded += Native_Loaded;
		}

		void Native_Loaded (object sender, EventArgs e)
		{
			// There appears to be a race that causes this to be called before `OnElementChanged`
			var element = Element as HybridWebView;
			if (element != null)
				element.Native = native;
		}

		protected override void OnElementChanged (VisualElementChangedEventArgs e)
		{
			base.OnElementChanged (e);
			((HybridWebView)e.NewElement).Native = native;
		}
	}
}

