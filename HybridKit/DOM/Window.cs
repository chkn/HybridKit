using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HybridKit.DOM {

	public class Window : ScriptObject {

		public Document Document => MemberOrIndex<Document> ("document");

		protected Window (ScriptObject untyped): base (untyped)
		{
		}

		internal Window (IScriptEvaluator host): base (host, "window")
		{
		}
	}
}
