using System;
using System.Threading.Tasks;

using Xamarin.Forms;

using HybridKit.DOM;

namespace HybridKit.Forms {

	public class HybridWebView : WebView, IWebView {

		internal IWebView Native {
			get;
			set;
		}

		public CachedResources Cache => Native.Cache;
		public Window Window => Native.Window;

		event EventHandler IWebView.Loaded {
			add { Native.Loaded += value; }
			remove { Native.Loaded -= value; }
		}

		event EventHandler<NavigatingEventArgs> IWebView.Navigating {
			add { Native.Navigating += value; }
			remove { Native.Navigating -= value; }
		}

		void IWebView.LoadFile (string bundleRelativePath)
		{
			Source = new BundleWebViewSource (bundleRelativePath);
		}

		void IWebView.LoadString (string html, string baseUrl)
		{
			Source = new HtmlWebViewSource {
				BaseUrl = baseUrl,
				Html = html
			};
		}
	}
}

