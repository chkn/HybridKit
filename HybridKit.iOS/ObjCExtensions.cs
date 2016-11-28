using System;
using System.Runtime.InteropServices;

using Foundation;
using ObjCRuntime;

namespace HybridKit {

	enum ObjcAssociationPolicy
	{
		Assign = 0,
		RetainNonatomic = 1,
		CopyNonatomic = 3,
		Retain = 01401,
		Copy = 01403
	};

	static class ObjCExtensions {

		static readonly IntPtr respondsToSelector = Selector.GetHandle ("respondsToSelector:");

		public static void SetAssociatedObject (this INativeObject obj, IntPtr key, NSObject value, ObjcAssociationPolicy policy = ObjcAssociationPolicy.RetainNonatomic)
		{
			SetAssociatedObject (obj.Handle, key, value, policy);
			GC.KeepAlive (obj);
		}
		public static void SetAssociatedObject (this IntPtr obj, IntPtr key, NSObject value, ObjcAssociationPolicy policy = ObjcAssociationPolicy.RetainNonatomic)
		{
			objc_setAssociatedObject (obj, key, value.Handle, policy);
			GC.KeepAlive (value);
		}

		public static NSObject GetAssociatedObject (this INativeObject obj, IntPtr key)
		{
			var result = GetAssociatedObject (obj.Handle, key);
			GC.KeepAlive (obj);
			return result;
		}
		public static NSObject GetAssociatedObject (this IntPtr obj, IntPtr key)
		{
			return Runtime.TryGetNSObject (objc_getAssociatedObject (obj, key));
		}

		public static bool RespondsToSelector (this IntPtr id, IntPtr sel)
		{
			return objc_msgSend_bool (id, respondsToSelector, sel);
		}

		[DllImport (Constants.ObjectiveCLibrary)]
		static extern void objc_setAssociatedObject (IntPtr obj, IntPtr key, IntPtr value, ObjcAssociationPolicy policy);
		[DllImport (Constants.ObjectiveCLibrary)]
		static extern IntPtr objc_getAssociatedObject (IntPtr obj, IntPtr key);

		[DllImport (Constants.ObjectiveCLibrary, EntryPoint = "objc_msgSend")]
		static extern bool objc_msgSend_bool (IntPtr id, IntPtr sel, IntPtr arg);
	}
}

