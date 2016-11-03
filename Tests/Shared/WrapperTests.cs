using System;
using System.Threading.Tasks;

using NUnit.Framework;

namespace HybridKit.Tests {

	[TestFixture]
	public class WrapperTests : TestBase {

		[Test]
		public async Task WrapperSetStringMember ()
		{
			await WebView.RunScriptAsync (window => {
				// first, set untyped
				window.obj = new {
					Foo = "bar"
				};
				Assert.AreEqual ("bar", window.obj.Foo, "#1");

				var typed = (TypedScriptObject)window.obj;
				Assert.IsNotNull (typed, "#2");
				Assert.AreEqual ("bar", typed.Foo, "#3");
			});
		}

		class TypedScriptObject : ScriptObject {

			public string Foo {
				get { return ScriptThis.Foo; }
				set { ScriptThis.Foo = value; }
			}

			protected TypedScriptObject (ScriptObject untyped): base (untyped)
			{
			}
		}
	}
}

