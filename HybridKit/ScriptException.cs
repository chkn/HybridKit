﻿using System;
using System.Reflection;
using System.Collections.Generic;

namespace HybridKit {

	public class ScriptException : Exception {

		static readonly FieldInfo RemoteStackTraceString = typeof (Exception).GetTypeInfo ().GetDeclaredField ("_remoteStackTraceString");

		internal ScriptException (string json)
			: this (json != null ? JSON.Parse<Dictionary<string,string>> (json) : null)
		{
		}

		internal ScriptException (IDictionary<string,string> errorDict)
			: base (errorDict != null ? errorDict ["message"] : "An unknown error occurred while executing a script")
		{
			// Hack to add JS stack trace to the exception..
			string trace;
			if (errorDict != null && RemoteStackTraceString != null && errorDict.TryGetValue ("stack", out trace))
				RemoteStackTraceString.SetValue (this, Environment.NewLine + trace);
		}
	}
}

