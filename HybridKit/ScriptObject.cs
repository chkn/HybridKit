using System;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Dynamic;
using System.Reflection;
using System.Linq.Expressions;
using System.Xml.Serialization;
using System.Collections.Generic;

namespace HybridKit {

	public class ScriptObject : IDynamicMetaObjectProvider {

		#pragma warning disable 414

		// Disable warning that this field is set but never used.
		// We need to keep a ref to the parent object to prevent it
		//  from being prematurely deleted from the byRefObjects hash
		ScriptObject parent;

		#pragma warning restore 414

		IScriptEvaluator host;
		string refScript, disposeScript;

		protected dynamic ScriptThis {
			get { return this; }
		}

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
				//Console.WriteLine ("DISPOSING: {0}", disposeScript);
				host.EvalOnMainThread (disposeScript);
			}
		}

		/// <summary>
		/// Returns a <c>ScriptObject</c> representing a member of this instance.
		/// </summary>
		/// <param name="name">The name of the member.</param>
		[XmlPreserve]
		ScriptObject Member (string name)
		{
			// FIXME: Escape to Javascript allowed member names?
			var script = Ref ().Append ('.').Append (name).ToString ();
			return new ScriptObject (this, script);
		}

		/// <summary>
		/// Returns a <c>ScriptObject</c> representing an index of this instance.
		/// </summary>
		/// <param name="name">The index.</param>
		[XmlPreserve]
		ScriptObject Index (object index)
		{
			var sb = Ref ().Append ('[');
			MarshalIn (sb, index);
			return new ScriptObject (this, sb.Append (']').ToString ());
		}

		/// <summary>
		/// Gets the value of this instance.
		/// </summary>
		[XmlPreserve]
		object Get (Type expectedType = null)
		{
			return Eval (refScript, expectedType);
		}

		/// <summary>
		/// Sets the value of this instance to the specified value.
		/// </summary>
		/// <param name="value">Value.</param>
		[XmlPreserve]
		object Set (object value, Type expectedType = null)
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
		object Invoke (Type expectedType = null, params object [] args)
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

		/// <summary>
		/// Assuming this instance represents a JavaScript RegExp object,
		///  convert it to a C# Regex.
		/// </summary>
		/// <returns>The regex.</returns>
		[XmlPreserve]
		Regex ToRegex ()
		{
			var opts = RegexOptions.None;
			if (host.Eval (refScript + ".ignoreCase") == "true")
				opts |= RegexOptions.IgnoreCase;
			if (host.Eval (refScript + ".multiline") == "true")
				opts |= RegexOptions.Multiline;
			return new Regex (host.Eval (refScript + ".source"), opts);
		}

		/// <summary>
		/// Assuming this instance represents a JavaScript array by reference,
		///  unboxes it into a C# array.
		/// </summary>
		/// <returns>The array.</returns>
		/// <param name="arrayType">Array type.</param>
		[XmlPreserve]
		Array ToArray (Type arrayType)
		{
			return (Array)Eval (refScript, arrayType, ScriptType.MarshalByVal);
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

		object Eval (string script, Type expectedType = null, ScriptType marshalAs = default (ScriptType))
		{
			var result = host.Eval (MarshalOut (script, marshalAs));
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
		internal static void MarshalIn (StringBuilder buf, object obj)
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
				buf.Append ("function(){return HybridKit.callback(" + sf.Id + ",arguments)}");
				return;
			}

			// For everything else, pass by value
			JSON.Stringify (obj, buf);
		}

		/// <summary>
		/// Marshals a value that is received from JavaScript.
		/// </summary>
		static string MarshalOut (string script, ScriptType type)
		{
			return "HybridKit.marshalOut(function(){return " + script + "}," + (int)type + ")";
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
			static readonly MethodInfo ToRegexInfo;
			static readonly MethodInfo IndexInfo;
			static readonly MethodInfo ToArrayInfo;

			static ScriptMetaObject ()
			{
				var typeInfo = typeof (ScriptObject).GetTypeInfo ();
				GetInfo = typeInfo.GetDeclaredMethod (nameof (Get));
				SetInfo = typeInfo.GetDeclaredMethod (nameof (Set));
				InvokeInfo = typeInfo.GetDeclaredMethod (nameof (Invoke));
				EqualsInfo = typeInfo.GetDeclaredMethod (nameof (Equals));
				ToRegexInfo = typeInfo.GetDeclaredMethod (nameof (ToRegex));
				IndexInfo = typeInfo.GetDeclaredMethod (nameof (Index));
				ToArrayInfo = typeInfo.GetDeclaredMethod (nameof (ToArray));
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
				var typeInfo = binder.Type.GetTypeInfo ();

				// If the result type is a subclass of ScriptObject, assume it is a strongly-typed
				//  binding and call the constructor with our instance.
				if (typeInfo.IsSubclassOf (typeof (ScriptObject)))
					return StronglyTypedCastResult (binder.Type);

				// As another special case, allow converting JavaScript RegExp objects into C# Regex
				if (typeof (Regex).GetTypeInfo ().IsAssignableFrom (typeInfo))
					return GetRegexResult (Value, binder.Type);

				// Unbox by-ref arrays
				if (typeInfo.IsArray)
					return UnboxArrayResult (Value, binder.Type);

				// Otherwise, get the value of our current instance and try to cast that.
				return GetValueResult (Value, binder.Type);
			}

			public override DynamicMetaObject BindGetMember (GetMemberBinder binder)
			{
				return GetValueResult (Value.Member (binder.Name), binder.ReturnType);
			}

			public override DynamicMetaObject BindSetMember (SetMemberBinder binder, DynamicMetaObject value)
			{
				return SetValueResult (Value.Member (binder.Name), binder.ReturnType, value);
			}

			public override DynamicMetaObject BindInvokeMember (InvokeMemberBinder binder, DynamicMetaObject[] args)
			{
				return InvokeResult (Value.Member (binder.Name), binder.ReturnType, args);
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

			public override DynamicMetaObject BindGetIndex (GetIndexBinder binder, DynamicMetaObject[] indexes)
			{
				if (indexes.Length != 1)
					return base.BindGetIndex (binder, indexes);
				return GetValueResult (Value, indexes [0].Expression, binder.ReturnType);
			}

			DynamicMetaObject StronglyTypedCastResult (Type resultType)
			{
				ConstructorInfo constructor = null;
				var typeInfo = resultType.GetTypeInfo ();
				foreach (var ctor in typeInfo.DeclaredConstructors) {
					var parameters = ctor.GetParameters ();
					if (parameters.Length == 1 && typeof (ScriptObject).GetTypeInfo ().IsAssignableFrom (parameters [0].ParameterType.GetTypeInfo ())) {
						constructor = ctor;
						break;
					}
				}
				return new DynamicMetaObject (
					AddConvertIfNeeded (
						Expression.New (
							constructor,
							Expression.Constant (Value, typeof (ScriptObject))
						), resultType),
					GetRestrictions ());
			}

			DynamicMetaObject GetRegexResult (ScriptObject field, Type resultType)
			{
				return new DynamicMetaObject (
					AddConvertIfNeeded (
						Expression.Call (
							Expression.Constant (field, typeof (ScriptObject)),
							ToRegexInfo
						), resultType),
					GetRestrictions ());
			}

			DynamicMetaObject UnboxArrayResult (ScriptObject field, Type resultType)
			{
				return new DynamicMetaObject (
					AddConvertIfNeeded (
						Expression.Call (
							Expression.Constant (field, typeof (ScriptObject)),
							ToArrayInfo,
							Expression.Constant (resultType, typeof (Type))
						), resultType),
					GetRestrictions ());
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

			DynamicMetaObject GetValueResult (ScriptObject field, Expression index, Type resultType)
			{
				return new DynamicMetaObject (
					AddConvertIfNeeded (
						Expression.Call (
							Expression.Call (Expression.Constant (field, typeof (ScriptObject)), IndexInfo, Expression.Convert (index, typeof (object))),
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

