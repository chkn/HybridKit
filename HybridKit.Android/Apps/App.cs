using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Runtime.CompilerServices;

using Android.App;
using Android.Content;
using Android.OS;
using Android.Runtime;
using Android.Views;
using Android.Widget;
using Android.Webkit;

using HybridKit.Android;

namespace HybridKit.Apps {

	public abstract class App : Activity {

		// We need this explicitly for the TP reflection
		public App ()
		{
		}

		protected override void OnCreate (Bundle savedInstanceState)
		{
			base.OnCreate (savedInstanceState);

			// KLUGE: X.Android builds dlls so our entry point is not run.
			//  Depend on some F# compiler internals to locate the type and run it.
			var type = GetType ().DeclaringType;
			var targetTypePrefix = "<StartupCode$" + type.Name + ">.$" + type.Name;
			var targetType = type.Assembly.GetTypes ().Single (ty => ty.FullName.StartsWith (targetTypePrefix, StringComparison.Ordinal));
			RuntimeHelpers.RunClassConstructor (targetType.TypeHandle);

			var webView = new HybridWebView (this);
			webView.Settings.JavaScriptEnabled = true;
			SetContentView (webView);
			OnRun (webView);
		}

		protected abstract void OnRun (IWebView webView);
	}
}
