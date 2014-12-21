using System;
using System.IO;
using System.Collections.Generic;

using Foundation;

namespace HybridKit {

	public class BundleCacheFile {

		public string BundleRelativePath { get; private set; }
		public string MimeType { get; private set; }
		public NSUrl LocalUrl { get; private set; }

		NSCachedUrlResponse cachedResponse;

		public BundleCacheFile (string bundleRelativePath, string mimeType)
		{
			BundleRelativePath = bundleRelativePath;
			MimeType = mimeType;
			LocalUrl = BundleCache.GetBundleUrl (bundleRelativePath);
			if (LocalUrl == null)
				throw new FileNotFoundException (bundleRelativePath);
		}

		public NSCachedUrlResponse GetCachedResponse (NSUrlRequest req)
		{
			if (cachedResponse == null) {
				var data = NSData.FromUrl (LocalUrl);
				var resp = new NSUrlResponse (req.Url, MimeType, (nint)data.Length, null);
				cachedResponse = new NSCachedUrlResponse (resp, data);
			}
			return cachedResponse;
		}
	}

	public class BundleCache : NSUrlCache {

		IDictionary<string,BundleCacheFile> urlToBundleFiles;

		public BundleCache (IDictionary<string,BundleCacheFile> urlToBundleFiles)
		{
			this.urlToBundleFiles = urlToBundleFiles;
		}

		public override NSCachedUrlResponse CachedResponseForRequest (NSUrlRequest request)
		{
			BundleCacheFile file;
			return urlToBundleFiles.TryGetValue (request.Url.AbsoluteString, out file) ?
				file.GetCachedResponse (request) : base.CachedResponseForRequest (request);
		}

		public NSUrlRequest GetCachedRequest (NSUrlRequest request)
		{
			BundleCacheFile file;
			return urlToBundleFiles.TryGetValue (request.Url.AbsoluteString, out file) ?
				NSUrlRequest.FromUrl (file.LocalUrl) : request;
		}

		public static NSUrl GetBundleUrl (string bundleRelativePath)
		{
			var fileName = Path.GetFileNameWithoutExtension (bundleRelativePath);
			var extension = Path.GetExtension (bundleRelativePath);
			var subdirectory = Path.GetDirectoryName (bundleRelativePath);
			return NSBundle.MainBundle.GetUrlForResource (fileName, extension, subdirectory);
		}
	}
}

