using System;
using System.Threading.Tasks;

using HybridKit.DOM;

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
		/// Gets the JavaScript global window object.
		/// </summary>
		Window Window { get; }

		// Events:

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
	}

	/// <summary>
	/// An object that can evaluate scripts.
	/// </summary>
	/// <remarks>
	/// This interface should be considered private API.
	/// If the type implementing this interface is public,
	/// this interface's methods should have explicit implementations.
	/// <para>Generally, implementations are also responsible for intercepting
	/// 	JavaScript <c>prompt</c> calls and passing them to <c>ScriptFunction.HandlePrompt</c>.
	/// </para>
	/// </remarks>
	interface IScriptEvaluator {

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
}

