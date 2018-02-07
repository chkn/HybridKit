using System;
using System.Text;
using System.Reflection;
using System.Collections.Generic;

namespace HybridKit {

	public class ScriptFunction : IDisposable {

		static uint nextId;
		static readonly Dictionary<uint,WeakReference> functions = new Dictionary<uint,WeakReference> (); // LOCK

		object target;
		MethodBase method;

		public uint Id {
			get;
			private set;
		}

		public event EventHandler Disposed;

		protected ScriptFunction ()
		{
			lock (functions) {
				Id = nextId++;
				functions.Add (Id, new WeakReference (this));
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
			Dispose (disposing: true);
			GC.SuppressFinalize (this);
		}
		protected virtual void Dispose (bool disposing)
		{
			lock (functions)
				functions.Remove (Id);
			if (disposing)
				Disposed?.Invoke (this, EventArgs.Empty);
		}
		~ScriptFunction()
		{
			Dispose (disposing: false);
		}

		public static ScriptFunction ById (uint id)
		{
			WeakReference wr;
			ScriptFunction func = null;
			lock (functions) {
				if (functions.TryGetValue (id, out wr)) {
					func = wr.Target as ScriptFunction;
					if (func == null)
						functions.Remove (id);
				}
			}
			return func;
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

			result = ScriptObject.MarshalToScript (func.Invoke (info.Args));
			return true;
		}
	}
}

