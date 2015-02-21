using System;
using System.Threading.Tasks;

namespace HybridKit {

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
		/// Runs the specified script, possibly asynchronously.
		/// </summary>
		/// <remarks>
		/// This method may dispatch to a different thread to run the passed lambda.
		/// </remarks>
		/// <param name="script">A lambda that interacts with the passed JavaScript global object.</param>
		Task RunScriptAsync (ScriptLambda script);

		// Events:

		event EventHandler Loaded;
	}

	/// <summary>
	/// An object that can evaluate scripts.
	/// </summary>
	/// <remarks>
	/// This interface should be considered private API.
	/// If the type implementing this interface is public,
	/// this interface's methods should have explicit implementations.
	/// </remarks>
	interface IScriptEvaluator {

		/// <summary>
		/// Runs the specified script in the context of the web view and returns the result.
		/// </summary>
		/// <param name="script">Script to execute in the web view.</param>
		/// <returns>Result of evaluating the script</returns>
		string Eval (string script);

		/// <summary>
		/// Called from a non-UI thread to dispatch the specified script to be run in the context of the web view.
		/// </summary>
		/// <remarks>
		/// The dispatch should be done asynchronously, meaning this method should return immediately.
		/// </remarks>
		/// <param name="script">Script to execute in the web view.</param>
		void EvalOnMainThread (string script);
	}
}

