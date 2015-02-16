using System;
using System.Reflection;
using System.Collections.Generic;

namespace HybridKit {

	public class ScriptException : Exception {

		static readonly FieldInfo RemoteStackTraceString = typeof (Exception).GetTypeInfo ().GetDeclaredField ("_remoteStackTraceString");

		internal ScriptException (string json)
			: this (json != null ? JSON.Parse<Dictionary<string,object>> (json) : null)
		{
		}

		internal ScriptException (IDictionary<string,object> errorDict)
			: base (errorDict != null ? errorDict ["message"].ToString () : "An unknown error occurred while executing a script")
		{
			// Hack to add JS stack trace to the exception..
			object trace;
			if (errorDict != null && RemoteStackTraceString != null && errorDict.TryGetValue ("stack", out trace))
				RemoteStackTraceString.SetValue (this, Environment.NewLine + trace.ToString ());
		}
	}
}

