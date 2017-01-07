using System;
using System.IO;
using System.Threading;
using System.ComponentModel;
using System.Text.RegularExpressions;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;

namespace HybridKit.Apps {

	/// <summary>
	/// An <see cref="HtmlPart"/> that has a collection of updatable bindings. 
	/// </summary>
	public abstract class HtmlView : HtmlPart, INotifyPropertyChanged {

		// A child will inherit all parent's bindings
		HtmlView parent;

		public BindingCollection Bindings { get; private set; }

		/// <summary>
		/// Raised when the value of a binding has changed.
		/// </summary>
		public event PropertyChangedEventHandler PropertyChanged;

		public HtmlView ()
		{
			Bindings = new BindingCollection (this);
		}
		public HtmlView (HtmlView parent): this ()
		{
			this.parent = parent;
		}

		void BindingInvalidated (object sender, EventArgs e) => Update ();
		void BindingPropertyChanged (object sender, PropertyChangedEventArgs e)
		{
			PropertyChanged?.Invoke (this, new PropertyChangedEventArgs (((IBinding)sender).Name));
		}

		public class BindingCollection : IEnumerable<IBinding>, INotifyCollectionChanged {

			static int nextInstanceId = 0;
			const string IdPrefix = "__hk";

			// http://stackoverflow.com/a/6732899/578190
			static readonly Regex InvalidIdChars = new Regex (@"[\s\x00]");

			// This ID is used to prefix all bindings for this instance
			string instanceIdPrefix = IdPrefix + Interlocked.Add (ref nextInstanceId, 1) + "_";

			HtmlView view;
			Dictionary<string, IBinding> bindings = new Dictionary<string, IBinding> ();
			BindingCollection Parent => view.parent?.Bindings;

			public event NotifyCollectionChangedEventHandler CollectionChanged;

			public IBinding this [string name] {
				get {
					IBinding result;
					return bindings.TryGetValue (name, out result)? result : (Parent? [name]);
				}
			}

			internal BindingCollection (HtmlView view)
			{
				this.view = view;
			}

			/// <summary>
			/// Returns an identifier for the given binding name suitable for use as an HTML class name.
			/// </summary>
			/// <param name="name">binding name</param>
			/// <returns>identifier suitable for use as an HTML class name, or NULL if binding name was not found</returns>
			public string GetId (string name)
			{
				return bindings.ContainsKey (name)? InvalidIdChars.Replace (instanceIdPrefix + name, "_") : Parent?.GetId (name);
			}

			public void Add (IBinding binding) => Add (binding, true);
			internal void Add (IBinding binding, bool handleInvalidation)
			{
				IBinding existing = null;
				if (bindings.TryGetValue (binding.Name, out existing)) {
					if (existing == binding)
						return;

					existing.Invalidated -= view.BindingInvalidated;
					existing.PropertyChanged -= view.BindingPropertyChanged;
				}

				bindings [binding.Name] = binding;
				if (handleInvalidation)
					binding.Invalidated += view.BindingInvalidated;
				binding.PropertyChanged += view.BindingPropertyChanged;

				var cc = CollectionChanged;
				if (cc != null) {
					var args = (existing == null)?
						new NotifyCollectionChangedEventArgs (NotifyCollectionChangedAction.Add, binding)
					  : new NotifyCollectionChangedEventArgs (NotifyCollectionChangedAction.Replace, binding, existing, 0);
					cc (this, args);
				}
			}

			internal void Remove (IBinding binding) => bindings.Remove (binding.Name);

			public IEnumerator<IBinding> GetEnumerator () => bindings.Values.GetEnumerator ();
			IEnumerator IEnumerable.GetEnumerator () => GetEnumerator ();
		}
	}
}
