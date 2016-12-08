using System;
using System.Threading.Tasks;

namespace HybridKit.DOM {

	public class Element : ScriptObject {

		protected Element (ScriptObject untyped): base (untyped)
		{
		}

		public Task<string> GetInnerText () => this ["innerText"].GetValue<string> ();
		public Task SetInnerText (string value) => this ["innerText"].SetValue (value);
	}
}
