using System;

// HACK: Provide an attribute to prevent certain members from being linked out
namespace System.Xml.Serialization {
	class XmlPreserveAttribute : Attribute {
	}
}

namespace HybridKit {
	using System.Xml.Serialization;

	public delegate void ScriptLambda (dynamic window);

	static class HybridKit {

		public const string Magic = "hybridkit1";

		internal const string AndroidAssetPrefix = "file:///android_asset/";
	}

	enum ScriptType {
		Exception = 0,
		MarshalByVal = 1,
		MarshalByRef = 2
	}

	struct MarshaledValue {

		[XmlPreserve] public ScriptType ScriptType { get; set; }

		// Only one of these will be set depending on ScriptType:
		[XmlPreserve] public object Value { get; set; } // for blittable types
		[XmlPreserve] public string RefScript { get; set; } // script to ref MarshalByRef types
		[XmlPreserve] public string DisposeScript { get; set; } // script to release references on MarshalByRef types
	}
}



