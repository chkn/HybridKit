using System;
using System.Threading.Tasks;

using Android.App;

using NUnit.Framework;

using HybridKit.Android;

namespace HybridKit.Tests {

	public abstract partial class TestBase {

		protected internal static HybridWebView WebView {
			get;
			set;
		}

		[SetUp]
		public void BaseSetup ()
		{
			Setup ();
		}
	}
}

