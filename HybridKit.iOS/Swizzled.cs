using System;
using System.Text;
using System.Collections.Generic;
using System.Runtime.InteropServices;

using Foundation;
using ObjCRuntime;

namespace HybridKit {

	sealed class Swizzled<TIMP> : NSObject, IDisposable
		where TIMP : class /* delegate */
	{
		public TIMP Original {
			get;
			private set;
		}
		IntPtr method;
		SetImp setImp;

		// Keep this ref just so it is not GC'd
		TIMP replacement;

		internal Swizzled (IntPtr method, SetImp setImp, TIMP replacement)
		{
			this.method = method;
			this.setImp = setImp;
			this.replacement = replacement;
			Original = setImp (method, replacement);
		}

		protected override void Dispose (bool disposing)
		{
			if (Original != null) {
				setImp (method, Original);
				Original = null;
				if (disposing)
					GC.SuppressFinalize (this);
			}
		}
		~Swizzled ()
		{
			// Unswizzle to ensure calls don't go to invalid function pointer
			Dispose (false);
		}

		public delegate TIMP SetImp (IntPtr method, TIMP imp);
	}
	static class Swizzled {
		public static Swizzled<TIMP> InstanceMethod<TIMP> (IntPtr classHandle, string sel, Swizzled<TIMP>.SetImp setImp, TIMP replacement)
			where TIMP : class
		{
			return InstanceMethod (classHandle, Selector.GetHandle (sel), setImp, replacement);
		}
		public static Swizzled<TIMP> InstanceMethod<TIMP> (IntPtr classHandle, IntPtr selHandle, Swizzled<TIMP>.SetImp setImp, TIMP replacement)
			where TIMP : class
		{
			Swizzled<TIMP> result;

			// Check if we're already swizzled on this class or a superclass..
			IntPtr curClass = classHandle;
			do {
				result = curClass.GetAssociatedObject (selHandle) as Swizzled<TIMP>;
				if (result != null)
					return result;
			} while ((curClass = class_getSuperclass (curClass)) != IntPtr.Zero);

			var methodHandle = class_getInstanceMethod (classHandle, selHandle);
			if (methodHandle == IntPtr.Zero)
				return null;

			result = new Swizzled<TIMP> (methodHandle, setImp, replacement);
			classHandle.SetAssociatedObject (selHandle, result);
			return result;
		}

		[DllImport (Constants.ObjectiveCLibrary)]
		static extern IntPtr class_getSuperclass (IntPtr cls);
		[DllImport (Constants.ObjectiveCLibrary)]
		static extern IntPtr class_getInstanceMethod (IntPtr cls, IntPtr name);
	}

	static class Imps {
		public static readonly Swizzled<IMP1>.SetImp SetImp1 = method_setImplementation;
		public static readonly Swizzled<IMP4>.SetImp SetImp4 = method_setImplementation;

		[MonoNativeFunctionWrapper]
		public delegate void IMP1 (IntPtr id, IntPtr sel, IntPtr arg1);
		[DllImport (Constants.ObjectiveCLibrary)]
		public static extern IMP1 method_setImplementation (IntPtr method, IMP1 imp);

		[MonoNativeFunctionWrapper]
		public delegate IntPtr IMP4 (IntPtr id, IntPtr sel, IntPtr arg1, IntPtr arg2, IntPtr arg3, IntPtr arg4);
		[DllImport (Constants.ObjectiveCLibrary)]
		public static extern IMP4 method_setImplementation (IntPtr method, IMP4 imp);
	}
}
