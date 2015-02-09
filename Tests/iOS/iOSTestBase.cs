using System;
using NUnit.Framework;

using UIKit;
using HybridKit;

namespace HybridKit.Tests {

	public abstract partial class TestBase {

		protected UIWebView WebView {
			get;
			set;
		}

		[SetUp]
		public void BaseSetup ()
		{
			WebView = new UIWebView ();
			Setup ();
		}
	}
}

