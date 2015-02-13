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
				Assert.IsTrue (window.foo == "bazbong", "#3");
				Assert.IsTrue ("bazbong" == window.foo, "#4");
			});
		}

		[Test]
		public async Task SetGetGlobalNumber ()
		{
			await WebView.RunScriptAsync (window => {
				window.foo = 10;
				Assert.AreEqual (10, (int)window.foo, "#1");
				Assert.IsTrue (10 == window.foo, "#2");
				Assert.IsTrue (window.foo == 10, "#3");

				var bar = window.foo + 5;
				Assert.AreEqual (15, bar, "#2");
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
				Assert.AreEqual ("Foobar", (string)document.body.innerHTML, "#1");
			});
		}
	}
}

