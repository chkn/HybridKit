using System;

namespace KitchenSink {

	public static class KitchenSink {

		public static void CallJavaScript (dynamic window)
		{
			var node = window.document.createTextNode("Hello from HybridKit!");
			window.document.body.appendChild (node);
			window.alert ("Hello from C#!");
		}
	}
}

