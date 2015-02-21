using System;
using System.Threading.Tasks;

using NUnit.Framework;

using UIKit;
using HybridKit;

namespace HybridKit.Tests {

	public abstract partial class TestBase {

		protected UIWebView NativeWebView {
			get;
			set;
		}

		protected void LoadHtml (string html)
		{
			NativeWebView.LoadHtmlString (html, null);
		}

		[SetUp]
		public async Task BaseSetup ()
		{
			NativeWebView = new UIWebView ();
			WebView = NativeWebView.AsHybridWebView ();
			await Setup ();
		}
	}
}

