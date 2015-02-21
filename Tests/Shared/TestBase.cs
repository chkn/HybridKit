using System;
using System.Threading.Tasks;

namespace HybridKit.Tests {

	public partial class TestBase {

		protected IWebView WebView {
			get;
			set;
		}

		protected virtual Task Setup ()
		{
			// Subclasses can override this for additional setup
			return Task.FromResult<object> (null);
		}
	}
}

