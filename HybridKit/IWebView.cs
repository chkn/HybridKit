using System;
using System.Threading.Tasks;

namespace HybridKit {

	public class NavigatingEventArgs : EventArgs {
		public string Url { get; private set; }
		public bool Cancel { get; set; }
		public NavigatingEventArgs (string url)
		{
			Url = url;
		}
	}

	/// <summary>
	/// Cross-platform interface to the native web view.
	/// </summary>
	/// <remarks>
	/// Generally, implementations are also responsible for intercepting
	///  JavaScript <c>prompt</c> calls and passing them to <c>ScriptFunction.HandlePrompt</c>.
	/// </remarks>
	public interface IWebView {

		/// <summary>
		/// Provides a facility for pre-caching certain resources.
		/// </summary>
		/// <remarks>
		/// The resources that are added to this cache are *always* used, and
		///   the network will never be used to access the cached URLs, even if
		///   a newer version of the resource exists remotely.
		/// </remarks>
		CachedResources Cache { get; }

		/// <summary>
		/// Raised when the web view has loaded its document and is ready to execute scripts.
		/// </summary>
		event EventHandler Loaded;

		/// <summary>
		/// Raised when the web view is about to navigate to a new URL.
		/// </summary>
		event EventHandler<NavigatingEventArgs> Navigating;

		/// <summary>
		/// Loads a page from the app bundle into the <c>WebView</c>.
		/// </summary>
		/// <remarks>
		/// For Android projects, the page must be added to the Assets folder of your project.
		/// The <c>bundleRelativePath</c> argument refers to the path relative to that folder.
		/// </remarks>
		/// <param name="bundleRelativePath">Bundle-relative path of the page to load.</param>
		void LoadFile (string bundleRelativePath);

		void LoadString (string html, string baseUrl = null);

		/// <summary>
		/// Runs the specified script in the context of the web view and returns the result.
		/// </summary>
		/// <remarks>
		/// The implementor should dispatch to the main thread if necessary.
		/// </remarks>
		/// <param name="script">Script to execute in the web view.</param>
		/// <returns>Result of evaluating the script</returns>
		Task<string> EvalAsync (string script);
	}
	public static class IWebViewEx {

		public static async Task<TResult> EvalAsync<TResult> (this IWebView webView, string scriptFmt, params object [] args)
		{
			// Marshal the args.
			var strArgs = new string [args.Length];
			for (var i = 0; i < args.Length; i++)
				strArgs [i] = ScriptObject.MarshalToScript (args [i], webView);

			// Make the call..
			var script = string.Format (scriptFmt, strArgs);
			return await ScriptObject.Eval<TResult> (webView, script);
		}

		// FIXME: Allow more than one ScriptObject arg in args
		public static TResult EvalLazy<TResult> (this IWebView webView, string scriptFmt, params object [] args)
			where TResult : ScriptObject
		{
			ScriptObject parent = null;

			// Marshal the args.
			var strArgs = new string [args.Length];
			for (var i = 0; i < args.Length; i++) {
				var scriptObject = args [i] as ScriptObject;
				if (scriptObject != null) {
					if (parent != null)
						throw new ArgumentException ("Maximum of 1 ScriptObject argument allowed currently");
					parent = scriptObject;
				}
				strArgs [i] = ScriptObject.MarshalToScript (args [i], webView);
			}

			var script = string.Format (scriptFmt, strArgs);
			var so = (parent == null)? new ScriptObject (webView, script) : new ScriptObject (parent, script);
			return ScriptObject.AsTyped<TResult> (webView, so);
		}
	}
}

