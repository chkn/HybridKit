using System;
using System.IO;
using System.Collections.Generic;
using System.Threading.Tasks;

using Foundation;

namespace HybridKit {

	static class BundleCache {

		// Referenced by BundleWebViewSource.cs
		public static NSUrl GetBundleUrl (string bundleRelativePath)
		{
			var fileName = Path.GetFileNameWithoutExtension (bundleRelativePath);
			var extension = Path.GetExtension (bundleRelativePath);
			var subdirectory = Path.GetDirectoryName (bundleRelativePath);
			return NSBundle.MainBundle.GetUrlForResource (fileName, extension, subdirectory);
		}
	}
}

