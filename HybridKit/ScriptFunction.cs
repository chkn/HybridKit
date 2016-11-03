using System;
using System.Text;
using System.Reflection;
using System.Collections.Generic;

namespace HybridKit {

	public class ScriptFunction : IDisposable {

		static int nextId;
		static readonly Dictionary<int,ScriptFunction> functions = new Dictionary<int, ScriptFunction> ();

		object target;
		MethodBase method;

		public int Id {
			get;
			private set;
		}

		public event EventHandler Disposed;

		protected ScriptFunction ()
		{
			lock (functions) {
				Id = nextId++;
				functions.Add (Id, this);
			}
		}
		public ScriptFunction (MethodBase method, object target = null): this ()
		{
			if (method == null)
				throw new ArgumentNullException ("method");
			this.method = method;
			this.target = target;
		}
		public ScriptFunction (Delegate del): this (del.GetMethodInfo (), del.Target)
		{
		}

		public virtual object Invoke (params object[] args)
		{
			var ctor = method as ConstructorInfo;
			return ctor != null ? ctor.Invoke (args) : method.Invoke (target, args);
		}

		public void Dispose ()
		{
			lock (functions)
				functions.Remove (Id);
			var disposed = Disposed;
			if (disposed != null)
				disposed (this, EventArgs.Empty);
		}

		public static ScriptFunction ById (int id)
		{
			ScriptFunction result;
			lock (functions)
				return functions.TryGetValue (id, out result)? result : null;
		}

		/// <summary>
		/// HybridKit does its C# callbacks via the JavaScript <c>prompt</c> function.
		///  All calls to <c>prompt</c> should be passed through this method.
		/// </summary>
		/// <remarks>
		/// If this method returns <c>false</c>,
		///  then the default behavior should be carried out. If this method returns <c>true</c>,
		///  the prompt was handled; the <c>result</c>  should be returned to script and 
		///  no further action should be taken.
		/// </remarks>
		/// <returns><c>true</c>, if prompt was handled, <c>false</c> otherwise.</returns>
		public static bool HandlePrompt (string prompt, string defaultValue, out string result)
		{
			result = null;
			if (defaultValue != HybridKit.PromptHookDefaultValue)
				return false;

			var info = JSON.Parse<PromptCallback> (prompt);
			var func = ById (info.Id);
			if (func == null)
				throw new ObjectDisposedException ("ScriptFunction with Id = " + info.Id);

			var buf = new StringBuilder ();
			ScriptObject.MarshalIn (buf, func.Invoke (info.Args));
			result = buf.ToString ();
			return true;
		}
	}
}

