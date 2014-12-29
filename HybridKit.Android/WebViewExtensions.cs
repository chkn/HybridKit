using System;

using Android.Webkit;

namespace HybridKit.Android {

	public static class WebViewExtensions {

		/// <summary>
		/// Loads a page from the app bundle into the <c>WebView</c>.
		/// </summary>
		/// <remarks>
		/// For Android projects, the page must be added to the Assets folder of your project.
		/// The <c>bundleRelativePath</c> argument refers to the path relative to that folder.
		/// </remarks>
		/// <param name="webView">Web view in which to load the page.</param>
		/// <param name="bundleRelativePath">Bundle-relative path of the page to load.</param>
		public static void LoadFromBundle (this WebView webView, string bundleRelativePath)
		{
			var url = HybridKit.AndroidAssetPrefix + bundleRelativePath;
			webView.LoadUrl (url);
		}
	}
}

