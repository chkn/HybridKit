using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HybridKit.DOM {

	public class Document : ScriptObject {

		protected Document (ScriptObject untyped): base (untyped)
		{
		}

		public Task<TResult> GetElementById<TResult> (string id)
		{
			return this ["getElementById"].Invoke<TResult> (new object[] { id });
		}
	}
}
