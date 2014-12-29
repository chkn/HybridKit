using System;
using System.Reflection;

using Xamarin.Forms;

namespace HybridKit.Forms {

	public class BundleWebViewSource : UrlWebViewSource {

		public static readonly BindableProperty BundleRelativePathProperty =
			BindableProperty.Create ("BundleRelativePath", typeof (string), typeof (BundleWebViewSource), null,
			propertyChanged: (bindable, oldvalue, newvalue) => bindable.SetValue (UrlProperty, GetFullUrl ((string)newvalue)));

		static readonly MethodInfo iOS_GetBundleUrl =
			Type.GetType ("HybridKit.BundleCache, HybridKit.iOS")?.GetTypeInfo ().GetDeclaredMethod ("GetBundleUrl");

		public string BundleRelativePath {
			get { return (string)GetValue (BundleRelativePathProperty); }
			set { SetValue (BundleRelativePathProperty, value); }
		}

		public BundleWebViewSource ()
		{
		}

		public BundleWebViewSource (string bundleRelativePath)
		{
			BundleRelativePath = bundleRelativePath;
		}

		static string GetFullUrl (string bundleUrl)
		{
			return Device.OnPlatform<string> (
				iOS: iOS_GetBundleUrl?.Invoke (null, new object[] { bundleUrl })?.ToString (),
				Android: "file:///android_asset/" + bundleUrl,
				WinPhone: null // not supported
			);
		}
	}
}

