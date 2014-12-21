
HybridKit = {
	magic: "hybridkit1", //sync with HybridKit.cs

	nextByRefId: 0,
	byRefObjects: { },

	types: { //sync with ScriptType in HybridKit.cs
		"exception": 0, // indicates that the result is an exception thrown
		"blittable": 1, // can be fully represented by JSON.stringify
		"marshalByRef": 2, // must be looked up in byRefObjects
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
		}
		switch (obj.constructor.name) {
		case "Number":
		case "String":
		case "Boolean":
		case "Date":
		case "Error":
			return this.types.blittable;
		}
		return this.types.marshalByRef;
	},

	// called by ScriptObject.MarshalOut
	marshalOut: function (fn) {
		var type, obj;
		try {
			obj = fn();
			type = this.getType(obj);
		} catch (e) {
			obj = e;
			type = this.types.exception;
		}
		var result = { "ScriptType": type };
		switch (type) {

		case this.types.blittable:
		case this.types.exception:
			result.JsonValue = JSON.stringify(obj);
			break;

		case this.types.marshalByRef:
			var byRefId = this.nextByRefId++;
			this.byRefObjects[byRefId] = obj;
			result.RefScript = "HybridKit.byRefObjects[" + byRefId + "]";
			result.DisposeScript = "delete " + result.RefScript;
			break;
		}
		return JSON.stringify(result);
	}
};

// Make Error objects blittable, thanks to http://stackoverflow.com/a/18391400/578190
Object.defineProperty(Error.prototype, 'toJSON', {
    value: function () {
        var alt = {};

        Object.getOwnPropertyNames(this).forEach(function (key) {
            alt[key] = this[key];
        }, this);

        return alt;
    },
    configurable: true
});