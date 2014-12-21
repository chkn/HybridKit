
HybridKit = {
	magic: "hybridkit1", //sync with HybridKit.cs

	nextByRefId: 0,
	byRefObjects: { },

	types: { //sync with ScriptType in HybridKit.cs
		"blittable": 0, // can be fully represented by JSON.stringify
		"marshalByRef": 1, // must be looked up in byRefObjects
	},

	getType: function (obj) {
		if (obj == null)
			return this.types.blittable;
		switch (typeof obj) {
		case "undefined":
		case "number":
		case "string":
		case "boolean":
			return this.types.blittable;

		case "function":
			return this.types.marshalByRef;
		}
		switch (obj.constructor.name) {
		case "Number":
		case "String":
		case "Boolean":
		case "Date":
			return this.types.blittable;
		}
		//FIXME: is this logic correct?
		// If the object has anything that isn't blittable, the object isn't blittable
		for (var key in obj) {
			if (this.getType(obj[key]) != this.types.blittable)
				return this.types.marshalByRef;
		}
		return this.types.blittable;
	},

	// called by ScriptObject.MarshalOut
	marshalOut: function (obj) {
		var type = this.getType (obj);
		var result = { "ScriptType": type };
		if (type === this.types.blittable) {
			result.JsonValue = JSON.stringify(obj);
		} else if (type === this.types.marshalByRef) {
			if (typeof obj.__byRefId === 'undefined')
				obj.__byRefId = this.nextByRefId++;
			this.byRefObjects[obj.__byRefId] = obj;
			result.Script = "HybridKit.byRefObjects[" + obj.__byRefId + "]";
		}
		return JSON.stringify(result);
	}
};
