using System;
using System.Collections.Generic;
using System.Linq;

using Foundation;
using UIKit;

namespace KitchenSink.iOS {

	[Register ("AppDelegate")]
	public partial class AppDelegate : UIApplicationDelegate {

		UIWindow window;

		public override bool FinishedLaunching (UIApplication app, NSDictionary options)
		{
			// create a new window instance based on the screen size
			window = new UIWindow (UIScreen.MainScreen.Bounds) {
				RootViewController = new WebViewController ()
			};

			// make the window visible
			window.MakeKeyAndVisible ();		
			return true;
		}
	}
}

