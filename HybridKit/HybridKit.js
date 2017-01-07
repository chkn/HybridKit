
HybridKit = {
	magic: "hybridkit1", //sync with HybridKit.cs
	promptHookDefaultValue: "@HKCallback$",

	nextByRefId: 0,
	byRefObjects: { },

	types: { //sync with ScriptType in HybridKit.cs
		unknown      : 0,
		exception    : 1, // indicates that the result is an exception thrown
		marshalByVal : 2, // can be fully represented by JSON.stringify
		marshalByRef : 3, // must be looked up in byRefObjects
	},

	getType: function (obj) {
		if (obj == null)
			return this.types.marshalByVal;
		switch (typeof obj) {
		case "undefined":
		case "number":
		case "string":
		case "boolean":
			return this.types.marshalByVal;
		}
		switch (obj.constructor.name) {
		case "Number":
		case "String":
		case "Boolean":
		case "Date":
			return this.types.marshalByVal;
		}
		return (obj instanceof Error)? this.types.marshalByVal : this.types.marshalByRef;
	},

    // called by ScriptObject.MarshalToManaged
	marshalToManaged: function (fnOrObj, type) {
		var obj;
		try {
			obj = (typeof fnOrObj == 'function')? fnOrObj() : fnOrObj;
			if (!type)
				type = this.getType(obj);
		} catch (e) {
			obj = e;
			type = this.types.exception;
		}
		var result = { "ScriptType": type };
		switch (type) {

		case this.types.exception:
		case this.types.marshalByVal:
			result.Value = obj;
			break;

		case this.types.marshalByRef:
			var byRefId = this.nextByRefId++;
			this.byRefObjects[byRefId] = obj;
			result.RefScript = "HybridKit.byRefObjects[" + byRefId + "]";
			result.DisposeScript = "delete " + result.RefScript;
			break;
		}
		return this.toJson(result);
	},

	// called by ScriptObject.MarshalIn
	callback: function(id, args) {
		// ensure the args are an array
		var array = new Array(args.length);
		for (var i = 0; i < args.length; i++)
			array[i] = args[i];
		return eval(self.prompt(JSON.stringify({
			//sync with PromptCallback in HybridKit.cs
			Id: id,
			Args: array
		}), this.promptHookDefaultValue));
	},

	toJson: function (obj) {
		return obj? JSON.stringify(obj, (function (notRoot, val) {
			if (val instanceof Error) {
				var alt = {};
				var keys = Object.getOwnPropertyNames(val);
				for (var i = 0; i < keys.length; i++) {
					var key = keys[i];
					alt[key] = val[key];
				}
				return alt;
			}
			return val;
		}).bind(this)) : "null";
	},

	toString: function (obj) {
		return (obj != null) ? obj.toString() : null;
	}
};
