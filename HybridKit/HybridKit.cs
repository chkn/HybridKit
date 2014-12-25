using System;

namespace HybridKit {
	static class HybridKit {

		public const string Magic = "hybridkit1";
	}

	enum ScriptType {
		Exception = 0,
		Blittable = 1,
		MarshalByRef = 2
	}

	struct MarshaledValue {

		public ScriptType ScriptType { get; set; }

		// Only one of these will be set depending on ScriptType:
		public string JsonValue { get; set; } // for blittable types
		public string RefScript { get; set; } // script to ref MarshalByRef types
		public string DisposeScript { get; set; } // script to release references on MarshalByRef types
	}

	public delegate void ScriptLambda (dynamic window);
}

