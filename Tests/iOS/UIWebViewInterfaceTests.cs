using System;
using System.Threading.Tasks;

using UIKit;
using Foundation;

using NUnit.Framework;

namespace HybridKit.Tests {

	[TestFixture]
	public class UIWebViewInterfaceTests {

		[Test]
		public void SingleInterfacePerWebView ()
		{
			var webView = new UIWebView ();
			var interface1 = webView.AsHybridWebView ();
			var interface2 = webView.AsHybridWebView ();
			Assert.AreSame (interface1, interface2, "#1");
		}

		[Test]
		public async Task DelegatePreset ()
		{
			await DelegatePreset<TestDelegate> ();
		}

		[Test]
		public async Task DelegatePostset ()
		{
			await DelegatePostset<TestDelegate> ();
		}

		[Test]
		public async Task DelegateSubclassPreset ()
		{
			await DelegatePreset<TestDelegateSubclass> ();
		}

		[Test]
		public async Task DelegateSubclassPostset ()
		{
			await DelegatePostset<TestDelegateSubclass> ();
		}

		[Test]
		public async Task DelegateNonHybridAfterHybrid ()
		{
			var del = new TestDelegate ();
			var webView1 = new UIWebView {
				Delegate = del
			};
			var hybrid = webView1.AsHybridWebView ();

			var webView2 = new UIWebView {
				Delegate = del
			};
			await TestLoadCallbacks (webView2, hybrid, del, false);
		}

		[Test]
		public async Task UnswizzledDelegateNonHybridAfterHybrid ()
		{
			var del1 = new TestDelegate ();
			var webView1 = new UIWebView {
				Delegate = del1
			};
			var hybrid = webView1.AsHybridWebView ();

			var del2 = new UnswizzledTestDelegateSubclass ();
			var webView2 = new UIWebView {
				Delegate = del2
			};
			await TestLoadCallbacks (webView2, hybrid, del2, false);
		}

		Task DelegatePreset<TDelegate> () where TDelegate : TestDelegate, new()
		{
			var webView = new UIWebView ();

			var del = new TDelegate ();
			webView.Delegate = del;

			var hybrid = webView.AsHybridWebView ();
			return TestLoadCallbacks (webView, hybrid, del, true);
		}

		Task DelegatePostset<TDelegate> () where TDelegate : TestDelegate, new()
		{
			var webView = new UIWebView ();

			var del = new TDelegate ();
			var hybrid = webView.AsHybridWebView ();

			webView.Delegate = del;
			return TestLoadCallbacks (webView, hybrid, del, true);
		}


		async Task TestLoadCallbacks (UIWebView webView, IWebView hybrid, TestDelegate del, bool hybridLoadedShouldBeCalled)
		{
			var timeout = TimeSpan.FromSeconds (2);
			var loadedTcs = new TaskCompletionSource<object> ();
			hybrid.Loaded += delegate {
				loadedTcs.TrySetResult (null);
			};

			webView.LoadHtmlString ("<html><head></head><body></body></html>", null);
			await Task.WhenAny (
				Task.Delay (timeout),
				Task.WhenAll (loadedTcs.Task, del.Finished)
			);

			Assert.AreEqual (hybridLoadedShouldBeCalled, loadedTcs.Task.IsCompleted, "#1");
			Assert.IsTrue (del.LoadStartedCalled, "#2");
			Assert.IsTrue (del.LoadFinishedCalled, "#3");
		}

		class TestDelegate : UIWebViewDelegate {

			readonly TaskCompletionSource<object> finished = new TaskCompletionSource<object> ();

			public Task Finished {
				get { return finished.Task; }
			}

			public bool ShouldStartLoadCalled {
				get;
				private set;
			}

			public bool LoadStartedCalled {
				get;
				private set;
			}

			public bool LoadFinishedCalled {
				get;
				private set;
			}

			public bool LoadFailedCalled {
				get;
				private set;
			}

			public override bool ShouldStartLoad (UIWebView webView, Foundation.NSUrlRequest request, UIWebViewNavigationType navigationType)
			{
				ShouldStartLoadCalled = true;
				return true;
			}

			public override void LoadStarted (UIWebView webView)
			{
				LoadStartedCalled = true;
			}

			public override void LoadingFinished (UIWebView webView)
			{
				LoadFinishedCalled = true;
				finished.TrySetResult (null);
			}

			public override void LoadFailed (UIWebView webView, Foundation.NSError error)
			{
				LoadFailedCalled = true;
				finished.TrySetException (new NSErrorException (error));
			}
		}

		class TestDelegateSubclass : TestDelegate {
		}

		class UnswizzledTestDelegateSubclass : TestDelegateSubclass {
		}
	}
}

