using System;
using System.IO;
using System.Text;
using System.Reflection;
using System.Globalization;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.ComponentModel;

namespace HybridKit.Apps {

	public abstract class Binding<TValue> : HtmlPart, IBinding {

		TValue currentValue;

		protected IEqualityComparer<TValue> Comparer
			{ get; set; } = EqualityComparer<TValue>.Default;

		public string Name { get; private set; }

		public TValue Value {
			get { return currentValue; }
			set { OnValueChanging (currentValue, value, updateDOM: true); }
		}
		object IBinding.Value {
			get { return Value; }
			set { Value = (TValue)value; }
		}

		/// <summary>
		/// Raised when the <see cref="Value"/> property of this instance has changed.
		/// </summary>
		public event PropertyChangedEventHandler PropertyChanged;

		public Binding (string name)
		{
			Name = name;
		}

		/// <summary>
		/// Called when the <see cref="Value"/> of this instance is changing.
		/// </summary>
		protected virtual void OnValueChanging (TValue oldValue, TValue newValue, bool updateDOM)
		{
			currentValue = newValue;

			if (oldValue is IInvalidatable)
				((IInvalidatable)oldValue).Invalidated -= ValueInvalidated;
			if (newValue is IInvalidatable)
				((IInvalidatable)newValue).Invalidated += ValueInvalidated;

			if (!Comparer.Equals (oldValue, newValue)) {
				if (updateDOM) Update ();
				PropertyChanged?.Invoke (this, new PropertyChangedEventArgs (nameof (Value)));
			}
		}

		internal void ValueInvalidated (object sender, EventArgs e) => Update ();
	}

	/// <summary>
	/// A binding for a scalar value.
	/// </summary>
	/// <typeparam name="TValue">The type of the value being bound.</typeparam>
	public class ScalarBinding<TValue> : Binding<TValue> {

		public string FormatString { get; set; } = null;
		public IFormatProvider FormatProvider { get; set; } = CultureInfo.CurrentCulture;

		public ScalarBinding (string name): base (name)
		{
		}

		public override void WriteHtml (TextWriter writer)
		{
			if (Value is IHtmlWriter)
				((IHtmlWriter)Value).WriteHtml (writer);

			// FIXME: Escape
			else if (Value is IFormattable)
				writer.Write (((IFormattable)Value).ToString (FormatString, FormatProvider));

			else
				writer.Write (Value);
		}
	}

	public class TwoWayBinding<TValue> : ScalarBinding<TValue> {

		/// <summary>
		/// If non-null, indicates that this binding is responsible for
		///  writing the attribute name and value iff the value is not false or null.
		/// </summary>
		public string OmitAttributeName { get; private set; }

		static bool isBool = (typeof (TValue) == typeof (bool));

		public TwoWayBinding (string name, string omitAttributeName = null): base (name)
		{
			OmitAttributeName = omitAttributeName;
		}

		public override void WriteHtml (TextWriter writer) => WriteHtml (writer, isAttribute: true);
		public void WriteHtml (TextWriter writer, bool isAttribute)
		{
			if (isAttribute && OmitAttributeName != null) {
				if (isBool && !((bool)(object)Value))
					return;
				if (((object)Value) == null)
					return;

				writer.Write (OmitAttributeName);
				if (isBool)
					return;
				writer.Write ('=');
			}
			base.WriteHtml (writer);
		}
	}

	public class VectorBinding<TValue> : ScalarBinding<IEnumerable<TValue>> {

		public string Separator { get; set; } = " ";

		static bool isHtmlWriter = typeof (IHtmlWriter).GetTypeInfo ().IsAssignableFrom (typeof (TValue).GetTypeInfo ());

		public VectorBinding (string name): base (name)
		{
		}

		public override void WriteHtml (TextWriter writer)
		{
			using (var iter = Value.GetEnumerator ()) {
				var hasNext = iter.MoveNext ();
				while (hasNext) {
					WriteSingleHtml (writer, iter.Current);
					if ((hasNext = iter.MoveNext ()) && !isHtmlWriter)
						writer.Write (Separator);
				}
			}
		}

		protected virtual void WriteSingleHtml (TextWriter writer, TValue value)
		{
			// Can't use static bool because the value might be null, or it might be a value type
			if (Value is IHtmlWriter)
				((IHtmlWriter)value).WriteHtml (writer);

			// FIXME: Escape
			else if (Value is IFormattable)
				writer.Write (((IFormattable)value).ToString (FormatString, FormatProvider));

			else
				writer.Write (value);
		}

		protected override void OnValueChanging (IEnumerable<TValue> oldValue, IEnumerable<TValue> newValue, bool updateDOM)
		{
			using (var oldIter = oldValue?.GetEnumerator ())
			using (var newIter = newValue?.GetEnumerator ()) {
				bool hasNextOld, hasNextNew = false;
				while ((hasNextOld = oldIter?.MoveNext () ?? false) || (hasNextNew = newIter?.MoveNext () ?? false))
					OnSingleValueChanging (hasNextOld? oldIter.Current : default (TValue), hasNextNew? newIter.Current : default (TValue));
			}
			base.OnValueChanging (oldValue, newValue, updateDOM);
		}

		protected virtual void OnSingleValueChanging (TValue oldValue, TValue newValue)
		{
			if (oldValue is IInvalidatable)
				((IInvalidatable)oldValue).Invalidated -= ValueInvalidated;
			if (newValue is IInvalidatable)
				((IInvalidatable)newValue).Invalidated += ValueInvalidated;
		}
	}
}
