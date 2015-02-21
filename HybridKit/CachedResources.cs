using System;
using System.IO;
using System.Text;
using System.Reflection;
using System.Collections.Generic;

namespace HybridKit {

	public class Cached {
		public string MimeType { get; private set; }
		public Func<Stream> DataSource { get; private set; }

		public static Cached Resource<T> (string embeddedResourceName)
		{
			var mimeType = GetMimeType (embeddedResourceName);
			if (mimeType == null)
				throw new InvalidOperationException ("Cannot guess mime type; call overload that takes mimeType argument");
			return Resource<T> (embeddedResourceName, mimeType);
		}
		public static Cached Resource<T> (string embeddedResourceName, string mimeType)
		{
			var asm = typeof (T).GetTypeInfo ().Assembly;
			return Resource (
				() => asm.GetManifestResourceStream (embeddedResourceName),
				mimeType
			);
		}
		public static Cached Resource (string data, string mimeType)
		{
			if (data == null)
				throw new ArgumentNullException ("data");
			var stream = new MemoryStream (Encoding.UTF8.GetBytes (data));
			return Resource (() => { stream.Position = 0; return stream; }, mimeType);
		}
		public static Cached Resource (Func<Stream> dataSource, string mimeType)
		{
			return new Cached (dataSource, mimeType);
		}

		private Cached (Func<Stream> dataSource, string mimeType)
		{
			if (dataSource == null)
				throw new ArgumentNullException ("dataSource");
			if (mimeType == null)
				throw new ArgumentNullException ("mimeType");
			MimeType = mimeType;
			DataSource = dataSource;
		}

		static string GetMimeType (string fileName)
		{
			var ext = Path.GetExtension (fileName);
			switch (ext) {

			case ".js":  return "application/javascript";
			case ".png": return "image/png";
			case ".css": return "text/css";
			}
			return null;
		}
	}

	public class CachedResources : IEnumerable<KeyValuePair<string,Cached>> {

		readonly Dictionary<string,Cached> cache;

		public event EventHandler<CachedEventArgs> ItemAdded;

		public CachedResources ()
		{
			cache = new Dictionary<string,Cached> ();
		}

		public void Add (string url, Cached resource)
		{
			cache.Add (url, resource);
			var itemAdded = ItemAdded;
			if (itemAdded != null)
				itemAdded (this, new CachedEventArgs (url, resource));
		}

		public Cached GetCached (string url)
		{
			Cached resource;
			return cache.TryGetValue (url, out resource)? resource : null;
		}

		#region IEnumerable implementation
		IEnumerator<KeyValuePair<string,Cached>> IEnumerable<KeyValuePair<string,Cached>>.GetEnumerator ()
		{
			return cache.GetEnumerator ();
		}
		System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator ()
		{
			return cache.GetEnumerator ();
		}
		#endregion
	}

	public class CachedEventArgs : EventArgs {
		public string Url { get; private set; }
		public Cached Item { get; private set; }
		public CachedEventArgs (string url, Cached item)
		{
			Url = url;
			Item = item;
		}
	}
}

