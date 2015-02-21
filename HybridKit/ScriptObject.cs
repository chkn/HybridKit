using System;
using System.IO;
using System.Text;
using System.Dynamic;
using System.Reflection;
using System.Linq.Expressions;
using System.Xml.Serialization;
using System.Collections.Generic;

namespace HybridKit {

	public sealed class ScriptObject : IDynamicMetaObjectProvider {

		#pragma warning disable 414

		// Disable warning that this field is set but never used.
		// We need to keep a ref to the parent object to prevent it
		//  from being prematurely deleted from the byRefObjects hash
		ScriptObject parent;

		#pragma warning restore 414

		IScriptEvaluator host;
		string refScript, disposeScript;

		internal ScriptObject (IScriptEvaluator host, string refScript = "self", string disposeScript = null)
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
		[XmlPreserve]
		public object Get (Type expectedType = null)
		{
			return Eval (refScript, expectedType);
		}

		/// <summary>
		/// Sets the value of this instance to the specified value.
		/// </summary>
		/// <param name="value">Value.</param>
		[XmlPreserve]
		public object Set (object value, Type expectedType = null)
		{
			var sb = Ref ().Append ('=');
			MarshalIn (sb, value);
			return Eval (sb.ToString (), expectedType);
		}

		/// <summary>
		/// Invokes this instance representing a JavaScript function.
		/// </summary>
		/// <param name="args">Arguments to the function invocation.</param>
		[XmlPreserve]
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
			sb.Append (')');
			return Eval (sb.ToString (), expectedType);
		}

		public override string ToString ()
		{
			return Eval ("HybridKit.toString(" + refScript + ")", typeof (string))?.ToString ();
		}

		StringBuilder Ref ()
		{
			var sb = new StringBuilder ();
			sb.Append (refScript);
			return sb;
		}

		object Eval (string script, Type expectedType = null)
		{
			var result = host.Eval (MarshalOut (script));
			return UnmarshalResult (result, expectedType);
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
		static string MarshalOut (string script)
		{
			return "HybridKit.marshalOut(function(){return " + script + "})";
		}

		#region Equality

		public override bool Equals (object obj)
		{
			return obj == this;
		}

		// FIXME: Preliminary implementation
		public static bool operator == (object other, ScriptObject obj)
		{
			if (object.ReferenceEquals (obj, other))
				return true;
			if (object.ReferenceEquals (obj, null))
				return other == null;

			// FIXME: Don't depend on other's Type!
			var value = obj.Get (other?.GetType ());
			if (value == null)
				return other == null;

			return value.Equals (other);
		}
		public static bool operator != (object other, ScriptObject obj)
		{
			return !(other == obj);
		}

		#endregion

		#region IDynamicMetaObjectProvider implementation

		DynamicMetaObject IDynamicMetaObjectProvider.GetMetaObject (Expression parameter)
		{
			return new ScriptMetaObject (parameter, BindingRestrictions.Empty, this);
		}

		class ScriptMetaObject : DynamicMetaObject {

			static readonly MethodInfo GetInfo;
			static readonly MethodInfo SetInfo;
			static readonly MethodInfo InvokeInfo;
			static readonly MethodInfo EqualsInfo;

			static ScriptMetaObject ()
			{
				var typeInfo = typeof (ScriptObject).GetTypeInfo ();
				GetInfo = typeInfo.GetDeclaredMethod ("Get");
				SetInfo = typeInfo.GetDeclaredMethod ("Set");
				InvokeInfo = typeInfo.GetDeclaredMethod ("Invoke");
				EqualsInfo = typeInfo.GetDeclaredMethod ("Equals");
			}

			public new ScriptObject Value {
				get { return (ScriptObject)base.Value; }
			}

			public ScriptMetaObject (Expression parameter, BindingRestrictions restrictions, ScriptObject value)
				: base (parameter, restrictions, value)
			{
			}

			public override DynamicMetaObject BindConvert (ConvertBinder binder)
			{
				return GetValueResult (Value, binder.Type);
			}

			public override DynamicMetaObject BindGetMember (GetMemberBinder binder)
			{
				return GetValueResult (GetMemberScriptObject (binder.Name), binder.ReturnType);
			}

			public override DynamicMetaObject BindSetMember (SetMemberBinder binder, DynamicMetaObject value)
			{
				return SetValueResult (GetMemberScriptObject (binder.Name), binder.ReturnType, value);
			}

			public override DynamicMetaObject BindInvokeMember (InvokeMemberBinder binder, DynamicMetaObject[] args)
			{
				return InvokeResult (GetMemberScriptObject (binder.Name), binder.ReturnType, args);
			}

			public override DynamicMetaObject BindInvoke (InvokeBinder binder, DynamicMetaObject[] args)
			{
				return InvokeResult (Value, binder.ReturnType, args);
			}

			public override DynamicMetaObject BindBinaryOperation (BinaryOperationBinder binder, DynamicMetaObject arg)
			{
				switch (binder.Operation) {

				case ExpressionType.Equal:
					return EqualsResult (Value, binder.ReturnType, arg);
				}
				throw new NotImplementedException (binder.Operation.ToString ());
			}

			ScriptObject GetMemberScriptObject (string name)
			{
				// FIXME: Escape to Javascript allowed member names?
				var script = Value.Ref ().Append ('.').Append (name).ToString ();
				return new ScriptObject (Value, script);
			}

			DynamicMetaObject GetValueResult (ScriptObject field, Type resultType)
			{
				return new DynamicMetaObject (
					AddConvertIfNeeded (
						Expression.Call (
							Expression.Constant (field, typeof (ScriptObject)),
							GetInfo,
							Expression.Constant (resultType, typeof (Type))
						), resultType),
					GetRestrictions ());
			}

			DynamicMetaObject SetValueResult (ScriptObject field, Type resultType, DynamicMetaObject value)
			{
				return new DynamicMetaObject (
					AddConvertIfNeeded (
						Expression.Call (
							Expression.Constant (field, typeof (ScriptObject)),
							SetInfo,
							AddConvertIfNeeded (value.Expression, typeof (object)),
							Expression.Constant (resultType, typeof (Type))
						), resultType),
					GetRestrictions ());
			}

			DynamicMetaObject EqualsResult (ScriptObject field, Type resultType, DynamicMetaObject value)
			{
				return new DynamicMetaObject (
					AddConvertIfNeeded (
						Expression.Call (
							Expression.Constant (field, typeof (ScriptObject)),
							EqualsInfo,
							AddConvertIfNeeded (value.Expression, typeof (object))
						), resultType),
					GetRestrictions ());
			}

			DynamicMetaObject InvokeResult (ScriptObject func, Type resultType, DynamicMetaObject [] args)
			{
				var argExprs = new Expression [args.Length];
				for (var i = 0; i < args.Length; i++)
					argExprs [i] = Expression.Convert (args [i].Expression, typeof (object));
				return new DynamicMetaObject (
					AddConvertIfNeeded (
						Expression.Call (
							Expression.Constant (func, typeof (ScriptObject)),
							InvokeInfo,
							Expression.Constant (resultType, typeof (Type)),
							Expression.NewArrayInit (typeof (object), argExprs)
						), resultType),
					GetRestrictions ());
			}

			BindingRestrictions GetRestrictions ()
			{
				return BindingRestrictions.GetTypeRestriction (Expression, LimitType);
			}

			static Expression AddConvertIfNeeded (Expression expr, Type resultType)
			{
				return expr.Type != resultType ? Expression.Convert (expr, resultType) : expr;
			}
		}

		#endregion
	}
}

