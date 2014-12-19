using System;
using System.Dynamic;

namespace HybridKit {

	class ScriptObject : DynamicObject {

		IWebViewInterface host;
		string scriptPrefix;

		public ScriptObject (IWebViewInterface host, string scriptPrefix = "")
		{
			this.host = host;
			this.scriptPrefix = scriptPrefix;
		}

		public override bool TryInvoke (InvokeBinder binder, object[] args, out object result)
		{

		}
	}
}

