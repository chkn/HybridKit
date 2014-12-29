using System;

using Android.App;
using Android.Content;
using Android.Runtime;
using Android.Views;
using Android.Widget;
using Android.OS;

using Xamarin.Forms;
using Xamarin.Forms.Platform.Android;

using HybridKit.Forms;

namespace KitchenSink.Forms.Android {

	[Activity (Label = "KitchenSink.Forms.Android", MainLauncher = true, Icon = "@drawable/icon")]
	public class MainActivity : FormsApplicationActivity {

		protected override void OnCreate (Bundle bundle)
		{
			base.OnCreate (bundle);
			Xamarin.Forms.Forms.Init (this, bundle);
			HybridWebViewRenderer.Init ();
			LoadApplication (new App ());
		}
	}
}


