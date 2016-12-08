using System;
using System.IO;
using System.Linq;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Collections.Generic;

using HybridKit.DOM;

namespace HybridKit.Apps {

	class Binding {
		string currentValue;
		Element element;
		bool dirty;

		public string Value => currentValue;
		public Element Element => element;

		public Binding (string initialValue)
		{
			currentValue = initialValue;
		}

		public Task SetValue (string value)
		{
			if (value != currentValue) {
				currentValue = value;
				if (element != null)
					return element.SetInnerText (value);
				dirty = true;
			}
			return Tasks.Completed;
		}

		public Task SetElement (Element value)
		{
			element = value;
			if (dirty && value != null)
				return element.SetInnerText (currentValue);

			return Tasks.Completed;
		}
	}

	public class ExceptionEventArgs : EventArgs {
		public Exception Exception { get; private set; }
		public ExceptionEventArgs (Exception ex)
		{
			Exception = ex;
		}
	}

	public abstract class HtmlView {

		const string IdPrefix = "_hkid_";

		IWebView webView;
		Document document;
		Dictionary<string,Binding> bindings = new Dictionary<string,Binding> ();

		public bool IsRendered => (document != null);
		public event EventHandler<ExceptionEventArgs> ScriptException;

		public async void SetBinding (string name, object value)
		{
			Binding binding;
			var strValue = Convert.ToString (value); // FIXME: User-specified IFormatProvider?
			try {
				if (bindings.TryGetValue (name, out binding)) {
					await binding.SetValue (strValue);
				} else {
					bindings.Add (name, binding = new Binding (strValue));
					if (IsRendered)
						await SetBindingElement (name, binding);
				}
			} catch (Exception ex) {
				var handler = ScriptException;
				if (handler != null)
					handler (this, new ExceptionEventArgs (ex));
				else
					throw;
			}
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

		async void WebView_Loaded (object sender, EventArgs e)
		{
			var curWebView = (IWebView)sender;
			curWebView.Loaded -= WebView_Loaded;
			document = curWebView.Window.Document;

			// Get all the elements for all the bindings..
			await Task.WhenAll (bindings.Select (kv => SetBindingElement (kv.Key, kv.Value)));
		}

		Task SetBindingElement (string name, Binding binding)
		{
			return binding.SetElement (document.GetElementById (GetBindingId (name)));
		}
	}
}
