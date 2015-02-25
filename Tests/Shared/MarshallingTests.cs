using System;
using System.Threading.Tasks;

using NUnit.Framework;

namespace HybridKit.Tests {

	[TestFixture]
	public class MarshallingTests : TestBase {

		[Test]
		public async Task SetGetGlobalNull ()
		{
			await WebView.RunScriptAsync (window => {
				window.foo = null;
				Assert.AreEqual (null, window.foo, "#1");
				Assert.AreEqual (null, (string)window.foo, "#2");
				Assert.IsTrue (window.foo == null, "#3");
				Assert.IsTrue (null == window.foo, "#4");
			});
		}

		[Test]
		public async Task SetGetGlobalString ()
		{
			await WebView.RunScriptAsync (window => {
				window.foo = "bazbong";
				Assert.AreEqual ("bazbong", window.foo, "#1");
				Assert.AreEqual ("bazbong", window.foo.ToString (), "#2");
				Assert.AreEqual ("bazbong", (string)window.foo, "#3");
				Assert.IsTrue (window.foo == "bazbong", "#4");
				Assert.IsTrue ("bazbong" == window.foo, "#5");
				Assert.IsTrue (window.foo.Equals ("bazbong"), "#6");
				Assert.IsTrue ("bazbong".Equals (window.foo), "#7");
			});
		}

		[Test]
		public async Task SetGetGlobalNumber ()
		{
			await WebView.RunScriptAsync (window => {
				window.foo = 10;
				Assert.AreEqual (10, window.foo, "#1");
				Assert.AreEqual (10, (int)window.foo, "#2");
				Assert.IsTrue (10 == window.foo, "#3");
				Assert.IsTrue (window.foo == 10, "#4");
				Assert.IsTrue (window.foo.Equals (10), "#5");
				Assert.IsTrue (10d.Equals (window.foo), "#6");

				var bar = window.foo + 5;
				Assert.AreEqual (15, bar, "#7");
			});
		}

		[Test]
		public async Task SetGetGlobalBool ()
		{
			await WebView.RunScriptAsync (window => {
				window.foo = true;
				Assert.AreEqual (true, window.foo, "#1");
				Assert.IsTrue (window.foo, "#2");
				Assert.IsTrue (true == window.foo, "#3");
				Assert.IsTrue (window.foo == true, "#4");
				Assert.IsTrue (window.foo.Equals (true), "#5");
				Assert.IsTrue (true.Equals (window.foo), "#6");

				var bar = !window.foo;
				Assert.IsFalse (bar, "#7");
			});
		}

		[Test]
		public async Task SetGetPoco ()
		{
			await WebView.RunScriptAsync (window => {
				window.foo = new {
					Foo = "foo",
					Bar = 122.5,
					Baz = true
				};
				Assert.AreEqual ("foo", window.foo.Foo, "#1");
				Assert.AreEqual (122.5, window.foo.Bar, "#2");
				Assert.AreEqual (true, window.foo.Baz, "#3");
			});
		}

		[Test]
		public async Task DynamicOpsNumber ()
		{
			await WebView.RunScriptAsync (window => {
				window.foo = 1;
				window.bar = 2;
				Assert.AreEqual (3, window.foo + window.bar, "#1");
				Assert.AreEqual (3, window.foo | window.bar, "#2");
				Assert.AreEqual (0, window.foo & window.bar, "#3");
				//...
			});
		}

		[Test]
		public async Task DynamicOpsString ()
		{
			await WebView.RunScriptAsync (window => {
				window.foo = "Hello ";
				window.bar = "World!";
				Assert.AreEqual ("Hello World!", window.foo + window.bar, "#1");
				//...
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

