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

		public static void SetAssociatedObject (this NSObject obj, IntPtr key, NSObject value, ObjcAssociationPolicy policy = ObjcAssociationPolicy.RetainNonatomic)
		{
			objc_setAssociatedObject (obj.Handle, key, value.Handle, policy);
			GC.KeepAlive (value);
			GC.KeepAlive (obj);
		}
		public static NSObject GetAssociatedObject (this NSObject obj, IntPtr key)
		{
			var result = Runtime.GetNSObject (objc_getAssociatedObject (obj.Handle, key));
			GC.KeepAlive (obj);
			return result;
		}

		[DllImport (Constants.ObjectiveCLibrary)]
		static extern void objc_setAssociatedObject (IntPtr obj, IntPtr key, IntPtr value, ObjcAssociationPolicy policy);
		[DllImport (Constants.ObjectiveCLibrary)]
		static extern IntPtr objc_getAssociatedObject (IntPtr obj, IntPtr key); 
	}
}

