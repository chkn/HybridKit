using System;
using System.Threading.Tasks;

using NUnit.Framework;

namespace HybridKit.Tests {

	[TestFixture]
	public class CallJavaScriptTests : TestBase {

		[Test]
		public async Task SetGetGlobalString ()
		{
			await WebView.RunScriptAsync (window => {
				window.foo = "bazbong";
				Assert.AreEqual ("bazbong", window.foo.toString (), "#1");
				Assert.AreEqual ("bazbong", (string)window.foo, "#2");
				Assert.AreEqual ("bazbong", window.foo, "#3");
			});
		}

		[Test]
		public async Task CallGlobalUndefined ()
		{
			await WebView.RunScriptAsync (window => {
				try {
					window.doesNotExist ();
				} catch (Exception e) {
					Assert.That (e, Is.InstanceOf<ScriptException> (), "#1");
					return;
				}
				Assert.Fail ("expected exception");
			});
		}

		[Test]
		public async Task CallDocumentMethod ()
		{
			await WebView.RunScriptAsync (window => {
				var document = window.document;
				document.write ("Foobar");
				Assert.AreEqual ("Foobar", document.body.innerHTML, "#1");
			});
		}
	}
}

