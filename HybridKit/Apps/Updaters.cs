using System;
using System.IO;
using System.Threading.Tasks;

namespace HybridKit.Apps {

	public class ElementsUpdater : IUpdater {

		public const string InnerHTML = "{0}.innerHTML={1}";
		public const string InnerText = "{0}.innerText={1}";

		ScriptObject iterFn, elements;
		Task<ScriptObject> pendingIterFn, pendingElements;

		private ElementsUpdater (IWebView webView, string format)
		{
			if (webView == null)
				throw new ArgumentNullException (nameof (webView));
			pendingIterFn = webView.EvalAsync<ScriptObject> ("function(a,b){{var i=a.length;while(i--)" + string.Format (format, "a[i]", "b") + "}}");
		}
		public ElementsUpdater (IWebView webView, ScriptObject elements, string format): this (webView, format)
		{
			if (elements == null)
				throw new ArgumentNullException (nameof (elements));
			this.elements = elements;
		}
		public ElementsUpdater (IWebView webView, Task<ScriptObject> pendingElements, string format): this (webView, format)
		{
			if (pendingElements == null)
				throw new ArgumentNullException (nameof (pendingElements));
			this.pendingElements = pendingElements;
		}

		public static ElementsUpdater ForClassName (IWebView webView, string className, string format)
		{
			return new ElementsUpdater (webView, webView.EvalAsync<ScriptObject> ("document.getElementsByClassName({0})", className), format);
		}

		public async Task Update (IHtmlWriter writer)
		{
			if (iterFn == null) {
				iterFn = await pendingIterFn;
				pendingIterFn = null;
			}
			if (elements == null) {
				elements = await pendingElements;
				pendingElements = null;
			}
			using (var sw = new StringWriter ()) {
				writer.WriteHtml (sw);
				await iterFn.Invoke (elements, sw.ToString ());
			}
		}
	}

	/// <summary>
	/// Updates an attribute value of a given element.
	/// </summary>
	public class AttributeUpdater : IUpdater {

		ScriptObject setAttribute;
		string attrName;
		string format;

		public AttributeUpdater (ScriptObject element, string attrName, string format = "{0}")
		{
			if (element == null)
				throw new ArgumentNullException (nameof (element));
			setAttribute = element ["setAttribute"];
			this.attrName = attrName;
			this.format = format;
		}

		public Task Update (IHtmlWriter writer)
		{
			using (var sw = new StringWriter ()) {
				writer.WriteHtml (sw);
				var value = string.Format (format, sw.ToString ());
				return setAttribute.Invoke (attrName, value);
			}
		}
	}
}
