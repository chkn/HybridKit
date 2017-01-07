using System;
using System.IO;

namespace HybridKit.Apps {

	public abstract class HtmlPart : IHtmlWriter {

		/// <summary>
		/// Raised when the HTML content of this instance must be rewritten.
		/// </summary>
		public event EventHandler Invalidated;

		/// <summary>
		/// Writes the current HTML content of this instance to the given <see cref="TextWriter"/>. 
		/// </summary>
		public abstract void WriteHtml (TextWriter writer);

		/// <summary>
		/// Called when this instance is rendered.
		/// </summary>
		public virtual void OnRealized (IWebView webView)
		{
		}

		/// <summary>
		/// Called when this instance will no longer be rendered.
		/// </summary>
		public virtual void OnUnrealize ()
		{
		}

		/// <summary>
		/// Call this when the HTML content of this instance changes.
		/// </summary>
		/// <remarks>
		/// The base implementation raises the <see cref="Invalidated"/> event.
		/// </remarks>
		protected virtual void Update () => Invalidated?.Invoke (this, EventArgs.Empty);
	}
}
