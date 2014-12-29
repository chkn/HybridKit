using System;
using System.Collections.Generic;
using System.Linq;

using Foundation;
using UIKit;

using Xamarin.Forms.Platform.iOS;

using HybridKit.Forms;

namespace KitchenSink {

	[Register ("AppDelegate")]
	public class AppDelegate : FormsApplicationDelegate {

		public override bool FinishedLaunching (UIApplication app, NSDictionary options)
		{
			Xamarin.Forms.Forms.Init ();
			HybridWebViewRenderer.Init ();

			LoadApplication (new App ());
			return base.FinishedLaunching (app, options);
		}
	}
}

