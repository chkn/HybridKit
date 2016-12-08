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
			: base (GetMessage (errorDict))
		{
			// Hack to add JS stack trace to the exception..
			object trace;
			if (errorDict != null && RemoteStackTraceString != null && errorDict.TryGetValue ("stack", out trace)) {
				var traceStr = Environment.NewLine + trace.ToString ();
				if (!traceStr.EndsWith (Environment.NewLine, StringComparison.Ordinal))
					traceStr += Environment.NewLine;
				RemoteStackTraceString.SetValue (this, traceStr);
			}
		}

		static string GetMessage (IDictionary<string,object> errorDict)
		{
			object message;
			if (!errorDict.TryGetValue ("message", out message))
				message = "An unknown error occurred while executing a script";
			return message.ToString ();
		}
	}
}

