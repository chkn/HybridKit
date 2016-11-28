using System;
using System.IO;
using System.Diagnostics;
using System.Collections.Generic;

using HybridKit.DOM;

namespace HybridKit.Apps {

	class Binding {
		string currentValue;
		Element element;

		public string Value {
			get { return currentValue; }
			set {
				currentValue = value;
				if (element != null)
					element.InnerText = value;
			}
		}

		public Element Element {
			get { return element; }
			set {
				element = value;
				if (element != null && currentValue != null)
					element.InnerText = currentValue;
			}
		}
	}

	public abstract class HtmlView {

		const string IdPrefix = "_hkid_";

		IWebView webView;
		Document document;
		Dictionary<string,Binding> bindings = new Dictionary<string,Binding> ();

		public bool IsRendered => (document != null);

		public void SetBinding (string name, object value)
		{
			Binding binding;
			var strValue = Convert.ToString (value); // FIXME: User-specified IFormatProvider?

			if (!bindings.TryGetValue (name, out binding)) {
				bindings.Add (name, binding = new Binding ());
				if (IsRendered)
					SetBindingElement (name, binding);
			}

			binding.Value = strValue;
		}
		public string GetBinding (string name)
		{
			Binding binding;
			return bindings.TryGetValue (name, out binding)? binding.Value : null;
		}

		protected virtual string GetBindingId (string name) => IdPrefix + name;
		protected abstract void RenderHtml (TextWriter writer);

		public void Show (IWebView webView)
		{
			if (webView == null)
				throw new ArgumentNullException (nameof (webView));
			if (this.webView != null && this.webView != webView)
				throw new InvalidOperationException ("Already rendered");
			this.webView = webView;
			Reload ();
		}

		protected void Reload ()
		{
			Debug.WriteLine ("Reloading {0} ...", this);

			var htmlWriter = new StringWriter ();
			RenderHtml (htmlWriter);

			webView.Loaded += WebView_Loaded;
			webView.LoadString (htmlWriter.ToString ());
		}

		void WebView_Loaded (object sender, EventArgs e)
		{
			var curWebView = (IWebView)sender;
			curWebView.Loaded -= WebView_Loaded;
			document = curWebView.Window.Document;

			// Get all the elements for all the bindings..
			foreach (var kv in bindings)
				SetBindingElement (kv.Key, kv.Value);
		}

		void SetBindingElement (string name, Binding binding)
		{
			binding.Element = document.GetElementById (GetBindingId (name));
		}
	}
}
