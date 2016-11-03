using System;
using System.Threading.Tasks;
using System.Text.RegularExpressions;

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
		public async Task SetGetGlobalNumberArray ()
		{
			await WebView.RunScriptAsync (window => {
				window.foo = new[] { 3, 1, 2 };

				// Use by ref
				Assert.AreEqual (3, window.foo.length, "#1");
				Assert.AreEqual (3, window.foo [0], "#2");
				Assert.AreEqual (1, window.foo [1], "#3");
				Assert.AreEqual (2, window.foo [2], "#4");

				// Get by value
				var array = (int[])window.foo;
				Assert.AreEqual (3, array.Length, "#5");
				Assert.AreEqual (3, array [0], "#6");
				Assert.AreEqual (1, array [1], "#7");
				Assert.AreEqual (2, array [2], "#8");
			});
		}

		//FIXME: Regex marshalling probably needs more tests
		[Test]
		public async Task SetGetGlobalRegex ()
		{
			await WebView.RunScriptAsync (window => {
				var regExStr = "^\\d{3}-\\d{2}-(\\d{4})$";
				window.foo = new Regex (regExStr, RegexOptions.Multiline);
				Assert.IsTrue (window.foo.test ("123-45-6789"), "#1");
				Assert.IsFalse (window.foo.test ("abc-45-6789"), "#2");
				Assert.AreEqual ("6789", window.foo.exec ("123-45-6789") [1], "#3");

				var rx = (Regex)window.foo;
				Assert.AreEqual (regExStr, rx.ToString (), "#4");
				Assert.IsTrue ((rx.Options & RegexOptions.Multiline) == RegexOptions.Multiline, "#5");
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

				dynamic innerHTML = document.body.innerHTML;
				Assert.AreEqual ("Foobar", innerHTML.ToString(), "#2");
			});
		}

		[Test]
		public async Task PassAndCallStaticScriptFunction ()
		{
			await WebView.RunScriptAsync (window => {
				using (var func = new ScriptFunction (new Func<string> (StaticScriptFunction))) {
					window.foo = func;
					Assert.AreEqual ("Hello from C#!", window.foo(), "#1");
				}
			});
		}

		static string StaticScriptFunction ()
		{
			return "Hello from C#!";
		}
	}
}

