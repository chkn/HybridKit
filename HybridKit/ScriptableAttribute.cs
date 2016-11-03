using System;

namespace HybridKit {

	public class ScriptableAttribute : Attribute {

		public bool Value {
			get;
			set;
		}

		/// <summary>
		/// Initializes a new instance of the <see cref="HybridKit.ScriptableAttribute"/> class
		///  with the <c>Value</c> set to <c>true</c>.
		/// </summary>
		public ScriptableAttribute ()
		{
			Value = true;
		}

		/// <summary>
		/// Initializes a new instance of the <see cref="HybridKit.ScriptableAttribute"/> class
		///  with the specified value.
		/// </summary>
		/// <param name="value">Indicates whether the attributed member should be scriptable.</param>
		public ScriptableAttribute (bool value)
		{
			Value = value;
		}
	}
}

