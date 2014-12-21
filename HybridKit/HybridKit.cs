using System;

namespace HybridKit {
	static class HybridKit {

		public const string Magic = "hybridkit1";
	}

	enum ScriptType {
		Blittable = 0,
		MarshalByRef = 1
	}

	struct MarshaledValue {

		public ScriptType ScriptType { get; set; }

		// Only one of these will be set depending on ScriptType:
		public string JsonValue { get; set; } // for blittable types
		public string Script { get; set; } // script to ref MarshalByRef types
	}
}

