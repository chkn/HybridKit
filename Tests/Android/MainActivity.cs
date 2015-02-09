using System.Reflection;

using Android.App;
using Android.OS;
using Xamarin.Android.NUnitLite;

using Xamarin.Forms;
using Xamarin.Forms.Platform.Android;

using HybridKit.Forms;

namespace HybridKit.Tests.Android
{
	[Activity (Label = "HybridKit.Tests.Android", MainLauncher = true)]
	public class MainActivity : TestSuiteActivity
	{
		protected override void OnCreate (Bundle bundle)
		{
			TestBase.Context = this;

			// tests can be inside the main assembly
			AddTest (Assembly.GetExecutingAssembly ());
			// or in any reference assemblies
			// AddTest (typeof (Your.Library.TestClass).Assembly);

			// Once you called base.OnCreate(), you cannot add more assemblies.
			base.OnCreate (bundle);
		}
	}
}

