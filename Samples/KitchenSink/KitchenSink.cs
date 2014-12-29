using System;
using System.Diagnostics;

namespace KitchenSink {

	public static class KitchenSink {

		public static void CallJavaScript (dynamic window)
		{
			var document = window.document;

			var name = window.prompt ("What is your name?") ?? "World";
			var node = document.createTextNode (string.Format ("Hello {0} from C#!", name));
			document.body.appendChild (node);

			try {
				window.doesNotExist ();
			} catch (Exception e) {
				Debug.WriteLine ("Example of catching JavaScript exception: {0}", e);
			}
		}
	}
}

