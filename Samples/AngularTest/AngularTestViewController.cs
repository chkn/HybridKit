using System;
using System.Drawing;

using Foundation;
using UIKit;

using HybridKit;
using HybridKit.Angular;

namespace AngularTest
{
	public partial class AngularTestViewController : UIViewController
	{
		public AngularTestViewController ()
		{
		}

		public AngularTestViewController (IntPtr handle) : base (handle)
		{
		}

		public override void LoadView ()
		{
			View = new UIWebView ();
		}

		public override void ViewDidLoad ()
		{
			base.ViewDidLoad ();
			var webView = (UIWebView)View;
			webView.AsHybridWebView ().RegisterNgModule (typeof (TodoController).Assembly);
			webView.LoadFromBundle ("www/index.html");
		}
	}
}

