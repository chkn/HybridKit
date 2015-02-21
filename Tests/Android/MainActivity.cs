using System;
using System.Reflection;
using System.Threading.Tasks;

using Android.OS;
using Android.App;
using Android.Webkit;
using Android.Content;

using GuiUnit;
using GuiUnit.Android;

using HybridKit.Android;

namespace HybridKit.Tests.Android
{
	[Activity (Label = "HybridKit.Tests.Android", MainLauncher = true)]
	public class MainActivity : TestActivity
	{
		protected override void OnCreate (Bundle bundle)
		{
			base.OnCreate (bundle);
			TestBase.Activity = this;
		}

		protected override void RunTests ()
		{
			new TestRunner ().Execute (new[] {
				// NUnitLite options:
				"-labels",

				// List the test assemblies here:
				Assembly.GetExecutingAssembly ().Location
			});
		}
	}
}

