using System;
using System.Linq;
using System.Reflection;
using System.Collections.Generic;

using GuiUnit;

namespace HybridKit.Tests.iOS
{
	public class Application
	{
		// This is the main entry point of the application.
		static void Main (string[] args)
		{
			new TestRunner (Console.Out).Execute (new[] {
				// NUnitLite options:
				"-labels",

				// List the test assemblies here:
				Assembly.GetExecutingAssembly ().Location
			});
		}
	}
}
