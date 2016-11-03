using System;
using System.Reflection;

namespace HybridKit.Angular {

	public abstract class AngularAttribute : ScriptableAttribute {

		protected object Target {
			get;
			private set;
		}

		public static AngularAttribute GetAttribute (Assembly assembly)
		{
			return GetAttribute<AngularAttribute> (assembly);
		}
		public static TAttribute GetAttribute<TAttribute> (Assembly assembly)
			where TAttribute : AngularAttribute
		{
			var attr = assembly.GetCustomAttribute<TAttribute> ();
			if (attr != null)
				attr.Target = assembly;
			return attr;
		}

		public static AngularAttribute GetAttribute (TypeInfo typeInfo)
		{
			return GetAttribute<AngularAttribute> (typeInfo);
		}
		public static TAttribute GetAttribute<TAttribute> (TypeInfo typeInfo)
			where TAttribute : AngularAttribute
		{
			var attr = typeInfo.GetCustomAttribute<TAttribute> (inherit: false);
			if (attr != null)
				attr.Target = typeInfo;
			return attr;
		}

		public static AngularAttribute GetAttribute (ParameterInfo param)
		{
			return GetAttribute<AngularAttribute> (param);
		}
		public static TAttribute GetAttribute<TAttribute> (ParameterInfo param)
			where TAttribute : AngularAttribute
		{
			var attr = param.GetCustomAttribute<TAttribute> (inherit: false);
			if (attr != null)
				attr.Target = param;
			return attr;
		}

		public static bool IsPresent (TypeInfo typeInfo)
		{
			return GetAttribute (typeInfo) != null;
		}
		public static bool IsPresent (ParameterInfo param)
		{
			return GetAttribute (param) != null;
		}
	}

	[AttributeUsage (AttributeTargets.Assembly)]
	public class NgModuleAttribute : AngularAttribute {

		public string Name {
			get;
			private set;
		}

		public string [] Dependencies {
			get;
			private set;
		}

		public NgModuleAttribute ()
		{
		}
		public NgModuleAttribute (string name)
		{
			Name = name;
		}
		public NgModuleAttribute (string name, params string [] dependencies)
		{
			Name = name;
			Dependencies = dependencies;
		}
	}

	[AttributeUsage (AttributeTargets.Class)]
	public class NgControllerAttribute : AngularAttribute {

		public string Name {
			get;
			private set;
		}

		public NgControllerAttribute ()
		{
		}
		public NgControllerAttribute (string name)
		{
			Name = name;
		}
	}

	[AttributeUsage (AttributeTargets.Parameter)]
	public class NgInjectAttribute : AngularAttribute {

		public string Name {
			get;
			private set;
		}

		public NgInjectAttribute ()
		{
		}
		public NgInjectAttribute (string name)
		{
			Name = name;
		}
	}
}

