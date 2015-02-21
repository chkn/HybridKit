using System;
using System.Diagnostics;
using System.Threading.Tasks;

using NUnit.Framework;

namespace HybridKit.Tests {

	[TestFixture]
	public class IWebViewTests : TestBase {

		[Test]
		public async Task CachedResourceScript ()
		{
			var tcs = new TaskCompletionSource<object> ();
			var src = "http://this.url.cant.possibly.actually.exist/dummy/script.js";
			WebView.Cache.Add (src, Cached.Resource ("window.dummyScriptLoaded = true", "application/javascript"));
			WebView.Loaded += async delegate {
				await WebView.RunScriptAsync (window => {
					Assert.IsTrue (((bool?)window.dummyScriptLoaded).GetValueOrDefault (), "#2");
				});
				tcs.TrySetResult (null);
			};
			LoadHtml ("<html><head><script src=\"" + src + "\"></script></head><body></body></html>");
			await tcs.Task;
		}
	}
}

