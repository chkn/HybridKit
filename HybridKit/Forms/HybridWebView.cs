using System;
using System.Threading.Tasks;

using Xamarin.Forms;

namespace HybridKit.Forms {

	public class HybridWebView : WebView, IWebView {

		internal IWebView Native {
			get;
			set;
		}

		public CachedResources Cache {
			get { return Native.Cache; }
		}

		event EventHandler IWebView.Loaded {
			add { Native.Loaded += value; }
			remove { Native.Loaded -= value; }
		}

		/// <summary>
		/// Runs the specified script, possibly asynchronously.
		/// </summary>
		/// <remarks>
		/// This method may dispatch to a different thread to run the passed lambda.
		/// </remarks>
		/// <param name="script">A lambda that interacts with the passed JavaScript global object.</param>
		public Task RunScriptAsync (ScriptLambda script)
		{
			return Native.RunScriptAsync (script);
		}
	}
}

