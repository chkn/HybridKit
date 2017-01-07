using System;
using System.IO;
using System.Collections.Generic;

using HybridKit.Apps;

// KLUDGE: In `HybridKit` namespace instead of `HybridKit.Apps`
//  to keep things simpler for the F# TP and co.
namespace HybridKit {

	public class Controller : HtmlPart {

		HtmlView view;
		IWebView webView;
		HashSet<Controller> children = new HashSet<Controller> ();

		protected HtmlView View {
			get { return view; }
			set {
				if (view != value) {
					if (value == null)
						throw new ArgumentNullException ("value", "Cannot set to null once a non-null value is set");
					if (view != null)
						view.Invalidated -= ViewInvalidated;
					view = value;
					view.Invalidated += ViewInvalidated;
					LoadView ();
				}
			}
		}
		void ViewInvalidated (object sender, EventArgs e) => Update ();

		public Controller (HtmlView view)
		{
			View = view;
		}
		protected Controller ()
		{
		}

		protected void AddChild<TChild> (string bindingName, TChild child)
			where TChild : Controller
		{
			if (View == null)
				throw new InvalidOperationException ("View is null");

			var binding = new ScalarBinding<TChild> (bindingName) { Value = child };
			View.Bindings.Add (binding);

			// We can add more than one binding per child object, but we only
			//  want to record each child once so we only call OnRealized, etc once
			// (thus the HashSet)
			children.Add (child);
		}

		/// <summary>
		/// Called to load the <see cref="View"/> into the <see cref="WebView"/>.  
		/// </summary>
		public bool LoadView (IWebView webView)
		{
			this.webView = webView;
			return LoadView ();
		}
		bool LoadView ()
		{
			if (View == null || webView == null)
				return false;

			using (var htmlWriter = new StringWriter ()) {
				View.WriteHtml (htmlWriter);
				webView.Loaded += WebViewLoaded;
				webView.LoadString (htmlWriter.ToString ());
			}
			return true;
		}
		void WebViewLoaded (object sender, EventArgs e)
		{
			var curWebView = (IWebView)sender;
			curWebView.Loaded -= WebViewLoaded;
			if (curWebView == webView)
				OnRealized (webView);
		}

		public override void OnRealized (IWebView webView)
		{
			View.OnRealized (webView);
			foreach (var child in children)
				child.OnRealized (webView);
		}

		// FIXME: Call this
		public override void OnUnrealize ()
		{
			View.OnUnrealize ();
			foreach (var child in children)
				child.OnUnrealize ();
		}

		protected override void Update ()
		{
			if (!LoadView ())
				base.Update ();
		}

		/// <summary>
		/// Called to write the <see cref="View"/> HTML content
		///  if it is not the root view.
		/// </summary>
		public override void WriteHtml (TextWriter writer)
		{
			View?.WriteHtml (writer);
		}
	}
}
