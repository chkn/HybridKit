using System;
using System.Reflection;
using System.Threading.Tasks;

using Android.OS;
using Android.App;
using Android.Webkit;
using Android.Content;

using Xamarin.Android.NUnitLite;

using HybridKit.Android;

namespace HybridKit.Tests.Android
{
	[Activity (Label = "HybridKit.Tests.Android", MainLauncher = true)]
	public class MainActivity : TestSuiteActivity
	{
		protected override void OnCreate (Bundle bundle)
		{
			if ((int)Build.VERSION.SdkInt >= (int)BuildVersionCodes.Kitkat) {
				var dlg = new AlertDialog.Builder (this);
				dlg.SetTitle ("Deadlock!");
				dlg.SetMessage ("WARNING: These tests currently deadlock on Android KitKat or newer (which you are currently using)");
				dlg.SetPositiveButton ("OK", (IDialogInterfaceOnClickListener)null);
				dlg.SetNegativeButton ("More Info", delegate {
					StartActivity (new Intent (Intent.ActionDefault,
						global::Android.Net.Uri.Parse ("https://code.google.com/p/android/issues/detail?id=79924")));
				});
				dlg.Create ().Show ();
			}

			// Setup web view
			//FIXME: Recreate the web view before every test
			var webView = new HybridWebView (this);
			webView.Settings.JavaScriptEnabled = true;
			webView.LoadUrl ("about:blank");

			TestBase.WebView = webView;

			// tests can be inside the main assembly
			AddTest (Assembly.GetExecutingAssembly ());
			// or in any reference assemblies
			// AddTest (typeof (Your.Library.TestClass).Assembly);

			// Once you called base.OnCreate(), you cannot add more assemblies.
			base.OnCreate (bundle);
		}
	}
}

