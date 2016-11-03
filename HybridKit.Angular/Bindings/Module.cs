using System;
using System.Linq;
using System.Reflection;
using System.Collections.Generic;

namespace HybridKit.Angular {

	public class Module : ScriptObject {

		protected Module (ScriptObject untyped): base (untyped)
		{
		}

		public Module Controller (string name, Type type)
		{
			// FIXME
			var ctor = type.GetTypeInfo ().DeclaredConstructors.Single (c => c.GetParameters ().All (AngularAttribute.IsPresent));
			return Controller (name, ctor);
		}

		public Module Controller (string name, ConstructorInfo ctor)
		{
			var func = new ScriptFunction (ctor); // FIXME: leak?
			ScriptThis.controller (name, func);
			return this;
		}
	}
}

