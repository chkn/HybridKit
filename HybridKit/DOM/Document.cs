using System;
using System.Threading.Tasks;

namespace HybridKit.DOM {

	public class Document : ScriptObject {

		protected Document (ScriptObject untyped): base (untyped)
		{
		}

		public TResult GetElementById<TResult> (string id)
			where TResult : Element
		{
			return this ["getElementById"].InvokeLazy<TResult> (id);
		}
		public Element GetElementById (string id) => GetElementById<Element> (id);
	}
}
