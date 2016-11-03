using System;
using System.Linq;
using System.Reflection;
using System.Collections.Generic;

namespace HybridKit.Angular {

	public sealed class ModuleRegistration : IDisposable {

		// must lock registeredModules!
		readonly static List<WeakReference<ModuleRegistration>> registeredModules = new List<WeakReference<ModuleRegistration>> ();

		public string Name {
			get;
			private set;
		}

		IWebView webView;
		string [] dependencies;
		Type [] types;
		Action<Module> configure;
		bool registered;

		internal ModuleRegistration (IWebView webView, string name, string [] dependencies, IEnumerable<Type> types, Action<Module> configure)
			: this (webView, name, dependencies, types.ToArray (), configure)
		{
		}
		internal ModuleRegistration (IWebView webView, string name, string [] dependencies, Type [] types, Action<Module> configure)
		{
			Name = name;

			this.webView = webView;
			this.dependencies = dependencies ?? new string [0];
			this.types = types;
			this.configure = configure;

			Register ();
		}

		public void Register ()
		{
			if (registered)
				return;

			webView.Cache.Add (HybridAngular.ScriptUrl, Cached.Resource<ModuleRegistration> ("angular.min.js"));

			// Remove the handler first in case it was already added
			webView.Loaded -= HandleLoaded;
			webView.Loaded += HandleLoaded;

			registered = true;
			lock (registeredModules)
				registeredModules.Add (new WeakReference<ModuleRegistration> (this));
		}

		public void Unregister ()
		{
			webView.Loaded -= HandleLoaded;

			lock (registeredModules) {
				ModuleRegistration moduleReg;
				for (var i = 0; i < registeredModules.Count; i++) {
					if (!registeredModules [i].TryGetTarget (out moduleReg) || moduleReg == this)
						registeredModules.RemoveAt (i--);
				}
			}

			registered = false;
		}

		public void Dispose ()
		{
			Unregister ();
		}

		static async void HandleLoaded (object sender, EventArgs e)
		{
			var webView = (IWebView)sender;
			await webView.RunScriptAsync (window => {
				var moduleNames = new List<string> ();
				lock (registeredModules) {
					ModuleRegistration moduleReg;
					for (var i = 0; i < registeredModules.Count; i++) {
						if (!registeredModules [i].TryGetTarget (out moduleReg)) {
							registeredModules.RemoveAt (i--);
							continue;
						}
						if (moduleReg.webView != webView)
							continue;
						try {
							var module = (Module)window.angular.module (moduleReg.Name, moduleReg.dependencies);

							// Register controllers
							foreach (var type in moduleReg.types) {
								var typeInfo = type.GetTypeInfo ();
								var attr = AngularAttribute.GetAttribute<NgControllerAttribute> (typeInfo);
								if (attr == null)
									continue;

								module.Controller (attr.Name ?? type.Name, type);
							}

							if (moduleReg.configure != null)
								moduleReg.configure (module);
						} catch (Exception ex) {
							// FIXME: What to do with this?! We don't want to prevent all subsequent modules from loading..
							System.Diagnostics.Debug.WriteLine ("HybridKit.Angular: Exception loading module {0}: {1}", moduleReg.Name, ex);
						}
						moduleNames.Add (moduleReg.Name);
					}
				}
				if (moduleNames.Any ())
					Bootstrap (window, moduleNames.ToArray ());
			});
		}

		static void Bootstrap (dynamic window, string [] moduleNames)
		{
			window.angular.bootstrap (window.document, moduleNames);
		}
	}
}

