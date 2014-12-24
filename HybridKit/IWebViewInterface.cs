using System;
using System.Threading.Tasks;

namespace HybridKit {

	public interface IWebViewInterface {

		/// <summary>
		/// Gets the script to reference the callback function, if one is needed.
		/// </summary>
		/// <remarks>
		/// This is used for cases like Android, where the result of an evaluation
		///  must be passed to a callback function in order to return the result
		///  to managed code.
		/// </remarks>
		/// <value>The callback reference script, or null if none is needed.</value>
		string CallbackRefScript { get; }

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

