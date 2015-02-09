using System;
using NUnit.Framework;

using Android.App;

using HybridKit.Android;


namespace HybridKit.Tests {

	public abstract partial class TestBase {

		internal static Activity Context {
			get;
			set;
		}

		protected HybridWebView WebView {
			get;
			set;
		}

		[SetUp]
		public void BaseSetup ()
		{
			WebView = new HybridWebView (Context);
			WebView.Settings.JavaScriptEnabled = true;
			Setup ();
		}
	}
}

