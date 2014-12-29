using System;
using System.Threading.Tasks;

using Xamarin.Forms;

namespace HybridKit.Forms {

	interface IHybridWebViewRenderer {
		Task RunScriptAsync (ScriptLambda script);
	}

	public class HybridWebView : WebView {

		internal IHybridWebViewRenderer Renderer {
			get;
			set;
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
			return Renderer.RunScriptAsync (script);
		}
	}
}

