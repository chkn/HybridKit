using System;
using System.IO;
using System.Text;
using System.Dynamic;
using System.Reflection;
using System.Linq.Expressions;

namespace HybridKit {

	class ScriptObject : IDynamicMetaObjectProvider {

		#pragma warning disable 414

		// Disable warning that this field is set but never used.
		// We need to keep a ref to the parent object to prevent it
		//  from being prematurely deleted from the byRefObjects hash
		ScriptObject parent;

		#pragma warning restore 414

		IWebViewInterface host;
		string refScript, disposeScript;

		internal ScriptObject (IWebViewInterface host, string refScript = "self", string disposeScript = null)
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

		~ScriptObject ()
		{
			if (disposeScript != null) {
				//Console.WriteLine ("DISPOSING: {0}", disposeScript);
				host.EvalOnMainThread (disposeScript);
			}
		}

		/// <summary>
		/// Gets the value of this instance.
		/// </summary>
		public object Get (Type expectedType = null)
		{
			var result = host.Eval (MarshalOut (refScript));
			return UnmarshalResult (result, expectedType);
		}

		/// <summary>
		/// Sets the value of this instance to the specified value.
		/// </summary>
		/// <param name="value">Value.</param>
		public object Set (object value, Type expectedType = null)
		{
			var sb = Ref ().Append ('=');
			MarshalIn (sb, value);
			var s = MarshalOut (sb.ToString ());
			var result = host.Eval (s);
			return UnmarshalResult (result, expectedType);
		}

		/// <summary>
		/// Invokes this instance representing a JavaScript function.
		/// </summary>
		/// <param name="args">Arguments to the function invocation.</param>
		public object Invoke (Type expectedType = null, params object [] args)
		{
			var sb = Ref ().Append ('(');
			var first = true;
			foreach (var arg in args) {
				if (!first)
					sb.Append (',');
				else
					first = false;
				MarshalIn (sb, arg);
			}
			var s = MarshalOut (sb.Append (')').ToString ());
			var result = host.Eval (s);
			return UnmarshalResult (result, expectedType);
		}

		public override string ToString ()
		{
			var script = Ref ().Append (".toString()").ToString ();
			var so = new ScriptObject (this, script);
			return so.Get (typeof (string)).ToString ();
		}

		StringBuilder Ref ()
		{
			var sb = new StringBuilder ();
			sb.Append (refScript);
			return sb;
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
				throw new ScriptException (result.JsonValue);

			case ScriptType.MarshalByVal:
				return result.JsonValue != null ? JSON.Parse (result.JsonValue, expectedType) : null;

			case ScriptType.MarshalByRef:
				return new ScriptObject (host, result.RefScript, result.DisposeScript);
			}
			throw new Exception (string.Format ("Invalid ScriptType: {0}", result.ScriptType));
		}

		/// <summary>
		/// Marshals a value that is passed into JavaScript.
		/// </summary>
		static void MarshalIn (StringBuilder buf, object obj)
		{
			var so = obj as ScriptObject;
			if (so != null)
				buf.Append (so.refScript);
			else
				JSON.Stringify (obj, buf);
		}

		/// <summary>
		/// Marshals a value that is received from JavaScript.
		/// </summary>
		string MarshalOut (string script)
		{
			var marshalScript = "HybridKit.marshalOut(function(){return " + script + "})";
			var callback = host.CallbackRefScript;
			if (!string.IsNullOrEmpty (callback))
				marshalScript = callback + "(" + marshalScript + ")";
			return marshalScript;
		}

		#region IDynamicMetaObjectProvider implementation

		DynamicMetaObject IDynamicMetaObjectProvider.GetMetaObject (Expression parameter)
		{
			return new ScriptMetaObject (parameter, BindingRestrictions.Empty, this);
		}

		class ScriptMetaObject : DynamicMetaObject {

			static readonly MethodInfo SetInfo;
			static readonly MethodInfo InvokeInfo;

			static ScriptMetaObject ()
			{
				var typeInfo = typeof (ScriptObject).GetTypeInfo ();
				SetInfo = typeInfo.GetDeclaredMethod ("Set");
				InvokeInfo = typeInfo.GetDeclaredMethod ("Invoke");
			}

			public new ScriptObject Value {
				get { return (ScriptObject)base.Value; }
			}

			public ScriptMetaObject (Expression parameter, BindingRestrictions restrictions, ScriptObject value)
				: base (parameter, restrictions, value)
			{
			}

			public override DynamicMetaObject BindSetMember (SetMemberBinder binder, DynamicMetaObject value)
			{
				return SetValueResult (GetMemberScriptObject (binder.Name), binder.ReturnType, value);
			}

			public override DynamicMetaObject BindGetMember (GetMemberBinder binder)
			{
				return ConstantResult (GetMemberScriptObject (binder.Name));
			}

			public override DynamicMetaObject BindInvokeMember (InvokeMemberBinder binder, DynamicMetaObject[] args)
			{
				return InvokeResult (GetMemberScriptObject (binder.Name), binder.ReturnType, args);
			}

			public override DynamicMetaObject BindInvoke (InvokeBinder binder, DynamicMetaObject[] args)
			{
				return InvokeResult (Value, binder.ReturnType, args);
			}

			ScriptObject GetMemberScriptObject (string name)
			{
				// FIXME: Escape to Javascript allowed member names?
				var script = Value.Ref ().Append ('.').Append (name).ToString ();
				return new ScriptObject (Value, script);
			}

			DynamicMetaObject SetValueResult (ScriptObject field, Type resultType, DynamicMetaObject value)
			{
				return new DynamicMetaObject (
					Expression.Convert (
						Expression.Call (
							Expression.Constant (field, typeof (ScriptObject)),
							SetInfo,
							Expression.Convert (value.Expression, typeof (object)),
							Expression.Constant (resultType, typeof (Type))
						), resultType),
					GetRestrictions ());
			}

			DynamicMetaObject InvokeResult (ScriptObject func, Type resultType, DynamicMetaObject [] args)
			{
				var argExprs = new Expression [args.Length];
				for (var i = 0; i < args.Length; i++)
					argExprs [i] = Expression.Convert (args [i].Expression, typeof (object));
				return new DynamicMetaObject (
					Expression.Convert (
						Expression.Call (
							Expression.Constant (func, typeof (ScriptObject)),
							InvokeInfo,
							Expression.Constant (resultType, typeof (Type)),
							Expression.NewArrayInit (typeof (object), argExprs)
						), resultType),
					GetRestrictions ());
			}

			DynamicMetaObject ConstantResult (ScriptObject value)
			{
				return new DynamicMetaObject (Expression.Constant (value, typeof (ScriptObject)), GetRestrictions (), value);
			}

			BindingRestrictions GetRestrictions ()
			{
				return BindingRestrictions.GetTypeRestriction (Expression, LimitType);
			}
		}

		#endregion
	}
}

