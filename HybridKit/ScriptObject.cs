using System;
using System.IO;
using System.Text;
using System.Dynamic;
using System.Reflection;
using System.Linq.Expressions;

namespace HybridKit {

	class ScriptObject : IDynamicMetaObjectProvider {

		IWebViewInterface host;
		string script;

		public ScriptObject (IWebViewInterface host, string script = "self")
		{
			this.host = host;
			this.script = script;
		}

		// Convenience
		private ScriptObject (ScriptObject parent, StringBuilder newScript)
			: this (parent.host, newScript.ToString ())
		{
		}

		/// <summary>
		/// Gets the value of this instance.
		/// </summary>
		public object Get (Type expectedType = null)
		{
			var result = host.Eval (MarshalOut (script));
			return UnmarshalResult (result, expectedType);
		}

		/// <summary>
		/// Invokes this instance representing a JavaScript function.
		/// </summary>
		/// <param name="args">Arguments to the function invocation.</param>
		public object Invoke (Type expectedType = null, params object [] args)
		{
			var sb = EditScript ().Append ('(');
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
			var s = EditScript ().Append (".toString()").ToString ();
			return host.Eval (s);
		}

		StringBuilder EditScript ()
		{
			var sb = new StringBuilder ();
			sb.Append (script);
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

			case ScriptType.Blittable:
				return JSON.Parse (result.JsonValue, expectedType);

			case ScriptType.MarshalByRef:
				return new ScriptObject (host, result.Script);
			}
			throw new Exception (string.Format ("Invalid ScriptType: {0}", result.ScriptType));
		}

		static void MarshalIn (StringBuilder buf, object obj)
		{
			var so = obj as ScriptObject;
			if (so != null)
				buf.Append (so.script);
			else
				JSON.Stringify (obj, buf);
		}

		static string MarshalOut (string script)
		{
			return string.Format ("HybridKit.marshalOut({0})", script);
		}

		#region IDynamicMetaObjectProvider implementation

		DynamicMetaObject IDynamicMetaObjectProvider.GetMetaObject (Expression parameter)
		{
			return new ScriptMetaObject (parameter, BindingRestrictions.Empty, this);
		}

		class ScriptMetaObject : DynamicMetaObject {

			static readonly MethodInfo InvokeInfo = typeof (ScriptObject).GetMethod ("Invoke");

			public new ScriptObject Value {
				get { return (ScriptObject)base.Value; }
			}

			public ScriptMetaObject (Expression parameter, BindingRestrictions restrictions, ScriptObject value)
				: base (parameter, restrictions, value)
			{
			}

			public override DynamicMetaObject BindGetMember (GetMemberBinder binder)
			{
				return ConstantResult (GetMemberScriptObject (binder.Name));
			}

			public override DynamicMetaObject BindInvokeMember (InvokeMemberBinder binder, DynamicMetaObject[] args)
			{
				return InvokeResult (GetMemberScriptObject (binder.Name), binder.ReturnType, args);
			}

			ScriptObject GetMemberScriptObject (string name)
			{
				// FIXME: Escape to Javascript allowed member names?
				var script = Value.EditScript ().Append ('.').Append (name);
				return new ScriptObject (Value, script);
			}

			DynamicMetaObject InvokeResult (ScriptObject func, Type resultType, DynamicMetaObject [] args)
			{
				// First, convert args => object[]
				var argExprs = Array.ConvertAll (args, dmo => Expression.Convert (dmo.Expression, typeof (object)));
				return new DynamicMetaObject (
					Expression.Call (
						Expression.Constant (func, typeof (ScriptObject)),
						InvokeInfo,
						Expression.Constant (resultType, typeof (Type)),
						Expression.NewArrayInit (typeof (object), argExprs)
					),
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

