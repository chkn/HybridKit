using System;

using HybridKit;
using HybridKit.Angular;

namespace AngularTest {

	[NgController]
	public class TodoController {

		public TodoController ([NgInject ("$scope")] dynamic scope)
		{
			Console.WriteLine ("HERE!");
		}
	}
}

