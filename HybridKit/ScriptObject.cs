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

	public class ScriptObject {

		#pragma warning disable 414

		// Disable warning that this field is set but never used.
		// We need to keep a ref to the parent object to prevent it
		//  from being prematurely deleted from the byRefObjects hash
		ScriptObject parent;

		#pragma warning restore 414

		IWebView host;
		string refScript, disposeScript;

		internal ScriptObject (IWebView host, string refScript, string disposeScript = null)
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

		internal ScriptObject (ScriptObject parent, string refScript)
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
		public TScriptObject MemberOrIndex<TScriptObject> (object index)
			where TScriptObject : ScriptObject
		{
			var sb = Ref ().Append ('[');
			MarshalToScript (sb, index, host);
			return AsTyped<TScriptObject> (host, new ScriptObject (this, sb.Append (']').ToString ()));
		}

		/// <summary>
		/// Returns a <c>ScriptObject</c> representing a member or index of this instance.
		/// </summary>
		public ScriptObject this [object index] {
			get { return MemberOrIndex<ScriptObject> (index); }
		}

		/// <summary>
		/// Gets the value of this instance.
		/// </summary>
		public Task<T> GetValue<T> () => Eval<T> (host, refScript);

		/// <summary>
		/// Sets the value of this instance to the specified value.
		/// </summary>
		/// <param name="value">Value.</param>
		public Task SetValue (object value)
		{
			var sb = Ref ().Append ('=');
			MarshalToScript (sb, value, host);
			return Eval (host, sb.ToString ());
		}

		/// <summary>
		/// Invokes this instance representing a JavaScript function.
		/// </summary>
		/// <param name="args">Arguments to the function invocation.</param>
		public Task<T> Invoke<T> (params object [] args) => Eval<T> (host, GetInvokeScript (args));

		/// <summary>
		/// Invokes this instance representing a JavaScript function that does not return a value.
		/// </summary>
		/// <param name="args">Arguments to the function invocation.</param>
		public Task Invoke (params object [] args) => Eval (host, GetInvokeScript (args));

		public TScriptObject InvokeLazy<TScriptObject> (params object [] args)
			where TScriptObject : ScriptObject
		{
			return AsTyped<TScriptObject> (host, new ScriptObject (this, GetInvokeScript (args)));
		}

		string GetInvokeScript (object [] args)
		{
			var sb = Ref ().Append ('(');
			var first = true;
			foreach (var arg in args) {
				if (!first)
					sb.Append (',');
				else
					first = false;
				MarshalToScript (sb, arg, host);
			}
			sb.Append (')');
			return sb.ToString ();
		}

		/// <summary>
		/// Assuming this instance represents a JavaScript RegExp object,
		///  convert it to a C# Regex.
		/// </summary>
		/// <returns>The regex.</returns>
		public async Task<Regex> ToRegex ()
		{
			var opts = RegexOptions.None;
			var script = $"function(r){{return JSON.stringify([String(r.ignoreCase),String(r.multiline),r.source])}}({refScript})";
			var jsOpts = JSON.Parse<string[]> (await host.EvalAsync (script));
			if (jsOpts [0] == "true") opts |= RegexOptions.IgnoreCase;
			if (jsOpts [1] == "true") opts |= RegexOptions.Multiline;
			return new Regex (jsOpts [2], opts);
		}

		/// <summary>
		/// Assuming this instance represents a JavaScript array by reference,
		///  unboxes it into a C# array.
		/// </summary>
		/// <returns>The array.</returns>
		public Task<T[]> ToArray<T> ()
		{
			return Eval<T[]> (host, refScript, ScriptType.MarshalByVal);
		}

		internal static async Task<T> Eval<T> (IWebView host, string script, ScriptType marshalAs = default (ScriptType))
		{
			return AsTyped<T> (host, await Eval (host, script, typeof (T), marshalAs));
		}

		internal static async Task<object> Eval (IWebView host, string script, Type expectedType = null, ScriptType marshalAs = default (ScriptType))
		{
			var result = await host.EvalAsync (MarshalToManaged (script, marshalAs));
			return UnmarshalResult (host, result, expectedType);
		}

		internal static T AsTyped<T> (IWebView host, object obj)
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

			return (T)Convert.ChangeType (obj, typeof (T));
		}

		static object UnmarshalResult (IWebView host, string result, Type expectedType = null)
		{
			return UnmarshalResult (host, new StringReader (result), expectedType);
		}

		static object UnmarshalResult (IWebView host, TextReader reader, Type expectedType = null)
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
		internal static string MarshalToScript (object obj, IWebView host = null)
		{
			var buf = new StringBuilder ();
			MarshalToScript (buf, obj, host);
			return buf.ToString ();
		}
		internal static void MarshalToScript (StringBuilder buf, object obj, IWebView host = null)
		{
			var so = obj as ScriptObject;
			if (so != null) {
				if (host != null && so.host != host)
					throw new ArgumentException ("ScriptObject passed from different web view");
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

