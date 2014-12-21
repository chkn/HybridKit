using System;
using System.Threading.Tasks;

namespace HybridKit {

	public interface IWebViewInterface {

		string Eval (string script);
		void EvalOnMainThread (string script);
	}
}

