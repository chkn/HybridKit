using System;
using System.Reflection;
using System.Collections.Generic;

namespace HybridKit.Angular {

	public static class HybridAngular
	{
		public static readonly string Version = "1.3.14";

		public static readonly string ScriptUrl = "https://ajax.googleapis.com/ajax/libs/angularjs/" + Version + "/angular.min.js";


		/// <summary>
		/// Register all types with an <c>AngularAttribute</c> to be accessible from within the given web view.
		/// </summary>
		/// <remarks>
		///  This will attempt to bootstrap your angular app on each subsequent page load
		///   in the given web view.
		/// </remarks>
		/// <param name="webView">Web view within which to register the module.</param>
		/// <param name="assembly">Assembly to search for types.</param>
		/// <param name="configure">An optional lambda, executed at module registration,
		/// in which you can perform additional module configuration.</param>
		/// <returns>An <c>ModuleRegistration</c> that unregisters the module when disposed.</returns>
		public static ModuleRegistration RegisterNgModule (this IWebView webView, Assembly assembly, Action<Module> configure = null)
		{
			var types = new List<Type> ();
			var attr = AngularAttribute.GetAttribute<NgModuleAttribute> (assembly);

			foreach (var typeInfo in assembly.DefinedTypes) {
				if (AngularAttribute.IsPresent (typeInfo))
					types.Add (typeInfo.AsType ());
			}

			return RegisterNgModule (webView, attr?.Name ?? assembly.GetName ().Name, attr?.Dependencies, types, configure);
		}

		/// <summary>
		/// Register the specified types to be accessible from within the given web view.
		/// </summary>
		/// <remarks>
		///  This will attempt to bootstrap your angular app on each subsequent page load
		///   in the given web view. The passed types must be attributed with an <c>AngularAttribute</c>
		///   subclass to indicate what the type provides.
		/// </remarks>
		/// <param name="webView">Web view within which to register the module.</param>
		/// <param name = "moduleName">Name to give the module.</param>
		/// <param name="types">Types.</param>
		/// <returns>An <c>ModuleRegistration</c> that unregisters the module when disposed.</returns>
		public static ModuleRegistration RegisterNgModule (this IWebView webView, string moduleName, params Type [] types)
		{
			return RegisterNgModule (webView, moduleName, null, (IEnumerable<Type>)types, null);
		}

		/// <summary>
		/// Register the specified types to be accessible from within the given web view.
		/// </summary>
		/// <remarks>
		///  This will attempt to bootstrap your angular app on each subsequent page load
		///   in the given web view. The passed types must be attributed with an <c>AngularAttribute</c>
		///   subclass to indicate what the type provides.
		/// </remarks>
		/// <param name="webView">Web view within which to register the module.</param>
		/// <param name = "moduleName">Name to give the module.</param>
		/// <param name = "moduleDependencies">Names of other modules this module depends on (may be null).</param>
		/// <param name="types">Types.</param>
		/// <returns>An <c>ModuleRegistration</c> that unregisters the module when disposed.</returns>
		public static ModuleRegistration RegisterNgModule (this IWebView webView, string moduleName, string [] moduleDependencies, IEnumerable<Type> types, Action<Module> configure = null)
		{
			// Validate arguments
			if (webView == null)
				throw new ArgumentNullException ("webView");
			if (moduleName == null)
				throw new ArgumentNullException ("moduleName");
			if (string.IsNullOrWhiteSpace (moduleName))
				throw new ArgumentException ("Invalid moduleName");
			if (types == null)
				throw new ArgumentNullException ("types");

			return new ModuleRegistration (webView, moduleName, moduleDependencies, types, configure);
		}
	}
}

