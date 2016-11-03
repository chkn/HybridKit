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

		// This is what will be passed as the 'defaultValue' argument to the JavaScript 'prompt'
		//  function to indicate that we are to handle it specially as a callback into C#.
		public const string PromptHookDefaultValue = "@HKCallback$";

		internal const string AndroidAssetPrefix = "file:///android_asset/";
	}

	enum ScriptType {
		Unknown = 0,
		Exception = 1,
		MarshalByVal = 2,
		MarshalByRef = 3
	}

	struct MarshaledValue {

		[XmlPreserve] public ScriptType ScriptType { get; set; }

		// Only one of these will be set depending on ScriptType:
		[XmlPreserve] public object Value { get; set; } // for blittable types
		[XmlPreserve] public string RefScript { get; set; } // script to ref MarshalByRef types
		[XmlPreserve] public string DisposeScript { get; set; } // script to release references on MarshalByRef types
	}

	struct PromptCallback {
		[XmlPreserve] public int Id { get; set; }
		[XmlPreserve] public object [] Args { get; set; }
	}
}



