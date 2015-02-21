using System;

using UIKit;

using NUnit.Framework;

namespace HybridKit.Tests {

	[TestFixture]
	public class UIWebViewInterfaceTests {

		[Test]
		public void SingleInterfacePerWebView ()
		{
			var webView = new UIWebView ();
			var interface1 = webView.AsHybridWebView ();
			var interface2 = webView.AsHybridWebView ();
			Assert.AreSame (interface1, interface2, "#1");
		}
	}
}

