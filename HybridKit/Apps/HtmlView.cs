using System;
using System.IO;
using System.Linq;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Collections.Generic;

namespace HybridKit.Apps {

	class Binding {
		string currentValue;

		IWebView webView;
		ScriptObject elements;
		bool dirty;

		public string Value => currentValue;

		public Binding (string initialValue)
		{
			currentValue = initialValue;
		}

		public Task SetValue (string value)
		{
			if (value != currentValue) {
				currentValue = value;
				if (elements != null)
					return ApplyValue ();
				dirty = true;
			}
			return Tasks.Completed;
		}

		public Task SetElements (IWebView webView, ScriptObject elements)
		{
			this.webView = webView;
			this.elements = elements;
			if (dirty && elements != null) {
				dirty = false;
				return ApplyValue ();
			}
			return Tasks.Completed;
		}

		Task ApplyValue ()
			=> webView.EvalAsync<string> ("function(a){{var i=a.length;while(i--)a[i].innerText={0};}}({1})", currentValue, elements);
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
		Dictionary<string,Binding> bindings = new Dictionary<string,Binding> ();

		bool wasRendered;
		public bool IsRendered { get; private set; }
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
						await SetBindingElements (name, binding);
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

		public static string GetBindingId (string name) => IdPrefix + name;
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
			wasRendered = IsRendered;
			IsRendered = false;

			var htmlWriter = new StringWriter ();
			RenderHtml (htmlWriter);

			webView.Loaded += WebView_Loaded;
			webView.LoadString (htmlWriter.ToString ());
		}

		async void WebView_Loaded (object sender, EventArgs e)
		{
			var curWebView = (IWebView)sender;
			curWebView.Loaded -= WebView_Loaded;

			// Get all the elements for all the bindings..
			if (!wasRendered && webView == curWebView)
				await Task.WhenAll (bindings.Select (kv => SetBindingElements (kv.Key, kv.Value)));

			IsRendered = true;
		}

		async Task SetBindingElements (string name, Binding binding)
		{
			var elements = await webView.EvalAsync<ScriptObject> ("document.getElementsByClassName({0})", GetBindingId (name));
			await binding.SetElements (webView, elements);
		}
	}
}
