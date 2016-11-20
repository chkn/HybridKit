using System;
using System.IO;
using System.Text;
using System.Linq;
using System.Text.RegularExpressions;
using System.Reflection;
using System.Linq.Expressions;
using System.Xml.Serialization;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace HybridKit {

	public interface IDynamicScriptObject {

		TScriptObject MemberOrIndex<TScriptObject> (object index)
			where TScriptObject : ScriptObject;
		IDynamicScriptObject this [object index] { get; }
		Task<T> GetValue<T> ();
		Task SetValue (object value);
		Task<T> Invoke<T> (params object [] args);
		Task<Regex> ToRegex ();
		Task<T []> ToArray<T> ();
	}

	public class ScriptObject : IDynamicScriptObject {

		#pragma warning disable 414

		// Disable warning that this field is set but never used.
		// We need to keep a ref to the parent object to prevent it
		//  from being prematurely deleted from the byRefObjects hash
		ScriptObject parent;

		#pragma warning restore 414

		IScriptEvaluator host;
		string refScript, disposeScript;

		internal ScriptObject (IScriptEvaluator host, string refScript, string disposeScript = null)
		{
			if (host == null)
				throw new ArgumentNullException ("host");
			if (refScript == null)
				throw new ArgumentNullException ("refScript");
			this.host = host;
			this.refScript = refScript;
			this.disposeScript = disposeScript;
			//Console.WriteLine ("CREATING: {0}", refScript);
		}

		private ScriptObject (ScriptObject parent, string refScript)
			: this (parent.host, refScript)
		{
			this.parent = parent;
		}

		/// <summary>
		/// For use by strongly-typed subclasses of <c>ScriptObject</c>
		/// </summary>
		/// <param name="untyped">The untyped <c>ScriptObject</c> for this instance.</param>
		protected ScriptObject (ScriptObject untyped)
			: this (untyped, untyped.refScript)
		{
		}

		~ScriptObject ()
		{
			if (disposeScript != null) {
				host.EvalAsync (disposeScript);
				disposeScript = null;
			}
		}

		StringBuilder Ref () => new StringBuilder (refScript);

		/// <summary>
		/// Returns an object representing a member or index of this instance.
		/// </summary>
		protected TScriptObject MemberOrIndex<TScriptObject> (object index)
			where TScriptObject : ScriptObject
		{
			var sb = Ref ().Append ('[');
			MarshalToScript (sb, index);
			return AsTyped<TScriptObject> (new ScriptObject (this, sb.Append (']').ToString ()));
		}
		TScriptObject IDynamicScriptObject.MemberOrIndex<TScriptObject> (object index)
		{
			return MemberOrIndex<TScriptObject> (index);
		}

		/// <summary>
		/// Returns a <c>ScriptObject</c> representing a member or index of this instance.
		/// </summary>
		protected IDynamicScriptObject this [object index] {
			get { return MemberOrIndex<ScriptObject> (index); }
		}
		IDynamicScriptObject IDynamicScriptObject.this [object index] {
			get { return this [index]; }
		}

		/// <summary>
		/// Gets the value of this instance.
		/// </summary>
		protected Task<T> GetValue<T> ()
		{
			return Eval<T> (refScript);
		}
		Task<T> IDynamicScriptObject.GetValue<T> ()
		{
			return GetValue<T> ();
		}

		/// <summary>
		/// Sets the value of this instance to the specified value.
		/// </summary>
		/// <param name="value">Value.</param>
		protected Task SetValue (object value)
		{
			var sb = Ref ().Append ('=');
			MarshalToScript (sb, value);
			return Eval (sb.ToString ());
		}
		Task IDynamicScriptObject.SetValue (object value)
		{
			return SetValue (value);
		}

		/// <summary>
		/// Invokes this instance representing a JavaScript function.
		/// </summary>
		/// <param name="args">Arguments to the function invocation.</param>
		protected Task<T> Invoke<T> (params object [] args)
		{
			var sb = Ref ().Append ('(');
			var first = true;
			foreach (var arg in args) {
				if (!first)
					sb.Append (',');
				else
					first = false;
				MarshalToScript (sb, arg);
			}
			sb.Append (')');
			return Eval<T> (sb.ToString ());
		}
		Task<T> IDynamicScriptObject.Invoke<T> (params object [] args)
		{
			return Invoke<T> (args);
		}

		/// <summary>
		/// Assuming this instance represents a JavaScript RegExp object,
		///  convert it to a C# Regex.
		/// </summary>
		/// <returns>The regex.</returns>
		protected async Task<Regex> ToRegex ()
		{
			var opts = RegexOptions.None;
			var script = $"function(r){{return JSON.stringify([String(r.ignoreCase),String(r.multiline),r.source])}}({refScript})";
			var jsOpts = JSON.Parse<string[]> (await host.EvalAsync (script));
			if (jsOpts [0] == "true") opts |= RegexOptions.IgnoreCase;
			if (jsOpts [1] == "true") opts |= RegexOptions.Multiline;
			return new Regex (jsOpts [3], opts);
		}
		Task<Regex> IDynamicScriptObject.ToRegex ()
		{
			return ToRegex ();
		}

		/// <summary>
		/// Assuming this instance represents a JavaScript array by reference,
		///  unboxes it into a C# array.
		/// </summary>
		/// <returns>The array.</returns>
		protected Task<T[]> ToArray<T> ()
		{
			return Eval<T[]> (refScript, ScriptType.MarshalByVal);
		}
		Task<T []> IDynamicScriptObject.ToArray<T> ()
		{
			return ToArray<T> ();
		}

		async Task<T> Eval<T> (string script, ScriptType marshalAs = default (ScriptType))
		{
			return AsTyped<T> (await Eval (script, typeof (T), marshalAs));
		}
		async Task<object> Eval (string script, Type expectedType = null, ScriptType marshalAs = default (ScriptType))
		{
			var result = await host.EvalAsync (MarshalToManaged (script, marshalAs));
			return UnmarshalResult (result, expectedType);
		}

		T AsTyped<T> (object obj)
		{
			if (obj is T)
				return (T)obj;

			var scriptObject = obj as ScriptObject;
			if (scriptObject != null) {
				var ctor = (from c in typeof (T).GetTypeInfo ().DeclaredConstructors
							let args = c.GetParameters ()
							where args.Length == 1 && args [0].ParameterType == typeof (ScriptObject)
							select c).FirstOrDefault ();
				if (ctor == null)
					throw new ArgumentException ("Target type doesn't have a constructor that takes ScriptObject", "T");

				return (T)ctor.Invoke (new object [] { scriptObject });
			}

			if (typeof (T).GetTypeInfo ().IsAssignableFrom (typeof (ScriptObject).GetTypeInfo ()))
				return (T)(object)new ScriptObject (host, JSON.Stringify (obj));

			throw new ArgumentException ("Object cannot be converted to target type", nameof (obj));
		}

		object UnmarshalResult (string result, Type expectedType = null)
		{
			return UnmarshalResult (new StringReader (result), expectedType);
		}
		object UnmarshalResult (TextReader reader, Type expectedType = null)
		{
			var result = JSON.Parse<MarshaledValue> (reader);
			switch (result.ScriptType) {

			case ScriptType.Exception:
				throw new ScriptException ((IDictionary<string,object>)result.Value);

			case ScriptType.MarshalByVal:
				return JSON.Convert (result.Value, expectedType);

			case ScriptType.MarshalByRef:
				return new ScriptObject (host, result.RefScript, result.DisposeScript);
			}
			throw new Exception (string.Format ("Invalid ScriptType: {0}", result.ScriptType));
		}

		/// <summary>
		/// Marshals a value that is passed into JavaScript.
		/// </summary>
		internal static void MarshalToScript (StringBuilder buf, object obj)
		{
			var so = obj as ScriptObject;
			if (so != null) {
				buf.Append (so.refScript);
				return;
			}

//			var del = obj as Delegate;
//			if (del != null)
//				obj = new ScriptFunction (del); // FIXME: This will leak!

			var sf = obj as ScriptFunction;
			if (sf != null) {
				buf.Append ($"function(){{return HybridKit.callback({sf.Id},arguments)}}");
				return;
			}

			// For everything else, pass by value
			JSON.Stringify (obj, buf);
		}

		/// <summary>
		/// Returns a script to marshal the value returned by the given script.
		/// </summary>
		static string MarshalToManaged (string script, ScriptType type)
		{
			return $"HybridKit.marshalToManaged(function(){{return {script};}},{(int)type})";
		}
	}
}

