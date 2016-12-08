using System;
using System.Threading.Tasks;

namespace HybridKit {

	static class Tasks {
		public static readonly Task Completed = Task.FromResult<object> (null);
	}
}
