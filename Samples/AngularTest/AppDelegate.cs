using System;
using System.Linq;
using System.Collections.Generic;

using Foundation;
using UIKit;

namespace AngularTest {

	[Register ("AppDelegate")]
	public partial class AppDelegate : UIApplicationDelegate {

		public override UIWindow Window {
			get;
			set;
		}
		
		public override bool FinishedLaunching (UIApplication application, NSDictionary launchOptions)
		{
			Window = new UIWindow (UIScreen.MainScreen.Bounds);
			Window.RootViewController = new AngularTestViewController ();
			Window.MakeKeyAndVisible ();
			return true;
		}
	}
}

